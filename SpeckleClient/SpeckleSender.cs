using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace SpeckleClient
{
    public class SpeckleSender
    {
        // connectivity:
        SpeckleServer server;
        WebSocket ws;

        // buckets:
        List<SpeckleLayer> layers;
        List<object> objects;
        string name;

        // converer:
        SpeckleConverter converter;

        // timers:
        int dataDebounceInterval = 1000;
        int reconnDebounceInterval = 1000;
        int metaDebounceInterval = 750;

        System.Timers.Timer isReadyCheck;
        System.Timers.Timer wsReconnecter;
        System.Timers.Timer DataSender;
        System.Timers.Timer MetadataSender;

        /// <summary>
        /// Event emitted when everything is ready.
        /// </summary>
        public event SpeckleEvents OnReady;
        /// <summary>
        /// Event emitted when a volatile message was received.
        /// </summary>
        public event SpeckleEvents OnMessage;
        public event SpeckleEvents OnBroadcast;
        /// <summary>
        /// Event emitted when an api call returns.
        /// </summary>
        public event SpeckleEvents OnDataSent;
        public event SpeckleEvents OnError;


        #region constructors

        /// <summary>
        /// Creates a new sender.
        /// </summary>
        /// <param name="_server">Speckle Server to connect to.</param>
        public SpeckleSender(string apiUrl, string token, SpeckleConverter _converter, string documentId = null, string documentName = null)
        {
            converter = _converter;

            server = new SpeckleServer(apiUrl, token);

            this.server.OnError += (sender, e) =>
            {
                this.OnError?.Invoke(this, new SpeckleEventArgs(e.EventInfo));
            };

            server.OnReady += (e, data) =>
            {
                this.setup();
            };
        }

        /// <summary>
        /// Creates a new sender from a previously serialised one.
        /// </summary>
        /// <param name="serialisedObject"></param>
        public SpeckleSender(string serialisedObject, SpeckleConverter _converter)
        {
            dynamic description = JsonConvert.DeserializeObject(serialisedObject);

            server = new SpeckleServer((string)description.restEndpoint, (string)description.token, (string)description.streamId);


            this.server.OnError += (sender, e) =>
            {
                this.OnError?.Invoke(this, new SpeckleEventArgs(e.EventInfo));
            };

            this.server.OnReady += (sender, e) =>
            {
                this.setup();
            };

            converter = _converter;
            converter.encodeObjectsToNative = description.encodeNative;
            converter.encodeObjectsToSpeckle = description.encodeSpeckle;

        }

        #endregion

        #region main setup calls

        private void setup()
        {
            if (server.streamId == null)
                server.createNewStream((success, data) =>
                {
                    if (!success)
                    {
                        OnError?.Invoke(this, new SpeckleEventArgs("Failed to create stream."));
                        return;
                    }
                    server.streamId = data.streamId;
                    setupWebsocket();
                });
            else
                server.getStream((success, data) =>
                {
                    if (!success)
                    {
                        OnError?.Invoke(this, new SpeckleEventArgs("Failed to retrieve stream."));
                        return;
                    }
                    setupWebsocket();
                });

            // start the is ready checker timer.
            // "ready" is defined as:
            // a) we have a valid stream id (means we've contacted the server sucessfully)
            // && b) we have a live wsSessionId (means sockets were correctly initialised)

            isReadyCheck = new System.Timers.Timer(150);
            isReadyCheck.AutoReset = false; isReadyCheck.Enabled = true;
            isReadyCheck.Elapsed += (sender, e) =>
            {
                if (server.streamId == null) { isReadyCheck.Start(); return; }
                if (server.wsSessionId == null) { isReadyCheck.Start(); return; }

                dynamic data = new ExpandoObject();
                data.streamId = server.streamId;
                data.wsSessionId = server.wsSessionId;

                OnReady?.Invoke(this, new SpeckleEventArgs("sender ready", data));
            };

            // data sender debouncer
            DataSender = new System.Timers.Timer(dataDebounceInterval);
            DataSender.AutoReset = false; DataSender.Enabled = false;
            DataSender.Elapsed +=
            (sender, e) =>
            {
                Debug.WriteLine("SPKSENDER: Sending data payload.");

                dynamic x = new ExpandoObject();
                x.objects = converter.convert(this.objects);
                x.objectProperties = converter.getObjectProperties(this.objects);
                x.layers = layers;
                x.streamName = name;

                server.updateStream(x as ExpandoObject, (success, data) =>
                {
                    if(!success)
                    {
                        OnError?.Invoke(this, new SpeckleEventArgs("Failed to update stream."));
                        return;
                    }

                    converter.commitCache();
                    OnDataSent?.Invoke(this, new SpeckleEventArgs("Stream was updated."));
                });
                DataSender.Stop();
            };

            // metadata sender debouncer
            MetadataSender = new System.Timers.Timer(metaDebounceInterval);
            MetadataSender.AutoReset = false; MetadataSender.Enabled = false;
            MetadataSender.Elapsed += (sender, e) =>
            {
                Debug.WriteLine("SPKSENDER: Sending meta payload.");

                dynamic payload = new ExpandoObject();
                payload.layers = layers;
                payload.streamName = name;

                server.updateStreamMetadata(payload as ExpandoObject, (success, response) =>
                {
                    if (!success)
                    {
                        OnError?.Invoke(this, new SpeckleEventArgs("Failed to update stream metadata."));
                        return;
                    }
                    OnDataSent?.Invoke(this, new SpeckleEventArgs("Stream metadata was updated."));
                });
            };

        }

        private void setupWebsocket()
        {
            wsReconnecter = new System.Timers.Timer(reconnDebounceInterval);
            wsReconnecter.AutoReset = false; wsReconnecter.Enabled = false;
            wsReconnecter.Elapsed += (sender, e) =>
            {
                ws.Connect();
            };

            ws = new WebSocket(server.wsEndpoint + "?access_token=" + server.token);

            ws.OnOpen += (sender, e) =>
            {
                wsReconnecter.Stop();
                ws.Send(JsonConvert.SerializeObject(new { eventName = "join-stream", args = new { streamid = server.streamId, role = "sender" } }));
            };

            ws.OnClose += (sender, e) =>
            {
                wsReconnecter.Start();
                server.wsSessionId = null;
                this.OnError?.Invoke(this, new SpeckleEventArgs("Disconnected from server."));
            };

            ws.OnMessage += (sender, e) =>
            {
                if (e.Data == "ping")
                {
                    ws.Send("alive");
                    return;
                }
                dynamic message = JsonConvert.DeserializeObject(e.Data);

                if (message.eventName == "ws-session-id")
                {
                    server.wsSessionId = message.sessionId;
                    return;
                }

                if (message.eventName == "volatile-message")
                {
                    OnMessage?.Invoke(this, new SpeckleEventArgs("volatile-message", message));
                    return;
                }

                if (message.eventName == "volatile-broadcast")
                {
                    OnBroadcast?.Invoke(this, new SpeckleEventArgs("volatile-broadcast", message));
                }
            };

            ws.Connect();
        }

        #endregion

        #region public methods

        /// <summary>
        /// Sends a data update. 
        /// </summary>
        /// <param name="_objects">List of objects to convert and send.</param>
        /// <param name="_layers">List of layers.</param>
        /// <param name="_name">The name of the stream.</param>
        public void sendDataUpdate(List<dynamic> _objects, List<SpeckleLayer> _layers, string _name)
        {
            this.objects = _objects;
            this.layers = _layers;
            this.name = _name;

            // in case there is a metadata update in progress
            MetadataSender.Stop();

            // it's time to... hit the button!
            DataSender.Start();
        }

        /// <summary>
        /// Sends a metadata update (just layers and stream name).
        /// </summary>
        /// <param name="layers">List of layers.</param>
        /// <param name="_name">The name of the stream.</param>
        public void sendMetadataUpdate(List<SpeckleLayer> layers, string _name)
        {
            this.layers = layers;
            this.name = _name;

            MetadataSender.Start();
        }

        /// <summary>
        /// Saves instance to the stream history.
        /// </summary>
        /// <param name="name">A specific name to save it by.</param>
        public void saveToHistory(string name = "History")
        {
            server.createNewStreamHistory((success, data) =>
            {
                if (!success)
                {
                    OnError?.Invoke(this, new SpeckleEventArgs("Failed to create a new history instance."));
                    return;
                }

            });
        }

        /// <summary>
        /// Returns the id to which this sender sends data.
        /// </summary>
        /// <returns></returns>
        public string getStreamId()
        {
            return server.streamId;
        }

        public string getServer()
        {
            return server.restEndpoint;
        }

        #endregion;

        #region volatile messaging
        /// <summary>
        /// Sends a volatile message that will be broadcast to this stream's clients.
        /// </summary>
        /// <param name="message">Message to broadcast.</param>
        public void broadcastVolatileMessage(string message)
        {
            this.ws.Send(JsonConvert.SerializeObject(new { eventName = "volatile-broadcast", args = message }));
        }

        public void sendVolatileMessage(string message, string socketId)
        {
            this.ws.Send(JsonConvert.SerializeObject(new { eventName = "volatile-message", args = message }));
        }
        #endregion

        public override string ToString()
        {
            dynamic description = new
            {
                restEndpoint = server.restEndpoint,
                wsEndpoint = server.wsEndpoint,
                streamId = server.streamId,
                token = server.token,
                encodeNative = converter.encodeObjectsToNative,
                encodeSpeckle = converter.encodeObjectsToSpeckle
            };

            return JsonConvert.SerializeObject(description);
        }

        /// <summary>
        /// Call this method whenever you are done with this component (document is closed, etc.)
        /// </summary>
        public void Dispose(bool delete = false)
        {
            if (delete)
            {
                //server.apiCall(@"/api/stream", Method.DELETE, etc, etc) 
            }

            if (ws != null) ws.Close();
            if (DataSender != null) DataSender.Dispose();
            if (MetadataSender != null) MetadataSender.Dispose();
            if (wsReconnecter != null) wsReconnecter.Dispose();
            if (isReadyCheck != null) isReadyCheck.Dispose();
        }
    }
}
