using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Tridion.Dxa.Api.Client.ContentModel;
using Tridion.Dxa.Api.Client.Utils;

namespace Sdl.Web.Tridion.ApiClient
{
    public static class Extensions
    {
        public static ContentNamespace Namespace(this Localization localization)
            => CmUri.NamespaceIdentiferToId(localization.CmUriScheme);

        public static int PublicationId(this Localization localization)
        {
            int pubId;
            if (!int.TryParse(localization.Id, out pubId))
                throw new DxaItemNotFoundException($"Invalid publication id '{localization.Id}' stored in localization.");
            return pubId;
        }
    }
}
