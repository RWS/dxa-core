using Tridion.Dxa.Api.Client.ContentModel;

namespace Tridion.Dxa.Api.Client.Utils
{
    public static class ContentModelHelpers
    {
        public static CmUri CmUri(this IItem item)
            => new CmUri(item.NamespaceId, item.PublicationId, item.ItemId, item.ItemType);
    }
}
