using System.Net;
using Tridion.Dxa.Api.Client.HttpClient.Request;

namespace Tridion.Dxa.Api.Client.HttpClient.Auth
{
    /// <summary>
    /// Authentication
    /// </summary>
    public interface IAuthentication : ICredentials
    {
        /// <summary>
        /// Provide manual authentication to request (instead of server challange)
        /// </summary>
        void ApplyManualAuthentication(IHttpClientRequest request);
    }
}
