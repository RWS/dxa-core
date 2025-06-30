using System;

namespace Tridion.Dxa.Framework.Tridion.Providers.OAuth
{
    public interface IToken
    {
        object AccessToken { get; set; }
        DateTime ExpiresAt { get; set; }
        bool Expired { get; }
    }
}
