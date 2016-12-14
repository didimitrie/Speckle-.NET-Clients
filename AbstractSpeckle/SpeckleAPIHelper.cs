using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;

using WebSocketSharp;
using System.Dynamic;

using Newtonsoft.Json;
using RestSharp;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SpeckleAbstract
{
    public enum SpeckleRoles { Sender, Receiver }

    /// <summary>
    /// Is structured as follows: (string) EventInfo (random metadata); (dynamic) Data (the actual event data, if any).
    /// </summary>
    public class SpeckleEventArgs : EventArgs
    {
        public string EventInfo;
        public dynamic Data;
        public SpeckleEventArgs(string text, dynamic _Data = null) { EventInfo = text; Data = _Data; }
    }

    public delegate void SpeckleEvents(object source, SpeckleEventArgs e);

    /// <summary>
    /// Class that standardises communication with the Speckle Server API.
    /// <para>
    /// You can initialise this class either as a SENDER or as a RECEIVER.
    /// </para>
    /// </summary>
    public class SpeckleApiProxy : IDisposable
    {
        public readonly string wsEndpoint;
        public readonly string restEndpoint;
        public readonly string token;
        private string streamId;

        SpeckleRoles role;

        WebSocket ws;
        bool wsConnected;
        int wsReconnAttempts;
        string wsSessionId;

        int dataDebounceInterval = 1000;
        int reconnDebounceInterval = 1000;
        int metaDebounceInterval = 1000;

        System.Timers.Timer Reconnecter;
        System.Timers.Timer DataSender;
        System.Timers.Timer MetaDataSender;

        System.Timers.Timer IsReadyChecker;
        bool isReady;

        SpeckleConverter converter;

        dynamic metaDataBucket;
        List<object> objectBucket;

        public dynamic stream;
        public dynamic liveInstance;
        string lastReqHash;

        /// <summary>
        /// TODO: Triggered when WS receives a sessionId from the server (means ws got connected properly).
        /// </summary>
        public event SpeckleEvents OnWsReady;
        /// <summary>
        /// TODO: Triggered when API handshake was done.
        /// </summary>
        public event SpeckleEvents OnHandshakeReady;
        /// <summary>
        /// Triggered when the api proxy is ready: we have a server handshake, the stream exists (or was created) and the live connection is good to go (socket has a wsSessionId).
        /// </summary>
        public event SpeckleEvents OnInitReady;
        /// <summary>
        /// Triggered when we are creating a new stream. The stream id will be in the event data.
        /// </summary>
        public event SpeckleEvents OnStreamIdReceived;
        /// <summary>
        /// Triggered when a live update is received. 
        /// </summary>
        public event SpeckleEvents OnLiveUpdate;
        /// <summary>
        /// Triggered when metadata (stream name, structure) is changed. Listen to this to update the stream data.
        /// </summary>
        public event SpeckleEvents OnMetaUpdate;
        /// <summary>
        /// Triggered when a new history instance was saved for the stream.
        /// </summary>
        public event SpeckleEvents OnHistoryUpdate;
        /// <summary>
        /// Triggered when a volatile message is received.
        /// </summary>
        public event SpeckleEvents OnVolatileMessage;
        /// <summary>
        /// Triggered when receiving an ok from the server on a metadata or data update.
        /// </summary>
        public event SpeckleEvents OnDataSent;
        public event SpeckleEvents OnDisconnect;

        #region constructors

        /// <summary>
        /// Creates a new Speckle Api Proxy, which allows you to communicate with a Speckle backend.
        /// If creating a RECEIVER, a streamId must be provided. If creating a SENDER, NOT providing a streamId will create a new stream.
        /// </summary>
        /// <param name="_role">Sender or Receiver</param>
        /// <param name="_converter">Converter to use. If null, defaults to a new GhConverter.</param>
        /// <param name="_wsEndpoint">Websocket endpoint (ws://...)</param>
        /// <param name="_restEndpoint">Rest api endpoint (http://...)</param>
        /// <param name="_token">User api token</param>
        /// <param name="_streamId">The data stream you want to listen to. If not provided and the _role is Sender, it will create a new stream. If not provided and _role is Receiver, will throw an error.</param>
        public SpeckleApiProxy(SpeckleRoles _role, SpeckleConverter _converter = null, string _wsEndpoint = @"ws://10.211.55.2:8080", string _restEndpoint = @"http://10.211.55.2:8080", string _token = "asdf", string _streamId = null)
        {
            role = _role;
            converter = _converter != null ? _converter : new GhConveter();
            if (_converter != null)
                converter = _converter;
            else
                converter = new GhConveter();


            wsEndpoint = _wsEndpoint; restEndpoint = _restEndpoint; token = _token;
            streamId = _streamId;

            isReadyCheckSetup();

            apiCall(@"/api/handshake", Method.POST, null, (success_handshake, response_handshake) =>
            {
                Debug.WriteLine("Received response. Success: " + success_handshake);
                if (!success_handshake)
                {
                    throw new Exception("Handshake failed: " + response_handshake);
                }

                setupTimers();

                switch (streamId)
                {
                    case null:
                        Debug.WriteLine("No streamid found.");

                        if (role == SpeckleRoles.Receiver)
                        {
                            throw new Exception("You need to provide a streamId to listen to.");
                        }

                        if (role == SpeckleRoles.Sender)
                        {
                            Debug.WriteLine("Making a new stream.");

                            apiCall(@"/api/stream", Method.POST, null, (success_stream, response_stream) =>
                            {
                                if (!success_stream)
                                    throw new Exception("Failed to create a new stream.");

                                streamId = response_stream.streamId;
                                OnStreamIdReceived?.Invoke(this, new SpeckleEventArgs("stream-id-received", streamId));
                                setupWS();
                            });
                        }
                        break;
                    // if we have a streamId, then check if it exists.
                    default:
                        apiCall(@"/api/stream/exists", Method.GET, null, (success_foundStream, response_foundStream) =>
                        {
                            if ((bool)response_foundStream.found)
                            {
                                Debug.WriteLine("We have a valid streamId, will initialise.");
                                OnStreamIdReceived?.Invoke(this, new SpeckleEventArgs("stream-id-validated", streamId));
                                setupWS();

                                if (role == SpeckleRoles.Receiver)
                                    receiverSetup();
                            }
                            else
                                throw new Exception("Stream Id was not valid: no such stream found.");
                        });
                        break;

                }
            });
        }

        /// <summary>
        /// Creates a new Speckle Api Proxy from a serialised string. Mainly to be used for reinitialising a previously serialised (and saved) proxy.
        /// Assumes we have the full information for recreating this object: role, streamId, wsEndpoint, restEndpoint, token. 
        /// </summary>
        /// <param name="serialisedObject">Speckle Proxy Serialisation string. Comes from SpeckleApiProxy.ToString().</param>
        public SpeckleApiProxy(string serialisedObject)
        {
            dynamic proxyobject = JsonConvert.DeserializeObject(serialisedObject);
            streamId = (string)proxyobject.streamId;
            wsEndpoint = (string)proxyobject.wsEndpoint;
            restEndpoint = (string)proxyobject.restEndpoint;
            token = (string)proxyobject.token;
            role = (SpeckleRoles)proxyobject.role;

            isReadyCheckSetup();

            var converterType = (string)proxyobject.converter;
            switch (converterType)
            {
                case "grasshopper-converter":
                    converter = new GhConveter();
                    break;
                case "rhino-converter":
                    converter = new RhConverter();
                    break;
                default:
                    Debug.WriteLine("Warning! converter fuckup, bad serialisation or old object. Defaulting to gh converter.");
                    converter = new GhConveter();
                    break;
            }


            apiCall(@"/api/handshake", Method.POST, null, (success_handshake, response_handshake) =>
            {
                Debug.WriteLine("Received response. Success: " + success_handshake);
                if (!success_handshake)
                {
                    throw new Exception("Handshake failed: " + response_handshake);
                }

                apiCall(@"/api/stream/exists", Method.GET, null, (success_foundStream, response_foundStream) =>
                {
                    if ((bool)response_foundStream.found)
                    {
                        Debug.WriteLine("We have a valid streamId, will initialise.");
                        OnStreamIdReceived?.Invoke(this, new SpeckleEventArgs("stream-id-validated", streamId));
                        setupTimers();
                        setupWS();

                        if (role == SpeckleRoles.Receiver)
                            receiverSetup();
                    }
                    else
                        throw new Exception("Stream Id was not valid: no such stream found.");
                });
            });

        }

        #endregion

        #region setups and checks

        private void isReadyCheckSetup()
        {
            IsReadyChecker = new System.Timers.Timer(100);
            IsReadyChecker.AutoReset = false; IsReadyChecker.Enabled = true;
            IsReadyChecker.Elapsed += (sender, e) =>
            {
                Debug.WriteLine("Checking for readyness.");
                if (streamId == null) { IsReadyChecker.Start(); return; }
                if (wsSessionId == null) { IsReadyChecker.Start(); return; }

                Debug.WriteLine("I am now ready!");
                isReady = true;
                OnInitReady?.Invoke(this, new SpeckleEventArgs("ready"));
                IsReadyChecker.Stop();
                IsReadyChecker.Dispose();
            };

            isReady = false;
        }

        private void receiverSetup()
        {
            apiCall(@"/api/stream", Method.GET, null, (success_gotStream, response_gotStream) =>
            {
                if (!success_gotStream)
                    throw new Exception("Failed to retrieve stream.");
                else
                {
                    OnLiveUpdate?.Invoke(this, new SpeckleEventArgs("first-call", response_gotStream.stream));
                }
            });
        }

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

                dynamic payload_a = new ExpandoObject();
                payload_a.objects = converter.convert(objectBucket, true);
                payload_a.metaData = metaDataBucket;

                byte[] payload = compressPayload(payload_a);

                apiCall(@"/api/live", Method.POST, payload, (success, response) =>
                {
                    OnDataSent?.Invoke(this, new SpeckleEventArgs(success.ToString()));
                });
                DataSender.Stop();
            };

            MetaDataSender = new System.Timers.Timer(metaDebounceInterval);
            MetaDataSender.AutoReset = false; MetaDataSender.Enabled = false;
            MetaDataSender.Elapsed += (sender, e) =>
            {
                Debug.WriteLine("TODO: Updating metadata.");

                byte[] payload = compressPayload(metaDataBucket);
                apiCall(@"/api/metadata", Method.POST, payload, (success, response) =>
                {
                    OnDataSent?.Invoke(this, new SpeckleEventArgs(success.ToString()));
                });

                MetaDataSender.Stop();
            };
        }

        private void setupWS()
        {
            ws = new WebSocket(wsEndpoint + "?access_token=" + token);
            ws.Compression = CompressionMethod.Deflate;

            ws.OnMessage += (sender, e) =>
            {
                if (e.IsPing)
                {
                    ws.SendAsync("alive", (Action<Boolean>)((result) => Debug.WriteLine("Socket " + wsSessionId + " (streamid: " + streamId + ") responded to ping.")));
                    return;
                }
                if (e.IsText)
                {
                    Debug.WriteLine("Got text message. Parsing it.");
                    dynamic message = Newtonsoft.Json.JsonConvert.DeserializeObject(e.Data);
                    wsMessageHandler(message);
                }
            };

            ws.OnOpen += (sender, e) =>
            {
                Debug.WriteLine("WS Connected.");
                Reconnecter.Stop();
                wsConnected = true;
                wsReconnAttempts = 0;
                if (streamId == null)
                    throw new Exception("Cannot join room, streamId is null. Live events will not be received.");
                else
                    ws.Send(JsonConvert.SerializeObject(new { eventName = "join-stream", args = streamId }));
            };

            ws.OnClose += (sender, e) =>
            {
                Debug.WriteLine("WS Disconnected.");
                wsConnected = false;
                Reconnecter.Start();
                isReadyCheckSetup();
                isReady = false;
                wsSessionId = null;
                OnDisconnect?.Invoke(this, new SpeckleEventArgs("ws-disconnected"));
            };

            ws.EmitOnPing = true;

            ws.Connect();
        }

        #endregion

        #region public domain functions

        /// <summary>
        /// Updates the stream's live data (data and metadata). 
        /// </summary>
        /// <param name="objects">Data to send.</param>
        /// <param name="metadata">Metadata of stream.</param>
        public void updateData(List<object> objects, dynamic metadata)
        {
            if (role == SpeckleRoles.Receiver) throw new Exception("This is a receiver. It does not send data!");

            objectBucket = objects;
            metaDataBucket = metadata;
            DataSender.Start();
        }

        /// <summary>
        /// Updates the stream's live instance metadata (stream name, structure).
        /// </summary>
        /// <param name="payload"></param>
        public void updateMetadata(dynamic payload)
        {
            if (role == SpeckleRoles.Receiver) throw new Exception("This is a receiver. It does not send data!");

            metaDataBucket = payload;
            MetaDataSender.Start();
        }

        /// <summary>
        /// Will send the payload to the server and keep it to the stream's history.
        /// </summary>
        /// <param name="payload">Data to send.</param>
        public void updateHistory(dynamic payload)
        {
            if (role == SpeckleRoles.Receiver) throw new Exception("This is a receiver. It does not send data!"); ;
            // TODOD
        }

        /// <summary>
        /// Sends a volatile message. Does not persist it to the database. Live clients can consume it.
        /// </summary>
        /// <param name="message">Message to send.</param>
        public void sendVolatileMessage(string message)
        {
            // should be debounced
            ws.Send(JsonConvert.SerializeObject(new { eventName = "volatile-message", args = message }));
        }

        /// <summary>
        /// Gets the stream id.
        /// </summary>
        /// <returns>The stream id.</returns>
        public string getStreamId()
        {
            return streamId;
        }

        #endregion

        #region utils: payload compression, general api call wrapper, socket message parser & distribution, final object disposal

        private void apiCall(string apiEndpoint, Method method, byte[] payload, Action<bool, dynamic> callback)
        {
            var client = new RestClient(restEndpoint + apiEndpoint);
            var request = new RestRequest(method);

            request.AddHeader("speckle-token", token); // Can and probably should be replaced with a proper jwt token system, with tokens expiring every n weeks or so. xxx
            request.AddHeader("speckle-stream-id", streamId);
            request.AddHeader("speckle-ws-id", wsSessionId);

            request.AddHeader("Content-Encoding", "gzip");
            request.AddHeader("content-type", "application/json; charset=utf-8");
            request.AddParameter("application/json", payload, ParameterType.RequestBody);

            client.ExecuteAsync(request, response =>
            {
                dynamic parsedResponse = JsonConvert.DeserializeObject(response.Content);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    callback(true, parsedResponse);
                else
                    callback(false, parsedResponse);
            });

        }

        private void wsMessageHandler(dynamic message)
        {
            string eventName = message.eventName;

            switch (eventName)
            {
                case "ws-session-id":
                    wsSessionId = message.sessionId;
                    Debug.WriteLine("Set session id to " + wsSessionId);
                    OnWsReady?.Invoke(this, new SpeckleEventArgs(wsSessionId));
                    break;
                case "volatile-message":
                    // trigger event with data
                    OnVolatileMessage?.Invoke(this, new SpeckleEventArgs("volatile-message", message.args));
                    break;
                case "live-update":
                    // get update from server -> trigger event in the callback
                    OnLiveUpdate?.Invoke(this, new SpeckleEventArgs("live-update", message.args));
                    break;
                case "metadata-update":
                    // metadata is in payload -> trigger event with content
                    OnMetaUpdate?.Invoke(this, new SpeckleEventArgs("metadata-update", message.args));
                    break;
                case "history-update":
                    // get new stream history from server -> trigger the event (doesn't exist yet)
                    OnHistoryUpdate?.Invoke(this, new SpeckleEventArgs("history-update"));
                    break;
                default:
                    Debug.WriteLine("Unknown event name.");
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

        /// <summary>
        /// Gets rid of all the disposale objects, making sure events and timers are killed.
        /// </summary>
        public void Dispose()
        {
            if (ws != null)
            {
                ws.Close();
                ws = null;
            }
            
            // timer disposal
            if (Reconnecter != null)
                Reconnecter.Dispose();
            if (DataSender != null)
                DataSender.Dispose();
            if (MetaDataSender != null)
                MetaDataSender.Dispose();
            if (IsReadyChecker != null)
                IsReadyChecker.Dispose();

            // event disposal
            OnLiveUpdate = null;
            OnMetaUpdate = null;
            OnHistoryUpdate = null;
            OnVolatileMessage = null;
            OnInitReady = null;
        }

        /// <summary>
        /// Returns a serialised json object of this instance. Useful for serialisation and reinitialisation.
        /// <para>For example: {"role":0,"streamId":"HyOU568Xe","wsEndpoint":"ws://10.211.55.2:8080","restEndpoint":"http://10.211.55.2:8080","token":"asdf"}</para>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            // look ma, almost json! 
            var SPK = new
            {
                role = role,
                streamId = streamId,
                wsEndpoint = wsEndpoint,
                restEndpoint = restEndpoint,
                token = token,
                converter = converter.serialise()
            };
            return JsonConvert.SerializeObject(SPK);
        }
        #endregion
    }


}
