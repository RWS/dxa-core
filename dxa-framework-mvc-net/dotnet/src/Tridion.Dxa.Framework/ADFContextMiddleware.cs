using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdl.Web.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Tridion.Dxa.Framework.ADF;
using Tridion.Dxa.Framework.ADF.ClaimStore;
using Tridion.Dxa.Framework.ADF.ClaimStore.Cookie;
using Tridion.Dxa.Framework.ADF.Configuration;

namespace Tridion.Dxa.Framework
{
    public class ADFContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<DxaMiddleware> _logger;
        private AmbientDataConfig _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        //private List<string> _excludePaths;
        private bool _isAcceptingForwardedClaims;
        private string _adfCookiePrefix;
        private bool _cookieClaimDefaultValue;
        private string _cookieClaimName;
        //private Uri _cookieClaimNameURI;

        private CookieConfig _sessionCookieConfig;
        private CookieConfig _trackingCookieConfig;
        private CookieConfiguration _cookieConfiguration;
        private ClaimConfiguration _claimConfiguration;

        public ADFContextMiddleware(RequestDelegate next, ILogger<DxaMiddleware> logger, AmbientDataConfig config, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _next = next;
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task Invoke(HttpContext context)
        {
            _logger.Log(LogLevel.Trace, "ADFContextMiddleware Invoke");

            //var ambientDataContext = context.RequestServices.GetService<AmbientDataContext>();

            // Capture server headers
            //var headers = context.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString());

            // Filter headers based on the configuration
            //var allowedHeaderClaims = _config.ForwardedClaims
            //    .Where(claim => claim.Uri.StartsWith("taf:request:headers"))
            //    .Select(claim => claim.Uri);

            //var filteredHeaders = headers
            //    .Where(header => allowedHeaderClaims.Any(claim => header.Key.EndsWith(claim)))
            //    .ToDictionary(header => header.Key, header => header.Value);

            // Store the filtered server headers in the ClaimStoreService
            var claimStore = context.RequestServices.GetRequiredService<ClaimStoreDataService>();
            claimStore.UserAgent = context.Request.Headers["User-Agent"].ToString();


            // First of all, bail out from resource requests on excluded paths as fast as we can.
            //string requestPath = context.Request.Path;
            //foreach (string excludePath in _excludePaths)
            //{
            //    if (requestPath.StartsWith(excludePath))
            //    {
            //        //  return Task.CompletedTask;
            //        await Task.Yield(); // Or another suitable awaitable operation
            //    }
            //}

            //ForwarededClaims
            claimStore.ForwardedClaims = _config.ForwardedClaims.Claim.Select(x => x.Uri).ToList();

            _isAcceptingForwardedClaims = claimStore.ForwardedClaims != null && claimStore.ForwardedClaims.Count > 0;
            if (_isAcceptingForwardedClaims)
            {
                Log.Debug(string.Format("Cookie forwarding is enabled since we have {0} cookies configured for forwarding.", claimStore.ForwardedClaims.Count));
            }

            _claimConfiguration = new ClaimConfiguration();
            _cookieConfiguration = new CookieConfiguration();

            _claimConfiguration.Configure(_config);
            _cookieConfiguration.Configure(_config);

            // let's get the configuration where ADF cookie could be present
            CookieConfig adfCookieConfig = _cookieConfiguration.GetCookieConfiguration(CookieType.ADF);

            // If the cookie name is not configured, then we return default name.
            if (string.IsNullOrEmpty(adfCookieConfig.CookieName))
            {
                _adfCookiePrefix = _cookieConfiguration.GetDefaultCookieName(CookieType.ADF) + ".";
            }
            else
            {
                _adfCookiePrefix = adfCookieConfig.CookieName + ".";
            }
            Log.Debug("Configured ADF cookie prefix: " + _adfCookiePrefix);

            _cookieClaimDefaultValue = _cookieConfiguration.DefaultCookieClaimValue;
            _cookieClaimName = _claimConfiguration.CookieClaimName?.ToString();
            //_cookieClaimNameURI = new Uri(_cookieClaimName);

            _sessionCookieConfig = _cookieConfiguration.GetCookieConfiguration(CookieType.SESSION);
            _trackingCookieConfig = _cookieConfiguration.GetCookieConfiguration(CookieType.TRACKING);


            SetWebRequestClaims(context.Request, claimStore);

            //Cookie sessionIdCookie, trackingIdCookie;
            //var forwardedClaimsCookies = ExtractAmbientRuntimeCookies(context, out sessionIdCookie, out trackingIdCookie);


            //claimStoreService.SetServerHeaders(filteredHeaders);

            await _next(context);
        }


        private void SetWebRequestClaims(HttpRequest httpContext, ClaimStoreDataService claimStore)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException("request");
            }

            // Request URI
            claimStore.Put(new Uri(WebClaims.REQUEST_URI), httpContext.Path);

            // Request full URL
            string unescapedFullUrl = Uri.UnescapeDataString(httpContext.Path);
            claimStore.Put(new Uri(WebClaims.REQUEST_FULL_URL), unescapedFullUrl);

            // Convert headers to a case-insensitive dictionary
            var headers = httpContext.Headers.ToDictionary(
                pair => pair.Key.ToLowerInvariant(),
                pair => pair.Value.ToArray()
            );

            // Request headers
            claimStore.Put(new Uri(WebClaims.REQUEST_HEADERS), headers);

            // Convert cookies to a dictionary
            var cookies = httpContext.Cookies.ToDictionary(
                cookie => cookie.Key,
                cookie => cookie.Value
            );
            // Cookies
            claimStore.Put(new Uri(WebClaims.REQUEST_COOKIES), cookies);

            // Convert parameters (query string and form) to a dictionary
            var parameters = new Dictionary<string, string[]>();

            // Query string parameters
            foreach (var queryParameter in httpContext.Query)
            {
                parameters[queryParameter.Key] = queryParameter.Value.ToArray();
            }

            // Form parameters
            //if (httpContext.Form != null && httpContext.Form.Count > 0)
            //{
            //    foreach (var formParameter in httpContext.Form)
            //    {
            //        string key = formParameter.Key;

            //        // Ignore specific form fields
            //        if (key == "__VIEWSTATE" || key == "__EVENTVALIDATION")
            //            continue;

            //        // If the same key is used in both query string and form, add the value to the end of the array
            //        if (parameters.ContainsKey(key))
            //        {
            //            var currentValue = parameters[key];
            //            var valueArray = new string[currentValue.Length + 1];
            //            Array.Copy(currentValue, valueArray, currentValue.Length);
            //            valueArray[currentValue.Length] = formParameter.Value;
            //            parameters[key] = valueArray;
            //        }
            //        else
            //        {
            //            parameters[key] = formParameter.Value.ToArray();
            //        }
            //    }
            //}
            parameters.Add("CONTENT_TYPE", new string[] { httpContext.ContentType });
            parameters.Add("QUERY_STRING", new string[] { httpContext.QueryString.ToString() });

            // Request parameters
            claimStore.Put(new Uri(WebClaims.REQUEST_PARAMETERS), parameters);

            // Create a dictionary to store server variables
            var variables = new Dictionary<string, string>();

            // Access server-related information directly from HttpContext
            variables.Add("AUTH_TYPE", httpContext.Headers["AUTH_TYPE"]);
            variables.Add("DOCUMENT_ROOT", Environment.GetEnvironmentVariable("DOCUMENT_ROOT") ?? string.Empty);
            variables.Add("PATH_TRANSLATED", Environment.GetEnvironmentVariable("PATH_TRANSLATED") ?? string.Empty);
            variables.Add("REMOTE_ADDR", httpContext.HttpContext.Connection.RemoteIpAddress.ToString());
            variables.Add("REMOTE_HOST", Dns.GetHostAddresses(httpContext.HttpContext.Connection.RemoteIpAddress.ToString())[0].ToString());
            variables.Add("REMOTE_USER", httpContext.HttpContext.User.Identity.Name ?? string.Empty);
            variables.Add("REQUEST_METHOD", httpContext.HttpContext.Request.Method);
            variables.Add("SECURE", httpContext.IsHttps ? "true" : "false");
            variables.Add("SCRIPT_NAME", httpContext.Path);
            variables.Add("SERVER_NAME", httpContext.Host.Host);
            variables.Add("SERVER_PORT", httpContext.Host.Port.ToString());
            variables.Add("SERVER_PROTOCOL", httpContext.Protocol);


            // Server variables
            claimStore.Put(new Uri(WebClaims.SERVER_VARIABLES), variables);
        }

        //private List<ClaimsCookie> ExtractAmbientRuntimeCookies(HttpContext context, out Cookie sessionIdCookie, out Cookie trackingIdCookie)
        //{
        //    List<ClaimsCookie> forwardedClaimCookies = new List<ClaimsCookie>();

        //    sessionIdCookie = null;
        //    trackingIdCookie = null;

        //    string userHostAddress = context.Connection.RemoteIpAddress.ToString();
        //    string invalidSessionIdCookieMessageFormatString = "Received an invalid session ID cookie ({0}) " + (string.IsNullOrEmpty(userHostAddress) ? "from an undetermined IP address" : ("from IP address " + userHostAddress)) + ".";
        //    string invalidTrackingIdCookieMessageFormatString = "Received an invalid tracking ID cookie ({0}) " + (string.IsNullOrEmpty(userHostAddress) ? "from an undetermined IP address" : ("from IP address " + userHostAddress)) + ".";

        //    string sessionCookieName = _sessionCookieConfig.CookieName;
        //    string trackingCookieName = _trackingCookieConfig.CookieName;

        //    foreach (var cookieName in context.Request.Cookies.Keys)
        //    {
        //        var cookieValue = context.Request.Cookies[cookieName];

        //        if (sessionCookieName.Equals(cookieName))
        //        {
        //            if (CookieHelpers.IsCookieValid(cookieValue))
        //            {
        //                sessionIdCookie = new Cookie(cookieName, cookieValue);
        //            }
        //            else
        //            {
        //                _logger.LogWarning(String.Format(invalidSessionIdCookieMessageFormatString, cookieValue));
        //            }
        //        }
        //        else if (trackingCookieName.Equals(cookieName))
        //        {
        //            if (CookieHelpers.IsCookieValid(cookieValue))
        //            {
        //                trackingIdCookie = new Cookie(cookieName, cookieValue);
        //            }
        //            else
        //            {
        //                _logger.LogWarning(String.Format(invalidTrackingIdCookieMessageFormatString, cookieValue));
        //            }
        //        }
        //        else if (cookieName.StartsWith(_adfCookiePrefix))
        //        {
        //            forwardedClaimCookies.Add(new ClaimsCookie(cookieName, cookieValue));
        //        }
        //    }

        //    return forwardedClaimCookies;
        //}
    }
}
