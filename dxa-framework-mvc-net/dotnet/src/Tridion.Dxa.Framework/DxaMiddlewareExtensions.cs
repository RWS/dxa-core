using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Mvc.Formats;
using Tridion.Dxa.Framework.Mvc.Configuration;
using Tridion.Dxa.Framework.Tridion.Extensions;

namespace Tridion.Dxa.Framework
{
    /// <summary>
    /// Dxa Middleware Extensions
    /// </summary>
    public static class DxaMiddlewareExtensions
    {
        private static ApplicationPartManager GetApplicationPartManager(IServiceCollection services)
        {
            ApplicationPartManager applicationPartManager = GetServiceFromCollection<ApplicationPartManager>(services);
            return applicationPartManager;
        }

        private static T GetServiceFromCollection<T>(IServiceCollection services)
        {
            ServiceDescriptor serviceDescriptor = services.LastOrDefault<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(d => d.ServiceType == typeof(T)));
            return (T)(serviceDescriptor != null ? serviceDescriptor.ImplementationInstance : (object)null);
        }

        public static IApplicationBuilder UseDxa(this IApplicationBuilder app)
        {
            // Rewrite rule for versioned URLs
            var rewriteOptions = new RewriteOptions()
                .AddRewrite(
                    SiteConfiguration.SystemFolder + "/" + SiteConfiguration.VersionRegex + "/(.*)",
                    SiteConfiguration.SystemFolder + "/$2",
                    skipRemainingRules: true
                );
            
            app.UseRewriter(rewriteOptions);

            // Use dxa middleware
            app.UseMiddleware<DxaMiddleware>();

            // Register data formatters
            DataFormatters.Formatters.Add("json", new JsonFormatter());
            DataFormatters.Formatters.Add("rss", new RssFormatter());
            DataFormatters.Formatters.Add("atom", new AtomFormatter());

            return app;
        }

        public static void ConfigureDxaEndpoints(this IEndpointRouteBuilder endpoints, IServiceProvider serviceProvider)
        {
            // Register area routes dynamically
            var areaRegistrars = GetAreaRegistrars(serviceProvider);
            foreach (var registrar in areaRegistrars)
            {
                registrar.Register(endpoints);
            }
        }


        public static IApplicationBuilder UseDxaRestApi(this IApplicationBuilder app)
        {
            // Rewrite rule for versioned urls
            var rewrite = new RewriteOptions().AddRewrite(
                SiteConfiguration.SystemFolder + "/" + SiteConfiguration.VersionRegex + "/(.*)",  // RegEx to match Route
                SiteConfiguration.SystemFolder + "/$2",                     // URL to rewrite route
                skipRemainingRules: true         // Should skip other rules
            );
            app.UseRewriter(rewrite);

            // Use dxa middleware
            app.UseMiddleware<DxaMiddleware>();

            // Register data formatters
            DataFormatters.Formatters.Add("json", new JsonFormatter());
            DataFormatters.Formatters.Add("rss", new RssFormatter());
            DataFormatters.Formatters.Add("atom", new AtomFormatter());

            return app;
        }

        private static List<AreaRegistration> GetAreaRegistrars(IServiceProvider services)
        {
            var registrars = new List<AreaRegistration>();
            var applicationPartsManager = services.GetService<ApplicationPartManager>();
            if (applicationPartsManager == null) return registrars;

            var appParts = new HashSet<string>();
            foreach (var appPart in applicationPartsManager.ApplicationParts)
            {
                if (appPart is AssemblyPart asmPart && !appParts.Contains(appPart.Name) && !asmPart.Name.StartsWith("Tridion.Dxa.Framework") && !asmPart.Name.StartsWith("Microsoft"))
                {
                    var types = asmPart.Assembly.GetLoadableTypes();
                    foreach (var type in types)
                    {
                        if (type.IsAbstract || !type.IsSubclassOf(typeof(AreaRegistration))) continue;
                        appParts.Add(asmPart.Name);
                        registrars.Add((AreaRegistration)Activator.CreateInstance(type));
                        break;
                    }
                }
            }
            return registrars;
        }
    }
}
