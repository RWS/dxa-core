using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework
{
    public class DxaMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<DxaMiddleware> _logger;
        private readonly List<string> _ignoredPaths;

        public DxaMiddleware(
            RequestDelegate next,
            ILogger<DxaMiddleware> logger,
            IOptions<DxaFrameworkOptions> options)
        {
            _next = next;
            _logger = logger;
            _ignoredPaths = options.Value?.IgnoredPaths ?? new List<string>();
        }


        public async Task Invoke(HttpContext context, ILocalizationResolver localizationResolver, IContentProvider contentProvider)
        {
            _logger.LogTrace("DxaMiddleware processing request for path: {Path}", context.Request.Path);

            // Skip middleware for excluded paths
            var path = context.Request.Path.Value;
            if (ShouldSkipMiddleware(path))
            {
                await _next(context);
                return;
            }

            // Handle health check
            if (IsHealthCheckPath(path))
            {
                await HandleHealthCheck(context);
                return;
            }

            try
            {
                // Initialize DXA context
                var webRequestContext = context.RequestServices.GetRequiredService<WebRequestContext>();
                SiteConfiguration.Init(context.RequestServices);

                // Resolve localization
                var localization = await ResolveLocalizationAsync(context, localizationResolver, webRequestContext);
                if (localization == null) return;

                webRequestContext.Localization = localization;

                // Handle static content
                if (IsStaticContentRequest(context, localization))
                {
                    await HandleStaticContentAsync(context, localization, contentProvider);
                    return;
                }

                // Process versioned URL rewriting
                await ProcessVersionedUrls(context, localization);

                // Continue pipeline for non-static content
                await _next(context);
            }
            catch (Exception ex) when (ex is not DxaUnknownLocalizationException and not DxaItemNotFoundException)
            {
                _logger.LogError(ex, "Error processing DXA request");
                throw;
            }
        }

        private bool ShouldSkipMiddleware(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            // Check against configured paths
            foreach (var ignoredPath in _ignoredPaths)
            {
                if (path.StartsWith(ignoredPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsHealthCheckPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith("/system/health", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<Localization> ResolveLocalizationAsync(HttpContext context,
            ILocalizationResolver localizationResolver, WebRequestContext webRequestContext)
        {
            try
            {
                return webRequestContext.Localization ??
                       localizationResolver.ResolveLocalization(new Uri(context.Request.GetDisplayUrl()));
            }
            catch (DxaUnknownLocalizationException ex)
            {
                var unknownLocalizationHandler = SiteConfiguration.UnknownLocalizationHandler;
                if (unknownLocalizationHandler != null)
                {
                    var localization = unknownLocalizationHandler.HandleUnknownLocalization(ex, context.Request, context.Response);
                    if (localization != null)
                    {
                        return localization;
                    }
                }

                await SendNotFoundResponse(ex.Message, context.Response);
                return null;
            }
            catch (DxaItemNotFoundException ex)
            {
                _logger.LogError(ex, "Localization resolution failed");
                await SendNotFoundResponse(ex.Message, context.Response);
                return null;
            }
        }

        private bool IsStaticContentRequest(HttpContext context, Localization localization)
        {
            var path = context.Request.Path.Value;
            var staticsRootUrl = localization.BinaryCacheFolder.Replace("\\", "/");

            if (path.StartsWith($"/{staticsRootUrl}/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(staticsRootUrl.Length + 1);
            }

            return localization.IsStaticContentUrl(path);
        }

        private async Task HandleStaticContentAsync(HttpContext context, Localization localization, IContentProvider contentProvider)
        {
            var path = context.Request.Path.Value;
            var staticsRootUrl = localization.BinaryCacheFolder.Replace("\\", "/");

            if (path.StartsWith($"/{staticsRootUrl}/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(staticsRootUrl.Length + 1);
            }

            // Prevent direct access to BinaryData folder
            if (path.StartsWith($"/{SiteConfiguration.StaticsFolder}/", StringComparison.OrdinalIgnoreCase))
            {
                await SendNotFoundResponse(
                    $"Attempt to directly access the static content cache through URL '{path}'",
                    context.Response);
                return;
            }

            try
            {
                using (var staticContentItem = contentProvider.GetStaticContentItem(path, localization))
                {
                    // Handle caching headers - this may set Status304NotModified
                    await SetCacheHeaders(context.Response, staticContentItem, localization);

                    // If we set 304, return immediately without writing content
                    if (context.Response.StatusCode == StatusCodes.Status304NotModified)
                    {
                        return;
                    }

                    // Set content type
                    context.Response.ContentType = staticContentItem.ContentType ?? "application/octet-stream";

                    // Write content to response
                    await using (var contentStream = staticContentItem.GetContentStream())
                    {
                        await contentStream.CopyToAsync(context.Response.Body);
                    }
                }
            }
            catch (DxaItemNotFoundException ex)
            {
                await SendNotFoundResponse(ex.Message, context.Response);
            }
        }

        private Task SetCacheHeaders(HttpResponse response, StaticContentItem staticContentItem, Localization localization)
        {
            var lastModified = staticContentItem.LastModified;
            var isVersionedUrl = response.HttpContext.Items.ContainsKey("IsVersionedUrl");

            if (response.HttpContext.Request.Headers.TryGetValue("If-Modified-Since", out var ifModifiedSinceHeader) &&
                DateTime.TryParse(ifModifiedSinceHeader, out var ifModifiedSince) &&
                lastModified <= ifModifiedSince.AddSeconds(1))
            {
                _logger.LogDebug("Static content item last modified at {LastModified} => Sending HTTP 304 (Not Modified)", lastModified);
                response.StatusCode = StatusCodes.Status304NotModified;
                return Task.CompletedTask;
            }

            if (!localization.IsXpmEnabled)
            {
                var maxAge = isVersionedUrl ? TimeSpan.FromDays(7) : TimeSpan.FromHours(1);
                response.Headers.CacheControl = $"private, max-age={maxAge.TotalSeconds}";
                response.Headers.Expires = (DateTime.UtcNow + maxAge).ToString("R");
            }

            response.Headers.LastModified = lastModified.ToString("R");
            return Task.CompletedTask;
        }


        private Task ProcessVersionedUrls(HttpContext context, Localization localization)
        {
            var path = context.Request.Path.Value;
            var versionLessUrl = SiteConfiguration.RemoveVersionFromPath(path);

            if (path != versionLessUrl)
            {
                _logger.LogDebug("Rewriting versioned static content URL '{Url}' to '{VersionLessUrl}'", path, versionLessUrl);
                context.Request.Path = versionLessUrl;
                context.Items["IsVersionedUrl"] = true;
            }

            return Task.CompletedTask;
        }

        private static Task HandleHealthCheck(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("DXA Health Check OK.");
        }

        private async Task SendNotFoundResponse(string message, HttpResponse response)
        {
            _logger.LogWarning("{Message}. Sending HTTP 404 (Not Found) response.", message);
            response.StatusCode = StatusCodes.Status404NotFound;
            response.ContentType = "text/plain";
            await response.WriteAsync(message);
        }
    }
}