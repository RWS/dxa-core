using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Sdl.Web.Common;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Mvc.Formats;
using Sdl.Web.Mvc.Html;
using Sdl.Web.Tridion.Mapping;
using Tridion.Dxa.Example.WebApp.Common;
using Tridion.Dxa.Example.WebApp.Common.Extensions;
using Tridion.Dxa.Framework;
using Tridion.Dxa.Framework.ADF;
using Tridion.Dxa.Framework.Mvc.Configuration;
using Tridion.Dxa.Framework.Mvc.OutputCache;
using Tridion.Dxa.Framework.Tridion.Extensions;

namespace Tridion.Dxa.Example.WebApp
{
    internal class Startup
    {
        public IWebHostEnvironment Environment { get; }

        public IConfiguration Configuration { get; }

        internal string FirstConfiguredUrl { get; set; }

        private ILogger<Startup> Logger { get; set; }

        private IApplicationBuilder Application { get; set; }

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            Environment = environment;
            Configuration = configuration;

            if (string.IsNullOrWhiteSpace(configuration[nameof(CommonSettings.URLPathBase)]))
            {
                // This variable is referenced in bootstrapping json and the value is resolved from configuration.
                configuration[nameof(CommonSettings.URLPathBase)] = "/";
            }

            CommonSettings.URLPathBase = configuration[nameof(CommonSettings.URLPathBase)];
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add session services FIRST
            services.AddSession(options =>
            {
                //options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddMemoryCache();
            services.AddCors();
            //services.ConfigureForwardedHeaders(Configuration.GetSection("ForwardedHeadersOptions"));

            // Configure MVC with modules
            services.AddControllers()
                .AddNewtonsoftJson() //For ?format=json
                .ConfigureApplicationPartManager(apm =>
                apm.ApplicationParts.Add(new AssemblyPart(typeof(Startup).Assembly)
            ));
            
            //Resource Labels
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            //  Core MVC services
            services.AddMvc().AddViewLocalization();
            services.AddScoped<FormatDataAttribute>();
            services.AddScoped<DxaOutputCacheAttribute>();

            services.AddControllersWithViews(options =>
            {
                options.Filters.Add<FormatDataAttribute>(1); // Lower order numbers execute first
                options.Filters.Add<DxaOutputCacheAttribute>(2);
            })
            .AddRazorRuntimeCompilation()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

            // Infrastructure services
            ConfigureInfrastructureServices(services);

            services.AddSingleton<IStringLocalizerFactory, TridionStringLocalizerFactory>();
            services.AddSingleton<IStringLocalizer, TridionStringLocalizer>();
            services.AddSingleton<AmbientDataConfig>();
            services.AddSingleton<ClaimStoreDataService>();

            // Add DXA to the mix
            services.AddDxa(Configuration);

            // Auto-register modules
            AddDxaModules(services);
            //services.AddTransient<ISearchProvider, TridionSitesSearchProvider>();
            services.AddHealthChecks();

            // Add DXA Tridion Docs specific
            /*services.AddTransient<INavigationProvider, DynamicNavigationProvider>();
            services.AddTransient<IContentProviderExt, DefaultContentProvider>();
            services.AddTransient<ILocalizationResolver, DynamicDocumentationLocalizationResolver>();
            services.AddTransient<ISearchProvider, IQSearchProvider>();
            GenericTopic.Register();*/
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, ILogger<Startup> logger)
        {
            // In ASP.NET Core 3.0 and later, it is no longer possible to inject ILogger in Startup.cs
            // https://github.com/aspnet/Announcements/issues/353
            Logger = logger;
            Application = app;
            Markup.HttpContextAccessor = httpContextAccessor;


            // This ensures that X-Forwarded-* headers coming from a proxy (e.g. Load Balancer) are taken into account.
            // For example, when determining if the connection is over HTTP or HTTPS, which is important if the Load Balancer uses TLS-termination.
            // Without taking X-Forwarded-* headers into account, TAM would think the connection is over HTTP, whereas it is over HTTPS (from client-perspective).
            // This affects HSTS and (Secure) Cookies, so the Middleware should be run before those two.
            app.UseForwardedHeaders();

            FirstConfiguredUrl = Application.ServerFeatures.Get<IServerAddressesFeature>()?.Addresses?.FirstOrDefault();

            if (Environment.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseStaticFiles();

            app.UseRouting();

            // Development is used when running in Visual Studio on a developer environment
            if (!Environment.IsDevelopment())
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            if (IsHttpsConfigured())
            {
                app.UseHttpsRedirection();
            }


            // Error handling
            //ConfigureErrorHandling(app, env);

            // Handle ignored routes first
            app.UseWhen(context =>
            {
                var path = context.Request.Path;

                // Ignore .axd requests
                if (path.Value?.EndsWith(".axd") == true) return false;

                // Ignore CID path
                var cidUrlPath = configuration["cid-service-proxy-pattern"];
                if (!string.IsNullOrEmpty(cidUrlPath) && path.StartsWithSegments(cidUrlPath))
                    return false;

                // Ignore configured URLs
                var ignoreUrls = configuration.GetSection("ignore-urls").Get<string[]>();
                if (ignoreUrls?.Any(url => path.StartsWithSegments(url)) == true)
                    return false;

                return true;
            }, appBuilder =>
            {
                appBuilder.UseHttpsRedirection();
                appBuilder.UseStaticFiles();
                appBuilder.UseCookiePolicy();
                appBuilder.UseMiddleware<ADFContextMiddleware>(
                    configuration.GetSection("AmbientConfig").Get<AmbientDataConfig>());
                appBuilder.UseDxa();
            });

            app.UseRouting();
            app.UseSession(); // This must come before UseEndpoints            
            app.UseEndpoints(endpoints =>
            {
                // XPM blank page
                endpoints.MapAreaControllerRoute(
                    name: "Core_Blank",
                    areaName: "Core",
                    pattern: "se_blank.html",
                    defaults: new { controller = "Page", action = "Blank" });

                // Navigation JSON
                endpoints.MapAreaControllerRoute(
                    name: "Core_Navigation",
                    areaName: "Core",
                    pattern: "navigation.json",
                    defaults: new { controller = "Navigation", action = "SiteMapJson" });

                endpoints.MapAreaControllerRoute(
                    name: "Core_Navigation_loc",
                    areaName: "Core",
                    pattern: "{localization}/navigation.json",
                    defaults: new { controller = "Navigation", action = "SiteMapJson" });

                // Google Site Map
                endpoints.MapAreaControllerRoute(
                    name: "Core_Sitemap",
                    areaName: "Core",
                    pattern: "sitemap.xml",
                    defaults: new { controller = "Navigation", action = "SiteMapXml" });

                endpoints.MapAreaControllerRoute(
                    name: "Core_Sitemap_Loc",
                    areaName: "Core",
                    pattern: "{localization}/sitemap.xml",
                    defaults: new { controller = "Navigation", action = "SiteMapXml" });

                // Navigation subtree
                endpoints.MapControllerRoute(
                    name: "NavSubtree",
                    pattern: "api/navigation/subtree/{sitemapItemId?}",
                    defaults: new { controller = "Navigation", action = "GetNavigationSubtree" });

                endpoints.MapControllerRoute(
                    name: "NavSubtree_Loc",
                    pattern: "{localization}/api/navigation/subtree/{sitemapItemId?}",
                    defaults: new { controller = "Navigation", action = "GetNavigationSubtree" });

                // For resolving ids to urls
                endpoints.MapAreaControllerRoute(
                    name: "Core_Resolve",
                    areaName: "Core",
                    pattern: "resolve/{**itemId}",
                    defaults: new { controller = "Page", action = "Resolve" });

                endpoints.MapAreaControllerRoute(
                    name: "Core_Resolve_Loc",
                    areaName: "Core",
                    pattern: "{localization}/resolve/{**itemId}",
                    defaults: new { controller = "Page", action = "Resolve" });

                // Admin routes
                if (configuration.GetValue<bool>("Dxa:AdminRefreshEnabled"))
                {
                    endpoints.MapControllerRoute(
                        name: "Core_Admin",
                        pattern: "admin/{action=Refresh}",
                        defaults: new { controller = "Admin" });

                    endpoints.MapControllerRoute(
                        name: "Core_Admin_Loc",
                        pattern: "{localization}/admin/{action=Refresh}",
                        defaults: new { controller = "Admin" });
                }

                // Tridion Page Route (catch-all)
                endpoints.MapAreaControllerRoute(
                    name: "Core_Page",
                    areaName: "Core",
                    pattern: "{**pageUrl}",
                    defaults: new { controller = "Page", action = "Page" });

                // DXA endpoints and default route
                endpoints.ConfigureDxaEndpoints(serviceProvider);
            });
        }

        private bool IsHttpsConfigured()
        {
            // This code tries to detect (at runtime) if https configured or not 
            // and there is no standard way of doing this!

            IEnumerable<object> serverFeatures = Application.ServerFeatures
                .Where(sf => sf.Key == typeof(IServerAddressesFeature))
                .Select(sf => sf.Value);

            // Server is IIS
            if (serverFeatures.Any(ct => ct.GetType().FullName.StartsWith(typeof(IISServerDefaults).Namespace, StringComparison.Ordinal)))
            {
                return !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT"));
            }

            // Server is Kestrel
            return serverFeatures.Any(ct => ct.GetType().FullName.StartsWith(typeof(KestrelConfigurationLoader).Namespace, StringComparison.Ordinal))
                && Configuration.GetSection("Kestrel:EndPoints")
                .GetChildren().EmptyIfNull().Select(endpoint => endpoint.GetValue<string>("Url"))
                .Any(url => !string.IsNullOrEmpty(url)
                && url.StartsWith("https", StringComparison.InvariantCultureIgnoreCase));
        }

        //public static void ConfigureForwardedHeaders(this IServiceCollection services, IConfiguration config)
        //{
        //    if (config == null)
        //    {
        //        throw new ArgumentNullException("config");
        //    }

        //    services.Configure(delegate (ForwardedHeadersOptions options)
        //    {
        //        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
        //        options.KnownProxies.Clear();
        //        options.KnownNetworks.Clear();
        //    });
        //    TypeDescriptor.AddAttributes(typeof(IPAddress), new TypeConverterAttribute(typeof(IPAddressTypeConverter)));
        //    TypeDescriptor.AddAttributes(typeof(Microsoft.AspNetCore.HttpOverrides.IPNetwork), new TypeConverterAttribute(typeof(IPNetworkTypeConverter)));
        //    services.Configure<ForwardedHeadersOptions>(config);
        //}

        private void ConfigureInfrastructureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = _ => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.Configure<KestrelServerOptions>(o => o.AllowSynchronousIO = true);
            services.Configure<IISServerOptions>(o => o.AllowSynchronousIO = true); //If using IIS

            // Redis cache configuration
            if (NeedsRedisCache())
            {
                var redisConfig = Configuration.GetSection("SdlWebDelivery:Caching:Handlers:regularDistributedCache");
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConfig["ConnectionString"];
                    options.InstanceName = redisConfig["InstanceName"];
                });

                // Add direct Redis connection for advanced operations
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                    ConnectionMultiplexer.Connect(redisConfig["ConnectionString"]));
            }
        }

        private bool NeedsRedisCache()
        {
            var regions = Configuration.GetSection("SdlWebDelivery:Caching:Regions").GetChildren();
            return regions.Any(r =>
                Configuration.GetValue<string>(
                    $"SdlWebDelivery:Caching:Handlers:{r["CacheName"]}:Type") == "RedisCacheHandler"
            );
        }

        //private void ConfigureErrorHandling(IApplicationBuilder app, IWebHostEnvironment env)
        //{
        //    if (env.IsDevelopment())
        //    {
        //        app.UseDeveloperExceptionPage();
        //    }
        //    else
        //    {
        //        app.UseExceptionHandler("/Home/Error");
        //        app.UseHsts();
        //    }
        //}

        private void AddDxaModules(IServiceCollection services)
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var applicationPartManager = GetApplicationPartManager(services) ??
                throw new DxaException("Application Part Manager not available");

            foreach (var dllPath in Directory.GetFiles(baseDirectory, "*.Module.*.dll"))
            {
                try
                {
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);

                    // Check for AreaRegistration implementations
                    var areaRegistrations = assembly.GetLoadableTypes()
                        .Where(t => t.IsSubclassOf(typeof(AreaRegistration)) && !t.IsAbstract);

                    if (!areaRegistrations.Any()) continue;

                    // Add main assembly as ApplicationPart
                    var assemblyPart = new AssemblyPart(assembly);
                    if (!applicationPartManager.ApplicationParts.Any(p => p.Name == assemblyPart.Name))
                    {
                        applicationPartManager.ApplicationParts.Add(assemblyPart);
                    }

                    // Handle embedded views
                    services.Configure<MvcRazorRuntimeCompilationOptions>(options =>
                    {
                        options.FileProviders.Add(new EmbeddedFileProvider(assembly));
                    });

                    // Handle separate Views assembly
                    var viewsAssemblyPath = Path.Combine(
                        Path.GetDirectoryName(dllPath),
                        $"{Path.GetFileNameWithoutExtension(dllPath)}.Views.dll"
                    );

                    if (File.Exists(viewsAssemblyPath))
                    {
                        var viewsAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(viewsAssemblyPath);
                        var viewsPart = new CompiledRazorAssemblyPart(viewsAssembly);

                        if (!applicationPartManager.ApplicationParts.Any(p => p.Name == viewsPart.Name))
                        {
                            applicationPartManager.ApplicationParts.Add(viewsPart);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new DxaException($"Failed to load Dxa module {dllPath}", ex);
                }
            }
        }

        private static ApplicationPartManager GetApplicationPartManager(IServiceCollection services)
        {
            var manager = services.FirstOrDefault(sd =>
                sd.ServiceType == typeof(ApplicationPartManager))?.ImplementationInstance as ApplicationPartManager;

            return manager ?? throw new InvalidOperationException("ApplicationPartManager not registered");
        }
    }
}
