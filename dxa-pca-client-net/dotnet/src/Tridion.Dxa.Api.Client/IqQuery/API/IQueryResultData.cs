﻿using System.Collections.Generic;

namespace Tridion.Dxa.Api.Client.IqQuery.API
{
    /// <summary>
    /// Represents a result set of a query.
    /// </summary>
    /// <typeparam name="T">The type of each individual result.</typeparam>
    public interface IQueryResultData<T> where T : IQueryResult
    {
        /// <summary>
        /// Returns number of hits
        /// </summary>
        int Hits { get; set; }

        /// <summary>
        /// Return query results.
        /// </summary>
        IList<T> QueryResults { get; set; }
    }
}
