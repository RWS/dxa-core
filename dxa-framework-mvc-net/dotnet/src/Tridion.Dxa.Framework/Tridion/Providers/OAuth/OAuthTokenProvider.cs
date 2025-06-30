using System;
using System.Collections.Concurrent;
using System.Security.Authentication;
using Microsoft.Extensions.Options;
using Sdl.Web.Common;
using Tridion.Dxa.Api.Client.HttpClient;
using Tridion.Dxa.Framework.Tridion.Providers.Discovery;

namespace Tridion.Dxa.Framework.Tridion.Providers.OAuth
{
    public class OAuthTokenProvider : IOAuthTokenProvider
    {
        private readonly IDiscoveryClient _discoveryClient;
        private readonly ConcurrentDictionary<string, IToken> _tokens = new ConcurrentDictionary<string, IToken>();
        private readonly string _clientId;
        private readonly string _clientSecret;
        private Uri _endpoint;

        public OAuthTokenProvider(IDiscoveryClient discoveryClient, IOptions<DxaFrameworkOptions> options)
        {
            _discoveryClient = discoveryClient;
            _endpoint = options.Value.Services.Token;
            _clientId = options.Value.OAuth.ClientId;
            _clientSecret = options.Value.OAuth.ClientSecret;
            if (options.Value.OAuth == null)
                throw new DxaException("OAuth settings not provided in configuration.");
            Enabled = options.Value.OAuth.Enabled;
        }

        public bool Enabled { get; set; }

        public HttpHeaders OAuthHeaders => Enabled ? new HttpHeaders { { "authorization", $"Bearer {TokenNoExceptions?.AccessToken}" } } : new HttpHeaders();

        public virtual IToken TokenNoExceptions
        {
            get
            {
                try
                {
                    return Enabled ? GetTokenAndRefresh : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        private Uri GetTokenServiceUri
        {
            get
            {
                if (_endpoint == null)
                {
                    _endpoint = _discoveryClient.GetServiceCapability("TokenService", null)?.Uri;
                }

                return _endpoint;
            }
        }

        private OAuthClient Client
        {
            get
            {
                var endpoint = GetTokenServiceUri;
                return endpoint != null ? new OAuthClient(_endpoint) : null;
            }
        }

        private IToken GetTokenAndRefresh =>
            _tokens.AddOrUpdate("TOKEN", key => CreateOrRefreshToken(null),
                (key, value) => CreateOrRefreshToken(value));

        protected virtual IToken CreateOrRefreshToken(IToken token)
        {
            OAuthClient client = Client;
            if (client == null) return null;

            if (token == null)
            {
                try
                {
                    token = client.GetToken(_clientId, _clientSecret);
                }
                catch (AuthenticationException)
                {
                    throw;
                }
                catch
                {

                }
            }

            if (token == null || !token.Expired) return token;

            RefreshableToken refreshableToken = token as RefreshableToken;
            if (refreshableToken != null)
            {
                try
                {
                    return client.RefreshToken(refreshableToken.RefreshToken, _clientId);
                }
                catch
                {
                    // this could happen if the token service goes down and is then brought back up. The refresh token
                    // will then be invalid so we should just attempt to request a new token.
                    token = null;
                }
            }

            // one last try to grab a token
            try
            {
                token = client.GetToken(_clientId, _clientSecret);
            }
            catch
            {
            }

            return token;
        }
    }
}
