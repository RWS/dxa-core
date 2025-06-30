﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Modules.DynamicDocumentation.Models;
using Sdl.Web.Tridion.ApiClient;
using Tridion.Dxa.Api.Client.ContentModel;

namespace Sdl.Web.Modules.DynamicDocumentation.Providers
{
    /// <summary>
    /// Condition Provider
    /// </summary>
    public class ConditionProvider
    {
        private static readonly string ConditionUsed = "conditionsused.generated.value";
        private static readonly string ConditionMetadata = "conditionmetadata.generated.value";

        private class Condition
        {
            [JsonProperty("datatype")]
            public string Datatype { get; set; }
            [JsonProperty("range")]
            public bool Range { get; set; }
            [JsonProperty("values")]
            public string[] Values { get; set; }
        }

        private readonly IApiClientFactory _apiClientFactory;

        public ConditionProvider(IApiClientFactory apiClientFactory)
        {
            _apiClientFactory = apiClientFactory;
        }

        public string GetConditionsJson(int publicationId) 
            => JsonConvert.SerializeObject(GetConditions(publicationId));

        public Dictionary<string, object> GetMergedConditions(Conditions conditions)
        {
            if (conditions.UserConditions == null)
                return new Dictionary<string, object>();

            var conditionsMap = GetConditions(conditions.PublicationId);


            var finalResult = conditionsMap.ToDictionary(x => x.Key,
                    x =>
                        conditions.UserConditions.ContainsKey(x.Key) ? conditions.UserConditions[x.Key] : x.Value.Values);


            return finalResult;
        }

        private Dictionary<string, Condition> GetConditions(int publicationId)
        {
            var conditionUsed = GetMetadata(publicationId, ConditionUsed);
            var conditionMetadata = GetMetadata(publicationId, ConditionMetadata);
            Dictionary<string, string[]> d1 =
                JsonConvert.DeserializeObject<Dictionary<string, string[]>>(conditionUsed);
            Dictionary<string, Condition> d2 =
                JsonConvert.DeserializeObject<Dictionary<string, Condition>>(conditionMetadata);
            foreach (var v in d1)
            {
                d2[v.Key].Values = v.Value;
            }
            return d2;
        }

        private string GetMetadata(int publicationId, string metadataName)
        {
            try
            {
                return SiteConfiguration.CacheProvider.GetOrAdd($"{publicationId}-{metadataName}",
                    CacheRegion.Conditions,
                    () =>
                    {
                        var client = _apiClientFactory.CreateClient();
                        var publication = client.GetPublication(ContentNamespace.Docs, publicationId,
                            $"requiredMeta:{metadataName}", null);
                        if (publication.CustomMetas == null || publication.CustomMetas.Edges.Count == 0)
                        {
                            throw new DxaItemNotFoundException(
                                $"Metadata '{metadataName}' is not found for publication {publicationId}.");
                        }

                        object metadata = publication.CustomMetas.Edges[0].Node.Value;
                        string metadataString = metadata != null ? (string) metadata : "{}";
                        return metadataString;
                    });
            }
            catch (Exception)
            {
                throw new DxaItemNotFoundException(
                    $"Metadata '{metadataName}' is not found for publication {publicationId}.");
            }
        }
    }
}
