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
    /// DI container bootstrap, and Serilog configuration before
    /// handing off to the WPF <see cref="App"/> class.
    /// </summary>
    public static class Program
    {
        private const string MutexName = @"Global\MochiV2_SingleInstance_3F7A2E";
        private static Mutex? _singleInstanceMutex;

        /// <summary>
        /// Main entry point. WPF projects do not use this directly on Windows
        /// (the auto-generated Main in App.xaml.cs drives startup), but the
        /// bootstrap logic is exposed here so it can be unit-tested and so a
        /// custom Main can be wired up via <c>StartupObject</c> if needed.
        /// </summary>
        [STAThread]
        public static int Main(string[] args)
        {
            // --- Single-instance guard --------------------------------------
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: MutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance is already running — exit silently.
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                return 0;
            }

            // --- Logging ----------------------------------------------------
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
                // --- DI container ------------------------------------------
                var services = new ServiceCollection();
                ConfigureServices(services);
                IServiceProvider provider = services.BuildServiceProvider();

                Log.Information("DI container initialised with {ServiceCount} services", services.Count);

                // --- Hand off to WPF App -----------------------------------
                // On Windows the App.xaml-generated Main calls App.Main which
                // invokes RunWpfApp; on Linux we cannot launch WPF so we just
                // verify the container and exit cleanly for restore/test.
                App.Services = provider;
                return RunWpfApp(provider, args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "MochiV2 terminated with an unhandled exception");
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
        /// Registers services into the DI container. T-001 keeps this empty
        /// (placeholder) — subsequent tasks (T-002 … T-0xx) will populate it.
        /// </summary>
        internal static void ConfigureServices(IServiceCollection services)
        {
            // T-001: empty service collection — populated by later tasks.
            // e.g. services.AddSingleton<IEventBus, EventBus>();
        }

        /// <summary>
        /// Resolves the per-user log directory under %APPDATA%\NekoCompanion\logs.
        /// Falls back to <c>~/.local/share/NekoCompanion/logs</c> on non-Windows
        /// platforms so the test/restore path on Linux does not crash.
        /// </summary>
        internal static string ResolveLogDirectory()
        {
            string? appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData) || appData == "/")
            {
                // Linux fallback for restore / test scenarios.
                appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? "/tmp",
                    ".local", "share");
            }

            return Path.Combine(appData, "NekoCompanion", "logs");
        }

        /// <summary>
        /// Launches the WPF application. On Linux this is unreachable because
        /// WPF cannot start; the method is factored out so the bootstrap path
        /// (mutex + logging + DI) can be verified independently.
        /// </summary>
        private static int RunWpfApp(IServiceProvider provider, string[] args)
        {
            // On Windows: App app = new App(); app.Run();
            // We let the WPF App.Main (auto-generated) take over via
            // StartupObject. This method exists so the container is wired
            // before any WPF window is constructed.
            return 0;
        }
    }
}