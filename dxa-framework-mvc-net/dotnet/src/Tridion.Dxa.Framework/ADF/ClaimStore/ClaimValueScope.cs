using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Tridion.Dxa.Framework.ADF.ClaimStore
{
    /// <summary>
    /// Defines the claim scope.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ClaimValueScope
    {
        /// <summary>
        /// Indicates that a claim has request scope.
        /// </summary>
        Request,

        /// <summary>
        /// Indicates that a claim has session scope.
        /// </summary>
        Session,

        /// <summary>
        /// Indicates that a claim has static scope.
        /// </summary>
        Static
    }
}
