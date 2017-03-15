using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleClient
{
    /// <summary>
    /// Helper class for speckle functionality.
    /// </summary>
    [Serializable]
    public class SpeckleServer
    {
        public string wsEndpoint, restEndpoint, wsSessionId, token, streamId;
        public event SpeckleEvents OnReady;
        public event SpeckleEvents OnError;

        public dynamic stream;

        public SpeckleServer(string apiUrl, string _token, string _streamId = null)
        {
            token = _token;
            restEndpoint = apiUrl;
            streamId = _streamId;

            var client = new RestClient(apiUrl);
            var request = new RestRequest(Method.GET);

            request.AddHeader("speckle-token", token);
            client.ExecuteAsync(request, response =>
            {
                dynamic parsedResponse;

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    OnError?.Invoke(this, new SpeckleEventArgs("Failed to contact server on init."));
                    return;
                }

                parsedResponse = parseResponse(response);
                if (parsedResponse == null)
                {
                    OnError?.Invoke(this, new SpeckleEventArgs("Failed to parse server response on init."));
                    return;
                }

                wsEndpoint = parsedResponse.ws;
                OnReady?.Invoke(this, null);
            });
        }

        /// <summary>
        /// Makes a generic API call. Sets the necessary custom headers (speckle-token, speckle-stream-id, speckle-ws-id).
        /// </summary>
        /// <param name="endpoint">Usually @"/api/ACTION"</param>
        /// <param name="method">POST or GET</param>
        /// <param name="payload">The compressed payload. Use the "compressPayload" to compress it.</param>
        /// <param name="callback">Takes two arguments: response success (bool) and server response.</param>
        public void genericApiCall(string endpoint, Method method, byte[] payload, Action<bool, dynamic> callback)
        {
            var client = new RestClient(restEndpoint + endpoint);
            var request = new RestRequest(method);

            request.AddHeader("speckle-token", token); // api access token
            request.AddHeader("speckle-ws-id", wsSessionId); // socket session id

            request.AddHeader("Content-Encoding", "gzip");
            request.AddHeader("content-type", "application/json; charset=utf-8");
            request.AddParameter("application/json", payload, ParameterType.RequestBody);

            client.ExecuteAsync(request, response =>
            {
                dynamic parsedResponse = null;

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    callback(false, null);
                    return;
                }

                parsedResponse = parseResponse(response);
                callback(true, parsedResponse as ExpandoObject);
            });

        }

        public void createNewStream(Action<Boolean, dynamic> callback)
        {
            var client = new RestClient(restEndpoint + @"/streams");
            var request = new RestRequest(Method.POST);
            request.AddHeader("speckle-token", token);

            client.ExecuteAsync(request, response =>
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    callback(false, null);
                    return;
                }

                dynamic parsedResponse = parseResponse(response);
                if (parsedResponse == null)
                {
                    callback(false, null);
                    return;
                }

                callback(true, parsedResponse.data);

            });
        }

        public void createNewStreamHistory(Action<bool, dynamic> callback)
        {
            var client = new RestClient(restEndpoint + @"/streams/" + streamId + "/history");
            var request = new RestRequest(Method.POST);

            request.AddHeader("speckle-token", token); // api access token
            request.AddHeader("speckle-ws-id", wsSessionId); // socket session id

            client.ExecuteAsync(request, response =>
            {
                dynamic parsedResponse = null;

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    callback(false, null);
                    return;
                }

                parsedResponse = parseResponse(response);
                callback(true, parsedResponse as ExpandoObject);
            });
        }

        public void getStream(Action<Boolean, dynamic> callback)
        {
            var client = new RestClient(restEndpoint + @"/streams/" + streamId + "/data");
            var request = new RestRequest(Method.GET);
            request.AddHeader("speckle-token", token);

            client.ExecuteAsync(request, response =>
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    callback(false, null);
                    return;
                }

                dynamic parsedResponse = parseResponse(response);
                if (parsedResponse == null)
                {
                    callback(false, null);
                    return;
                }

                callback(true, parsedResponse.data);
            });
        }

        public void getGeometry(string hash, string type, Action<Boolean, dynamic> callback)
        {
            var client = new RestClient(restEndpoint + @"/geometry/" + hash + "/" + type);
            var request = new RestRequest(Method.GET);

            client.ExecuteAsync(request, response =>
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    callback(false, null);
                    return;
                }
                var parsedResponse = parseResponse(response);
                if (parsedResponse == null)
                {
                    callback(false, null);
                    return;
                }
                callback(true, parsedResponse);
            });
        }

        public void updateStream(dynamic payload, Action<Boolean, dynamic> callback)
        {
            var client = new RestClient(restEndpoint + @"/streams/" + streamId + @"/data");
            var request = new RestRequest(Method.PUT);

            request.AddHeader("speckle-token", token); // api access token
            request.AddHeader("speckle-ws-id", wsSessionId); // socket session id

            request.AddHeader("Content-Encoding", "gzip");
            request.AddHeader("content-type", "application/json; charset=utf-8");
           

            byte[] compressedPayload = compressPayload(payload);

            System.Diagnostics.Debug.WriteLine("---------------------------");
            System.Diagnostics.Debug.WriteLine(compressedPayload.Length);
            System.Diagnostics.Debug.WriteLine("---------------------------");

            if (compressedPayload.Length > 3e6)
            {
                this.OnError?.Invoke(this, new SpeckleEventArgs("Compressed payload size exceeds 3mb. Consider splitting this into multiple streams. Data was NOT sent."));
                return;
            }

            request.AddParameter("application/json", compressedPayload, ParameterType.RequestBody);

            client.ExecuteAsync(request, response =>
            {
                dynamic parsedResponse = null;

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    callback(false, null);
                    return;
                }

                parsedResponse = parseResponse(response);
                callback(true, parsedResponse as ExpandoObject);
            });
        }

        public void updateStreamMetadata(dynamic payload, Action<Boolean, dynamic> callback)
        {
            var client = new RestClient(restEndpoint + @"/streams/" + streamId + @"/meta");
            var request = new RestRequest(Method.PUT);

            request.AddHeader("speckle-token", token); // api access token
            request.AddHeader("speckle-ws-id", wsSessionId); // socket session id

            request.AddHeader("Content-Encoding", "gzip");
            request.AddHeader("content-type", "application/json; charset=utf-8");

            request.AddParameter("application/json", compressPayload(payload), ParameterType.RequestBody);

            client.ExecuteAsync(request, response =>
            {
                dynamic parsedResponse = null;

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    callback(false, null);
                    return;
                }

                parsedResponse = parseResponse(response);
                callback(true, parsedResponse as ExpandoObject);
            });
        }

        #region utils

        public ExpandoObject parseResponse(IRestResponse obj)
        {
            dynamic parsedResponse;
            try
            {
                parsedResponse = JsonConvert.DeserializeObject<ExpandoObject>(obj.Content);
            }
            catch
            {
                parsedResponse = null;
            }
            return parsedResponse as ExpandoObject;
        }

        /// <summary>
        /// Compresses a dynamic object using GZip. Use it to generate payloads for the apiCall() method.
        /// </summary>
        /// <param name="payload">Object to compress.</param>
        /// <returns>The compressed byte array.</returns>
        public byte[] compressPayload(dynamic payload)
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

        #endregion
    }
}
