using Tridion.Dxa.Api.Client.IqQuery.API;

namespace Tridion.Dxa.Api.Client.IqQuery.Model.Search
{
    /// <summary>
    /// Default implementation for Criteria.
    /// </summary>
    public class SearchCriteria : ICriteria
    {
        public string RawQuery { get; }

        public SearchCriteria(string rawQuery)
        {
            RawQuery = rawQuery;
        }
    }
}
