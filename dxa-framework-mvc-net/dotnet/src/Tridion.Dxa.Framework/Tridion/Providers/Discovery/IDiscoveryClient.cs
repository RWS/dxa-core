using Tridion.Dxa.Api.Client.HttpClient.Auth;

namespace Tridion.Dxa.Framework.Tridion.Providers.Discovery
{
    public interface IDiscoveryClient
    {
        ServiceResponseValue GetServiceCapability(string capability, IAuthentication authentication);
    }
}
