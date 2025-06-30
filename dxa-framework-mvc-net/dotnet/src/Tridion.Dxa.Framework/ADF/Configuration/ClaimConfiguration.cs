using Sdl.Web.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Tridion.Dxa.Framework.ADF.ClaimStore;

namespace Tridion.Dxa.Framework.ADF.Configuration
{
    internal sealed class ClaimConfiguration
    {
        private IDictionary<Uri, ClaimValueScope> _configuredClaimScopes;
        private List<string> _globallyAcceptedClaims;
        private List<string> _forwardedClaims;

        public ClaimConfiguration()
        {

        }

        public void Configure(AmbientDataConfig config)
        {
            string cookieClaimUri = config.Cookies?.CookieClaim?.Name;

            CookieClaimName = GetCookieClaimUri(cookieClaimUri);

            _configuredClaimScopes = InitializeConfiguredClaimScopes();
            _forwardedClaims = config.ForwardedClaims?.Claim?.Select(claim => claim.Uri).ToList() ?? new List<string>();
            _globallyAcceptedClaims = config.Cookies?.Cookie.Select(claim => claim.Name).ToList() ?? new List<string>();

            ForwardedClaimsCookieName = config.ForwardedClaims?.CookieName ?? "TAFContext";
            GloballyAcceptedClaimsCookieName = config.Cookies?.CookieClaim?.Name ?? "TAFContext";
        }

        public Uri CookieClaimName
        {
            get;
            set;
        }

        public string GloballyAcceptedClaimsCookieName
        {
            get;
            set;
        }

        public string ForwardedClaimsCookieName
        {
            get;
            set;
        }

        public List<string> GloballyAcceptedClaims
        {
            get
            {
                return _globallyAcceptedClaims;
            }
        }

        public List<string> ForwardedClaims
        {
            get
            {
                return _forwardedClaims;
            }
        }

        public ClaimValueScope? GetConfiguredClaimScope(Uri claimUri)
        {
            if (_configuredClaimScopes.ContainsKey(claimUri))
            {
                return _configuredClaimScopes[claimUri];
            }
            return null;
        }

        private IDictionary<Uri, ClaimValueScope> InitializeConfiguredClaimScopes()
        {
            Dictionary<Uri, ClaimValueScope> scopes = new Dictionary<Uri, ClaimValueScope>();
            // Add default SESSION scoped claims
            scopes.Add(new Uri(WebClaims.SESSION_ID), ClaimValueScope.Session);
            scopes.Add(new Uri(WebClaims.TRACKING_ID), ClaimValueScope.Session);
            scopes.Add(CookieClaimName, ClaimValueScope.Session);

            return scopes;
        }

        private Uri GetCookieClaimUri(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return new Uri(WebClaims.DEFAULT_COOKIE_CLAIM);
            }
            else
            {
                try
                {
                    return new Uri(uri);
                }
                catch (Exception)
                {
                    Log.Debug("The URI could not be read from the config file. " +
                            "Probably missing or not configured properly.");
                    return new Uri(WebClaims.DEFAULT_COOKIE_CLAIM);
                }
            }
        }
    }

}
