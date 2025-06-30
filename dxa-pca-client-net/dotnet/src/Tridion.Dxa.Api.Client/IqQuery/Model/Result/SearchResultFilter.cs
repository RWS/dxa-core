using System.Collections.Generic;
using Tridion.Dxa.Api.Client.IqQuery.API;

namespace Tridion.Dxa.Api.Client.IqQuery.Model.Result
{
    /// <summary>
    /// Search Result Filter
    /// </summary>
    public class SearchResultFilter : IResultFilter
    {
        public int? MaxResults { get; set; }
        public int? StartOfRange { get; set; }
        public int? EndOfRange { get; set; }
        public HashSet<string> ExcludeFields { get; set; }
        public bool IsHighlightingEnabled { get; set; }
        public bool IsHighlightInAllEnabled { get; set; }
    }
}
