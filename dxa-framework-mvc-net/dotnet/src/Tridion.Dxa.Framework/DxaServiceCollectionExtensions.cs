using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Sdl.Web.Common;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Mvc.Html;
using Sdl.Web.Tridion;
using Sdl.Web.Tridion.ApiClient;
using Sdl.Web.Tridion.Caching;
using Sdl.Web.Tridion.Linking;
using Sdl.Web.Tridion.Mapping;
using Sdl.Web.Tridion.ModelService;
using Sdl.Web.Tridion.Navigation;
using Sdl.Web.Tridion.Providers.Binary;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Tridion.Dxa.Framework.Mvc.Configuration;
using Tridion.Dxa.Framework.Tridion.Extensions;
using Tridion.Dxa.Framework.Tridion.Providers.Discovery;
using Tridion.Dxa.Framework.Tridion.Providers.OAuth;

namespace Tridion.Dxa.Framework
{
    public class HttpContextAccessor : IHttpContextAccessor
    {
        private static AsyncLocal<HttpContext> _httpContextCurrent = new AsyncLocal<HttpContext>();
        HttpContext IHttpContextAccessor.HttpContext { get => _httpContextCurrent.Value; set => _httpContextCurrent.Value = value; }
    }

    public static class DxaServiceCollectionExtensions
    {
        public static IServiceCollection AddDxa(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddHttpContextAccessor();
            
            // Configuration         
            services.Configure<DxaFrameworkOptions>(options =>
            {
                configuration.GetSection("Dxa").Bind(options);
            });

            services.AddSingleton<IDiscoveryClient, DiscoveryClient>();
            services.AddSingleton<IOAuthTokenProvider, OAuthTokenProvider>();

            services.AddSingleton<IApiClientFactory, ApiClientFactory>();
            services.AddSingleton<WebRequestContext, WebRequestContext>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddTransient<IActionContextAccessor, ActionContextAccessor>();

            services.AddSingleton<ILogger, DefaultLogger>();

            //services.AddSingleton<ICacheProvider, DefaultCacheProvider>();
            services.AddSingleton<ICacheProvider>(provider =>
            new KeylockCacheProvider(
                new DefaultCacheProvider(
                    provider.GetRequiredService<IMemoryCache>(),
                    provider.GetRequiredService<IDistributedCache>(),
                    provider.GetRequiredService<IConfiguration>()
                )
            ));
            services.AddTransient<IBinaryProvider, DefaultBinaryProvider>();
            services.AddTransient<IMediaHelper, BaseMediaHelper>();
            services.AddTransient<IContentProvider, DefaultContentProvider>();
            services.AddTransient<ICursorIndexerService, CursorIndexerService>();

            services.AddTransient<IModelServiceProvider, DefaultModelServiceProvider>();
            services.AddTransient<ILinkResolver, DefaultLinkResolver>();
            services.AddTransient<INavigationProvider, StaticNavigationProvider>();
            services.AddTransient<ILocalizationResolver, DefaultLocalizationResolver>();

            return services;
        }

        public static IServiceCollection AddDxaWebApi(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.Configure<DxaFrameworkOptions>(options =>
            {
                configuration.GetSection("Dxa").Bind(options);
            });

            services.AddSingleton<IDiscoveryClient, DiscoveryClient>();
            services.AddSingleton<IOAuthTokenProvider, OAuthTokenProvider>();

            services.AddSingleton<IApiClientFactory, ApiClientFactory>();
            services.AddSingleton<WebRequestContext, WebRequestContext>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddTransient<IActionContextAccessor, ActionContextAccessor>();

            // Default service configuration         
            services.AddSingleton<ILogger, DefaultLogger>();
            //services.AddTransient<ICacheProvider, DefaultCacheProvider>();
            services.AddSingleton<ICacheProvider>(provider =>
               new KeylockCacheProvider(
                   new DefaultCacheProvider(
                       provider.GetRequiredService<IMemoryCache>(),
                       provider.GetRequiredService<IDistributedCache>(),
                       provider.GetRequiredService<IConfiguration>()
                   )
            ));
            services.AddTransient<IContentProvider, DefaultContentProvider>();
            //services.AddTransient<IModelServiceProvider, DefaultModelServiceProvider>();
            services.AddTransient<ILinkResolver, DefaultLinkResolver>();
            //services.AddTransient<INavigationProvider, StaticNavigationProvider>();
            services.AddTransient<ILocalizationResolver, DefaultLocalizationResolver>();

            return services;
        }


        public static IServiceCollection AddDxaModule(this IServiceCollection services, Assembly dxaModuleAssembly)
        {
            try
            {
                var applicationPartManager = GetApplicationPartManager(services);
                if (applicationPartManager == null)
                    throw new NullReferenceException("Application Part Manager not available.");
                if (dxaModuleAssembly == null)
                    throw new NullReferenceException("Dxa Module Assembly not specified.");
                var allTypes = dxaModuleAssembly.GetLoadableTypes();
                var type = allTypes.FirstOrDefault(t => t.IsSubclassOf(typeof(AreaRegistration)));
                if (type == null)
                    throw new DxaException(
                        $"Failed to find type AreaRegistration in module: {dxaModuleAssembly.FullName}");
                var part = new AssemblyPart(dxaModuleAssembly);
                // Handle views included as embedded resources in assembly
                services.Configure<MvcRazorRuntimeCompilationOptions>(options =>
                {
                    options.FileProviders.Add(
                        new EmbeddedFileProvider(dxaModuleAssembly));
                });
                applicationPartManager.ApplicationParts.Add(part);
                // Load our views if they are compiled into another assembly
                var assemblyPath = Path.GetDirectoryName(dxaModuleAssembly.Location);
                var dllName = Path.GetFileNameWithoutExtension(dxaModuleAssembly.Location);
                var viewAsmLocation = Path.Combine(assemblyPath, $"{dllName}.Views.dll");
                if (!File.Exists(viewAsmLocation)) return services;
                var viewsAssembly = Assembly.LoadFile(viewAsmLocation);
                var viewsPart = new CompiledRazorAssemblyPart(viewsAssembly);
                applicationPartManager.ApplicationParts.Add(viewsPart);
                return services;
            }
            catch (Exception e)
            {
                throw new DxaException("Failed to add Dxa module", e);
            }
        }

        public static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
        {
            var applicationPartManager = GetServiceFromCollection<ApplicationPartManager>(services);
            return applicationPartManager;
        }

        public static T GetServiceFromCollection<T>(this IServiceCollection services)
        {
            var serviceDescriptor = services.LastOrDefault<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(d => d.ServiceType == typeof(T)));
            return (T)serviceDescriptor?.ImplementationInstance;
        }
    }
}
