using Microsoft.Extensions.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Tridion.Dxa.Framework;

namespace Sdl.Web.Common.Configuration
{
    /// <summary>
    /// Represents configuration that applies to the entire web application.
    /// </summary>
    public static class SiteConfiguration
    {
        public const string VersionRegex = "(v\\d*.\\d*)";
        public const string SystemFolder = "system";
        public const string CoreModuleName = "core";
        public const string StaticsFolder = "BinaryData";
        public const string DefaultVersion = "v1.00";

        //A set of refresh states, keyed by localization id and then type (eg "config", "resources" etc.) 
        private static readonly Dictionary<string, Dictionary<string, DateTime>> _refreshStates = new Dictionary<string, Dictionary<string, DateTime>>();
        //A set of locks to use, one per localization
        private static readonly Dictionary<string, object> _localizationLocks = new Dictionary<string, object>();
        //A global lock
        private static readonly object _lock = new object();
        private static string _defaultModuleName;

        private static DxaFrameworkOptions _dxaFrameworkOptions = new DxaFrameworkOptions();

        /// <summary>
        /// Gets the Logger (Logging Provider)
        /// </summary>
        /// <remarks>
        /// This is only set if a Logger is configured explicitly.
        /// Avoid using this property directly.  For logging, use class <see cref="Log"/>.
        /// </remarks>
        public static ILogger Logger { get; }

        /// <summary>
        /// Gets the Cache Provider.
        /// </summary>
        public static ICacheProvider CacheProvider { get; private set; }

        /// <summary>
        /// Gets the Binary Provider.
        /// </summary>
        public static IBinaryProvider BinaryProvider { get; private set; }

        /// <summary>
        /// Gets the Model Service Provider.
        /// </summary>
        public static IModelServiceProvider ModelServiceProvider { get; private set; }

        /// <summary>
        /// Gets the Content Provider used for obtaining the Page and Entity Models and Static Content.
        /// </summary>
        public static IContentProvider ContentProvider { get; private set; }

        /// <summary>
        /// Gets the Content Provider used for obtaining the Navigation Models
        /// </summary>.
        public static INavigationProvider NavigationProvider { get; private set; }

        /// <summary>
        /// Gets the Context Claims Provider.
        /// </summary>
        public static IContextClaimsProvider ContextClaimsProvider { get; private set; }

        /// <summary>
        /// Gets the Link Resolver.
        /// </summary>
        public static ILinkResolver LinkResolver { get; private set; }

        /// <summary>
        /// Gets the Conditional Entity Evaluator.
        /// </summary>
        public static IConditionalEntityEvaluator ConditionalEntityEvaluator { get; private set; }

        /// <summary>
        /// Gets the Media helper used for generating responsive markup for images, videos etc.
        /// </summary>
        public static IMediaHelper MediaHelper { get; private set; }

        /// <summary>
        /// Gets the Localization Resolver used for mapping URLs to Localizations.
        /// </summary>
        public static ILocalizationResolver LocalizationResolver { get; private set; }

        /// <summary>
        /// Gets the Handler for Unknown Localizations (failed publication URL lookups).
        /// </summary>
        public static IUnknownLocalizationHandler UnknownLocalizationHandler { get; private set; }


        public static void Init(IServiceProvider serviceProvider)
        {
            CacheProvider = (ICacheProvider)serviceProvider.GetService(typeof(ICacheProvider));
            LocalizationResolver = (ILocalizationResolver)serviceProvider.GetService(typeof(ILocalizationResolver));
            BinaryProvider = (IBinaryProvider)serviceProvider.GetService(typeof(IBinaryProvider));
            ContentProvider = (IContentProvider)serviceProvider.GetService(typeof(IContentProvider));
            ModelServiceProvider = (IModelServiceProvider)serviceProvider.GetService(typeof(IModelServiceProvider));
            MediaHelper = (IMediaHelper)serviceProvider.GetService(typeof(IMediaHelper));
            LinkResolver = (ILinkResolver)serviceProvider.GetService(typeof(ILinkResolver));
            NavigationProvider = (INavigationProvider)serviceProvider.GetService(typeof(INavigationProvider));
        }

        public static string GetPageController() => "Page";

        public static string GetPageAction() => "Page";

        public static string GetRegionController() => "Region";

        public static string GetRegionAction() => "Region";

        public static string GetEntityController() => "Entity";

        public static string GetEntityAction() => "Entity";

        public static string GetDefaultModuleName()
        {
            if (_defaultModuleName != null) return _defaultModuleName;
            // Might come here multiple times in case of a race condition, but that doesn't matter.

            IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

            _dxaFrameworkOptions = configuration.GetSection("dxa")
                      .Get<DxaFrameworkOptions>();

            string defaultModuleSetting = _dxaFrameworkOptions.DefaultModule;
            _defaultModuleName = string.IsNullOrEmpty(defaultModuleSetting) ? "Core" : defaultModuleSetting;
            Log.Debug("Default Module Name: '{0}'", _defaultModuleName);
            return _defaultModuleName;
        }

        /// <summary>
        /// Removes the version number from a URL path for an asset
        /// </summary>
        /// <param name="path">The URL path</param>
        /// <returns>The 'real' path to the asset</returns>
        public static string RemoveVersionFromPath(string path) => Regex.Replace(path, SystemFolder + "/" + VersionRegex + "/", delegate
        {
            return SystemFolder + "/";
        });

        /// <summary>
        /// Take a partial URL (so not including protocol, domain, port) and make it full by
        /// Adding the protocol, domain, port etc. from the given localization
        /// </summary>
        public static string MakeFullUrl(string url, Localization loc)
        {
            if (url.StartsWith(loc.Path))
            {
                url = url.Substring(loc.Path.Length);
            }
            return url.StartsWith("http") ? url : loc.GetBaseUrl() + url;
        }

        /// <summary>
        /// Generic a GUID
        /// </summary>
        /// <param name="prefix">prefix for the GUID</param>
        /// <returns>Prefixed Unique Identifier</returns>
        public static string GetUniqueId(string prefix) => prefix + Guid.NewGuid().ToString("N");

        #region Thread Safe Settings Update Helper Methods
        public static bool CheckSettingsNeedRefresh(string type, Localization localization) // TODO: Move to class Localization
        {
            Dictionary<string, DateTime> localizationRefreshStates;
            if (!_refreshStates.TryGetValue(localization.Id, out localizationRefreshStates))
            {
                return false;
            }
            DateTime settingsRefresh;
            if (!localizationRefreshStates.TryGetValue(type, out settingsRefresh))
            {
                return false;
            }
            return settingsRefresh.AddSeconds(1) < localization.LastRefresh;
        }

        public static void ThreadSafeSettingsUpdate<T>(string type, Dictionary<string, T> settings, string localizationId, T value) // TODO
        {
            lock (GetLocalizationLock(localizationId))
            {
                settings[localizationId] = value;
                UpdateRefreshState(localizationId, type);
            }
        }

        private static void UpdateRefreshState(string localizationId, string type) // TODO
        {
            //Update is already done under a localization lock, so we don't need to lock again here
            if (!_refreshStates.ContainsKey(localizationId))
            {
                _refreshStates.Add(localizationId, new Dictionary<string, DateTime>());
            }
            Dictionary<string, DateTime> states = _refreshStates[localizationId];
            if (states.ContainsKey(type))
            {
                states[type] = DateTime.Now;
            }
            else
            {
                states.Add(type, DateTime.Now);
            }
        }

        private static object GetLocalizationLock(string localizationId)
        {
            if (!_localizationLocks.ContainsKey(localizationId))
            {
                lock (_lock)
                {
                    _localizationLocks.Add(localizationId, new object());
                }
            }
            return _localizationLocks[localizationId];
        }

        #endregion
    }
}
