using System;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.AspNetCore.Http; // New namespace
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.Modules.Search.Models;
using Sdl.Web.Modules.Search.Providers;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Mvc.Controllers;

namespace Sdl.Web.Modules.Search.Controllers
{
    public class SearchController : EntityController
    {
        protected ISearchProvider SearchProvider { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="searchProvider">The Search Provider to use. Set using Dependency Injection (ISearchProvider interface must be defined in Unity.config)</param>
        /// <param name="contentProvider">The Content Provider to use, passed to base constructor</param>
        public SearchController(ISearchProvider searchProvider, IContentProvider contentProvider)
            : base(contentProvider) // Pass the contentProvider to the base constructor
        {
            SearchProvider = searchProvider;
        }

        /// <summary>
        /// Enrich the SearchQuery View Model with request query string parameters and populate the results using a configured Search Provider.
        /// </summary>
        protected override ViewModel EnrichModel(ViewModel model)
        {
            using (new Tracer(model))
            {
                base.EnrichModel(model);

                if (model is not SearchQuery searchQuery || !searchQuery.GetType().IsGenericType)
                {
                    throw new DxaSearchException($"Unexpected View Model: '{model}'. Expecting type SearchQuery<T>.");
                }

                var queryString = HttpContext.Request.Query; // Use Query property instead of QueryString

                // Map standard query string parameters
                searchQuery.QueryText = queryString["q"].ToString();
                searchQuery.Start = queryString.ContainsKey("start") ? Convert.ToInt32(queryString["start"]) : 1;

                // Convert query string to a NameValueCollection if needed
                var queryStringParameters = new NameValueCollection();
                foreach (var key in queryString.Keys)
                {
                    queryStringParameters.Add(key, queryString[key].ToString());
                }

                searchQuery.QueryStringParameters = queryStringParameters;

                var searchItemType = searchQuery.GetType().GetGenericArguments()[0];
                SearchProvider.ExecuteQuery(searchQuery, searchItemType, WebRequestContext.Current.Localization);

                return searchQuery;
            }
        }
    }
}
