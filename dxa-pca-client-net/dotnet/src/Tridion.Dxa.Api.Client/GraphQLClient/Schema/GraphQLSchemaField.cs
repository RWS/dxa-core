using System.Collections.Generic;

namespace Tridion.Dxa.Api.Client.GraphQLClient.Schema
{
    public class GraphQLSchemaField
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<GraphQLSchemaFieldArgs> Args { get; set; }
        public GraphQLSchemaTypeInfo Type { get; set; }
    }
}
