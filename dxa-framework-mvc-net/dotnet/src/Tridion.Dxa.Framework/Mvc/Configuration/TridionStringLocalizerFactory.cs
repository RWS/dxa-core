using Microsoft.Extensions.Localization;
using System;

namespace Sdl.Web.Mvc.Configuration
{
    public class TridionStringLocalizerFactory : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => new TridionStringLocalizer();

        public IStringLocalizer Create(string baseName, string location) => new TridionStringLocalizer();
    }

}
