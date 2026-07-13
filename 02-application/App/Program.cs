using System;
using System.IO;
using System.Threading;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MochiV2
{
    /// <summary>
    /// Entry point for MochiV2. Handles single-instance enforcement,
    /// DI container bootstrap, Serilog configuration.
    /// </summary>
    public static class Program
    {
        private const string MutexName = @"Global\MochiV2_SingleInstance_3F7A2E";
        private static Mutex? _singleInstanceMutex;

        [STAThread]
        public static int Main(string[] args)
        {
            //--- Single-instance guard --------------------------------------
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: MutexName, out bool createdNew);
            if (!createdNew)
            {
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

            Log.Information("MochiV2 starting up (v{Version})", "0.2.0");
            Log.Debug("Log directory: {LogDir}", logDir);

            try
            {
                //--- DI container: register ALL services ----------------------
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
        /// Registers ALL services in the DI container.
        /// Order matters: interfaces first, then services that depend on them.
        /// </summary>
        internal static void ConfigureServices(IServiceCollection services)
        {
            // --- Core infrastructure (no dependencies) ---------------------
            services.AddSingleton<MochiV2.Core.Events.EventBus>();
            services.AddSingleton<MochiV2.Core.Behavior.IRandom, MochiV2.Core.Behavior.StandardRandom>();
            services.AddSingleton<MochiV2.Core.Behavior.ITimeProvider, MochiV2.Core.Behavior.StopwatchTimeProvider>();
            services.AddSingleton<MochiV2.Core.Behavior.IWorkAreaProvider>(sp =>
            {
                // Default full-screen work area — will be refined at runtime
                return new MochiV2.Core.Behavior.WorkAreaRect(0, 0, 1920, 1080);
            });

            // --- Asset loading ---------------------------------------------
            services.AddSingleton<MochiV2.Core.Animation.AssetManifestLoader>();

            // --- FSM -------------------------------------------------------
            services.AddSingleton<MochiV2.Core.Behavior.FSM>(sp =>
            {
                var builder = new MochiV2.Core.Behavior.FSMBuilder();
                // Wire core transitions
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "walk_left", MochiV2.Core.Models.FSMState.WalkLeft);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "walk_right", MochiV2.Core.Models.FSMState.WalkRight);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "blink", MochiV2.Core.Models.FSMState.Blink);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "meow_left", MochiV2.Core.Models.FSMState.MeowLeft);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "meow_right", MochiV2.Core.Models.FSMState.MeowRight);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "scratch_left", MochiV2.Core.Models.FSMState.ScratchLeft);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "scratch_right", MochiV2.Core.Models.FSMState.ScratchRight);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "jump_var1", MochiV2.Core.Models.FSMState.JumpVar1);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "jump_var2", MochiV2.Core.Models.FSMState.JumpVar2);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "playful", MochiV2.Core.Models.FSMState.Playful);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "hungry_standard", MochiV2.Core.Models.FSMState.HungryStandard);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "hungry_critical", MochiV2.Core.Models.FSMState.HungryCritical);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "sleep", MochiV2.Core.Models.FSMState.Sleeping);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "run_var1", MochiV2.Core.Models.FSMState.RunVar1);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "run_var2", MochiV2.Core.Models.FSMState.RunVar2);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "surprised", MochiV2.Core.Models.FSMState.Surprised);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "eating", MochiV2.Core.Models.FSMState.Eating);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Idle, "walk_forward", MochiV2.Core.Models.FSMState.WalkForward);

                // Walk states return to Idle
                builder.AddTransition(MochiV2.Core.Models.FSMState.WalkLeft, "stop", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.WalkRight, "stop", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.WalkForward, "stop", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.RunVar1, "stop", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.RunVar2, "stop", MochiV2.Core.Models.FSMState.Idle);

                // PlayOnce states return to Idle after finishing
                builder.AddTransition(MochiV2.Core.Models.FSMState.JumpVar1, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.JumpVar2, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.MeowLeft, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.MeowRight, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.ScratchLeft, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.ScratchRight, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Blink, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Surprised, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Fall, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.WakeUp, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Playful, "stop", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Angry, "release", MochiV2.Core.Models.FSMState.Fall);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Eating, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.ClimbUp, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Stretching, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.Drinking, "finish", MochiV2.Core.Models.FSMState.Idle);
                builder.AddTransition(MochiV2.Core.Models.FSMState.HappyHop, "finish", MochiV2.Core.Models.FSMState.Idle);

                 //Sleep transitions
                builder.AddTransition(MochiV2.Core.Models.FSMState.Sleeping, "wake", MochiV2.Core.Models.FSMState.WakeUp);
                builder.AddTransition(MochiV2.Core.Models.FSMState.HungryStandard, "eat", MochiV2.Core.Models.FSMState.Eating);
                builder.AddTransition(MochiV2.Core.Models.FSMState.HungryCritical, "eat", MochiV2.Core.Models.FSMState.Eating);

                // Interrupts
                builder.AddInterrupt("drag", FSMState.Drag);
                builder.AddInterrupt("surprised", FSMState.Surprised);

                return builder.Build();
            });

            // --- Animation -------------------------------------------------
            services.AddSingleton<MochiV2.Core.Animation.AnimationManager>();

            // --- Movement + Physics ---------------------------------------
            services.AddSingleton<MochiV2.Core.Behavior.MovementService>(sp =>
            {
                var workArea = sp.GetRequiredService<MochiV2.Core.Behavior.IWorkAreaProvider>();
                var random = sp.GetRequiredService<MochiV2.Core.Behavior.IRandom>();
                return new MochiV2.Core.Behavior.MovementService(workArea, random, 128.0);
            });
            services.AddSingleton<MochiV2.Core.Behavior.MicroMotionService>();
            services.AddSingleton<MochiV2.Core.Behavior.BehaviorPlanner>();
            services.AddSingleton<MochiV2.Core.Behavior.InteractionHandler>();

            //--- Surface (Post-MVP Phase E) ---------------------------------
            services.AddSingleton<MochiV2.Core.Behavior.ISurfaceProvider, MochiV2.Infrastructure.Window.Win32SurfaceProvider>();
            services.AddSingleton<MochiV2.Core.Behavior.SurfaceClimber>();

            //--- Pomodoro (Post-MVP Phase F) --------------------------------
            services.AddSingleton<MochiV2.Core.Services.PomodoroService>();
            services.AddSingleton<MochiV2.Core.Services.SpeechBubbleService>();

            //--- G-1 Features (Post-MVP Phase G) ----------------------------
            services.AddSingleton<MochiV2.Core.Services.HydrationReminderService>();
            services.AddSingleton<MochiV2.Core.Services.DailyQuoteService>();
            services.AddSingleton<MochiV2.Core.Services.MoodCheckInService>();
            services.AddSingleton<MochiV2.Core.Services.QuickLauncherService>();
            services.AddSingleton<MochiV2.Core.Services.HotkeyService>();

            //--- G-2 Features (Post-MVP Phase G) ----------------------------
            services.AddSingleton<MochiV2.Core.Services.ScreenEdgePeekService>();
            services.AddSingleton<MochiV2.Core.Services.PurrService>();
            services.AddSingleton<MochiV2.Core.Services.ItemDropService>();

            //--- G-3 Features (Post-MVP Phase G) ----------------------------
            services.AddSingleton<MochiV2.Core.Services.KeyboardReactionService>();
            services.AddSingleton<MochiV2.Core.Services.MiniBallGameService>();
            services.AddSingleton<MochiV2.Core.Services.WeatherService>();

            //--- I: Chat/LLM (Post-MVP Phase I) -----------------------------
            services.AddSingleton<MochiV2.Core.Services.ILLMProvider, MochiV2.Core.Services.OpenAICompatibleProvider>();
            services.AddSingleton<MochiV2.Core.Services.ChatService>();

            // --- Physics ---------------------------------------------------
            services.AddSingleton<MochiV2.Core.Physics.PhysicsEngine>(sp =>
            {
                var workArea = sp.GetRequiredService<MochiV2.Core.Behavior.IWorkAreaProvider>();
                var initialPos = new MochiV2.Core.Models.Position(100, 100, MochiV2.Core.Models.Facing.Right);
                return new MochiV2.Core.Physics.PhysicsEngine(workArea, 128.0, initialPos);
            });

            // --- Particles -------------------------------------------------
            services.AddSingleton<MochiV2.Core.Particles.ParticleSystem>();

            // --- Services (needs, mood, feeding, sleep) -------------------
            services.AddSingleton<MochiV2.Core.Services.NeedsTicker>();
            services.AddSingleton<MochiV2.Core.Services.MoodResolver>();
            services.AddSingleton<MochiV2.Core.Services.FeedingService>();
            services.AddSingleton<MochiV2.Core.Services.SleepService>();

            // --- Awareness services --------------------------------------
            services.AddSingleton<MochiV2.Core.Services.CursorCuriosityService>();
            services.AddSingleton<MochiV2.Core.Services.TypingRateService>();
            services.AddSingleton<MochiV2.Core.Services.NightModeService>();

            // --- Infrastructure ------------------------------------------
            services.AddSingleton<MochiV2.Infrastructure.Audio.AudioManager>(sp =>
            {
                // AudioManager will be configured with manifest after load
                return new MochiV2.Infrastructure.Audio.AudioManager();
            });
            services.AddSingleton<MochiV2.Infrastructure.Input.CursorPoller>();
            services.AddSingleton<MochiV2.Infrastructure.Input.KeyRateHook>();
            services.AddSingleton<MochiV2.Infrastructure.Storage.SaveManager>();

            // --- UI (Tray) ------------------------------------------------
            services.AddSingleton<MochiV2.UI.Tray.TrayIconController>();

            // --- Renderer --------------------------------------------------
            services.AddSingleton<MochiV2.UI.Overlay.MochiRenderer>();
        }

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

        private static int RunWpfApp(IServiceProvider provider, string[] args)
        {
            App.Services = provider;
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}