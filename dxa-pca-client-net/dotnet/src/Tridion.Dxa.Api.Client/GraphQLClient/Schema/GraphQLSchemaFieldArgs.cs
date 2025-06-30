namespace Tridion.Dxa.Api.Client.GraphQLClient.Schema
{
    public class GraphQLSchemaFieldArgs
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string DefaultValue { get; set; }
        public GraphQLSchemaTypeInfo Type { get; set; }
    }
}
