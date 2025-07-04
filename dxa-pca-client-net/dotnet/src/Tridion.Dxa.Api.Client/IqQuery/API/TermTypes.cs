﻿using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Tridion.Dxa.Api.Client.IqQuery.API
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TermTypes
    {
        [EnumMember(Value = "EXACT")]
        Exact,

        [EnumMember(Value = "FUZZY")]
        Fuzzy,

        [EnumMember(Value = "WILDCARD")]
        Wildcard,
    }
}
