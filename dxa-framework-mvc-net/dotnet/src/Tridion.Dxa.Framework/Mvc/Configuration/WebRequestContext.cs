using System;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Sdl.Web.Mvc.Configuration
{
    /// <summary>
    /// Container for request level context data, wraps the HttpContext.Items dictionary, which is used for this purpose
    /// </summary>
    public class WebRequestContext
    {
        private const int MaxWidth = 1024;
        private const string PreviewSessionTokenHeader = "x-preview-session-token";
        private const string PreviewSessionTokenCookie = "preview-session-token";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public WebRequestContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            Current = this;
        }

        public static WebRequestContext Current { get; private set; }

        /// <summary>
        /// The current request localization
        /// </summary>
        public Localization Localization
        {
            get
            {
                return (Localization)GetFromContextStore("Localization");
            }
            set
            {
                AddToContextStore("Localization", value);
            }
        }

        /// <summary>
        /// The Tridion Context Engine
        /// </summary>
        public ContextEngine ContextEngine
        {
            get
            {
                ContextEngine result = (ContextEngine)GetFromContextStore("ContextEngine");
                if (result == null)
                {
                    try
                    {
                        result = new ContextEngine();
                        AddToContextStore("ContextEngine", result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        throw;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// The maximum width for media objects for this requests display width
        /// </summary>
        public int MaxMediaWidth
        {
            get
            {
                //Pixel Ratio can be non-integer value (if zoom is applied to browser) - so we use a min of 1, and otherwise round when calculating max width
                double pixelRatio = ContextEngine.GetClaims<DeviceClaims>().PixelRatio;
                int displayWidth = IsContextCookiePresent ? ContextEngine.GetClaims<BrowserClaims>().DisplayWidth : 1024;
                if (displayWidth == 0) displayWidth = MaxWidth;
                return (int?)GetFromContextStore("MaxMediaWidth") ?? (int)AddToContextStore("MaxMediaWidth", Convert.ToInt32(Math.Max(1.0, pixelRatio) * Math.Min(displayWidth, MaxWidth)));
            }
        }

        /// <summary>
        /// The size of display of the device which initiated this request
        /// </summary>
        public ScreenWidth ScreenWidth
        {
            get
            {
                object val = GetFromContextStore("ScreenWidth");
                return (ScreenWidth?)val ?? (ScreenWidth)AddToContextStore("ScreenWidth", CalculateScreenWidth());
            }
        }

        /// <summary>
        /// The current request URL
        /// </summary>
        public string RequestUrl => _httpContextAccessor.HttpContext.Request.GetDisplayUrl();

        /// <summary>
        /// String array of client-supported MIME accept types
        /// </summary>
        public string[] AcceptTypes => null;//HttpContext.Current.Request.AcceptTypes;

        /// <summary>
        /// Current Page Model
        /// </summary>
        public PageModel PageModel
        {
            get
            {
                return (PageModel)GetFromContextStore("PageModel");
            }
            set
            {
                AddToContextStore("PageModel", value);
            }
        }

        /// <summary>
        /// True if the request is for localhost domain
        /// </summary>
        public bool IsDeveloperMode => (bool?)GetFromContextStore("IsDeveloperMode") ?? (bool)AddToContextStore("IsDeveloperMode", GetIsDeveloperMode());

        private bool GetIsDeveloperMode()
        {
            try
            {
                return _httpContextAccessor.HttpContext.Request.Host.Host.ToLower() == "localhost";
            }
            catch (Exception)
            {
                //Do nothing
            }

            return false;
        }

        /// <summary>
        /// True if the request is from XPM (NOTE currently always true for staging as we cannot reliably distinguish XPM requests)
        /// </summary>
        [Obsolete("Use WebRequestContext.Current.IsSessionPreview or Localization.IsXpmEnabled")]
        public bool IsPreview
            => (bool?)GetFromContextStore("IsPreview") ?? (bool)AddToContextStore("IsPreview", Localization.IsXpmEnabled);

        /// <summary>
        /// True if the request is from XPM Session Preview
        /// </summary>
        public bool IsSessionPreview
        {
            get
            {
                /*
                var claimStore = AmbientDataContext.CurrentClaimStore;
                if (claimStore == null) return false;

                var headers = claimStore?.Get<Dictionary<string, string[]>>(new Uri(WebClaims.REQUEST_HEADERS));
                if (headers != null && headers.ContainsKey(PreviewSessionTokenHeader))
                {
                    return true;
                }

                var cookies = claimStore?.Get<Dictionary<string, string>>(new Uri(WebClaims.REQUEST_COOKIES));
                if (cookies != null && cookies.ContainsKey(PreviewSessionTokenCookie))
                {
                    return true;
                }*/
                return false;
            }
        }

        /// <summary>
        /// True if the request is an include page
        /// </summary>
        public bool IsInclude
            => (bool?)GetFromContextStore("IsInclude") ?? (bool)AddToContextStore("IsInclude", RequestUrl.Contains("system/include/"));

        /// <summary>
        /// Cache key salt used to "mix" in with keys used for caching to provie uniqueness per request.
        /// </summary>
        public long CacheKeySalt
        {
            get
            {
                return (long?)GetFromContextStore("CacheKeySalt") ?? 0;
            }
            set
            {
                AddToContextStore("CacheKeySalt", value);
            }
        }

        protected ScreenWidth CalculateScreenWidth()
        {
            int width = IsContextCookiePresent ? ContextEngine.GetClaims<BrowserClaims>().DisplayWidth : MaxWidth;
            // zero width is not valid and probably means the context engine was not correctly initialized so
            // again default to 1024
            if (width == 0) width = MaxWidth;
            if (width < SiteConfiguration.MediaHelper.SmallScreenBreakpoint)
            {
                return ScreenWidth.ExtraSmall;
            }
            if (width < SiteConfiguration.MediaHelper.MediumScreenBreakpoint)
            {
                return ScreenWidth.Small;
            }
            if (width < SiteConfiguration.MediaHelper.LargeScreenBreakpoint)
            {
                return ScreenWidth.Medium;
            }
            return ScreenWidth.Large;
        }

        protected Localization GetCurrentLocalization()
        {
            var localizationResolver = _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<ILocalizationResolver>();
            // should do something when this is null
            return localizationResolver.ResolveLocalization(new Uri(RequestUrl));
        }

        protected object GetFromContextStore(string key) => _httpContextAccessor.HttpContext.Items[key];

        protected object AddToContextStore(string key, object value)
        {
            if (_httpContextAccessor.HttpContext == null) return value;
            _httpContextAccessor.HttpContext.Items[key] = value;
            return value;
        }

        private bool IsContextCookiePresent => _httpContextAccessor.HttpContext?.Request.Cookies["context"] != null;
    }
}
