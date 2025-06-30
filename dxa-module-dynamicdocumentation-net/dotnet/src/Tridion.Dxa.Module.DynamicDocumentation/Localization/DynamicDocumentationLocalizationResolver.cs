using System;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Tridion;
using Sdl.Web.Tridion.ApiClient;

namespace Sdl.Web.Modules.DynamicDocumentation.Localization
{
    public class DynamicDocumentationLocalizationResolver : GraphQLMashupLocalizationResolver
    {
        private readonly Common.Configuration.Localization _localization;

        public DynamicDocumentationLocalizationResolver(IApiClientFactory apiClientFactory) : base(apiClientFactory)
        {
                _localization = new DocsLocalization();
                _localization.EnsureInitialized();
        }

        //public DynamicDocumentationLocalizationResolver()
        //{
        //    _localization = new DocsLocalization();
        //    _localization.EnsureInitialized();
        //}

        public override Common.Configuration.Localization ResolveLocalization(Uri url)
        {
            // Attempt to resolve url to docs localization otherwise use dummy (for homepage)
            return ResolveDocsLocalization(url) ?? _localization;
        }
    }
}
