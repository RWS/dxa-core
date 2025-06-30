using System.Collections.Generic;
using Tridion.Dxa.Api.Client.HttpClient;

namespace Tridion.Dxa.Api.Client.GraphQLClient.Response
{
    public class GraphQLTypedResponse<T> : IGraphQLTypedResponse<T>
    {
        public dynamic Data { get; set; }
        public List<GraphQLError> Errors { get; set; }
        public HttpHeaders Headers { get; set; }
        public T TypedResponseData { get; set; }
    }
}
