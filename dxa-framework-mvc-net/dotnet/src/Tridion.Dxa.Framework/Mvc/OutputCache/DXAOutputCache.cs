using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Mvc.Html;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Sdl.Web.Mvc;

namespace Tridion.Dxa.Framework.Mvc.OutputCache
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class DxaOutputCacheAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var options = serviceProvider.GetRequiredService<IOptions<DxaFrameworkOptions>>();
            return new DxaOutputCacheFilter(options);
        }

        private class DxaOutputCacheFilter : IAsyncActionFilter, IAsyncResultFilter
        {
            private readonly bool _enabled;
            private readonly bool _ignorePreview;
            private static readonly object CacheKeyStack = new object();
            private static readonly object DisablePageOutputCacheKey = new object();

            public DxaOutputCacheFilter(IOptions<DxaFrameworkOptions> options)
            {
                _enabled = options.Value.OutputCachingEnabled;
                _ignorePreview = options.Value.OutputCacheSettings?.IgnorePreview ?? false;
            }

            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                if (!_enabled)
                {
                    await next();
                    return;
                }

                var controller = context.Controller as Controller;
                if (controller?.ViewData[DxaViewDataItems.DisableOutputCache] != null &&
                    (bool)controller.ViewData[DxaViewDataItems.DisableOutputCache])
                {
                    await next();
                    return;
                }

                if (IgnoreCaching(context.Controller))
                {
                    await next();
                    return;
                }

                OutputCacheItem cachedOutput = null;
                string cacheKey = CalcCacheKey(context);
                PushCacheKey(context.HttpContext, cacheKey);
                SiteConfiguration.CacheProvider.TryGet(cacheKey, CacheRegions.RenderedOutput, out cachedOutput);

                if (cachedOutput != null)
                {
                    context.Result = new ContentResult
                    {
                        Content = cachedOutput.Content,
                        ContentType = cachedOutput.ContentType,
                    };
                    return;
                }

                await next();
            }

            public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
            {
                if (!_enabled || context.Result is ContentResult)
                {
                    await next();
                    return;
                }

                if (IgnoreCaching(context.Controller))
                {
                    SetDisablePageOutputCache(context.HttpContext, true);
                }

                string cacheKey = PopCacheKey(context.HttpContext);
                if (cacheKey == null)
                {
                    await next();
                    return;
                }

                OutputCacheItem cachedOutput;
                SiteConfiguration.CacheProvider.TryGet(cacheKey, CacheRegions.RenderedOutput, out cachedOutput);

                if (cachedOutput == null)
                {
                    var originalBody = context.HttpContext.Response.Body;
                    try
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            context.HttpContext.Response.Body = memoryStream;
                            await next();

                            memoryStream.Position = 0;
                            using (var reader = new StreamReader(memoryStream))
                            {
                                var content = await reader.ReadToEndAsync();

                                var model = (context.Controller as Controller)?.ViewData?.Model as ViewModel;
                                bool commitCache = ShouldCommitCache(context, model);

                                if (commitCache)
                                {
                                    if (context.HttpContext.Response.ContentType?.Contains("text/html") == true &&
                                        model != null &&
                                        WebRequestContext.Current.Localization.IsXpmEnabled)
                                    {
                                        content = Markup.TransformXpmMarkupAttributes(content);
                                        content = Markup.DecorateMarkup(new HtmlString(content), model).ToString();
                                    }

                                    var cacheItem = new OutputCacheItem
                                    {
                                        Content = content,
                                        ContentType = context.HttpContext.Response.ContentType,
                                    };

                                    SiteConfiguration.CacheProvider.Store(cacheKey, CacheRegions.RenderedOutput, cacheItem);
                                    Log.Trace($"ViewModel={model?.MvcData} added to DxaOutputCache.");
                                }

                                memoryStream.Position = 0;
                                await memoryStream.CopyToAsync(originalBody);
                            }
                        }
                    }
                    finally
                    {
                        context.HttpContext.Response.Body = originalBody;
                    }
                }
                else
                {
                    context.Result = new ContentResult
                    {
                        Content = cachedOutput.Content,
                        ContentType = cachedOutput.ContentType
                    };
                }
            }

            private bool ShouldCommitCache(ResultExecutingContext context, ViewModel model)
            {
                bool isChildAction = context.HttpContext.Items["IsChildAction"] != null;
                bool disableCache = DisablePageOutputCache(context.HttpContext);

                bool commitCache = !isChildAction || !disableCache;

                if (isChildAction && model != null && (IgnoreCaching(model) || model.IsVolatile))
                {
                    SetDisablePageOutputCache(context.HttpContext, true);
                    commitCache = false;
                    Log.Trace($"ViewModel={model.MvcData} is marked not to be added to DxaOutputCache.");
                }

                return commitCache && (_ignorePreview || !WebRequestContext.Current.IsSessionPreview) &&
                       !IgnoreCaching(context.Controller);
            }

            private string CalcCacheKey(ActionExecutingContext context)
            {
                var sb = new StringBuilder();

                // Safely get WebRequestContext
                var webRequestContext = WebRequestContext.Current;
                if (webRequestContext == null)
                {
                    throw new InvalidOperationException("WebRequestContext is not available");
                }

                // Safely get localization ID
                var localizationId = webRequestContext.Localization?.Id ?? "null-localization";

                // Safely get user agent
                var userAgent = context.HttpContext.Request.Headers.TryGetValue("User-Agent", out var agent)
                    ? agent.ToString()
                    : "no-user-agent";

                // Safely get cache key salt - removed the problematic coalesce
                var cacheKeySalt = webRequestContext.CacheKeySalt; // Just use the value directly

                sb.Append($"{context.ActionDescriptor.Id}-{localizationId}-{context.HttpContext.Request.Path}-{userAgent}:{cacheKeySalt}");

                // Handle action arguments
                foreach (var p in context.ActionArguments.Where(p => p.Value != null))
                {
                    // Use ToString() to avoid potential hash code issues with different types
                    sb.Append($"{p.Key.ToString().GetHashCode()}:{p.Value.ToString().GetHashCode()}-");
                }
                return sb.ToString();
            }

            private static bool DisablePageOutputCache(HttpContext httpContext)
            {
                return httpContext.Items.TryGetValue(DisablePageOutputCacheKey, out var value) &&
                       value is bool result &&
                       result;
            }

            private static void SetDisablePageOutputCache(HttpContext httpContext, bool disable)
            {
                httpContext.Items[DisablePageOutputCacheKey] = disable;
            }

            private static void PushCacheKey(HttpContext httpContext, string key)
            {
                if (!(httpContext.Items[CacheKeyStack] is Stack<string> stack))
                {
                    stack = new Stack<string>();
                    httpContext.Items[CacheKeyStack] = stack;
                }
                stack.Push(key);
            }

            private static string PopCacheKey(HttpContext httpContext)
            {
                if (httpContext.Items[CacheKeyStack] is Stack<string> stack && stack.Count > 0)
                {
                    return stack.Pop();
                }
                return null;
            }

            private static bool IgnoreCaching(object obj)
            {
                return obj != null && Attribute.GetCustomAttribute(obj.GetType(), typeof(DxaNoOutputCacheAttribute)) != null;
            }

            [Serializable]
            private sealed class OutputCacheItem
            {
                public string ContentType { get; set; }
                public Encoding ContentEncoding { get; set; }
                public string Content { get; set; }
            }
        }
    }
}