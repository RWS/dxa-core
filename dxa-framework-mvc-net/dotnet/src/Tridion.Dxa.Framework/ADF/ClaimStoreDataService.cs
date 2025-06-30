using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Tridion.Dxa.Framework.ADF.ClaimStore;

namespace Tridion.Dxa.Framework.ADF
{
    /// <summary>
    /// Claim Store Data Service
    /// </summary>
    public class ClaimStoreDataService
    {
        private IDictionary<Uri, object> ClaimValues = new ConcurrentDictionary<Uri, object>();
        private IHttpContextAccessor _httpContextAccessor;
        private AmbientDataConfig _config;

        public ClaimStoreDataService(IHttpContextAccessor httpContextAccessor, AmbientDataConfig config)
        {
            _httpContextAccessor = httpContextAccessor;
            _config = config;
        }

        public void Put(Uri claimUri, object claimValue)
        {
            ClaimValues[claimUri] = claimValue;
        }

        public T Get<T>(Uri claimUri)
        {
            if (ClaimValues.TryGetValue(claimUri, out var claimValue) && claimValue is T result)
            {
                return result;
            }

            return default;
        }

        // Methods and properties to interact with HttpContext's Items
        public string UserAgent
        {
            get { return GetItems("Tridion.ContentDelivery.AmbientData.UserAgentName") as string; }
            set { SetItems("Tridion.ContentDelivery.AmbientData.UserAgentName", value); }
        }

        public IClaimStore CurrentClaimStore
        {
            get { return GetItems("Tridion.ContentDelivery.AmbientData.ClaimStore") as IClaimStore; }
            set { SetItems("Tridion.ContentDelivery.AmbientData.ClaimStore", value); }
        }

        public List<string> GloballyAcceptedClaims
        {
            get { return GetItems("Tridion.ContentDelivery.AmbientData.GloballyAcceptedClaims") as List<string>; }
            set { SetItems("Tridion.ContentDelivery.AmbientData.GloballyAcceptedClaims", value); }
        }

        public string GloballyAcceptedClaimsCookieName
        {
            get { return GetItems("Tridion.ContentDelivery.AmbientData.GloballyAcceptedClaimsCookieName") as string; }
            set { SetItems("Tridion.ContentDelivery.AmbientData.GloballyAcceptedClaimsCookieName", value); }
        }

        public List<string> ForwardedClaims
        {
            get { return GetItems("Tridion.ContentDelivery.AmbientData.ForwardedClaims") as List<string>; }
            set { SetItems("Tridion.ContentDelivery.AmbientData.ForwardedClaims", value); }
        }

        public string ForwardedClaimsCookieName
        {
            get { return GetItems("Tridion.ContentDelivery.AmbientData.ForwardedClaimsCookieName") as string; }
            set { SetItems("Tridion.ContentDelivery.AmbientData.ForwardedClaimsCookieName", value); }
        }

        private object GetItems(string keyName)
        {
            try
            {
                if (_httpContextAccessor.HttpContext != null)
                {
                    return _httpContextAccessor.HttpContext.Items[keyName];
                }
            }
            catch (Exception)
            {
                // Don't log anything since not having the claimStore available is reasonable if ADF is not configured or we are not running under a web context.
            }
            return null;
        }

        private void SetItems(string keyName, object value)
        {
            try
            {
                if (_httpContextAccessor.HttpContext != null)
                {
                    _httpContextAccessor.HttpContext.Items[keyName] = value;
                }
            }
            catch (Exception)
            {
                // Don't log anything since not having the claimStore available is reasonable if ADF is not configured or we are not running under a web context.
            }
        }

        public void SetServerHeaders(Dictionary<string, string> headers)
        {
            var allowedHeaderClaims = _config.ForwardedClaims.Claim
                .Where(claim => claim.Uri.StartsWith("taf:request:headers"))
                .Select(claim => claim.Uri);

            foreach (var header in headers)
            {
                if (allowedHeaderClaims.Any(claim => header.Key.EndsWith(claim)))
                {
                    Put(new Uri(header.Key), header.Value);
                }
            }
        }
    }
}
