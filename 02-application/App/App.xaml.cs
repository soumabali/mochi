using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MochiV2
{
    /// <summary>
    /// WPF application class. The static <see cref="Services"/> property is
    /// set by <see cref="Program.Main"/> before any WPF window is created so
    /// that XAML code-behind and views can resolve registered services.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Root DI service provider for the running application.</summary>
        public static IServiceProvider? Services { get; internal set; }

        /// <summary>
        /// Per-instance logger convenience accessor. Falls back to the global
        /// <c>Log.Logger</c> so early-stage code (pre-DI) can still log.
        /// </summary>
        public static Serilog.ILogger Logger => Log.Logger;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // If Program.Main did not run (e.g. auto-generated Main on Windows),
            // bootstrap the container here as a safety net.
            Services ??= BootstrapContainer();

            Log.Information("WPF App.OnStartup — base directory: {BaseDir}",
                AppDomain.CurrentDomain.BaseDirectory);

            // T-003 will create the overlay window here.
            // T-004 will load the asset manifest here.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("WPF App.OnExit — exit code {ExitCode}", e.ApplicationExitCode);
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private static IServiceProvider BootstrapContainer()
        {
            var services = new ServiceCollection();
            Program.ConfigureServices(services);
            return services.BuildServiceProvider();
        }
    }
}