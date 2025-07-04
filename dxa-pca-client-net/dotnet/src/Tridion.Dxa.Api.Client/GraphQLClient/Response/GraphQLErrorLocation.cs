﻿namespace Tridion.Dxa.Api.Client.GraphQLClient.Response
{
    /// <summary>
    /// GraphQL Error locations.
    /// </summary>
    public class GraphQLErrorLocation
    {
        /// <summary>
        /// Error line number.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Error column number.
        /// </summary>
        public int Column { get; set; }
    }
}
