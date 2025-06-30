using System.Collections.Generic;

namespace Tridion.Dxa.Api.Client.IqQuery.API
{
    /// <summary>
    /// Contains several filters for query result manipulation.
    /// </summary>
    public interface IQueryFilter
    {
        int? MaxResults { get; set; }

        int? StartOfRange { get; set; }

        int? EndOfRange { get; set; }

        HashSet<string> ExcludeFields { get; set; }

        bool IsHighlightingEnabled { get; set; }

        bool IsHighlightInAllEnabled { get; set; }
    }
}
