using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tridion.Dxa.Api.Client.Core;
using Tridion.Dxa.Api.Client.HttpClient.Auth;
using Tridion.Dxa.Api.Client.HttpClient.Exceptions;
using Tridion.Dxa.Api.Client.HttpClient.Request;
using Tridion.Dxa.Api.Client.HttpClient.Response;
using SystemNetHttpClient = System.Net.Http.HttpClient;

namespace Tridion.Dxa.Api.Client.HttpClient
{
    /// <summary>
    /// Http Client
    /// </summary>
    public class HttpClient : IHttpClient, IDisposable
    {
        public Uri BaseUri { get; set; }
        public int Timeout { get; set; } = 10000;
        public int RetryCount { get; set; } = 3;
        public string UserAgent { get; set; } = "TRIDION.PCA.NET";
        public HttpHeaders Headers { get; set; } = new HttpHeaders();
        public ILogger Logger { get; } = new NullLogger();
        protected readonly IAuthentication _auth;

        private readonly SystemNetHttpClient _http;

        public HttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _http = new SystemNetHttpClient(handler, disposeHandler: true)
            {
                // Per-request timeout is enforced via CancellationToken so callers can change Timeout dynamically.
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
        }

        public HttpClient(string endpoint) : this()
        {
            BaseUri = new Uri(endpoint);
        }

        public HttpClient(string endpoint, IAuthentication auth) : this()
        {
            BaseUri = new Uri(endpoint);
            _auth = auth;
        }

        public HttpClient(Uri endpoint) : this()
        {
            BaseUri = endpoint;
        }

        public HttpClient(Uri endpoint, IAuthentication auth) : this()
        {
            BaseUri = endpoint;
            _auth = auth;
        }

        public HttpClient(string endpoint, ILogger logger) : this(endpoint)
        {
            Logger = logger ?? new NullLogger();
        }

        public HttpClient(string endpoint, ILogger logger, IAuthentication auth) : this(endpoint, auth)
        {
            Logger = logger ?? new NullLogger();
        }

        public HttpClient(Uri endpoint, ILogger logger) : this(endpoint)
        {
            Logger = logger ?? new NullLogger();
        }

        public HttpClient(Uri endpoint, ILogger logger, IAuthentication auth) : this(endpoint, auth)
        {
            Logger = logger ?? new NullLogger();
        }

        public virtual bool Ping()
        {
            try
            {
                using (HttpRequestMessage request = CreateHttpRequest(new HttpClientRequest { Method = "HEAD" }))
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Timeout)))
                using (HttpResponseMessage response = _http
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                    .GetAwaiter().GetResult())
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public virtual IHttpClientResponse<T> Execute<T>(IHttpClientRequest clientRequest)
            => ExecuteAsync<T>(clientRequest, CancellationToken.None).GetAwaiter().GetResult();

        public virtual async Task<IHttpClientResponse<T>> ExecuteAsync<T>(IHttpClientRequest clientRequest,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                return await RetryBlockAsync(
                    () => SendOnceAsync<T>(clientRequest, cancellationToken),
                    RetryCount).ConfigureAwait(false);
            }
            catch (HttpClientException)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to get http response from '{BaseUri}' with request: '{clientRequest}'");
                throw new HttpClientException(
                    $"Failed to get http response from '{BaseUri}' with request: {clientRequest}", e);
            }
        }

        private async Task<IHttpClientResponse<T>> SendOnceAsync<T>(IHttpClientRequest clientRequest,
            CancellationToken cancellationToken)
        {
            using (HttpRequestMessage request = CreateHttpRequest(clientRequest))
            using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Timeout)))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            using (HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, linkedCts.Token)
                .ConfigureAwait(false))
            {
                byte[] data = response.Content != null
                    ? await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
                    : Array.Empty<byte>();

                LogErrorResponse(data);

                string contentType = response.Content?.Headers.ContentType?.ToString();

                if (!response.IsSuccessStatusCode)
                {
                    // Mirror original WebException-based behavior: surface status + body to caller.
                    throw new HttpClientException(
                        $"Failed to get http response from '{BaseUri}' with request: {clientRequest}",
                        null,
                        (int)response.StatusCode,
                        Encoding.UTF8.GetString(data));
                }

                T deserialized = Deserialize<T>(data, contentType, clientRequest.Binder, clientRequest.Convertors);

                return new HttpClientResponse<T>
                {
                    StatusCode = (int)response.StatusCode,
                    ContentType = contentType,
                    Headers = BuildHeaders(response),
                    ResponseData = deserialized
                };
            }
        }

        protected virtual HttpRequestMessage CreateHttpRequest(IHttpClientRequest clientRequest)
        {
            IHttpClientRequest requestCopy = new HttpClientRequest(clientRequest);
            requestCopy.Authentication = requestCopy.Authentication ?? _auth;
            Uri requestUri = requestCopy.BuildRequestUri(this);

            // BasicHttpAuth (and similar) inject auth headers into requestCopy.Headers here.
            // ICredentials-based challenge auth (HttpWebRequest.Credentials) is no longer supported —
            // implement IAuthentication.ApplyManualAuthentication instead.
            requestCopy.Authentication?.ApplyManualAuthentication(requestCopy);

            var request = new HttpRequestMessage(new HttpMethod(requestCopy.Method), requestUri);

            if (!string.IsNullOrEmpty(UserAgent))
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            foreach (var x in Headers)
                TryAddHeader(request, x.Key, x.Value?.ToString());
            foreach (var x in requestCopy.Headers)
                TryAddHeader(request, x.Key, x.Value?.ToString());

            // Preserve original behavior: only POST carries a body.
            if (requestCopy.Method == "POST")
            {
                byte[] serialized = Serialize(requestCopy.Body, requestCopy.ContentType);
                var content = new ByteArrayContent(serialized ?? Array.Empty<byte>());
                if (!string.IsNullOrEmpty(requestCopy.ContentType))
                {
                    try
                    {
                        content.Headers.ContentType = MediaTypeHeaderValue.Parse(requestCopy.ContentType);
                    }
                    catch
                    {
                        // best-effort — ignore unparseable content type
                    }
                }
                request.Content = content;
            }

            if (Logger.IsTracingEnabled)
            {
                Logger.Trace("Performing Http Request:");
                Logger.Trace($"[{request.Method}] {request.RequestUri}");
                Logger.Trace($"[BODY] {requestCopy.Body}");
                foreach (var h in request.Headers)
                    Logger.Trace($"[HEADER] {h.Key}={string.Join(",", h.Value)}");
                if (request.Content?.Headers != null)
                {
                    foreach (var h in request.Content.Headers)
                        Logger.Trace($"[HEADER] {h.Key}={string.Join(",", h.Value)}");
                }
            }

            return request;
        }

        private static void TryAddHeader(HttpRequestMessage request, string key, string value)
        {
            if (string.IsNullOrEmpty(key) || value == null) return;
            // Content-Type belongs on Content.Headers — handled when the POST body is attached.
            if (string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase)) return;

            if (!request.Headers.TryAddWithoutValidation(key, value))
                request.Content?.Headers.TryAddWithoutValidation(key, value);
        }

        private static HttpHeaders BuildHeaders(HttpResponseMessage response)
        {
            var headers = new HttpHeaders();
            foreach (var h in response.Headers)
                headers[h.Key] = string.Join(",", h.Value);
            if (response.Content?.Headers != null)
            {
                foreach (var h in response.Content.Headers)
                    headers[h.Key] = string.Join(",", h.Value);
            }
            return headers;
        }

        private void LogErrorResponse(byte[] data)
        {
            string responseData = string.Empty;
            try
            {
                responseData = Encoding.UTF8.GetString(data);
            }
            catch { }

            if (Logger.IsTracingEnabled && responseData.Contains("errors"))
            {
                Logger.Trace($"Error Response: {responseData}");
            }
        }

        protected virtual bool IsJsonMimeType(string contentType)
            => !string.IsNullOrEmpty(contentType) && contentType.ToLower().Contains("application/json");

        protected virtual byte[] Serialize(object data, string contentType)
        {
            if (data is byte[])
                return (byte[])data;

            if (data is string)
                return Encoding.UTF8.GetBytes((string)data);

            if (IsJsonMimeType(contentType))
                return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));

            throw new Exception($"{contentType} not supported.");
        }

        protected virtual T Deserialize<T>(byte[] data, string contentType, ISerializationBinder binder, List<JsonConverter> convertors)
        {
            if (data == null)
                return default(T);

            if (typeof(T) == typeof(byte[]))
                return (T)(object)data;

            if (typeof(T) == typeof(string))
                return (T)(object)Encoding.UTF8.GetString(data);

            if (!IsJsonMimeType(contentType))
                throw new HttpClientException($"ContentType: '{contentType}' not supported.");

            string json = Encoding.UTF8.GetString(data);
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                SerializationBinder = binder
            };
            if (convertors == null) return JsonConvert.DeserializeObject<T>(json, settings);
            foreach (var x in convertors)
                settings.Converters.Add(x);
            return JsonConvert.DeserializeObject<T>(json, settings);
        }

        protected async Task<T> RetryBlockAsync<T>(Func<Task<T>> block, int retryCount)
        {
            if (retryCount < 0)
                return default(T);

            int sleepTime = 1000;
            while (retryCount > 0)
            {
                retryCount--;
                try
                {
                    return await block().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (e is HttpClientException hce)
                    {
                        Logger.Debug($"Received HTTP error status code = {hce.StatusCode}");
                    }
                    else if (e is HttpRequestException hre)
                    {
                        Logger.Debug($"Received HTTP exception: {hre.Message}");
                    }

                    if (retryCount <= 0)
                    {
                        Logger.Debug("Failed to receive a valid response after exhausting all retry attempts..");

                        if (e is HttpClientException finalHce && !string.IsNullOrEmpty(finalHce.Response))
                        {
                            try
                            {
                                dynamic obj = JsonConvert.DeserializeObject(finalHce.Response);
                                var serverResponseMsg = obj?.error?.message;
                                if (serverResponseMsg != null)
                                    Logger.Debug($"Response message from server was {serverResponseMsg}");
                            }
                            catch
                            {
                                // body may not be JSON — ignore
                            }
                        }

                        throw;
                    }

                    Logger.Debug($"Sleeping for {sleepTime}ms");
                    await Task.Delay(sleepTime).ConfigureAwait(false);
                    sleepTime += sleepTime;
                }
            }

            return default(T);
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
