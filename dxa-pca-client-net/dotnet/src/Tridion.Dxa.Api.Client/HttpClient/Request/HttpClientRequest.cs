using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using Tridion.Dxa.Api.Client.HttpClient.Auth;
using Tridion.Dxa.Api.Client.HttpClient.Utils;

namespace Tridion.Dxa.Api.Client.HttpClient.Request
{
    /// <summary>
    /// Http Client Request
    /// </summary>
    public class HttpClientRequest : IHttpClientRequest
    {
        public string AbsoluteUri { get; set; }
        public string Path { get; set; }
        public string Method { get; set; } = "GET";
        public string ContentType { get; set; } = "application/json; charset=utf-8";
        public object Body { get; set; } = string.Empty;
        public HttpQueryParams QueryParameters { get; } = new HttpQueryParams();
        public HttpHeaders Headers { get; set; } = new HttpHeaders();
        public IAuthentication Authentication { get; set; }
        public ISerializationBinder Binder { get; set; }
        public List<JsonConverter> Convertors { get; set; }

        public virtual Uri BuildRequestUri(IHttpClient httpClient)
        {
            if (!string.IsNullOrEmpty(AbsoluteUri))
                return new Uri(AbsoluteUri);

            return UriCreator.FromUri(httpClient.BaseUri)
                .WithPath(Path)
                .WithQueryParams(QueryParameters)
                .Build();
        }

        public HttpClientRequest()
        {
        }

        public HttpClientRequest(IHttpClientRequest request)
        {
            AbsoluteUri = request.AbsoluteUri;
            Path = request.Path;
            Method = request.Method;
            ContentType = request.ContentType;
            Body = request.Body;
            QueryParameters = new HttpQueryParams(request.QueryParameters);
            if (request.Headers != null)
                Headers = new HttpHeaders(request.Headers);
            Authentication = request.Authentication;
            Binder = request.Binder;
            Convertors = request.Convertors;
        }

        public object Clone() => new HttpClientRequest(this);

        public override string ToString()
            => $"HttpRequest: Path={Path} Method={Method} ContentType={ContentType}";
    }
}
