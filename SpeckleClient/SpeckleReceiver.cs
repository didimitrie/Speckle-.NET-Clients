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
        public event SpeckleEvents OnVolatileMessage;

        public event SpeckleEvents OnData;
        public event SpeckleEvents OnMetadata;
        public event SpeckleEvents OnHistory;

        #region constructors

        public SpeckleReceiver(SpeckleServer _server, SpeckleConverter _converter, string documentId = null, string documentName = null)
        {
            if (_server.streamId == null)
                throw new Exception("Receivers need a stream to listen to!");
            server = _server;
            converter = _converter;

            setup();
        }

        public SpeckleReceiver(string serialisedObject, SpeckleConverter _converter)
        {
            dynamic description = JsonConvert.DeserializeObject(serialisedObject);
            server = new SpeckleServer((string)description.restEndpoint, (string)description.wsEndpoint, (string)description.token, (string)description.streamId);

            converter = _converter;
            converter.encodeObjectsToNative = description.encodeNative;
            converter.encodeObjectsToSpeckle = description.encodeSpeckle;

            setup();
        }

        #endregion

        #region main setup calls

        private void setup()
        {
            cache = new Dictionary<string, object>();
            cacheKeys = new List<string>();

            server.apiCall(@"/api/handshake", Method.POST, null, (success, response) =>
            {
                server.apiCall(@"/api/stream", Method.GET, null, (success_gotStream, response_gotStream) =>
                {
                    if (!success_gotStream)
                        throw new Exception("Failed to retrieve stream.");
                    else
                    {
                        streamLiveInstance = response_gotStream;
                        streamFound = true;
                    }

                    setupWebsocket();
                });
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

                if (message.eventName == "volatile-message")
                {
                    OnVolatileMessage?.Invoke(this, new SpeckleEventArgs("volatile-message", message));
                    return;
                }

                if (message.eventName == "live-update")
                {
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
                foreach(var myprop in liveUpdate.objectProperties)
                {
                    if (myprop.objectIndex == k)
                        prop = myprop;
                }

                // TODO: Async doesn't guarantee object order anymore.
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

        public void getObject(dynamic obj, dynamic objectProperties, int index, Action<object,int> callback)
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

            var endpoint = @"/api/object?hash=" + (string)obj.hash;

            // if it's a brep or a curve, since we're decoding in .net, don't get
            // the speckle value (ie, a brep will just get his base64)
            // tricky: what if i will have a dynamo client requesting a
            // rhino object? 
            if (converter.encodedTypes.Contains((string)obj.type))
                endpoint += "&excludeValue=true";

            server.apiCall(endpoint, Method.GET, null, (success, response) =>
            {
                if (response.success)
                {
                    var castObject = converter.encodeObject(response.obj, objectProperties);
                    var cacheAdded = false;
                    try
                    {
                        cache.Add((string)obj.hash, castObject);
                        cacheAdded = true;
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Cache already contained said object." + e.ToString());
                    };

                    if (cacheAdded) cacheKeys.Add((string)obj.hash);

                    if (cache.Count >= maxCacheEl)
                    {
                        cache.Remove(cacheKeys.First());
                        cacheKeys.RemoveAt(0);
                    }

                    callback(castObject, index);
                }
                else
                    callback("Failed to retrieve object from server: " + response.objectHash, index);
            });
        }

        public string getStreamId()
        {
            return server.streamId;
        }

        public string getServer()
        {
            return server.restEndpoint;
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
            ws.Close();
            wsReconnecter.Dispose();
            isReadyCheck.Dispose();
        }
    }
}
