using System;
using System.Net;
using Tridion.Dxa.Api.Client.HttpClient.Auth;
using Tridion.Dxa.Api.Client.HttpClient.Request;

namespace Tridion.Dxa.Framework.Tridion.Providers.OAuth
{
    /// <summary>
    /// OAuth provider
    /// </summary>
    public class OAuth : IAuthentication
    {
        private readonly IOAuthTokenProvider _oauthTokenProvider;

        public OAuth(IOAuthTokenProvider oauthTokenProvider)
        {
            _oauthTokenProvider = oauthTokenProvider;
        }

        public NetworkCredential GetCredential(Uri uri, string authType) => null;

        /// <summary>
        /// Add OAuth headers to http request.
        /// <remarks>
        /// The CIL TokenProvider implementation handles aquiring/refreshing tokens from the
        /// token service so we can be sure that on call to this our OAuth token is valid.
        /// </remarks>
        /// </summary>
        /// <param name="request">Http Request</param>
        public void ApplyManualAuthentication(IHttpClientRequest request)
        {
            // no token provider means no need to add OAuth token
            if (_oauthTokenProvider == null) return;
            var oauthHeaders = _oauthTokenProvider.OAuthHeaders;
            foreach (var h in oauthHeaders)
            {
                request.Headers.Add(h.Key, h.Value);
            }
        }
    }
}
