using System;
using Tridion.Dxa.Api.Client.HttpClient;
using Tridion.Dxa.Api.Client.HttpClient.Request;

namespace Tridion.Dxa.Framework.Tridion.Providers.OAuth
{
    public class OAuthClient
    {
        private class OAuthResponseData
        {
            public string Access_Token { get; set; }
            public string Refresh_Token { get; set; }
            public string Token_Type { get; set; }
            public int? Expires_In { get; set; }
        }

        private readonly IHttpClient _client;

        public OAuthClient(string endpoint) : this(new Uri(endpoint))
        {
        }

        public OAuthClient(Uri endpoint)
        {
            _client = new HttpClient(endpoint);
        }

        public IToken RefreshToken(string refreshToken, string clientId = null, string clientResource = null, bool includeGrantType = true)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new ArgumentNullException("refreshToken");
            }

            string parameters =
                $"{(includeGrantType ? "grant_type=refresh_token&" : string.Empty)}refresh_token={refreshToken}";
            if (!string.IsNullOrEmpty(clientId))
            {
                parameters += "&client_id=" + clientId;
            }
            if (!string.IsNullOrEmpty(clientResource))
            {
                parameters += "&resource=" + clientResource;
            }

            return ProcessRequest(parameters);
        }

        public IToken GetToken(string clientId, string clientSecret, string clientResource = null, bool includeGrantType = true)
        {
            string parameters =
                $"{(includeGrantType ? "grant_type=client_credentials&" : string.Empty)}client_id={clientId}&client_secret={clientSecret}";
            if (!string.IsNullOrEmpty(clientResource))
            {
                parameters += "&resource=" + clientResource;
            }

            return ProcessRequest(parameters);
        }

        private IToken ProcessRequest(string parameters)
        {
            try
            {
                var oauthData = _client.Execute<OAuthResponseData>(new HttpClientRequest
                {
                    ContentType = "application/x-www-form-urlencoded",
                    Method = "POST",
                    Body = parameters
                }).ResponseData;

                IToken token = new Token();
                if (oauthData.Refresh_Token != null)
                {
                    token = new RefreshableToken { AccessToken = oauthData.Access_Token, RefreshToken = oauthData.Refresh_Token };
                }
                else
                {
                    token.AccessToken = oauthData.Access_Token;
                }

                if (oauthData.Expires_In != null)
                {
                    token.ExpiresAt = DateTime.UtcNow.AddSeconds(oauthData.Expires_In.Value);
                }

                return token;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
