using System;
using System.Collections.Concurrent;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;
using Sdl.Web.Tridion.ApiClient;
using Tridion.Dxa.Api.Client.ContentModel;

namespace Sdl.Web.Tridion
{
    public class DefaultLocalizationResolver : LocalizationResolver
    {
        private readonly IApiClientFactory _apiClientFactory;
        private static readonly ConcurrentDictionary<string, object> KeyLocks = new ConcurrentDictionary<string, object>();
        private static readonly object KnownLocalizationsLock = new object();

        public DefaultLocalizationResolver(IApiClientFactory apiClientFactory)
        {
            _apiClientFactory = apiClientFactory ?? throw new ArgumentNullException(nameof(apiClientFactory));
        }

        /// <summary>
        /// Resolves a matching <see cref="ILocalization"/> for a given URL.
        /// </summary>
        /// <param name="url">The URL to resolve.</param>
        /// <returns>A <see cref="ILocalization"/> instance which base URL matches that of the given URL.</returns>
        /// <exception cref="DxaUnknownLocalizationException">If no matching Localization can be found.</exception>
        public override Localization ResolveLocalization(Uri url)
        {
            using (new Tracer(url))
            {
                Log.Trace($"Resolving localization for url: '{url}'");
                string urlLeftPart = url.GetLeftPart(UriPartial.Path);

                // Handle escaped characters in URL
                int escapeIndex = urlLeftPart.IndexOf("%");
                if (escapeIndex > 0)
                {
                    urlLeftPart = urlLeftPart.Substring(0, escapeIndex);
                }

                // Check cache first
                Localization cachedResult = GetCachedLocalization(urlLeftPart);
                if (cachedResult != null)
                {
                    return cachedResult;
                }

                // Use fine-grained locking per URL
                lock (KeyLocks.GetOrAdd(urlLeftPart, new object()))
                {
                    try
                    {
                        // Double-check cache after acquiring lock
                        cachedResult = GetCachedLocalization(urlLeftPart);
                        if (cachedResult != null)
                        {
                            return cachedResult;
                        }

                        // Get publication mapping from API
                        PublicationMapping mapping = _apiClientFactory.CreateClient()
                            .GetPublicationMapping(ContentNamespace.Sites, urlLeftPart);

                        if (mapping == null || mapping.Port != url.Port.ToString())
                        {
                            throw new DxaUnknownLocalizationException(
                                $"No matching Localization found for URL '{urlLeftPart}'");
                        }

                        string localizationId = mapping.PublicationId.ToString();
                        Localization result;

                        lock (KnownLocalizationsLock)
                        {
                            if (!KnownLocalizations.TryGetValue(localizationId, out result))
                            {
                                result = new Localization
                                {
                                    Id = localizationId,
                                    Path = mapping.Path
                                };
                                KnownLocalizations.Add(localizationId, result);
                            }
                            else
                            {
                                // Update path if it was a partially created localization
                                result.Path = mapping.Path;
                            }
                        }

                        result.EnsureInitialized();
                        CacheLocalization(urlLeftPart, result);

                        Log.Trace($"Localization for url '{url}' initialized for Publication Id: {result.PublicationId()}, Path: {result.Path}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to resolve localization for URL '{urlLeftPart}': {ex.Message}");
                        throw new DxaUnknownLocalizationException($"No matching Localization found for URL '{urlLeftPart}' {ex}");
                    }
                    finally
                    {
                        RemoveLock(urlLeftPart);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a localization by its ID.
        /// </summary>
        public override Localization GetLocalization(string localizationId)
        {
            using (new Tracer(localizationId))
            {
                Localization result;
                if (!KnownLocalizations.TryGetValue(localizationId, out result))
                {
                    // Return partially constructed localization if not found
                    result = new Localization
                    {
                        Id = localizationId
                    };
                }
                return result;
            }
        }

        #region Caching Methods

        protected void CacheLocalization(string urlPart, Localization localization)
        {
            if (SiteConfiguration.CacheProvider == null)
            {
                Log.Warn("CacheProvider not configured - skipping localization caching");
                return;
            }

            try
            {
                Log.Trace($"Caching localization for url part: '{urlPart}'");
                SiteConfiguration.CacheProvider.Store(urlPart, CacheRegions.LocalizationResolving, localization);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to cache localization for URL part '{urlPart}': {ex.Message}");
            }
        }

        protected Localization GetCachedLocalization(string urlPart)
        {
            if (SiteConfiguration.CacheProvider == null)
            {
                return null;
            }

            try
            {
                if (SiteConfiguration.CacheProvider.TryGet(urlPart, CacheRegions.LocalizationResolving, out Localization result))
                {
                    Log.Trace($"Found cached localization for url part: '{urlPart}'");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to get cached localization for URL part '{urlPart}': {ex.Message}");
            }
            return null;
        }

        protected static void RemoveLock(string urlPart)
        {
            KeyLocks.TryRemove(urlPart, out _);
        }

        #endregion
    }
}