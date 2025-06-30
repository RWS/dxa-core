using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Sdl.Web.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Tridion.Dxa.Api.Client;
using Tridion.Dxa.Api.Client.ContentModel;
using Tridion.Dxa.Api.Client.GraphQL.Client;
using Tridion.Dxa.Api.Client.HttpClient.Auth;
using Tridion.Dxa.Api.Client.IqQuery.API;
using Tridion.Dxa.Api.Client.IqQuery.Client;
using Tridion.Dxa.Framework;
using Tridion.Dxa.Framework.ADF;
using Tridion.Dxa.Framework.Tridion.Providers.Discovery;
using Tridion.Dxa.Framework.Tridion.Providers.OAuth;
//using AmbientDataContext = Sdl.Web.Delivery.ServicesCore.ClaimStore.AmbientDataContext;

namespace Sdl.Web.Tridion.ApiClient
{
    public interface IApiClientFactory
    {
        /// <summary>
        /// Add a global claim to send to client
        /// </summary>
        /// <param name="claim">Claim to send</param>
        void AddGlobalClaim(ClaimValue claim);

        /// <summary>
        /// Remove global claim from client
        /// </summary>
        /// <param name="claim">Claim to remove</param>
        void RemoveGlobalClaim(ClaimValue claim);

        /// <summary>
        /// Returns a fully constructed IQ Search client
        /// </summary>
        /// <typeparam name="TSearchResultSet">Type used for result set</typeparam>
        /// <typeparam name="TSearchResult">Type ised for result</typeparam>
        /// <returns>IQ Search Client</returns>
        IqSearchClient<TSearchResultSet, TSearchResult> CreateSearchClient<TSearchResultSet, TSearchResult>()
            where TSearchResultSet : IQueryResultData<TSearchResult> where TSearchResult : IQueryResult;

        /// <summary>
        /// Returns a fully constructed IQ Search client
        /// </summary>
        /// <typeparam name="TSearchResultSet">Type used for result set</typeparam>
        /// <typeparam name="TSearchResult">Type ised for result</typeparam>
        /// <param name="searchIndex">Search Index</param>
        /// <returns>IQ Search Client</returns>
        IqSearchClient<TSearchResultSet, TSearchResult> CreateSearchClient<TSearchResultSet, TSearchResult>(string searchIndex)
            where TSearchResultSet : IQueryResultData<TSearchResult> where TSearchResult : IQueryResult;

        /// <summary>
        /// Returns a fully constructed IQ Search client
        /// </summary>
        /// <typeparam name="TSearchResultSet">Type used for result set</typeparam>
        /// <typeparam name="TSearchResult">Type ised for result</typeparam>
        /// <param name="endpoint">IQ Search endpoint</param>
        /// <param name="searchIndex">Search Index</param>
        /// <returns></returns>
        IqSearchClient<TSearchResultSet, TSearchResult> CreateSearchClient<TSearchResultSet, TSearchResult>(Uri endpoint, string searchIndex)
            where TSearchResultSet : IQueryResultData<TSearchResult> where TSearchResult : IQueryResult;

        /// <summary>
        /// Return a fully constructed Public Content Api client
        /// </summary>
        /// <returns>Public Content Api Client</returns>
        global::Tridion.Dxa.Api.Client.ApiClient CreateClient();
    }

    /// <summary>
    /// Api Client Factory creates clients with context claim forwarding and
    /// OAuthentication for using the GraphQL Api.
    /// </summary>
    public sealed class ApiClientFactory : IApiClientFactory
    {
        private readonly IDiscoveryClient _discoveryClient;
        private readonly IOAuthTokenProvider _oauthTokenProvider;
        private readonly IOptions<DxaFrameworkOptions> _options;

        private Uri _endpoint;
        private Uri _iqEndpoint;
        private string _iqSearchIndex;
        private readonly bool _claimForwarding = true;
        private readonly IAuthentication _oauth;
        private const string PreviewSessionTokenHeader = "x-preview-session-token";
        private const string PreviewSessionTokenCookie = "preview-session-token";

        private readonly ConcurrentDictionary<string, ClaimValue> _globalClaimValues = new ConcurrentDictionary<string, ClaimValue>();

        private readonly ClaimStoreDataService _claimStoreService;

        public ApiClientFactory(IDiscoveryClient discoveryClient, IOAuthTokenProvider oAuthTokenProvider, IOptions<DxaFrameworkOptions> options, ClaimStoreDataService claimStoreService)
        {
            _discoveryClient = discoveryClient;
            _oauthTokenProvider = oAuthTokenProvider;
            _options = options;
            _oauth = new OAuth(_oauthTokenProvider);
            _claimStoreService = claimStoreService;
        }

        private static Uri RewriteContentServiceUri(Uri contentServiceUri)
            => new Uri(contentServiceUri.AbsoluteUri.Replace("content.svc", "cd/api"));

        private static Uri RewriteIQServiceUri(Uri contentServiceUri)
            => new Uri(contentServiceUri.AbsoluteUri.Replace(":8081/content.svc", ":8097/search.svc"));

        private Uri GetEndpoint()
        {
            if (_endpoint != null) return _endpoint;
            var uri = _options.Value.Services.Content;
            if (uri != null)
            {
                _endpoint = RewriteContentServiceUri(uri);
                _iqEndpoint = RewriteIQServiceUri(uri);
                _iqSearchIndex = _options.Value.IQSearchIndex;
            }
            else
            {
                // try discovery service
                var responseContentServiceCapability = _discoveryClient.GetServiceCapability("ContentService", _oauth);
                if (responseContentServiceCapability != null && responseContentServiceCapability.Uri != null)
                {
                    _endpoint = RewriteContentServiceUri(responseContentServiceCapability.Uri);
                    _options.Value.Services.Content = _endpoint;
                }
                var responseIQQueryCapability = _discoveryClient.GetServiceCapability("IQQuery", _oauth);
                if (responseIQQueryCapability != null && responseIQQueryCapability.Uri != null)
                {
                    _iqEndpoint = responseIQQueryCapability.Uri;
                    _options.Value.Services.IQService = _iqEndpoint;
                    _iqSearchIndex = _options.Value.IQSearchIndex;
                }
            }

            return _endpoint;
        }

        /// <summary>
        /// Add a global claim to send to client
        /// </summary>
        /// <param name="claim">Claim to send</param>
        public void AddGlobalClaim(ClaimValue claim)
        {
            if (claim == null) return;
            _globalClaimValues.AddOrUpdate(claim.Uri, claim, (s, value) => value);
        }

        /// <summary>
        /// Remove global claim from client
        /// </summary>
        /// <param name="claim">Claim to remove</param>
        public void RemoveGlobalClaim(ClaimValue claim)
        {
            if (claim == null) return;
            ClaimValue removed;
            _globalClaimValues.TryRemove(claim.Uri, out removed);
        }

        /// <summary>
        /// Returns a fully constructed IQ Search client
        /// </summary>
        /// <typeparam name="TSearchResultSet">Type used for result set</typeparam>
        /// <typeparam name="TSearchResult">Type ised for result</typeparam>
        /// <returns>IQ Search Client</returns>
        public IqSearchClient<TSearchResultSet, TSearchResult> CreateSearchClient<TSearchResultSet, TSearchResult>()
            where TSearchResultSet : IQueryResultData<TSearchResult> where TSearchResult : IQueryResult => new IqSearchClient<TSearchResultSet, TSearchResult>(_iqEndpoint, _oauth, _iqSearchIndex);

        /// <summary>
        /// Returns a fully constructed IQ Search client
        /// </summary>
        /// <typeparam name="TSearchResultSet">Type used for result set</typeparam>
        /// <typeparam name="TSearchResult">Type ised for result</typeparam>
        /// <param name="searchIndex">Search Index</param>
        /// <returns>IQ Search Client</returns>
        public IqSearchClient<TSearchResultSet, TSearchResult> CreateSearchClient<TSearchResultSet, TSearchResult>(string searchIndex)
            where TSearchResultSet : IQueryResultData<TSearchResult> where TSearchResult : IQueryResult => new IqSearchClient<TSearchResultSet, TSearchResult>(_iqEndpoint, _oauth, searchIndex);

        /// <summary>
        /// Returns a fully constructed IQ Search client
        /// </summary>
        /// <typeparam name="TSearchResultSet">Type used for result set</typeparam>
        /// <typeparam name="TSearchResult">Type ised for result</typeparam>
        /// <param name="endpoint">IQ Search endpoint</param>
        /// <param name="searchIndex">Search Index</param>
        /// <returns></returns>
        public IqSearchClient<TSearchResultSet, TSearchResult> CreateSearchClient<TSearchResultSet, TSearchResult>(Uri endpoint, string searchIndex)
            where TSearchResultSet : IQueryResultData<TSearchResult> where TSearchResult : IQueryResult => new IqSearchClient<TSearchResultSet, TSearchResult>(endpoint, _oauth, searchIndex);

        /// <summary>
        /// Return a fully constructed Public Content Api client
        /// </summary>
        /// <returns>Public Content Api Client</returns>
        public global::Tridion.Dxa.Api.Client.ApiClient CreateClient()
        {
            var graphQl = new GraphQLClient(GetEndpoint(), new Logger(), _oauth);
            var client = new global::Tridion.Dxa.Api.Client.ApiClient(graphQl, new Logger())
            {   // Make sure our requests come back as R2 json
                DefaultModelType = DataModelType.R2
            };

            // Add context data to client

            //var claimStore = AmbientDataContext.CurrentClaimStore;
            //if (claimStore == null)
            //{
            //    Log.Debug("No claimstore found (is the ADF module configured in the Web.Config?) so unable to populate claims for PCA.");
            //}

            //var headers = claimStore?.Get<Dictionary<string, string[]>>(new Uri(WebClaims.REQUEST_HEADERS));
            //if (headers != null && headers.ContainsKey(PreviewSessionTokenHeader))
            //{
            //    Log.Debug($"Adding {PreviewSessionTokenHeader} to client.");
            //    client.HttpClient.Headers[PreviewSessionTokenHeader] = headers[PreviewSessionTokenHeader];
            //}

            //var cookies = claimStore?.Get<Dictionary<string, string>>(new Uri(WebClaims.REQUEST_COOKIES));
            //if (cookies != null && cookies.ContainsKey(PreviewSessionTokenCookie))
            //{
            //    Log.Debug($"Adding {PreviewSessionTokenCookie} to client.");
            //    client.HttpClient.Headers[PreviewSessionTokenHeader] = cookies[PreviewSessionTokenCookie];              
            //}

            //foreach (var claim in _globalClaimValues)
            //{
            //    Log.Debug($"Forwarding on global claim {claim.Key} with value {claim.Value}");
            //    client.GlobalContextData.ClaimValues.Add(claim.Value);
            //}

            if (!_claimForwarding)
            {
                Log.Debug("Claim forwarding from the claimstore has been disabled. Set pca-claim-forwarding to true in your appSettings to allow forwarding.");
                return client;
            }

            //if (claimStore == null)
            //{
            //    Log.Debug("The claimstore is not available so no claim forwarding from claimstore will be performed. Make sure the ADF module is configured in the Web.Config to enable this option.");
            //    return client;
            //}
            // Forward all claims

            //var forwardedClaimValues = AmbientDataContext.ForwardedClaims;
            //if (forwardedClaimValues == null || forwardedClaimValues.Count <= 0) return client;
            //var forwardedClaims =
            //    forwardedClaimValues.Select(claim => new Uri(claim, UriKind.RelativeOrAbsolute))
            //        .Distinct()
            //        .Where(uri => claimStore.Contains(uri) && claimStore.Get<object>(uri) != null && !uri.ToString().Equals("taf:session:preview:preview_session"))
            //        .ToDictionary(uri => uri, uri => claimStore.Get<object>(uri));

            //if (forwardedClaims.Count <= 0)
            //{
            //    Log.Debug("No claims from claimstore to forward.");
            //    return client;
            //}

            //foreach (var claim in forwardedClaims)
            //{
            //    Log.Debug($"Forwarding claim {claim.Key} from claimstore to PCA client.");
            //    client.GlobalContextData.ClaimValues.Add(new ClaimValue
            //    {
            //        Uri = claim.Key.ToString(),
            //        Value = JsonConvert.SerializeObject(claim.Value),
            //        Type = ClaimValueType.STRING
            //    });
            //}

            var forwardedClaimValues = _claimStoreService.ForwardedClaims;
            if (forwardedClaimValues == null || forwardedClaimValues.Count <= 0) return client;

            var forwardedClaims =
                forwardedClaimValues.Select(claim => new Uri(claim, UriKind.RelativeOrAbsolute))
                    .Distinct()
                    .Where(uri => _claimStoreService.Get<object>(uri) != null && !uri.ToString().Equals("taf:session:preview:preview_session"))
                    .ToDictionary(uri => uri, uri => _claimStoreService.Get<object>(uri));

            if (forwardedClaims.Count <= 0)
            {
                Log.Debug("No claims from claimstore to forward.");
                return client;
            }

            foreach (var claim in forwardedClaims)
            {
                Log.Debug($"Forwarding claim {claim.Key} from claimstore to PCA client.");
                client.GlobalContextData.ClaimValues.Add(new ClaimValue
                {
                    Uri = claim.Key.ToString(),
                    Value = JsonConvert.SerializeObject(claim.Value),
                    Type = ClaimValueType.STRING
                });
            }

            return client;
        }
    }
}
