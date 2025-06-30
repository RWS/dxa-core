using System.Collections.Generic;
using Tridion.Dxa.Api.Client.GraphQL.Client;
using Tridion.Dxa.Api.Client.GraphQLClient.Schema;

namespace Tridion.Dxa.Api.Client.CodeGen
{
    public class CodeGen
    {
        private readonly GraphQLSchema _schema;
        private readonly string _namespace;

        public CodeGen(IGraphQLClient client, string nameSpace)
        {
            _schema = client.Schema;
            _namespace = nameSpace;
        }

        public List<CodeGenInfo> GenerateTypes()
            => CodeGenEmitter.GenerateTypes(_schema, _namespace);

        public List<CodeGenInfo> GenerateQueryBuilders()
            => CodeGenEmitter.GenerateQueryBuilders(_schema, _namespace);
    }
}
