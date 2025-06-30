using Microsoft.Extensions.Localization;
using System.Collections.Generic;

namespace Sdl.Web.Mvc.Configuration
{
    /// <summary>
    /// ASP.NET Core StringLocalizer Provider which obtains the resources from the current <see cref="Sdl.Web.Common.Configuration.Localization"/>.
    /// </summary>
    public class TridionStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name]
        {
            get
            {
                string value = (string)WebRequestContext.Current.Localization.GetResources(name)[name];
                return new LocalizedString(name, value ?? name, resourceNotFound: value == null);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
            => new LocalizedString(name, string.Format(this[name].Value, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var resources = WebRequestContext.Current.Localization.GetResources();
            foreach (string key in resources.Keys)
            {
                yield return new LocalizedString(key, resources[key]?.ToString());
            }
        }
    }
}
