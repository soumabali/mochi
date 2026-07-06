using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MochiV2
{
    /// <summary>
    /// Entry point for MochiV2. Handles single-instance enforcement,
    /// container bootstrap, Serilog configuration before
    /// handing off to WPF <see cref="App"/> class.
    /// </summary>
    public static class Program
    {
        private const string MutexName = @"Global\MochiV2_SingleInstance_3F7A2E";
        private static Mutex? _singleInstanceMutex;

        /// <summary>
        /// Main entry point. WPF projects not using auto-generated Main
        /// need a custom Main wired via <c>StartupObject</c>.
        /// </summary>
        [STAThread]
        public static int Main(string[] args)
        {
            //--- Single-instance guard --------------------------------------
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: MutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance running — exit silently.
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                return 0;
            }

            //--- Logging ----------------------------------------------------
            string logDir = ResolveLogDirectory();
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("App", "MochiV2")
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logDir, "mochi-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("MochiV2 starting up (v{Version})", "0.1.0");
            Log.Debug("Log directory: {LogDir}", logDir);

            try
            {
                //--- container ------------------------------------------
                var services = new ServiceCollection();
                ConfigureServices(services);
                IServiceProvider provider = services.BuildServiceProvider();

                Log.Information("DI container initialised with {ServiceCount} services", services.Count);

                //--- Hand off to WPF App -----------------------------------
                App.Services = provider;
                return RunWpfApp(provider, args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "MochiV2 terminated with unhandled exception");
                return 1;
            }
            finally
            {
                Log.Information("MochiV2 shutting down");
                Log.CloseAndFlush();
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
        }

        /// <summary>
        /// Registers services in container.
        /// </summary>
        internal static void ConfigureServices(IServiceCollection services)
        {
            // Populated by subsequent tasks.
        }

        /// <summary>
        /// Resolves per-user log directory under %APPDATA%\NekoCompanion\logs.
        /// Falls back to ~/.local/share/NekoCompanion/logs on non-Windows.
        /// </summary>
        internal static string ResolveLogDirectory()
        {
            string? appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData) || appData == "/")
            {
                appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? "/tmp",
                    ".local", "share");
            }

            return Path.Combine(appData, "NekoCompanion", "logs");
        }

        /// <summary>
        /// Launches WPF application.
        /// </summary>
        private static int RunWpfApp(IServiceProvider provider, string[] args)
        {
            App.Services = provider;
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}