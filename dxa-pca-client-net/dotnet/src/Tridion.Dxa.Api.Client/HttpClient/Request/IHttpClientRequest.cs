using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using Tridion.Dxa.Api.Client.HttpClient.Auth;

namespace Tridion.Dxa.Api.Client.HttpClient.Request
{
    /// <summary>
    /// Http Client Request
    /// </summary>
    public interface IHttpClientRequest : ICloneable
    {
        /// <summary>
        /// Absolute Uri
        /// </summary>
        string AbsoluteUri { get; set; }

        /// <summary>
        /// Path
        /// </summary>
        string Path { get; set; }

        /// <summary>
        /// Http Method
        /// </summary>
        string Method { get; set; }

        /// <summary>
        /// Content Type
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// Request Body
        /// </summary>
        object Body { get; set; }

        /// <summary>
        /// Query Parameters
        /// </summary>
        HttpQueryParams QueryParameters { get; }

        /// <summary>
        /// Request Headers
        /// </summary>
        HttpHeaders Headers { get; set; }

        /// <summary>
        /// Authentication
        /// </summary>
        IAuthentication Authentication { get; set; }

        /// <summary>
        /// Builds full request uri
        /// </summary>
        /// <param name="httpClient">Http Client</param>
        /// <returns>Full Uri of request</returns>
        Uri BuildRequestUri(IHttpClient httpClient);

        /// <summary>
        /// Serialization binder used when deserializing the request.
        /// </summary>
        ISerializationBinder Binder { get; set; }

        /// <summary>
        /// Convertor used when deserialzing the request
        /// </summary>
        List<JsonConverter> Convertors { get; set; }
    }
}
