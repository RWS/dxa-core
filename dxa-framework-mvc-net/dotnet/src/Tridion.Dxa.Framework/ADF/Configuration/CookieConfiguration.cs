using System;
using System.Collections.Generic;
using System.Linq;
using Tridion.Dxa.Framework.ADF.ClaimStore.Cookie;

namespace Tridion.Dxa.Framework.ADF.Configuration
{
    internal sealed class CookieConfiguration
    {
        private readonly IDictionary<CookieType, KeyValuePair<string, string>> _cookieTypes;

        public CookieConfiguration()
        {
            _cookieTypes = new Dictionary<CookieType, KeyValuePair<string, string>>{
                {CookieType.ADF, new KeyValuePair<string,string>("TAFContext", "")},
                {CookieType.TRACKING, new KeyValuePair<string,string>("TAFTrackingId", "")},
                {CookieType.SESSION, new KeyValuePair<string,string>("TAFSessionId", "")}
            };
        }

        public void Configure(AmbientDataConfig config)
        {
            _config = config;
            //DefaultCookieClaimValue = config.ElementExistsAndContainsAttribute("/Configuration/Cookies/CookieClaim", "DefaultValue", true);

            if (config.Cookies != null && config.Cookies.CookieClaim != null)
            {
                DefaultCookieClaimValue = config.Cookies.CookieClaim.DefaultValue;
            }
        }

        private AmbientDataConfig _config
        {
            get;
            set;
        }

        public bool DefaultCookieClaimValue
        {
            get;
            private set;
        }

        public string GetDefaultCookieName(CookieType cookieType) => _cookieTypes[cookieType].Key;

        public string GetDefaultCookiePath(CookieType cookieType) => _cookieTypes[cookieType].Value;

        public CookieConfig GetCookieConfiguration(CookieType cookieType)
        {
            if (_config!=null && _config.Cookies != null && _config.Cookies.Cookie != null)
            {
                var cookie = _config.Cookies.Cookie
                    .FirstOrDefault(c => string.Equals(c.Type, cookieType.ToString(), StringComparison.InvariantCultureIgnoreCase));

                if (cookie != null)
                {
                    string name = string.IsNullOrEmpty(cookie.Name) ? GetDefaultCookieName(cookieType) : cookie.Name;
                    string path = string.IsNullOrEmpty(cookie.Path) ? GetDefaultCookiePath(cookieType) : cookie.Path;
                    return new CookieConfig(name, path);
                }
            }

            return new CookieConfig(GetDefaultCookieName(cookieType), GetDefaultCookiePath(cookieType));
        }

        public bool IsCookieTypePresent(CookieType cookieType)
        {
            if (_config.Cookies != null && _config.Cookies.Cookie != null)
            {
                return _config.Cookies.Cookie.Any(c => string.Equals(c.Type, cookieType.ToString(), StringComparison.InvariantCultureIgnoreCase));
            }

            return false;
        }
    }
}
