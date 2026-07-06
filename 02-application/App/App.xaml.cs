using System;
using System.Windows;
using System.IO;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using MochiV2.Core.Animation;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using MochiV2.UI.Overlay;

namespace MochiV2
{
    /// <summary>
    /// WPF application class. Creates overlay window on startup.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Root service provider for running application.</summary>
        public static IServiceProvider? Services { get; internal set; }

        /// <summary>Per-instance logger convenience accessor.</summary>
        public static Serilog.ILogger Logger => Log.Logger;

        private OverlayWindow? _overlay;
        private AnimationManager? _animManager;
        private System.Diagnostics.Stopwatch? _frameTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Services ??= BootstrapContainer();

            try
            {
                // Load asset manifest
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string assetsPath = Path.Combine(baseDir, "Assets");
                string manifestPath = Path.Combine(assetsPath, "manifest.json");
                string spriteBasePath = Path.Combine(assetsPath);

                Log.Information("WPF App.OnStartup base directory: {BaseDir}", baseDir);
                Log.Information("Manifest path: {ManifestPath}", manifestPath);
                Log.Information("Sprite base path: {SpriteBasePath}", spriteBasePath);

                if (!File.Exists(manifestPath))
                {
                    Log.Error("Manifest not found at {ManifestPath}", manifestPath);
                    MessageBox.Show($"Manifest not found at:\n{manifestPath}\n\nPlease ensure Assets/ folder is alongside MochiV2.exe",
                        "MochiV2 - Asset Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }

                // Load manifest
                var loader = new AssetManifestLoader();
                var manifest = loader.LoadAsync(manifestPath).GetAwaiter().GetResult();
                Log.Information("Manifest loaded: {SpriteCount} sprites, {SoundCount} sounds",
                    manifest.Sprites.Count, manifest.Sounds.Count);

                // Create event bus
                var eventBus = new EventBus();

                // Create animation manager
                _animManager = new AnimationManager(loader, eventBus);

                // Set initial state to Idle (uses IdleLeft which is holdFirstFrame)
                _animManager.TransitionTo(FSMState.Idle, manifest, spriteBasePath);
                Log.Information("Animation manager initialized with state: {State}", _animManager.ActiveState);

                // Create overlay window with animation manager
                _overlay = new OverlayWindow();
                _overlay.SetAnimationManager(_animManager);

                // Position window at bottom-center of screen
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                _overlay.Width = screenWidth;
                _overlay.Height = screenHeight;
                _overlay.Left = 0;
                _overlay.Top = 0;
                _overlay.Show();

                Log.Information("Overlay window shown. Screen: {W}x{H}", screenWidth, screenHeight);

                // Start frame timer for animation updates
                _frameTimer = System.Diagnostics.Stopwatch.StartNew();
                CompositionTarget.Rendering += OnRendering;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed during startup");
                MessageBox.Show($"Failed to start MochiV2:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "MochiV2 - Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_animManager == null || _frameTimer == null) return;

            double dt = _frameTimer.Elapsed.TotalMilliseconds;
            _frameTimer.Restart();
            _animManager.Update(dt);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            Log.Information("WPF App.OnExit exit code {ExitCode}", e.ApplicationExitCode);
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