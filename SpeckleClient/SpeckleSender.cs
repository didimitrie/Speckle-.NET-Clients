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
        public event SpeckleEvents OnVolatileMessage;
        /// <summary>
        /// Event emitted when an api call returns.
        /// </summary>
        public event SpeckleEvents OnDataSent;

        #region constructors

        /// <summary>
        /// Creates a new sender.
        /// </summary>
        /// <param name="_server">Speckle Server to connect to.</param>
        public SpeckleSender(SpeckleServer _server, SpeckleConverter _converter, string documentId = null, string documentName = null)
        {
            server = _server;
            converter = _converter;
            this.setup();
            
        }

        /// <summary>
        /// Creates a new sender from a previously serialised one.
        /// </summary>
        /// <param name="serialisedObject"></param>
        public SpeckleSender(string serialisedObject, SpeckleConverter _converter)
        {
            dynamic description = JsonConvert.DeserializeObject(serialisedObject);
            this.server = new SpeckleServer((string)description.restEndpoint, (string)description.wsEndpoint, (string)description.token, (string)description.streamId);
            this.converter = _converter;
            this.converter.encodeObjectsToNative = description.encodeNative;
            this.converter.encodeObjectsToSpeckle = description.encodeSpeckle;
            this.setup();
        }

        #endregion

        #region main setup calls

        private void setup()
        {
            server.apiCall(@"/api/handshake", Method.POST, null, (success, response) =>
            {
                if (!success) throw new Exception("Handshake failed.");

                if (server.streamId == null) // => create a new stream
                    server.apiCall(@"/api/stream", Method.POST, null, (success_stream, response_stream) =>
                    {
                        if (!success_stream)
                        {
                            throw new Exception("Failed to create a new stream.");
                        }

                        server.streamId = response_stream.streamId;
                        this.setupWebsocket();
                    });
                else // => check if stream exists
                    server.apiCall(@"/api/stream/exists", Method.GET, null, (success_existance, response_existance) =>
                    {
                        if ((bool)response_existance.found == false) throw new Exception("Failed to find stream. " + server.streamId);
                        this.setupWebsocket();
                    });
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

                byte[] payload = server.compressPayload(
                    new
                    {
                        objects = converter.convert(this.objects),
                        objectProperties = converter.getObjectProperties(this.objects),
                        layers = layers,
                        streamName = name
                    }
                 );

                server.apiCall(@"/api/live", Method.POST, payload, (success, response) =>
                {
                    OnDataSent?.Invoke(this, new SpeckleEventArgs(success.ToString()));
                });

                DataSender.Stop();
            };

            // metadata sender debouncer
            MetadataSender = new System.Timers.Timer(metaDebounceInterval);
            MetadataSender.AutoReset = false; MetadataSender.Enabled = false;
            MetadataSender.Elapsed += (sender, e) =>
            {
                Debug.WriteLine("SPKSENDER: Sending meta payload.");

                byte[] payload = server.compressPayload(
                    new
                    {
                        layers = layers,
                        streamName = name
                    }
                );

                server.apiCall(@"/api/metadata", Method.POST, payload, (success, response) =>
                {
                    OnDataSent?.Invoke(this, new SpeckleEventArgs(success.ToString()));
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
                    OnVolatileMessage?.Invoke(this, new SpeckleEventArgs("volatile-message", message));
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
        /// Sends a volatile message that will be broadcast to this stream's clients.
        /// </summary>
        /// <param name="message">Message to broadcast.</param>
        public void sendVolatile(string message)
        {
            this.ws.Send(JsonConvert.SerializeObject(new { eventName = "volatile-message", args = message }));
        }

        /// <summary>
        /// Saves instance to the stream history.
        /// </summary>
        /// <param name="name">A specific name to save it by.</param>
        public void saveToHistory(string name = "No Name")
        {
            throw new NotImplementedException();
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
            if(delete)
            {
                //server.apiCall(@"/api/stream", Method.DELETE, etc, etc) 
            }
            ws.Close();
            DataSender.Dispose();
            MetadataSender.Dispose();
            wsReconnecter.Dispose();
            isReadyCheck.Dispose();
        }
    }
}
