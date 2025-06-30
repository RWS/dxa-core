using Microsoft.AspNetCore.Mvc;
using Sdl.Web.Common.Models;
using System.ServiceModel.Syndication;

namespace Sdl.Web.Mvc.Formats
{
    public class AtomFormatter : FeedFormatter
    {
        public AtomFormatter()
        {
            AddMediaType("application/atom+xml");
            ProcessModel = true;
            AddIncludes = false;
        }

        public override ActionResult FormatData(ControllerContext controllerContext, object model)
        {
            SyndicationFeed feed = ExtractSyndicationFeed(model as PageModel);
            return feed == null ? null : new FeedResult(new Atom10FeedFormatter(feed)) { ContentType = "application/atom+xml" };
        }
    }
}
