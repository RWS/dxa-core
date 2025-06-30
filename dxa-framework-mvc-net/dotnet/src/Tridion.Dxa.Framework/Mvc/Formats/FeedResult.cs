using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace Sdl.Web.Mvc.Formats
{
    public class FeedResult : ActionResult
    {
        public SyndicationFeedFormatter Formatter { get; set; }
        public Encoding ContentEncoding { get; set; }
        public string ContentType { get; set; }

        public FeedResult(SyndicationFeedFormatter formatter)
        {
            Formatter = formatter;
            ContentEncoding = Encoding.UTF8;
        }

        public override void ExecuteResult(ActionContext context)
        {
            var response = context.HttpContext.Response;

            var mediaType = new MediaTypeHeaderValue(ContentType) { Encoding = ContentEncoding };
            response.ContentType = mediaType.ToString();

            using (XmlTextWriter writer = new XmlTextWriter(response.Body,
                Encoding.Default))
            {
                writer.Formatting = Formatting.Indented;
                Formatter.WriteTo(writer);
            }
        }
    }
}
