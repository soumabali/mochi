using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
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
    public partial class App : Application
    {
        public static IServiceProvider? Services { get; internal set; }
        public static Serilog.ILogger Logger => Log.Logger;

        // Services
        private OverlayWindow? _overlay;
        private MochiRenderer? _renderer;
        private AnimationManager? _animManager;
        private EventBus? _eventBus;
        private FSM? _fsm;
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

        // Frame timing
        private System.Diagnostics.Stopwatch? _frameTimer;
        private double _behaviorTimer;
        private double _needsTimer;
        private AssetManifest? _manifest;
        private string _assetsBasePath = "";
        private SaveData? _saveData;

        // Cat position + movement
        private double _catX, _catY;
        private double _catVelX, _catVelY;
        private float _displayScale = 2.5f; // 128px * 2.5 = 320px display
        private double _screenWidth, _screenHeight;
        private double _spriteDisplaySize;

        // Interaction state
        private bool _isDragging;
        private bool _wasClickThrough = true;
        private double _dragOffsetX, _dragOffsetY;
        private double _lastMouseX, _lastMouseY;
        private double _mouseVelX, _mouseVelY;
        private DateTime _lastMouseTime = DateTime.Now;
        private double _hoverTimer;
        private bool _isInteractMode;

        // Behavior
        private Random _rng = new Random();
        private double _nextBehaviorInterval = 5000;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Services ??= BootstrapContainer();

            try
            {
                // 1. Resolve services
                _eventBus = Services!.GetRequiredService<EventBus>();
                _fsm = Services.GetRequiredService<FSM>();
                _animManager = Services.GetRequiredService<AnimationManager>();
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
                _renderer = Services.GetRequiredService<MochiRenderer>();
                Log.Information("All services resolved");

                // 2. Load manifest
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _assetsBasePath = Path.Combine(baseDir, "Assets");
                string manifestPath = Path.Combine(_assetsBasePath, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    MessageBox.Show($"Manifest not found:\n{manifestPath}", "MochiV2", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }
                var loader = Services.GetRequiredService<AssetManifestLoader>();
                _manifest = loader.LoadAsync(manifestPath).GetAwaiter().GetResult();
                Log.Information("Manifest loaded: {Count} sprites", _manifest.Sprites.Count);

                // 3. Init animation
                _animManager.TransitionTo(FSMState.Idle, _manifest, _assetsBasePath);
                _renderer.AnimationManager = _animManager;

                // 4. Configure audio
                _audioManager.Configure(_manifest, _assetsBasePath);

                // 5. Load save
                _saveData = _saveManager.Load();
                if (_saveData != null && _saveManager.WelcomeBackNeeded)
                {
                    _audioManager.Play(FSMState.MeowLeft);
                }

                // 6. Init position
                _screenWidth = SystemParameters.PrimaryScreenWidth;
                _screenHeight = SystemParameters.PrimaryScreenHeight;
                _spriteDisplaySize = 128 * _displayScale;
                _catX = _screenWidth / 2 - _spriteDisplaySize / 2;
                _catY = _screenHeight - _spriteDisplaySize - 60;
                _catVelX = 0;
                _catVelY = 0;
                Log.Information("Cat at ({X:.0}, {Y:.0}) screen {W:.0}x{H:.0}", _catX, _catY, _screenWidth, _screenHeight);

                // 7. Start services
                _cursorPoller.Start();
                _keyRateHook.Start();
                _needsTicker.Update();
                Log.Information("Background services started");

                // 8. Create overlay
                _overlay = new OverlayWindow(_renderer);
                _overlay.SetAnimationManager(_animManager);
                _overlay.Width = _screenWidth;
                _overlay.Height = _screenHeight;
                _overlay.Left = 0;
                _overlay.Top = 0;
                // Wire mouse events for interaction
                _overlay.MouseLeftButtonDown += OnMouseLeftDown;
                _overlay.MouseMove += OnMouseMove;
                _overlay.MouseLeftButtonUp += OnMouseLeftUp;
                _overlay.MouseRightButtonDown += OnMouseRightDown;
                _overlay.Show();
                Log.Information("Overlay shown");

                // 9. Events
                _eventBus.Subscribe<AnimationFinishedEvent>(OnAnimationFinished);
                _eventBus.Subscribe<StateChangedEvent>(OnStateChanged);
                // Wire FSM.StateChanged → EventBus publish (FSM uses C# event, not EventBus)
                _fsm.StateChanged += (oldState, newState) =>
                {
                    _eventBus.Publish(new StateChangedEvent(oldState, newState));
                };

                // 10. Render loop
                _frameTimer = System.Diagnostics.Stopwatch.StartNew();
                CompositionTarget.Rendering += OnRendering;
                Log.Information("MochiV2 ready — render loop active");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Startup failed: {Msg}", ex.Message);
                MessageBox.Show($"Failed:\n{ex.Message}\n\n{ex.StackTrace}", "MochiV2", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        /// <summary>
        /// Main 60fps frame update. Orchestrates all subsystems.
        /// </summary>
        private void OnRendering(object? sender, EventArgs e)
        {
            if (_frameTimer == null) return;
            double dt = _frameTimer.Elapsed.TotalMilliseconds;
            _frameTimer.Restart();
            if (dt > 100) dt = 100; // clamp large gaps

            // 1. Animation update
            _animManager?.Update(dt);

            // 2. Movement — drive cat position based on FSM state
            UpdateMovement(dt);

            // 3. Particles
            _particles?.Update(dt / 1000.0);

            // 4. Needs (every 60s)
            _needsTimer += dt;
            if (_needsTimer >= 60000)
            {
                _needsTicker?.Update();
                _needsTimer = 0;
            }

            // 5. Mood
            _moodResolver?.Tick();

            // 6. Behavior planning (randomized interval when idle)
            _behaviorTimer += dt;
            if (_behaviorTimer >= _nextBehaviorInterval && _fsm?.CurrentState == FSMState.Idle)
            {
                PlanNextBehavior();
                _behaviorTimer = 0;
                _nextBehaviorInterval = 4000 + _rng.NextDouble() * 6000; // 4-10s
            }

            // 7. Night mode
            _nightMode?.CheckLocalTime();

            // 8. Cursor curiosity + typing
            _cursorCuriosity?.Tick();
            _typingRate?.Tick();

            // 9. Sleep
            _sleepService?.Update();

            // 10. Save
            _saveManager?.NotifyChanged();

            // 11. Interaction mode toggle (click-through based on mouse proximity)
            UpdateInteractionMode();

            // 12. Sync renderer
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
        /// Move cat based on current FSM state with smooth easing.
        /// </summary>
        private void UpdateMovement(double dt)
        {
            if (_fsm == null) return;

            double targetVelX = 0;
            double targetVelY = 0;
            double speed = 80; // pixels per second

            switch (_fsm.CurrentState)
            {
                case FSMState.WalkLeft:
                    targetVelX = -speed;
                    break;
                case FSMState.WalkRight:
                    targetVelX = speed;
                    break;
                case FSMState.RunVar1:
                case FSMState.RunVar2:
                    targetVelX = (_fsm.CurrentState == FSMState.RunVar1 ? -1 : 1) * speed * 1.8;
                    break;
                case FSMState.Drag:
                    // Cat follows cursor with elastic lag
                    targetVelX = (_lastMouseX - _catX - _spriteDisplaySize / 2) * 8;
                    targetVelY = (_lastMouseY - _catY - _spriteDisplaySize / 2) * 8;
                    break;
                default:
                    targetVelX = 0;
                    targetVelY = 0;
                    break;
            }

            // Smooth velocity lerp (easing)
            double lerp = Math.Min(1, dt / 200);
            _catVelX += (targetVelX - _catVelX) * lerp;
            _catVelY += (targetVelY - _catVelY) * lerp;

            // Apply velocity
            _catX += _catVelX * dt / 1000;
            _catY += _catVelY * dt / 1000;

            // Screen bounds — turn around at edges
            if (_catX < 0)
            {
                _catX = 0;
                _catVelX = 0;
                if (_fsm.CurrentState == FSMState.WalkLeft)
                    _fsm.TransitionTo(FSMState.WalkRight);
            }
            if (_catX > _screenWidth - _spriteDisplaySize)
            {
                _catX = _screenWidth - _spriteDisplaySize;
                _catVelX = 0;
                if (_fsm.CurrentState == FSMState.WalkRight)
                    _fsm.TransitionTo(FSMState.WalkLeft);
            }

            // Y clamp (cat stays near bottom unless dragging)
            if (_fsm.CurrentState != FSMState.Drag && _fsm.CurrentState != FSMState.Fall)
            {
                double groundY = _screenHeight - _spriteDisplaySize - 60;
                if (_catY > groundY) { _catY = groundY; _catVelY = 0; }
                if (_catY < 0) { _catY = 0; _catVelY = 0; }
            }
        }

        /// <summary>
        /// Plan next behavior when cat is idle.
        /// </summary>
        private void PlanNextBehavior()
        {
            if (_fsm == null) return;

            // Diverse behaviors: walk, blink, scratch, meow, jump, idle
            var behaviors = new (FSMState state, double weight)[]
            {
                (FSMState.WalkLeft, 2.0),
                (FSMState.WalkRight, 2.0),
                (FSMState.Blink, 3.0),
                (FSMState.ScratchLeft, 1.0),
                (FSMState.ScratchRight, 1.0),
                (FSMState.MeowLeft, 0.5),
                (FSMState.MeowRight, 0.5),
                (FSMState.JumpVar1, 0.3),
                (FSMState.JumpVar2, 0.3),
                (FSMState.WalkForward, 0.5),
                (FSMState.RunVar1, 0.2),
                (FSMState.RunVar2, 0.2),
            };

            double totalWeight = 0;
            foreach (var (_, w) in behaviors) totalWeight += w;
            double r = _rng.NextDouble() * totalWeight;
            FSMState chosen = FSMState.Idle;
            foreach (var (state, w) in behaviors)
            {
                r -= w;
                if (r <= 0) { chosen = state; break; }
            }

            if (chosen != FSMState.Idle)
            {
                try { _fsm.TransitionTo(chosen); } catch { }
                Log.Debug("Behavior: {State}", chosen);
            }
        }

        /// <summary>
        /// Toggle click-through based on mouse proximity to cat sprite.
        /// </summary>
        private void UpdateInteractionMode()
        {
            if (_overlay == null) return;

            // Get current mouse position
            try
            {
                var pt = _overlay.PointFromScreen(new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y));
                // Actually use Win32 GetCursorPos via SystemParameters
            }
            catch { }

            // Simple approach: check if mouse is near sprite rect
            double mouseScreenX = 0, mouseScreenY = 0;
            try
            {
                var pos = System.Windows.Forms.Cursor.Position;
                mouseScreenX = pos.X;
                mouseScreenY = pos.Y;
            }
            catch
            {
                // Fallback: skip interaction mode if Forms unavailable
                return;
            }

            // Sprite bounds in screen coords
            double spriteLeft = _catX;
            double spriteTop = _catY;
            double spriteRight = _catX + _spriteDisplaySize;
            double spriteBottom = _catY + _spriteDisplaySize;

            // Add hover margin (20px)
            bool isNear = mouseScreenX >= spriteLeft - 30 && mouseScreenX <= spriteRight + 30 &&
                          mouseScreenY >= spriteTop - 30 && mouseScreenY <= spriteBottom + 30;

            if (isNear && !_isInteractMode && !_isDragging)
            {
                // Enter interact mode — disable click-through
                _overlay.SetClickThrough(false);
                _isInteractMode = true;
            }
            else if (!isNear && _isInteractMode && !_isDragging)
            {
                // Exit interact mode — enable click-through
                _overlay.SetClickThrough(true);
                _isInteractMode = false;
            }
        }

        // ── Mouse event handlers ──────────────────────────────────────

        private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(_overlay);
            _lastMouseX = pos.X;
            _lastMouseY = pos.Y;
            _isDragging = true;
            _dragOffsetX = pos.X - _catX;
            _dragOffsetY = pos.Y - _catY;
            _overlay!.SetClickThrough(false);

            // Click on cat → meow
            if (_fsm != null)
            {
                try { _fsm.TransitionTo(FSMState.Angry); } catch { }
                _audioManager?.Play(FSMState.MeowLeft);
            }
            Log.Debug("Mouse down at ({X:.0}, {Y:.0})", pos.X, pos.Y);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(_overlay);
            var now = DateTime.Now;
            double dtMs = (now - _lastMouseTime).TotalMilliseconds;
            if (dtMs > 0)
            {
                _mouseVelX = (pos.X - _lastMouseX) / dtMs * 1000;
                _mouseVelY = (pos.Y - _lastMouseY) / dtMs * 1000;
            }
            _lastMouseX = pos.X;
            _lastMouseY = pos.Y;
            _lastMouseTime = now;

            if (_isDragging)
            {
                // Cat follows cursor during drag
                _catX = pos.X - _dragOffsetX;
                _catY = pos.Y - _dragOffsetY;
                if (_fsm?.CurrentState != FSMState.Drag)
                {
                    try { _fsm?.TransitionTo(FSMState.Drag); } catch { }
                }
            }
        }

        private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                // Release → fall with gravity
                try { _fsm?.TransitionTo(FSMState.Fall); } catch { }
                _audioManager?.Play(FSMState.Surprised);
                // Emit dust particles
                _particles?.EmitDust();
                Log.Debug("Mouse up — cat falls");
            }
        }

        private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
        {
            // Right-click → context menu
            var menu = new ContextMenu();

            var feedItem = new MenuItem { Header = "🐟 Feed Mochi" };
            feedItem.Click += (s, ev) => { _feedingService?.Feed(); _audioManager?.Play(FSMState.Eating); };

            var playItem = new MenuItem { Header = "🐾 Play" };
            playItem.Click += (s, ev) => { try { _fsm?.TransitionTo(FSMState.Playful); } catch { } };

            var sleepItem = new MenuItem { Header = "😴 Sleep/Wake" };
            sleepItem.Click += (s, ev) =>
            {
                if (_fsm?.CurrentState == FSMState.Sleeping)
                    _sleepService?.Wake();
                else
                    _sleepService?.Sleep();
            };

            var settingsItem = new MenuItem { Header = "⚙️ Settings" };
            settingsItem.Click += (s, ev) => { /* TODO: open settings window */ };

            var exitItem = new MenuItem { Header = "❌ Exit" };
            exitItem.Click += (s, ev) => { Shutdown(); };

            menu.Items.Add(feedItem);
            menu.Items.Add(playItem);
            menu.Items.Add(sleepItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(exitItem);

            var pos = e.GetPosition(_overlay);
            menu.IsOpen = true;
        }

        // ── Event handlers ─────────────────────────────────────────────

        private void OnAnimationFinished(AnimationFinishedEvent evt)
        {
            // Auto-return to Idle after playOnce states
            Log.Debug("Animation finished: {State}", evt.State);
        }

        private void OnStateChanged(StateChangedEvent evt)
        {
            Log.Debug("State: {Old} → {New}", evt.OldState, evt.NewState);
            if (_manifest != null)
            {
                _animManager?.TransitionTo(evt.NewState, _manifest, _assetsBasePath);
            }
            _audioManager?.Play(evt.NewState);
        }

        // ── Lifecycle ──────────────────────────────────────────────────

        protected override void OnExit(ExitEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            Log.Information("Shutting down");
            _saveManager?.Flush();
            _cursorPoller?.Stop();
            _keyRateHook?.Stop();
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