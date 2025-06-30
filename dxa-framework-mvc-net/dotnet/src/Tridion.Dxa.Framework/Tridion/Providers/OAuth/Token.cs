using System;

namespace Tridion.Dxa.Framework.Tridion.Providers.OAuth
{
    public class Token : IToken
    {
        /// <summary>
        /// The Access Token retrieved from the token provider
        /// </summary>
        public virtual object AccessToken { get; set; }

        /// <summary>
        /// If set denotes when is the token going to expire in UTC
        /// </summary>
        public virtual DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Returns true if token has expired
        /// </summary>
        public virtual bool Expired => DateTime.UtcNow >= ExpiresAt.Subtract(TimeSpan.FromSeconds(10));
    }
}
