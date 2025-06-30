using System;

namespace Tridion.Dxa.Api.Client.IqQuery.API
{
    /// <summary>
    /// Query Exception
    /// </summary>
    public class QueryException : Exception
    {
        public QueryException(string msg) : base(msg)
        {
        }

        public QueryException(string msg, Exception innerException) : base(msg, innerException)
        {
        }
    }
}
