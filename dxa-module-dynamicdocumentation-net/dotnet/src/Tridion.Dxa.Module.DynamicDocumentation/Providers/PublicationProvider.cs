﻿using System;
using System.Collections.Generic;
using System.Linq;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;
using Sdl.Web.Modules.DynamicDocumentation.Exceptions;
using Sdl.Web.Tridion.ApiClient;
using Tridion.Dxa.Api.Client.ContentModel;

namespace Sdl.Web.Modules.DynamicDocumentation.Providers
{
    /// <summary>
    /// Publication Provider
    /// </summary>
    public class PublicationProvider
    {
        protected static readonly string DateTimeFormat = "MM/dd/yyyy HH:mm:ss";

        private static readonly string PublicationTitleMeta = "publicationtitle.generated.value";
        private static readonly string PublicationProductfamilynameMeta = "FISHPRODUCTFAMILYNAME.logical.value";
        private static readonly string PublicationProductreleasenameMeta = "FISHPRODUCTRELEASENAME.version.value";
        private static readonly string PublicationVersionrefMeta = "ishversionref.object.id";
        private static readonly string PublicationLangMeta = "FISHPUBLNGCOMBINATION.lng.value";
        private static readonly string PublicationOnlineStatusMeta = "FISHDITADLVRREMOTESTATUS.lng.element";
        private static readonly string PublicationOnlineValue = "VDITADLVRREMOTESTATUSONLINE";
        private static readonly string PublicationCratedonMeta = "CREATED-ON.version.value";
        private static readonly string PublicationVersionMeta = "VERSION.version.value";
        private static readonly string PublicationLogicalId = "ishref.object.value";

        //private static readonly string CustomMetaFilter = $"requiredMeta:{PublicationTitleMeta},{PublicationProductfamilynameMeta},{PublicationProductreleasenameMeta},{PublicationVersionrefMeta},{PublicationLangMeta},{PublicationOnlineStatusMeta},{PublicationCratedonMeta},{PublicationVersionMeta},{PublicationLogicalId}";
        private static readonly string CustomMetaFilter = string.Empty;
        private readonly IApiClientFactory _apiClientFactory;

        public PublicationProvider(IApiClientFactory apiClientFactory)
        {
            _apiClientFactory = apiClientFactory;
        }
        public List<Models.Publication> PublicationList
        {
            get
            {
                try
                {
                    return SiteConfiguration.CacheProvider.GetOrAdd($"publications", CacheRegion.Publications, () =>
                    {
                        var client = _apiClientFactory.CreateClient();
                        var publications = client.GetPublications(ContentNamespace.Docs, new Pagination(), null,
                            CustomMetaFilter, null);
                        return
                            (from x in publications.Edges
                                where IsPublicationOnline(x.Node)
                                select BuildPublicationFrom(x.Node)).ToList();
                    });
                }
                catch (Exception)
                {
                    throw new DxaItemNotFoundException("Unable to fetch list of publications.");
                }
            }
        }

        public bool IsPublicationOnline(Publication publication)
        {
            var customMeta = publication.CustomMetas;
            if (customMeta == null) return false;
            try
            {
                return customMeta.Edges.Where(x => PublicationOnlineStatusMeta.Equals(x.Node.Key)).Any(x => PublicationOnlineValue.Equals(x.Node.Value));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void CheckPublicationOnline(int publicationId)
        {
            var client = _apiClientFactory.CreateClient();
            bool isOffline = false;
            try
            {
                var publication = client.GetPublication(ContentNamespace.Docs, publicationId, $"requiredMeta:{PublicationOnlineStatusMeta}", null);
                isOffline = publication.CustomMetas == null || publication.CustomMetas.Edges.Count == 0 ||
                            !PublicationOnlineValue.Equals(publication.CustomMetas.Edges[0].Node.Value);
            }
            catch (Exception)
            {
                Log.Error("Couldn't find publication metadata for id: " + publicationId);
            }
            if (isOffline)
            {
                throw new DynamicDocumentationException($"Unable to find publication {publicationId}");
            }
        }

        private Models.Publication BuildPublicationFrom(Publication publication)
        {
            Models.Publication result = new Models.Publication
            {
                Id = publication.ItemId.ToString(),
                Title = publication.Title
            };
            var customMeta = publication.CustomMetas;
            if (customMeta == null) return result;
            result.ProductFamily = null;
            result.ProductReleaseVersion = null;
            List<Models.CustomMeta> customMetas = new List<Models.CustomMeta>();

            foreach (var x in customMeta.Edges)
            {
                customMetas.Add(new Models.CustomMeta { Key = x.Node.Key, Value = x.Node.Value });

                if (x.Node.Key == PublicationTitleMeta)
                    result.Title = x.Node.Value;

                if (x.Node.Key == PublicationLangMeta)
                    result.Language = x.Node.Value;

                if (x.Node.Key == PublicationProductfamilynameMeta)
                {
                    if (result.ProductFamily == null) result.ProductFamily = new List<string>();
                    result.ProductFamily.Add(x.Node.Value);
                }

                if (x.Node.Key == PublicationProductreleasenameMeta)
                {
                    if (result.ProductReleaseVersion == null) result.ProductReleaseVersion = new List<string>();
                    result.ProductReleaseVersion.Add(x.Node.Value);
                }

                if (x.Node.Key == PublicationCratedonMeta)
                {
                    result.CreatedOn = DateTime.ParseExact(x.Node.Value, DateTimeFormat, null);
                }

                if (x.Node.Key == PublicationVersionMeta)
                {
                    result.Version = x.Node.Value;
                }

                if (x.Node.Key == PublicationVersionrefMeta)
                {
                    result.VersionRef = x.Node.Value;
                }

                if (x.Node.Key == PublicationLogicalId)
                {
                    result.LogicalId = x.Node.Value;
                }
            }

            result.CustomMetas = customMetas;

            return result;
        }
    }
}
