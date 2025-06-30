using System.Collections.Generic;

namespace Tridion.Dxa.Framework
{
    public class AmbientDataConfig
    {
        public ForwardedClaims ForwardedClaims { get; set; }
        public Cookies Cookies { get; set; }
    }

    public class ForwardedClaims
    {
        public string CookieName { get; set; }
        public List<Claim> Claim { get; set; }
    }

    public class Claim
    {
        public string Uri { get; set; }
    }

    public class Cookies
    {
        public CookieClaim CookieClaim { get; set; }
        public List<Cookie> Cookie { get; set; }
    }

    public class CookieClaim
    {
        public bool DefaultValue { get; set; }
        public string Name { get; set; }
    }

    public class Cookie
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }
}
