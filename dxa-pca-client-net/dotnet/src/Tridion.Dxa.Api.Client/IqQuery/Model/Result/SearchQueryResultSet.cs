using System.Collections.Generic;
using Tridion.Dxa.Api.Client.IqQuery.API;

namespace Tridion.Dxa.Api.Client.IqQuery.Model.Result
{
    /// <summary>
    /// Search Query Result Set
    /// </summary>
    public class SearchQueryResultSet : IQueryResultData<SearchQueryResult>
    {
        public int Hits { get; set; }
        public IList<SearchQueryResult> QueryResults { get; set; }
    }
}
