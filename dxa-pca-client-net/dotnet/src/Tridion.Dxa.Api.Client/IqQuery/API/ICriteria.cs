namespace Tridion.Dxa.Api.Client.IqQuery.API
{
    /// <summary>
    /// Search Criteria. Sent to the search index.
    /// </summary>
    public interface ICriteria
    {
        /// <summary>
        /// Gets the raw query.
        /// </summary>
        string RawQuery { get; }
    }
}
