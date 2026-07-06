using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using MochiV2.Core.Animation;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using MochiV2.Core.Particles;
using MochiV2.Core.Physics;
using MochiV2.Core.Services;
using MochiV2.Infrastructure.Audio;
using MochiV2.Infrastructure.Input;
using MochiV2.Infrastructure.Storage;
using MochiV2.UI.Overlay;
using MochiV2.UI.Tray;

namespace MochiV2
{
    /// <summary>
    /// WPF application class. Full integration startup pipeline.
    /// Wires ALL services: EventBus, FSM, AnimationManager, MovementService,
    /// BehaviorPlanner, NeedsTicker, AudioManager, SaveManager, Tray, etc.
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider? Services { get; internal set; }
        public static Serilog.ILogger Logger => Log.Logger;

        private OverlayWindow? _overlay;
        private MochiRenderer? _renderer;
        private AnimationManager? _animManager;
        private EventBus? _eventBus;
        private FSM? _fsm;
        private MovementService? _movement;
        private MicroMotionService? _microMotion;
        private BehaviorPlanner? _planner;
        private ParticleSystem? _particles;
        private NeedsTicker? _needsTicker;
        private MoodResolver? _moodResolver;
        private AudioManager? _audioManager;
        private CursorPoller? _cursorPoller;
        private KeyRateHook? _keyRateHook;
        private SaveManager? _saveManager;
        private TrayIconController? _trayIcon;
        private NightModeService? _nightMode;
        private CursorCuriosityService? _cursorCuriosity;
        private TypingRateService? _typingRate;
        private FeedingService? _feedingService;
        private SleepService? _sleepService;
        private InteractionHandler? _interactionHandler;
        private PhysicsEngine? _physics;

        private System.Diagnostics.Stopwatch? _frameTimer;
        private double _behaviorTimer; // accumulates time for behavior planning
        private double _needsTimer; // accumulates for needs ticking
        private AssetManifest? _manifest;
        private string _assetsBasePath = "";
        private SaveData? _saveData;

        // Sprite position (logical pixels)
        private double _catX, _catY;
        private float _displayScale = 2.0f; // 128px native * 2 = 256px

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Services ??= BootstrapContainer();

            try
            {
                // ── 1. Resolve core services from DI ──────────────────────
                _eventBus = Services!.GetRequiredService<EventBus>();
                _fsm = Services.GetRequiredService<FSM>();
                _animManager = Services.GetRequiredService<AnimationManager>();
                _movement = Services.GetRequiredService<MovementService>();
                _microMotion = Services.GetRequiredService<MicroMotionService>();
                _planner = Services.GetRequiredService<BehaviorPlanner>();
                _particles = Services.GetRequiredService<ParticleSystem>();
                _needsTicker = Services.GetRequiredService<NeedsTicker>();
                _moodResolver = Services.GetRequiredService<MoodResolver>();
                _audioManager = Services.GetRequiredService<AudioManager>();
                _cursorPoller = Services.GetRequiredService<CursorPoller>();
                _keyRateHook = Services.GetRequiredService<KeyRateHook>();
                _saveManager = Services.GetRequiredService<SaveManager>();
                _trayIcon = Services.GetRequiredService<TrayIconController>();
                _nightMode = Services.GetRequiredService<NightModeService>();
                _cursorCuriosity = Services.GetRequiredService<CursorCuriosityService>();
                _typingRate = Services.GetRequiredService<TypingRateService>();
                _feedingService = Services.GetRequiredService<FeedingService>();
                _sleepService = Services.GetRequiredService<SleepService>();
                _interactionHandler = Services.GetRequiredService<InteractionHandler>();
                _physics = Services.GetRequiredService<PhysicsEngine>();
                _renderer = Services.GetRequiredService<MochiRenderer>();

                Log.Information("All services resolved from DI container");

                // ── 2. Load asset manifest ────────────────────────────────
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _assetsBasePath = Path.Combine(baseDir, "Assets");
                string manifestPath = Path.Combine(_assetsBasePath, "manifest.json");

                Log.Information("Manifest path: {ManifestPath}", manifestPath);

                if (!File.Exists(manifestPath))
                {
                    Log.Error("Manifest not found at {ManifestPath}", manifestPath);
                    MessageBox.Show($"Manifest not found:\n{manifestPath}\n\nEnsure Assets/ is alongside MochiV2.exe",
                        "MochiV2", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }

                var loader = Services.GetRequiredService<AssetManifestLoader>();
                _manifest = loader.LoadAsync(manifestPath).GetAwaiter().GetResult();
                Log.Information("Manifest loaded: {SpriteCount} sprites, {SoundCount} sounds",
                    _manifest.Sprites.Count, _manifest.Sounds.Count);

                // ── 3. Initialize AnimationManager ───────────────────────
                _animManager.TransitionTo(FSMState.Idle, _manifest, _assetsBasePath);
                _renderer.AnimationManager = _animManager;
                Log.Information("AnimationManager initialized → Idle state");

                // ── 4. Configure AudioManager ─────────────────────────────
                _audioManager.Configure(_manifest, _assetsBasePath);
                Log.Information("AudioManager configured");

                // ── 5. Load save data ──────────────────────────────────────
                _saveData = _saveManager.Load();
                if (_saveData != null)
                {
                    Log.Information("Save data loaded: Level={Level}", _saveData.Level);
                    if (_saveManager.WelcomeBackNeeded)
                    {
                        Log.Information("Welcome back! Playing meow");
                        _audioManager.Play(FSMState.MeowLeft);
                    }
                }

                // ── 6. Set initial cat position (bottom-center) ───────────
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                _catX = screenWidth / 2 - (128 * _displayScale) / 2;
                _catY = screenHeight - (128 * _displayScale) - 60; // 60px from bottom
                Log.Information("Cat position: ({X}, {Y}) screen {W}x{H}", _catX, _catY, screenWidth, screenHeight);

                // ── 7. Start background services ──────────────────────────
                _cursorPoller.Start();       // FR-9: mouse polling 30Hz
                _keyRateHook.Start();        // FR-22: keyboard rate hook
                _needsTicker.Update();      // FR-10: initial needs tick
                Log.Information("Background services started (cursor poller, key hook, needs ticker)");

                // ── 8. Create overlay window ──────────────────────────────
                _overlay = new OverlayWindow(_renderer);
                _overlay.SetAnimationManager(_animManager);
                _overlay.Width = screenWidth;
                _overlay.Height = screenHeight;
                _overlay.Left = 0;
                _overlay.Top = 0;
                _overlay.Show();
                Log.Information("Overlay window shown {W}x{H}", screenWidth, screenHeight);

                // ── 9. Wire event subscriptions ───────────────────────────
                // AnimationFinishedEvent → FSM transition + behavior planning
                _eventBus.Subscribe<AnimationFinishedEvent>(OnAnimationFinished);
                // StateChangedEvent → animation transition
                _eventBus.Subscribe<StateChangedEvent>(OnStateChanged);
                Log.Information("Event subscriptions wired");

                // ── 10. Start render loop ─────────────────────────────────
                _frameTimer = System.Diagnostics.Stopwatch.StartNew();
                CompositionTarget.Rendering += OnRendering;
                Log.Information("MochiV2 startup complete — render loop active");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed during startup: {Message}", ex.Message);
                MessageBox.Show($"Failed to start MochiV2:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "MochiV2 - Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        /// <summary>
        /// Main frame update — called 60fps via CompositionTarget.Rendering.
        /// Orchestrates ALL subsystems in correct order.
        /// </summary>
        private void OnRendering(object? sender, EventArgs e)
        {
            if (_frameTimer == null) return;
            double dt = _frameTimer.Elapsed.TotalMilliseconds;
            _frameTimer.Restart();

            // ── 1. Update animation (frame advancement) ──────────────
            _animManager?.Update(dt);

            // ── 2. Update movement (walk/idle/screen-edge) ────────────
            // MovementService drives position when walking
            // (For MVP, cat stays at initial position — movement integration
            //  requires sprite position sync with render, done in Phase 2)

            // ── 3. Update micro-motion (breathing, fidgets) ────────────
            // MicroMotionService provides scale offsets applied in renderer

            // ── 4. Update particles ────────────────────────────────────
            _particles?.Update(dt / 1000.0);

            // ── 5. Update needs ticker (food/energy/happiness decay) ───
            _needsTimer += dt;
            if (_needsTimer >= 60000) // every 60 seconds
            {
                _needsTicker.Update();
                _needsTimer = 0;
            }

            // ── 6. Update mood resolver ────────────────────────────────
            _moodResolver?.Tick();

            // ── 7. Behavior planning (every ~5-15 seconds) ─────────────
            _behaviorTimer += dt;
            if (_behaviorTimer >= 8000 && _fsm?.CurrentState == FSMState.Idle) // every 8s when idle
            {
                var mood = _moodResolver?.CurrentMood ?? "Content";
                var personality = 0.5; // default mid-point
                var nextState = _planner?.PlanNextAction(_fsm.CurrentState, mood, personality);
                if (nextState.HasValue && nextState.Value != FSMState.Idle)
                {
                    var trigger = nextState.Value.ToString();
                    if (_fsm.CanTransition(nextState.Value, trigger))
                    {
                        _fsm.Fire(trigger);
                    }
                    else
                    {
                        // Direct state set for states not in transition table
                        try { _fsm.TransitionTo(nextState.Value); } catch { }
                    }
                }
                _behaviorTimer = 0;
            }

            // ── 8. Update night mode ──────────────────────────────────
            _nightMode?.CheckLocalTime();

            // ── 9. Update cursor curiosity ─────────────────────────────
            _cursorCuriosity?.Tick();

            // ── 10. Update typing rate ─────────────────────────────────
            _typingRate?.Tick();

            // ── 11. Update sleep service ──────────────────────────────
            _sleepService?.Update();

            // ── 12. Update save manager (debounced) ────────────────────
            _saveManager?.NotifyChanged();

            // ── 13. Update renderer with current position ─────────────
            if (_renderer != null)
            {
                _renderer.CatX = _catX;
                _renderer.CatY = _catY;
                _renderer.Scale = _displayScale;
                _renderer.Particles = _particles;
                _renderer.MicroMotion = _microMotion;
            }
        }

        /// <summary>
        /// When animation finishes (playOnce states), return to Idle.
        /// </summary>
        private void OnAnimationFinished(AnimationFinishedEvent evt)
        {
            Log.Debug("Animation finished: {State}", evt.State);
            // Auto-return to Idle is already handled by AnimationManager.Update
        }

        /// <summary>
        /// When FSM state changes, transition animation + play sound.
        /// </summary>
        private void OnStateChanged(StateChangedEvent evt)
        {
            Log.Debug("State changed: {Old} → {New}", evt.OldState, evt.NewState);
            if (_manifest != null)
            {
                _animManager?.TransitionTo(evt.NewState, _manifest, _assetsBasePath);
            }
            // Play sound for new state
            _audioManager?.Play(evt.NewState);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            Log.Information("MochiV2 shutting down");

            // Flush save
            _saveManager?.Flush();

            // Stop background services
            _cursorPoller?.Stop();
            _keyRateHook?.Stop();

            // Dispose services
            _cursorPoller?.Dispose();
            _keyRateHook?.Dispose();
            _trayIcon?.Dispose();
            _needsTicker?.Dispose();
            _moodResolver?.Dispose();
            _feedingService?.Dispose();
            _sleepService?.Dispose();

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