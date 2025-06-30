﻿using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Tridion.ApiClient;
using Tridion.Dxa.Api.Client;
using Tridion.Dxa.Api.Client.Utils;

namespace Sdl.Web.Tridion.Linking
{
    /// <summary>
    /// Default Link Resolver implementation
    /// </summary>
    public class DefaultLinkResolver : ILinkResolver, ILinkResolverExt
    {
        private readonly IApiClientFactory _apiClientFactory;
        public DefaultLinkResolver(IApiClientFactory apiClientFactory)
        {
            _apiClientFactory = apiClientFactory;
        }
        #region ILinkResolver Members

        /// <summary>
        /// Resolves a link URI (TCM URI or site URL) to a normalized site URL.
        /// </summary>
        /// <param name="sourceUri">The source URI (TCM URI or site URL)</param>
        /// <param name="resolveToBinary">Specifies whether a link to a Multimedia Component should be resolved directly to its Binary (<c>true</c>) or as a regular Component link.</param>
        /// <param name="localization">The context Localization (optional, since the TCM URI already contains a Publication ID, but this allows resolving in a different context).</param>
        /// <returns>The resolved URL.</returns>
        public string ResolveLink(string sourceUri, bool resolveToBinary = false, Localization localization = null)
            => ResolveLink(sourceUri, null, resolveToBinary, localization);
        #endregion

        #region ILinkResolverExt Members
        public string ResolveLink(string sourceUri, string pageContextId, bool resolveToBinary = false, Localization localization = null)
        {
            if (sourceUri == null) return null;

            string url = SiteConfiguration.CacheProvider.GetOrAdd($"{sourceUri}:{pageContextId}:{resolveToBinary}:{localization?.Id}",
                CacheRegions.LinkResolving,
                () =>
                {
                    if (sourceUri.IsCmUri())
                    {
                        var client = _apiClientFactory.CreateClient();
                        var cmUri = new CmUri(sourceUri);
                        switch (cmUri.ItemType)
                        {
                            case ItemType.Component:
                                string resolved = null;
                                if (resolveToBinary)
                                {
                                    resolved = client.ResolveBinaryLink(cmUri.Namespace, cmUri.PublicationId,
                                        cmUri.ItemId, null);
                                }

                                if (resolved != null)
                                    return resolved;

                                int pageId;
                                CmUri pageCmUri;
                                if (CmUri.TryParse(pageContextId, out pageCmUri))
                                {
                                    pageId = cmUri.ItemId;
                                }
                                else if (!int.TryParse(pageContextId, out pageId))
                                {
                                    pageId = -1;
                                }

                                return client.ResolveComponentLink(cmUri.Namespace, cmUri.PublicationId, cmUri.ItemId, pageId, null);
                            case ItemType.Page:
                                return client.ResolvePageLink(cmUri.Namespace, cmUri.PublicationId, cmUri.ItemId);
                        }
                    }
                    else
                    {
                        return sourceUri;
                    }
                    return null;
                });

            // Strip off default extension / page name
            if (url == null || !url.EndsWith(Constants.DefaultExtension)) return url;
            url = url.Substring(0, url.Length - Constants.DefaultExtension.Length);
            if (url.EndsWith("/" + Constants.DefaultExtensionLessPageName))
            {
                url = url.Substring(0, url.Length - Constants.DefaultExtensionLessPageName.Length);
            }
            return url;

        }
        #endregion

    }
}
