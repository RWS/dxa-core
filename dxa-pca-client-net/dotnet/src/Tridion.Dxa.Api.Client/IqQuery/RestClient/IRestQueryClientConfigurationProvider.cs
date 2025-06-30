using System;

namespace Tridion.Dxa.Api.Client.IqQuery.RestClient
{
    /// <summary>
    /// IRestQueryClientConfigurationProvider
    /// </summary>
    public interface IRestQueryClientConfigurationProvider
    {
        Uri Endpoint { get; }

        int Timeout { get; }

        string DefaultIndexName { get; }
    }
}
