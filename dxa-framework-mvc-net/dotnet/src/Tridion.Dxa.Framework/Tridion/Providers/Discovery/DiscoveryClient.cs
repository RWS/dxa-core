using System;
using Microsoft.Extensions.Options;
using Sdl.Web.Common;
using Tridion.Dxa.Api.Client.HttpClient;
using Tridion.Dxa.Api.Client.HttpClient.Auth;
using Tridion.Dxa.Api.Client.HttpClient.Request;

namespace Tridion.Dxa.Framework.Tridion.Providers.Discovery
{
    public class ServiceResponseValue
    {
        public string Id { get; set; }
        public string LastUpdateTime { get; set; }
        public Uri Uri { get; set; }
    }
    public struct ServiceResponseHeader
    {
        public ServiceResponseValue[] Value { get; set; }
    }

    public class DiscoveryClient : IDiscoveryClient
    {
        private readonly IHttpClient _client;

        public DiscoveryClient(IOptions<DxaFrameworkOptions> options)
        {
            if (options.Value.Services?.Discovery == null)
                throw new DxaException("Discovery Service Endpoint missing from configuration.");
            _client = new HttpClient(options.Value.Services.Discovery);
        }

        public DiscoveryClient(string endpoint)
        {
            _client = new HttpClient(endpoint);
        }

        public DiscoveryClient(Uri endpoint)
        {
            _client = new HttpClient(endpoint);
        }

        public ServiceResponseValue GetServiceCapability(string capability, IAuthentication authentication)
        {
            HttpHeaders headers = new HttpHeaders
            {
                {"OData-Version", "4.0"},
                {"OData-MaxVersion", "4.0"},
                {"Accept", "application/json;odata.metadata=minimal"}
            };

            var response = _client.Execute<ServiceResponseHeader>(new HttpClientRequest
            {
                Path = $"/{capability}Capabilities?$top=1",
                Headers = headers,
                Authentication = authentication
            }).ResponseData;

            if (response.Value == null || response.Value.Length == 0)
            {
                return null;
            }

            return response.Value[0];
        }

    }
}
