using System;
using System.Threading;
using System.Threading.Tasks;
using Tridion.Dxa.Api.Client.HttpClient.Auth;
using Tridion.Dxa.Api.Client.HttpClient.Request;
using Tridion.Dxa.Api.Client.HttpClient.Response;

namespace Tridion.Dxa.Api.Client.HttpClient
{
    /// <summary>
    /// Http Client
    /// </summary>
    public interface IHttpClient
    {
        Uri BaseUri { get; set; }
        int Timeout { get; set; }
        bool Ping();
        int RetryCount { get; set; }
        string UserAgent { get; set; }
        HttpHeaders Headers { get; set; }
        IHttpClientResponse<T> Execute<T>(IHttpClientRequest request);
        Task<IHttpClientResponse<T>> ExecuteAsync<T>(IHttpClientRequest request, CancellationToken cancellationToken);
    }
}
