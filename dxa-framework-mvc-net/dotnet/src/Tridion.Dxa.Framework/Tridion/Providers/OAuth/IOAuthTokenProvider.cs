using Tridion.Dxa.Api.Client.HttpClient;

namespace Tridion.Dxa.Framework.Tridion.Providers.OAuth
{
    public interface IOAuthTokenProvider
    {
        HttpHeaders OAuthHeaders { get; }
        IToken TokenNoExceptions { get; }
    }
}
