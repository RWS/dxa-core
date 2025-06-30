using Microsoft.AspNetCore.Routing;
using Sdl.Web.Modules.Search.Models;
using Tridion.Dxa.Framework.Mvc.Configuration;
using Microsoft.AspNetCore.Builder;

namespace Sdl.Web.Modules.Search
{
    public class SearchAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Search";

        public override void Register(IEndpointRouteBuilder endpointRouteBuilder)
        {
            // Map the area route using IEndpointRouteBuilder
            endpointRouteBuilder.MapAreaControllerRoute(
                name: $"{AreaName}_Default",
                areaName: $"{AreaName}",
                pattern: "{controller}/{action}/{id?}",
                defaults: new { controller = "Search", action = "Entity" }
            );

            RegisterViewModels();
        }

        protected override void RegisterViewModels()
        {
            // Search Entity Views
            RegisterViewModel("SearchBox", typeof(SearchBox));
            RegisterViewModel("SearchItem", typeof(SearchItem));
            RegisterViewModel("SearchResults", typeof(SearchQuery<SearchItem>), "Search");
        }
    }
}
