﻿using System.Collections.Generic;

namespace Tridion.Dxa.Api.Client.ContentModel
{
    /// <summary>
    /// Context Data
    /// </summary>
    public interface IContextData
    {
        /// <summary>
        /// List of claim values to pass to query.
        /// </summary>
        List<ClaimValue> ClaimValues { get; set; }
    }
}
