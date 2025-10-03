using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;
using System;
using System.ServiceModel.Syndication;
using Microsoft.AspNetCore.Http;
using System.Web;

namespace Sdl.Web.Mvc.Formats
{
    /// <summary>
    /// Abstract base class for syndication feed formatters.
    /// </summary>
    public abstract class FeedFormatter : BaseFormatter
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Constructor for dependency injection
        /// </summary>
        protected FeedFormatter(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Extracts a syndication feed from a given View Model.
        /// </summary>
        /// <param name="pageModel">The Page Model to extract the feed from.</param>
        /// <returns>The extracted syndication feed.</returns>
        protected SyndicationFeed ExtractSyndicationFeed(PageModel pageModel)
        {
            if (pageModel == null)
            {
                return null;
            }

            string description = null;
            if (pageModel.Meta != null)
            {
                pageModel.Meta.TryGetValue("description", out description);
            }

            string feedAlternateLink = GetPageUrlWithoutFormatParameter();

            return new SyndicationFeed(pageModel.Title, description, new Uri(feedAlternateLink))
            {
                Language = WebRequestContext.Current.Localization?.Culture,
                Items = pageModel.ExtractSyndicationFeedItems(WebRequestContext.Current.Localization)
            };
        }

        private string GetPageUrlWithoutFormatParameter()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                throw new InvalidOperationException("HttpContext is not available");
            }

            var request = httpContext.Request;
            var filtered = HttpUtility.ParseQueryString(request.QueryString.ToString());
            filtered.Remove("format");

            var baseUrl = $"{request.Scheme}://{request.Host}{request.Path}";
            return filtered.Count > 0 ? $"{baseUrl}?{filtered}" : baseUrl;
        }
    }
}
