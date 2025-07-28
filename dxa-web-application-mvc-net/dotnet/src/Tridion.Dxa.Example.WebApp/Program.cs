using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NLog;
using NLog.Web;
using NLog.Extensions.Logging;
using Sdl.Web.Common.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;

namespace Tridion.Dxa.Example.WebApp
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            IHostBuilder hostBuilder = null;
            DateTime startTime = DateTime.UtcNow;
            bool terminateProcess = false;

            Logger logger = LogManager.Setup()
                //.LoadConfigurationFromFile() // We could use .LoadConfigurationFromAppSettings and move nlog.config stuff into appsettings.json
                .LoadConfigurationFromAppSettings()
                .GetCurrentClassLogger();

            logger.Info("ServiceStarting");

            try
            {
                SetWorkingDirectory();

                hostBuilder = CreateHostBuilder(args);
                using IHost host = hostBuilder.Build();

                // Initialize the DXA static logger here:
                ILoggerFactory loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
                Log.Logger = new DefaultLogger(loggerFactory);

                Log.Info("Static logger initialized");

                host.Run();
            }
            catch (Exception ex)
            {
                terminateProcess = true;
                string errorMessage = ex.Message;

                if (ex is SocketException socketException)
                {
                    //https://support.microsoft.com/en-us/help/3039044/error-10013-wsaeacces-is-returned-when-a-second-bind-to-a-excluded-por
                    if (socketException.ErrorCode == 10013)
                    {
                        string firstConfiguredUrl = (hostBuilder?.Properties?.Values.OfType<Startup>().FirstOrDefault(v => v != null))?.FirstConfiguredUrl;
                        errorMessage = string.Format("ServiceBindFailed: {0}", firstConfiguredUrl);
                    }
                }

                logger.Error(ex, "ServiceFailed " + errorMessage);

                if (IsHostedInIIS())
                {
                    // IIS will restart the App Pool immediately after the process exits with an error code.
                    // To prevent very frequent retries, we ensure we exit at least one minute after start time.
                    TimeSpan stopDelay = startTime.AddMinutes(1).Subtract(DateTime.UtcNow);
                    if (stopDelay > TimeSpan.Zero)
                    {
                        logger.Info($"ServiceStopDelay{0}, (int)stopDelay.TotalSeconds");
                        Thread.Sleep((int)stopDelay.TotalMilliseconds);
                    }
                }
            }
            finally
            {
                logger.Info("ServiceStopped");

                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                LogManager.Flush();
                LogManager.Shutdown();

                if (terminateProcess)
                {
                    // Interestingly, Environment.Exit(-1) does something else than returning -1 from Main. The former is needed to make IIS restart the App Pool.
                    Environment.Exit(-1);
                }
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            NLogAspNetCoreOptions nlogOptions = new()
            {
                RemoveLoggerFactoryFilter = false,
                ShutdownOnDispose = false // We shutdown NLog in Program.Main (see above)
            };

            return Host.CreateDefaultBuilder(args)
                .UseConsoleLifetime()
                .UseWindowsService(options => options.ServiceName = "TridionDxaWebApp")
                .UseSystemd()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    IHostEnvironment env = hostingContext.HostingEnvironment;

                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseConfiguration(configuration)
                        .ConfigureLogging((context, logging) =>
                        {
                            logging.ClearProviders();
                            logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                            logging.AddNLog(nlogOptions);

                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                logging.AddEventLog(opt =>
                                {
#pragma warning disable CA1416 // Validate platform compatibility                                    
                                    opt.LogName = "Tridion";
                                    opt.SourceName = "Tridion Security";
#pragma warning restore CA1416 // Validate platform compatibility
                                });
                            }
                        })
                        .UseNLog(nlogOptions)
                        .UseKestrel(ProcessKestrelConfig)
                        .UseIIS()
                        .CaptureStartupErrors(false)
                        .UseStartup<Startup>();
                });
        }

        private static void ProcessKestrelConfig(WebHostBuilderContext context, KestrelServerOptions options)
        {
            // By default, we don't include an HTTP Server header (but you could configure it to be true; see below).
            options.AddServerHeader = false;

            // For some reason, the KestrelServerOptions properties are not bound to configuration, unless we explicitly do that here.
            // See https://github.com/dotnet/aspnetcore/issues/4765
            IConfigurationSection kestrelSection = context.Configuration.GetSection("Kestrel");
            kestrelSection.Bind(options);

            string[] certificatePasswordPaths = {
                "EndPoints:HttpsInlineCertFile:Certificate:Password",
                "EndPoints:Https:Certificate:Password",
                "Certificates:Default:Password"
            };

            foreach (string certificatePasswordPath in certificatePasswordPaths)
            {
                string configKey = $"Kestrel:{certificatePasswordPath}";
                string certificatePassword = context.Configuration[configKey];
                if (string.IsNullOrWhiteSpace(certificatePassword))
                {
                    continue;
                }

                context.Configuration[configKey] = certificatePassword;
            }
        }

        /// <summary>
        ///     When running as a Windows service, the default working directory is the Windows System32 folder.
        ///     This method changes it to the directory of the executing assembly instead.
        /// </summary>
        private static void SetWorkingDirectory()
        {
            if (!WindowsServiceHelpers.IsWindowsService())
            {
                return;
            }

            string currentAssembly = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(currentAssembly))
            {
                return;
            }

            string workingDir = Path.GetDirectoryName(currentAssembly);
            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                Directory.SetCurrentDirectory(workingDir);
            }
        }

        private static bool IsHostedInIIS()
            => Process.GetCurrentProcess().ProcessName.Equals("w3wp", StringComparison.OrdinalIgnoreCase);
    }
}