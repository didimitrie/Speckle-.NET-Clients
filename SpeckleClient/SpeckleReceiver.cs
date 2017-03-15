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
    public class SpeckleReceiver
    {
        // connectivity:
        SpeckleServer server;
        WebSocket ws;

        // converer:
        SpeckleConverter converter;

        // cache:
        Dictionary<string, object> cache;
        List<string> cacheKeys;
        int maxCacheEl = 10000;

        //timers:
        int reconnDebounceInterval = 1000;

        System.Timers.Timer isReadyCheck;
        System.Timers.Timer wsReconnecter;

        //first call stream
        dynamic streamLiveInstance;
        bool streamFound = false;

        /// <summary>
        /// Event emitted when everything is ready.
        /// </summary>
        public event SpeckleEvents OnReady;
        /// <summary>
        /// Event emitted when a volatile message was received.
        /// </summary>
        public event SpeckleEvents OnMessage;
        public event SpeckleEvents OnBroadcast; // do we need the separation? maybe yeah

        public event SpeckleEvents OnDataMessage;

        public event SpeckleEvents OnData;
        public event SpeckleEvents OnMetadata;
        public event SpeckleEvents OnHistory;
        public event SpeckleEvents OnError;

        #region constructors

        public SpeckleReceiver(string apiUrl, string token, string streamId, SpeckleConverter _converter, string documentId = null, string documentName = null)
        {
            converter = _converter;

            server = new SpeckleServer(apiUrl, token, streamId);
            server.OnError += (sender, e) =>
            {
                this.OnError?.Invoke(this, new SpeckleEventArgs(e.EventInfo));
            };

            server.OnReady += (sender, e) =>
            {
                this.setup();
            };
        }

        public SpeckleReceiver(string serialisedObject, SpeckleConverter _converter)
        {
            // TO MASSIVE DO
            // LOL

            dynamic description = JsonConvert.DeserializeObject(serialisedObject);
            server = new SpeckleServer((string)description.restEndpoint, (string)description.token, (string)description.streamId);

            converter = _converter;
            converter.encodeObjectsToNative = description.encodeNative;
            converter.encodeObjectsToSpeckle = description.encodeSpeckle;

            server.OnError += (sender, e) =>
            {
                this.OnError?.Invoke(this, new SpeckleEventArgs(e.EventInfo));
            };

            server.OnReady += (sender, e) =>
            {
                this.setup();
            };
        }

        #endregion

        #region main setup calls

        private void setup()
        {
            cache = new Dictionary<string, object>();
            cacheKeys = new List<string>();

            server.getStream((success, response) =>
            {
                if (!success)
                {
                    OnError?.Invoke(this, new SpeckleEventArgs("Failed to retrieve stream."));
                    return;
                }
                streamFound = true;
                streamLiveInstance = response; // this will fail!!!
                setupWebsocket();
            });

            // ready is defined as: streamId exists && wsSessionId && streamWas found.
            isReadyCheck = new System.Timers.Timer(150);
            isReadyCheck.AutoReset = false; isReadyCheck.Enabled = true;
            isReadyCheck.Elapsed += (sender, e) =>
            {
                if (server.streamId == null) { isReadyCheck.Start(); return; }
                if (server.wsSessionId == null) { isReadyCheck.Start(); return; }
                if (!streamFound) { isReadyCheck.Start(); return; }

                getObjects(streamLiveInstance as ExpandoObject, (castObjects) =>
                {
                    dynamic eventData = new ExpandoObject();
                    eventData.objects = castObjects;
                    eventData.layers = SpeckleLayer.fromExpandoList(streamLiveInstance.layers);
                    eventData.name = streamLiveInstance.name;

                    OnReady?.Invoke(this, new SpeckleEventArgs("receiver ready", eventData));
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
                ws.Send(JsonConvert.SerializeObject(new { eventName = "join-stream", args = new { streamid = server.streamId, role = "receiver" } }));
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

                dynamic message = JsonConvert.DeserializeObject<ExpandoObject>(e.Data);

                if (message.eventName == "ws-session-id")
                {
                    server.wsSessionId = message.sessionId;
                    return;
                }

                if (message.eventName == "volatile-broadcast")
                {
                    OnBroadcast?.Invoke(this, new SpeckleEventArgs("volatile-broadcast", message));
                    return;
                }

                if (message.eventName == "volatile-message")
                {
                    OnMessage?.Invoke(this, new SpeckleEventArgs("volatile-message", message));
                    return;
                }

                if (message.eventName == "live-update")
                {
                    OnDataMessage?.Invoke(this, new SpeckleEventArgs("Received update notification."));
                    getObjects(message.args as ExpandoObject, (castObjects) =>
                    {
                        dynamic eventData = new ExpandoObject();
                        eventData.objects = castObjects;
                        eventData.layers = SpeckleLayer.fromExpandoList(message.args.layers);
                        eventData.name = message.args.name;

                        OnData?.Invoke(this, new SpeckleEventArgs("live-update", eventData));
                    });
                    return;
                }

                if (message.eventName == "metadata-update")
                {
                    dynamic eventData = new ExpandoObject();
                    eventData.name = message.args.name;
                    eventData.layers = SpeckleLayer.fromExpandoList(message.args.layers);
                    OnMetadata?.Invoke(this, new SpeckleEventArgs("metadata-update", eventData));
                    return;
                }

                if (message.eventName == "history-update")
                {
                    // TODO
                    OnHistory?.Invoke(this, new SpeckleEventArgs("history-update"));
                    return;
                }
            };

            ws.Connect();
        }

        #endregion

        #region object getters

        public void getObjects(dynamic liveUpdate, Action<List<object>> callback)
        {
            // if stream contains no objects:
            if (liveUpdate.objects.Count == 0)
            {
                callback(new List<object>());
                return;
            }

            //List<object> castObjects = new List<object>((int) liveUpdate.objects.Count);
            //for (int i = 0; i < castObjects.Capacity; i++) castObjects.Add("this shouldn't be here.");

            object[] castObjects = new object[(int)liveUpdate.objects.Count];


            int k = 0; // current list head
            int insertionCount = 0; // cast objects head
            foreach (dynamic obj in liveUpdate.objects)
            {
                // check if we have a user prop
                dynamic prop = null;
                foreach (var myprop in liveUpdate.objectProperties)
                {
                    if (myprop.objectIndex == k)
                        prop = myprop;
                }

                // TODO: Async doesn't guarantee object order.
                // need to switch toa insertAt(k, obj) list, and pass that through politely to the guy below;
                getObject(obj as ExpandoObject, prop as ExpandoObject, k, (encodedObject, index) =>
                {
                    castObjects[index] = encodedObject;
                    if (++insertionCount == (int)liveUpdate.objects.Count)
                        callback(castObjects.ToList());
                });

                k++;
            }
        }

        public void getObject(dynamic obj, dynamic objectProperties, int index, Action<object, int> callback)
        {
            if (converter.nonHashedTypes.Contains((string)obj.type))
            {
                callback(converter.encodeObject(obj, objectProperties), index);
                return;
            }

            if (cache.ContainsKey(obj.hash))
            {
                callback(cache[obj.hash], index);
                return;
            }

            var type = "";
            if (converter.encodedTypes.Contains((string)obj.type))
                type = "native";

            server.getGeometry((string)obj.hash, type, (success, response) =>
            {
                if(!success)
                {
                    callback("Failed to retrieve object: " + (string) obj.hash, index);
                    return;
                }

                var castObject = converter.encodeObject(response.data, objectProperties);
                addToCache((string)obj.hash, castObject);

                callback(castObject, index);
            });
        }

        private void addToCache(string hash, object obj)
        {
            var cacheAdded = false;
            try
            {
                cache.Add(hash, obj);
                cacheAdded = true;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Cache already contained said object." + e.ToString());
            };

            if (cacheAdded)
                cacheKeys.Add((string)hash);

            if (cache.Count >= maxCacheEl)
            {
                cache.Remove(cacheKeys.First());
                cacheKeys.RemoveAt(0);
            }

        }

        public string getStreamId()
        {
            return server.streamId;
        }

        public string getServer()
        {
            return server.restEndpoint;
        }

        public string getToken()
        {
            return server.token;
        }

        #endregion

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
            // look ma, json! XD c#
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

        public void Dispose(bool delete = false)
        {
            if (delete)
            {
                //server.apiCall(@"/api/stream", Method.DELETE, etc, etc 
            }

            if (ws != null) ws.Close();
            if (wsReconnecter != null) wsReconnecter.Dispose();
            if (isReadyCheck != null) isReadyCheck.Dispose();
        }
    }
}
