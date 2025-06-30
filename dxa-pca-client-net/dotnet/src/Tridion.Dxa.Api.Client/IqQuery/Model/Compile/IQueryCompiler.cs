using Tridion.Dxa.Api.Client.IqQuery.Model.Search;

namespace Tridion.Dxa.Api.Client.IqQuery.Model.Compile
{
    /// <summary>
    /// Query Compiler
    /// </summary>
    public interface IQueryCompiler
    {
        string Compile(SearchNode node);
    }
}
