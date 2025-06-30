using System;
using Microsoft.Extensions.Logging;
using ILogger = Sdl.Web.Common.Interfaces.ILogger;

namespace Sdl.Web.Common.Logging
{
    /// <summary>
    /// DefaultLogger implementation.
    /// </summary>
    public class DefaultLogger : ILogger
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public DefaultLogger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger("DXA");
        }

        public void Trace(string messageFormat, params object[] parameters)
        {
            if (IsTracingEnabled)
                _logger.LogTrace(messageFormat, parameters);
        }

        public void Debug(string messageFormat, params object[] parameters)
        {
            if (IsDebugEnabled)
                _logger.LogDebug(messageFormat, parameters);
        }

        public void Info(string messageFormat, params object[] parameters) => _logger.LogInformation(messageFormat, parameters);

        public void Warn(string messageFormat, params object[] parameters) => _logger.LogWarning(messageFormat, parameters);

        public void Error(string messageFormat, params object[] parameters) => _logger.LogError(messageFormat, parameters);

        public void Error(Exception ex, string messageFormat, params object[] parameters) => _logger.LogError(ex, messageFormat, parameters);

        public void Error(Exception ex) => _logger.LogError(ex, ex.ToString());

        public bool IsTracingEnabled => _logger.IsEnabled(LogLevel.Trace);
        public bool IsDebugEnabled => _logger.IsEnabled(LogLevel.Debug);
    }
}
