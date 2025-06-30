using Tridion.Dxa.Api.Client.GraphQLClient;

namespace Tridion.Dxa.Api.Client
{
    /// <summary>
    /// GraphQL Queries
    /// </summary>
    public static class Queries
    {
        public static string Load(string queryName, bool loadFragments)
            => QueryResources.LoadQueryFromResource("Tridion.Dxa.Api.Client", queryName, loadFragments);

        public static string LoadFragments(string query)
            => QueryResources.LoadFragments("Tridion.Dxa.Api.Client", query);
    }
}
