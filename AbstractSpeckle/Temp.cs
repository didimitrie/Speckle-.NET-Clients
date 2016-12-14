class SpeckleAPIHelper
{
    SpeckleServer server;
    string wsSessionId;
    string streamId;
    string role;

    WebSocket ws;

    bool wsConected;
    int wsReconnAttempts;

    int dataDebounceInterval = 1000;
    int reconnDebounceInterval = 1000;
    int metaDebounceInterval = 500;

    System.Timers.Timer Reconnecter;
    System.Timers.Timer DataSender;
    System.Timers.Timer MetaDataSender;

    dynamic metaBucket;
    dynamic dataBucket;

    public event SpeckleEvents LiveUpdate;
    public event SpeckleEvents MetaUpdate;
    public event SpeckleEvents HistoryUpdate;


    /// <summary>
    /// Creates a new api helper object. 
    /// If everything is ok (we can ping the server and the token is valid), returns a callback(true).
    /// You can afterwards do one of the following: 
    /// <para>1) create a new sender: createNewSender(...)</para>
    /// <para>2) start sending to an existing stream: sendTo(streamId)</para>
    /// <para>3) start listening to an existing stream: listenTo(streamId)</para>
    /// </summary>
    /// <param name="_server">SpeckleServer to connect to.</param>
    /// <param name="callback">Callback(success): success will be true if connection succceeds.</param>
    public SpeckleAPIHelper(SpeckleServer _server, Action<bool> callback)
    {
        server = _server;
        wsConected = false;
        wsReconnAttempts = 0;
        callback(true);
    }

    /// <summary>
    /// Call this method before destroying any receiver or sender.
    /// </summary>
    public void beforeDestroy()
    {
        ws.Close();
        ws = null;
        Reconnecter.Dispose();
        MetaDataSender.Dispose();
        DataSender.Dispose();
    }

    #region Sender or Receiver creation

    /// <summary>
    /// Create a new stream sender from scratch.
    /// </summary>
    /// <param name="callback">callback(success, streamid): if successful you get a streamid back.</param>
    public void createNewSender(Action<bool, string> callback)
    {
        // PREVENTS THIS FROM BEING CALLED MULTIPLE TIMES
        if ((role != null))
        {
            callback(false, null);
            return;
        }

        setupTimers();
        setupSockets();

        role = "sender";

        // SEND REQUEST TO SERVER
        Debug.WriteLine("API HELPER: Creating a stream.");

        dynamic payload = new ExpandoObject();
        payload.token = server.token;

        var client = new RestClient(server.restEndpoint + @"/api/stream");
        var request = new RestRequest(Method.POST);
        request.AddHeader("Content-Encoding", "gzip");
        request.AddHeader("content-type", "application/json; charset=utf-8");

        request.AddParameter("application/json", compressPayload(payload), ParameterType.RequestBody);

        // IF SUCCESSFUL, PASS THE STREAMID BACK
        client.ExecuteAsync(request, response =>
        {
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                dynamic parsedResponse = JsonConvert.DeserializeObject(response.Content);
                if (parsedResponse.success == "true")
                {
                    streamId = (string)parsedResponse.streamId;

                    joinStream();

                    callback(true, streamId);
                    return;
                }
                else
                {
                    callback(false, (string)parsedResponse.message);
                    return;
                }
            }
            else
            {
                callback(false, "Server Timeout.");
                return;
            }

        });
    }

    /// <summary>
    /// Re-create a stream sender (load up from file)
    /// </summary>
    /// <param name="_streamId">Stream you want to emit to</param>
    /// <param name="callback">callback(success, message, streamid): if successful you get a streamid back.</param>
    public void sendTo(string _streamId, Action<bool, string, string> callback)
    {
        // PREVENTS THIS FROM BEING CALLED MULTIPLE TIMES
        if (role != null)
        {
            callback(false, "Already initialised as " + role + ".", null);
            return;
        }

        //TODO: joinStream();

        throw new NotImplementedException("Not yet implemented.");
    }

    public void listenTo(string _streamId, Action<bool, dynamic> callback)
    {
        if (role != "receiver")
        {
            callback(false, null);
            return;
        }

        streamId = _streamId;
        getStream((success, message, content) =>
        {
            Debug.WriteLine(message);
            if (success)
            {
                joinStream();
                //callback(success, the stream itself)
            }
            else
            {
                //callback(fail, nothing)
            }
        });

    }

    #endregion

    #region Timer and Socket setups
    private void setupTimers()
    {
        Reconnecter = new System.Timers.Timer(reconnDebounceInterval);
        Reconnecter.AutoReset = false; Reconnecter.Enabled = false;
        Reconnecter.Elapsed += (sender, e) =>
        {
            wsReconnAttempts++;
            Debug.WriteLine("Attempting to reconnect: " + wsReconnAttempts);
            ws.Connect();
        };

        DataSender = new System.Timers.Timer(dataDebounceInterval);
        DataSender.AutoReset = false; DataSender.Enabled = false;
        DataSender.Elapsed += (sender, e) =>
        {
            Debug.WriteLine("TODO: Sending data payload.");
        };

        MetaDataSender = new System.Timers.Timer(metaDebounceInterval);
        MetaDataSender.AutoReset = false; MetaDataSender.Enabled = false;
        MetaDataSender.Elapsed += (sender, e) =>
        {
            Debug.WriteLine("TODO: Updating metadata.");
            dispatchData(@"/api/metadata", metaBucket);
            MetaDataSender.Stop();
        };
    }

    private void setupSockets()
    {
        ws = new WebSocket("ws://10.211.55.2:8080?access_token=asdf");
        ws.Compression = CompressionMethod.Deflate;

        ws.OnMessage += (sender, e) =>
        {
            if (e.IsPing)
            {
                ws.SendAsync("alive", (Action<Boolean>)((result) => Debug.WriteLine("Socket " + wsSessionId + " (streamid: " + streamId + ") responded to ping.")));
                return;
            }
            if (e.IsBinary)
            {
                Debug.WriteLine("Got binary message. Duuumping it.");
                return;
            }
            if (e.IsText)
            {
                Debug.WriteLine("Got text message. Parsing it.");
                dynamic message = Newtonsoft.Json.JsonConvert.DeserializeObject(e.Data);
                messageImplementer(message);
            }
        };

        ws.OnOpen += (sender, e) =>
        {
            Debug.WriteLine("WS Connected.");
            wsConected = true;
            wsReconnAttempts = 0;

            if (streamId != null)
                joinStream();

            Reconnecter.Stop();
        };

        ws.OnClose += (sender, e) =>
        {
            Debug.WriteLine("WS Disconnected.");
            Reconnecter.Start();
        };

        ws.EmitOnPing = true;

        ws.Connect();
    }

    #endregion

    private void messageImplementer(dynamic message)
    {
        string eventName = message.eventName;

        switch (eventName)
        {
            case "ws-session-id":
                wsSessionId = message.sessionId;
                Debug.WriteLine("Set session id to " + wsSessionId);
                break;
            case "live-update":
                // WRONG. Correct flow: 1) getLiveStream(), then shoot the LiveUpdateEvent
                LiveUpdate?.Invoke(this, new SpeckleEventArgs("live-update", message.data));
                break;
            case "metadata-update":
                MetaUpdate?.Invoke(this, new SpeckleEventArgs("metadata-update", message.data));
                Debug.WriteLine("Hello New metadata.");
                break;
            case "history-update":
                HistoryUpdate?.Invoke(this, new SpeckleEventArgs("history-update", message.data));
                break;
            default:
                Debug.WriteLine("Unkown event name.");
                break;
        }
    }

    private byte[] compressPayload(dynamic payload)
    {
        var dataStream = new MemoryStream();
        using (var zipStream = new GZipStream(dataStream, CompressionMode.Compress))
        {
            using (var writer = new StreamWriter(zipStream))
            {
                writer.Write(JsonConvert.SerializeObject(payload));
            }
        }

        return dataStream.ToArray();
    }

    private void dispatchData(string endpoint, dynamic data)
    {
        dynamic payload = new ExpandoObject();
        payload.token = server.token;
        payload.streamId = streamId;
        payload.sessionId = wsSessionId;
        payload.data = data;

        var client = new RestClient(server.restEndpoint + endpoint);
        var request = new RestRequest(Method.POST);

        request.AddHeader("Content-Encoding", "gzip");
        request.AddHeader("Content-Type", "application/json; charset=utf-8");
        request.AddParameter("application/json", compressPayload(payload), ParameterType.RequestBody);
        client.ExecuteAsync(request, response =>
        {
            Debug.WriteLine(response.Content);
        });
    }

    private void deleteStream()
    {
        // TODO
    }

    public void joinStream()
    {
        dynamic msg = new ExpandoObject();
        msg.eventName = "join-stream";
        msg.args = streamId;
        ws.Send(JsonConvert.SerializeObject(msg));

    }

    public void updateStream()
    {

    }

    public void updateMetadata(dynamic metadata)
    {
        metaBucket = metadata;
        MetaDataSender.Start();
    }

    public void updateHistory()
    {

    }

    public void getStream(Action<bool, string, dynamic> callback)
    {
        dynamic payload = new ExpandoObject();
        payload.token = server.token;
        payload.streamId = streamId;
        payload.sessionId = wsSessionId;

        var client = new RestClient(server.restEndpoint + "/stream");
        var request = new RestRequest(Method.GET);

        request.AddHeader("Content-Encoding", "gzip");
        request.AddHeader("Content-Type", "application/json; charset=utf-8");
        request.AddParameter("application/json", compressPayload(payload), ParameterType.RequestBody);

        client.ExecuteAsync(request, response =>
        {
            Debug.WriteLine(response.Content);
            dynamic parsedResponse = JsonConvert.DeserializeObject(response.Content);
            if (parsedResponse.success == "true")
            {
                callback(true, "Stream found.", parsedResponse.content);
            }
        });

    }

    public void getHistoryList()
    {

    }

}