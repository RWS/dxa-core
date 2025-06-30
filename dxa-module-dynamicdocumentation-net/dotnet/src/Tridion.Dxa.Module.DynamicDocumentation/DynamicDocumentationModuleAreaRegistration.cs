using Microsoft.AspNetCore.Routing;
using Sdl.Web.Common.Models;
using Tridion.Dxa.Framework.Mvc.Configuration;
using Microsoft.AspNetCore.Builder;
using Sdl.Web.Modules.DynamicDocumentation.Models;

namespace Sdl.Web.Modules.DynamicDocumentation
{
    public class DynamicDocumentationModuleAreaRegistration : AreaRegistration
    {
        public static string AREA_NAME = "DynamicDocumentation";
        public override string AreaName => AREA_NAME;

        public override void Register(IEndpointRouteBuilder endpointRouteBuilder)
        {
            // Map the area route using IEndpointRouteBuilder
            endpointRouteBuilder.MapAreaControllerRoute(
                name: $"{AreaName}_Default",
                areaName: $"{AreaName}",
                pattern: "{controller}/{action}/{id?}",
                defaults: new { controller = "DynamicDocumentationPage", action = "Home" }
            );

            RegisterViewModels();
        }

        protected override void RegisterViewModels()
        {
            // Entity Views
            // Entity Views
            RegisterViewModel("Topic", typeof(Topic));

            // Page Views         
            RegisterViewModel("GeneralPage", typeof(PageModel));
            RegisterViewModel("ErrorPage", typeof(PageModel));

            // Regions
            RegisterViewModel("Main", typeof(RegionModel));
        }
    }
}
