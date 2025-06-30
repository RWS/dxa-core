using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Example.WebApp.Middleware.Logging
{
    /// <summary>
    /// Log required details from context, configurations or injected services for diagnosis purpose  
    /// </summary>
    internal class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IWebHostEnvironment _environment;

        //private readonly ClientSettings _clientSettings;
        private readonly ForwardedHeadersOptions _forwardedHeadersOptions;

        public LoggingMiddleware(RequestDelegate next, IWebHostEnvironment environment, ILogger<LoggingMiddleware> logger,           
            IOptionsMonitor<ForwardedHeadersOptions> forwardedHeadersOptions)
        {
            _next = next;
            _logger = logger;
            _environment = environment;

            //_clientSettings = clientSettings.CurrentValue;
            _forwardedHeadersOptions = forwardedHeadersOptions.CurrentValue;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    StringBuilder logMessageBuilder = new StringBuilder();

                   // LogClientSettings(logMessageBuilder);
                    LogForwardedHeadersOptions(logMessageBuilder);
                    LogHttpContext(context, logMessageBuilder);

                    _logger.LogTrace(logMessageBuilder.ToString());
                }
            }
            finally
            {
                await _next(context);
            }
        }       

        private void LogForwardedHeadersOptions(StringBuilder messageBuilder)
        {
            messageBuilder.AppendLine($"{nameof(ForwardedHeadersOptions)}");
            messageBuilder.AppendLine($"----------------------------------------");
            messageBuilder.AppendLine($"{nameof(ForwardedHeadersOptions)}");
            messageBuilder.AppendLine($"{nameof(_forwardedHeadersOptions.ForwardedHeaders)} : {_forwardedHeadersOptions.ForwardedHeaders}");
            messageBuilder.AppendLine($"{nameof(_forwardedHeadersOptions.KnownProxies)} : {string.Join(",", _forwardedHeadersOptions.KnownProxies.Select(p => p.ToString()))}");
            messageBuilder.AppendLine($"{nameof(_forwardedHeadersOptions.KnownNetworks)} : {string.Join(",", _forwardedHeadersOptions.KnownNetworks.Select(n => n.Prefix + "/" + n.PrefixLength))}");
            messageBuilder.AppendLine($"----------------------------------------");
        }

        private void LogHttpContext(HttpContext context, StringBuilder messageBuilder)
        {
            //Output request's forwarded headers related information
            messageBuilder.AppendLine($"{nameof(HttpContext)}");
            messageBuilder.AppendLine($"----------------------------------------");
            messageBuilder.AppendLine($"{nameof(context.Request.Scheme)} : {context.Request.Scheme}");
            messageBuilder.AppendLine($"{nameof(context.Request.Host)} : {context.Request.Host}");
            messageBuilder.AppendLine($"{nameof(context.Connection.LocalIpAddress)} : {context.Connection.LocalIpAddress}");
            messageBuilder.AppendLine($"{nameof(context.Connection.LocalPort)} : {context.Connection.LocalPort}");
            messageBuilder.AppendLine($"{nameof(context.Connection.RemoteIpAddress)} : {context.Connection.RemoteIpAddress}");
            messageBuilder.AppendLine($"{nameof(context.Connection.RemotePort)} : {context.Connection.RemotePort}");
            //Output request headers (starting with an X)
            List<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> forwardedHeaders = context.Request.Headers.Where(h => h.Key.StartsWith("X", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in forwardedHeaders)
            {
                messageBuilder.AppendLine($"Request-Header {header.Key}: {header.Value}");
            }
            messageBuilder.AppendLine($"----------------------------------------");
        }
    }
}
