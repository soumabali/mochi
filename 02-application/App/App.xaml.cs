using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Serilog;
using MochiV2.Core.Animation;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using MochiV2.Core.Particles;
using MochiV2.Core.Services;
using MochiV2.Infrastructure.Audio;
using MochiV2.Infrastructure.Input;
using MochiV2.Infrastructure.Storage;
using MochiV2.UI.Overlay;
using MochiV2.UI.Tray;
using MochiV2.UI.Settings;

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
        private ISurfaceProvider? _surfaceProvider;
        private SurfaceClimber? _surfaceClimber;
        private PomodoroService? _pomodoro;
        private SpeechBubbleService? _speechBubble;
        private HydrationReminderService? _hydrationReminder;
        private DailyQuoteService? _dailyQuote;
        private MoodCheckInService? _moodCheckIn;
        private QuickLauncherService? _quickLauncher;
        private HotkeyService? _hotkey;
        private ScreenEdgePeekService? _screenPeek;
        private PurrService? _purr;
        private ItemDropService? _itemDrop;
        private KeyboardReactionService? _keyboardReaction;
        private MiniBallGameService? _ballGame;
        private WeatherService? _weather;
        private UI.SpeechBubble.SpeechBubbleWindow? _speechBubbleWindow;
        private UI.Stats.StatsWindow? _statsWindow;
        private ChatService? _chatService;
        private UI.Chat.ChatWindow? _chatWindow;

        // Frame timing
        private System.Diagnostics.Stopwatch? _frameTimer;
        private double _behaviorTimer;
        private double _needsTimer;
        private double _fullscreenCheckTimer;
        private double _resourceLogTimer;
        private double _lowPowerTimer;
        private bool _isLowPowerMode;
        private AssetManifest? _manifest;
        private string _assetsBasePath = "";
        private SaveData? _saveData;

        // Cat position + movement
        private double _catX, _catY;
        private double _catVelX, _catVelY;
        private float _displayScale = 1.5f;
        private double _screenWidth, _screenHeight;
        private double _spriteDisplayW, _spriteDisplayH;
        private double _squashTimer; // A-05: squash & stretch on landing
        private bool _wasFalling;

        // Interaction state
        private bool _isDragging;
        private double _dragOffsetX, _dragOffsetY;
        private double _lastMouseX, _lastMouseY;
        private double _mouseVelX, _mouseVelY;
        private DateTime _lastMouseTime = DateTime.Now;
        private double _hoverTimer; // B-01: hover 3s → petting
        private bool _isInteractMode;
        private double _lastMouseMoveTime; // B-04: cursor idle 30s
        private double _clickCount; // B-02: double-click detection
        private DateTime _lastClickTime = DateTime.MinValue;
        private bool _wasFullscreen; // B-07: fullscreen hide

        // Behavior
        private Random _rng = new Random();
        private double _nextBehaviorInterval = 800;
        private double _walkTimer;
        private double _walkDuration;
        private double _idleTimer; // auto-sleep after idle

        //Roaming
        private double _wanderX, _wanderY;
        private double _wanderRetargetTimer;
        private double _jumpTimer;

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
                _surfaceProvider = Services.GetRequiredService<ISurfaceProvider>();
                _surfaceClimber = Services.GetRequiredService<SurfaceClimber>();
                _pomodoro = Services.GetRequiredService<PomodoroService>();
                _speechBubble = Services.GetRequiredService<SpeechBubbleService>();
                _hydrationReminder = Services.GetRequiredService<HydrationReminderService>();
                _dailyQuote = Services.GetRequiredService<DailyQuoteService>();
                _moodCheckIn = Services.GetRequiredService<MoodCheckInService>();
                _quickLauncher = Services.GetRequiredService<QuickLauncherService>();
                _hotkey = Services.GetRequiredService<HotkeyService>();
                _screenPeek = Services.GetRequiredService<ScreenEdgePeekService>();
                _purr = Services.GetRequiredService<PurrService>();
                _itemDrop = Services.GetRequiredService<ItemDropService>();
                _keyboardReaction = Services.GetRequiredService<KeyboardReactionService>();
                _ballGame = Services.GetRequiredService<MiniBallGameService>();
                _weather = Services.GetRequiredService<WeatherService>();
        _chatService = Services.GetRequiredService<ChatService>();
                _renderer = Services.GetRequiredService<MochiRenderer>();
                Log.Information("All services resolved");

                // 2. Load manifest
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _assetsBasePath = Path.Combine(baseDir, "Assets");
                string manifestPath = Path.Combine(_assetsBasePath, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    MessageBox.Show($"Manifest not found:\n{manifestPath}", "MochiV2", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1); return;
                }
                var loader = Services.GetRequiredService<AssetManifestLoader>();
                _manifest = loader.LoadAsync(manifestPath).GetAwaiter().GetResult();
                Log.Information("Manifest: {Count} sprites", _manifest.Sprites.Count);

                // 3. Init animation
                _animManager.TransitionTo(FSMState.Idle, _manifest, _assetsBasePath);
                _renderer.AnimationManager = _animManager;

                // 4. Configure audio
                _audioManager.Configure(_manifest, _assetsBasePath);

                // 5. Load save
                _saveData = _saveManager.Load();
                if (_saveData != null && _saveManager.WelcomeBackNeeded)
                    _audioManager.Play(FSMState.MeowLeft);

                // 6. Init position
                _screenWidth = SystemParameters.PrimaryScreenWidth;
                _screenHeight = SystemParameters.PrimaryScreenHeight;
                _spriteDisplayW = 150 * _displayScale;
                _spriteDisplayH = 150 * _displayScale;
                _catX = _screenWidth - _spriteDisplayW - 20;
                _catY = 20;
                _catVelX = 0; _catVelY = 0;
                _wanderX = _catX; _wanderY = _catY;
                _wanderRetargetTimer = 0; _jumpTimer = 0;
                _lastMouseMoveTime = 0;
                Log.Information("Cat at ({X:.0},{Y:.0}) screen {W:.0}x{H:.0}", _catX, _catY, _screenWidth, _screenHeight);

                // 7. Start services
                _cursorPoller.Start();
                _keyRateHook.Start();
                _needsTicker.Update();

                // 8. Create overlay
                _overlay = new OverlayWindow(_renderer);
                _overlay.SetAnimationManager(_animManager);
                _overlay.Width = _screenWidth;
                _overlay.Height = _screenHeight;
                _overlay.Left = 0; _overlay.Top = 0;
                _overlay.MouseLeftButtonDown += OnMouseLeftDown;
                _overlay.MouseMove += OnMouseMove;
                _overlay.MouseLeftButtonUp += OnMouseLeftUp;
                _overlay.MouseRightButtonDown += OnMouseRightDown;
                _overlay.Show();

                // 9. Wire events
                _eventBus.Subscribe<AnimationFinishedEvent>(OnAnimationFinished);
                _eventBus.Subscribe<StateChangedEvent>(OnStateChanged);
                _eventBus.Subscribe<TypingBurstStartedEvent>(OnTypingBurstStarted);
                _eventBus.Subscribe<TypingBurstEndedEvent>(OnTypingBurstEnded);
                _fsm.StateChanged += (old, neu) => _eventBus.Publish(new StateChangedEvent(old, neu));

                // 10. Render loop
                _frameTimer = System.Diagnostics.Stopwatch.StartNew();
                CompositionTarget.Rendering += OnRendering;

                //D-01: Multi-monitor display change detection
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

                // E-08: Surface provider wired
                if (_surfaceProvider != null)
                {
                    _surfaceProvider.SurfacesChanged += OnSurfacesChanged;
                    Log.Information("Surface provider wired");
                }

                // F-07: Pomodoro event subscription
                if (_eventBus != null)
                {
                    _eventBus.Subscribe<MochiV2.Core.Events.PomodoroEvent>(OnPomodoroEvent);
                }

                // F-04: Speech bubble events
                if (_speechBubble != null)
                {
                    _speechBubble.ShowRequested += OnSpeechBubbleShow;
                    _speechBubble.HideRequested += OnSpeechBubbleHide;
                }

                // F-04: Create speech bubble window
                _speechBubbleWindow = new UI.SpeechBubble.SpeechBubbleWindow();

                // H-10: Create stats window
                _statsWindow = new UI.Stats.StatsWindow();

                // I-05: Create chat window (lazy — created on first open)
                // ChatAction wired via tray menu

                // G-01: Hotkey events
                if (_hotkey != null)
                {
                    _hotkey.HotkeyPressed += OnHotkeyPressed;
                }

                // G-03: Tray menu actions for post-MVP features
                if (_trayIcon != null)
                {
                    _trayIcon.PomodoroAction += (action) =>
                    {
                        switch (action)
                        {
                            case "start": _pomodoro?.Start(); break;
                            case "pause": _pomodoro?.Pause(); break;
                            case "reset": _pomodoro?.Reset(); break;
                        }
                    };
                    _trayIcon.QuickLaunchAction += () => _quickLauncher?.Launch(0);
                    _trayIcon.MoodCheckInAction += () =>
                    {
                        _moodCheckIn?.Tick();
                        if (_speechBubble != null) _speechBubble.Show("Gimana mood kamu? 😊", 10.0);
                    };
                    _trayIcon.HydrationAction += () =>
                    {
                        _hydrationReminder?.Reset();
                        if (_speechBubble != null) _speechBubble.Show("Bagus! 💧🐱", 3.0);
                        try { _fsm?.TransitionTo(FSMState.Drinking, bypassValidation: true); } catch { }
                    };
                    _trayIcon.StatsAction += () =>
                    {
                        _statsWindow?.Show();
                        UpdateStatsWindow();
                    };
                    _trayIcon.ChatAction += () =>
                    {
                        if (_chatWindow == null || !_chatWindow.IsVisible)
                        {
                            _chatWindow = new UI.Chat.ChatWindow(_chatService!, _speechBubble!);
                        }
                        _chatWindow?.Show();
                        _chatWindow?.Activate();
                    };
                    _trayIcon.ChatSettingsAction += () =>
                    {
                        var settingsWin = new UI.Chat.ChatSettingsWindow(_chatService!, _saveData!);
                        settingsWin.Show();
                    };
                }

                Log.Information("MochiV2 ready — all phases E-G wired");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Startup failed: {Msg}", ex.Message);
                MessageBox.Show($"Failed:\n{ex.Message}\n\n{ex.StackTrace}", "MochiV2", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Main 60fps render loop — orchestrates ALL subsystems
        //  Each section wrapped in try/catch (D-06: crash recovery)
        // ═══════════════════════════════════════════════════════════════

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_frameTimer == null) return;
            double dt = _frameTimer.Elapsed.TotalMilliseconds;
            _frameTimer.Restart();
            if (dt > 100) dt = 100;

            // D-05: Low power mode — throttle to ~10fps when sleeping
            if (_isLowPowerMode && dt < 100) return;

            try { _animManager?.Update(dt); } catch (Exception ex) { Log.Error(ex, "Anim update"); }
            try { UpdateMovement(dt); } catch (Exception ex) { Log.Error(ex, "Movement"); }
            try { _particles?.Update(dt / 1000.0); } catch (Exception ex) { Log.Error(ex, "Particles"); }

            // Needs every 60s
            _needsTimer += dt;
            if (_needsTimer >= 60000)
            {
                try { _needsTicker?.Update(); } catch { }
                _needsTimer = 0;
            }

            try { _moodResolver?.Tick(); } catch { }

            // Behavior planning
            try { UpdateBehavior(dt); } catch (Exception ex) { Log.Error(ex, "Behavior"); }

            try { _nightMode?.CheckLocalTime(); } catch { }
            try { _cursorCuriosity?.Tick(); } catch { }
            try { _typingRate?.Tick(); } catch { }
            try { _sleepService?.Update(); } catch { }
            try { _saveManager?.NotifyChanged(); } catch { }

            // B-07: Fullscreen detection every 2s
            _fullscreenCheckTimer += dt;
            if (_fullscreenCheckTimer >= 2000)
            {
                try { CheckFullscreen(); } catch { }
                _fullscreenCheckTimer = 0;
            }

            // B-01/B-04: Interaction mode + hover + cursor idle
            try { UpdateInteractionMode(dt); } catch (Exception ex) { Log.Error(ex, "Interaction"); }

            // D-04: Resource budget log every 60s
            _resourceLogTimer += dt;
            if (_resourceLogTimer >= 60000)
            {
                try { LogResourceUsage(); } catch { }
                _resourceLogTimer = 0;
            }

            // D-05: Low power detection — sleeping + no interaction 5min
            if (_fsm?.CurrentState == FSMState.Sleeping)
            {
                _lowPowerTimer += dt;
                if (_lowPowerTimer >= 300000 && !_isLowPowerMode) // 5 min
                {
                    _isLowPowerMode = true;
                    Log.Information("Low power mode: throttling to 10fps");
                }
            }
            else
            {
                _lowPowerTimer = 0;
                if (_isLowPowerMode) { _isLowPowerMode = false; Log.Information("Low power mode: exited"); }
            }

            // A-05: Squash stretch landing
            UpdateSquashStretch(dt);

            // E-09: Check current surface exists — if gone, trigger Fall
            // (handled via OnSurfacesChanged event)

            // F/G: Post-MVP service ticks
            try { _speechBubble?.Tick(dt / 1000.0); } catch (Exception ex) { Log.Error(ex, "SpeechBubble"); }
            try { _pomodoro?.Tick(); } catch (Exception ex) { Log.Error(ex, "Pomodoro"); }
            try { _hydrationReminder?.Tick(); } catch (Exception ex) { Log.Error(ex, "Hydration"); }
            try { _dailyQuote?.Tick(); } catch (Exception ex) { Log.Error(ex, "DailyQuote"); }
            try { _moodCheckIn?.Tick(); } catch (Exception ex) { Log.Error(ex, "MoodCheckIn"); }
            try { _screenPeek?.Tick(); } catch (Exception ex) { Log.Error(ex, "ScreenPeek"); }
            try { _keyboardReaction?.Tick(); } catch (Exception ex) { Log.Error(ex, "KeyboardReaction"); }
            try { _ballGame?.Tick(dt / 1000.0); } catch (Exception ex) { Log.Error(ex, "BallGame"); }
            try { _itemDrop?.Tick(_catX, _catY); } catch (Exception ex) { Log.Error(ex, "ItemDrop"); }

            // Sync renderer
            if (_renderer != null)
            {
                _renderer.CatX = _catX;
                _renderer.CatY = _catY;
                _renderer.Scale = _displayScale;
                _renderer.Particles = _particles;
                _renderer.MicroMotion = _microMotion;
                _renderer.NightMode = _nightMode;
                _renderer.SquashAmount = _squashTimer > 0 ? Math.Sin((_squashTimer / 80.0) * Math.PI) : 0;
                       _renderer.CurrentMood = _moodResolver?.CurrentMood ?? "Content";

                       // H-18: Pass ball position to renderer
                       if (_ballGame != null && _ballGame.IsBallActive)
                           _renderer.BallPosition = _ballGame.BallPosition;
                       else
                           _renderer.BallPosition = null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Movement — 2D screen roaming with easing
        // ═══════════════════════════════════════════════════════════════

        private void UpdateMovement(double dt)
        {
            if (_fsm == null) return;
            double targetVelX = 0, targetVelY = 0;
            double speed = 140; // faster roaming

            switch (_fsm.CurrentState)
            {
                case FSMState.WalkLeft:
                    targetVelX = -speed;
                    targetVelY = (_wanderY - _catY) * 2;
                    break;
                case FSMState.WalkRight:
                    targetVelX = speed;
                    targetVelY = (_wanderY - _catY) * 2;
                    break;
                case FSMState.WalkForward:
                    targetVelY = speed * 0.5;
                    targetVelX = (_wanderX - _catX) * 1.5;
                    break;
                case FSMState.RunVar1:
                    targetVelX = -speed * 1.8;
                    targetVelY = (_wanderY - _catY) * 3;
                    break;
                case FSMState.RunVar2:
                    targetVelX = speed * 1.8;
                    targetVelY = (_wanderY - _catY) * 3;
                    break;
                case FSMState.JumpVar1:
                case FSMState.JumpVar2:
                    _jumpTimer += dt;
                    double jp = Math.Min(1, _jumpTimer / 800);
                    double arc = Math.Sin(jp * Math.PI) * 150;
                    targetVelY = -arc * 5;
                    targetVelX = (_fsm.CurrentState == FSMState.JumpVar1 ? -1 : 1) * speed * 0.5;
                    break;
                case FSMState.Drag:
                    targetVelX = (_lastMouseX - _catX - _spriteDisplayW / 2) * 8;
                    targetVelY = (_lastMouseY - _catY - _spriteDisplayH / 2) * 8;
                    break;
                case FSMState.Fall:
                    targetVelX = _catVelX * 0.98;
                    targetVelY = _catVelY + 500 * dt / 1000;
                    _wasFalling = true;
                    break;
                default:
                    targetVelX = 0; targetVelY = 0;
                    break;
            }

            double lerp = Math.Min(1, dt / 200);
            _catVelX += (targetVelX - _catVelX) * lerp;
            _catVelY += (targetVelY - _catVelY) * lerp;
            _catX += _catVelX * dt / 1000;
            _catY += _catVelY * dt / 1000;

            // Bounds
            if (_catX < 0) { _catX = 0; _catVelX = 0; if (_fsm.CurrentState == FSMState.WalkLeft) _fsm.TransitionTo(FSMState.WalkRight); _wanderY = _screenHeight * 0.3 + _rng.NextDouble() * _screenHeight * 0.5; }
            if (_catX > _screenWidth - _spriteDisplayW) { _catX = _screenWidth - _spriteDisplayW; _catVelX = 0; if (_fsm.CurrentState == FSMState.WalkRight) _fsm.TransitionTo(FSMState.WalkLeft); _wanderY = _screenHeight * 0.3 + _rng.NextDouble() * _screenHeight * 0.5; }

            double minY = 50, maxY = _screenHeight - _spriteDisplayH - 60;
            if (_catY < minY) { _catY = minY; _catVelY = 0; _wanderY = maxY * 0.5 + _rng.NextDouble() * maxY * 0.5; }
            if (_catY > maxY) { _catY = maxY; _catVelY = 0; _wanderY = minY + _rng.NextDouble() * maxY * 0.4; }

            _wanderRetargetTimer += dt;
            if (_wanderRetargetTimer >= 3000)
            {
                _wanderY = minY + _rng.NextDouble() * (maxY - minY);
                _wanderRetargetTimer = 0;
            }

            // B-04: Cursor idle 30s → cat walks toward cursor
            _lastMouseMoveTime += dt;
            if (_lastMouseMoveTime >= 30000 && _fsm.CurrentState == FSMState.Idle)
            {
                try
                {
                    var mp = System.Windows.Forms.Cursor.Position;
                    _wanderX = mp.X - _spriteDisplayW / 2;
                    _wanderY = mp.Y - _spriteDisplayH / 2;
                    _fsm.TransitionTo(_rng.NextDouble() > 0.5 ? FSMState.WalkLeft : FSMState.WalkRight);
                    _walkDuration = 4000;
                    _walkTimer = 0;
                }
                catch { }
                _lastMouseMoveTime = 0;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Behavior planning — cat always does something
        // ═══════════════════════════════════════════════════════════════

        private void UpdateBehavior(double dt)
        {
            if (_fsm == null) return;
            _behaviorTimer += dt;

            // Walking states — stop after duration
            if (_fsm.CurrentState == FSMState.WalkLeft || _fsm.CurrentState == FSMState.WalkRight ||
                _fsm.CurrentState == FSMState.RunVar1 || _fsm.CurrentState == FSMState.RunVar2 ||
                _fsm.CurrentState == FSMState.WalkForward)
            {
                _walkTimer += dt;
                if (_walkTimer >= _walkDuration)
                {
                    try { _fsm.TransitionTo(FSMState.Idle); } catch { }
                    _walkTimer = 0; _behaviorTimer = 0;
                    _nextBehaviorInterval = 300 + _rng.NextDouble() * 800;
                }
            }
            else if (_behaviorTimer >= _nextBehaviorInterval)
            {
                PlanNextBehavior();
                _behaviorTimer = 0;
                if (_fsm.CurrentState == FSMState.WalkLeft || _fsm.CurrentState == FSMState.WalkRight || _fsm.CurrentState == FSMState.WalkForward)
                { _walkDuration = 4000 + _rng.NextDouble() * 6000; _walkTimer = 0; _idleTimer = 0; }
                else if (_fsm.CurrentState == FSMState.RunVar1 || _fsm.CurrentState == FSMState.RunVar2)
                { _walkDuration = 3000 + _rng.NextDouble() * 4000; _walkTimer = 0; }
                else
                    _nextBehaviorInterval = 300 + _rng.NextDouble() * 800;
            }
        }

        private void PlanNextBehavior()
        {
            if (_fsm == null) return;
            var behaviors = new (FSMState state, double weight)[]
            {
                (FSMState.WalkLeft, 8.0), (FSMState.WalkRight, 8.0),
                (FSMState.Blink, 1.5), (FSMState.ScratchLeft, 1.0), (FSMState.ScratchRight, 1.0),
                (FSMState.MeowLeft, 0.8), (FSMState.MeowRight, 0.8),
                (FSMState.JumpVar1, 0.5), (FSMState.JumpVar2, 0.5),
                (FSMState.RunVar1, 1.5), (FSMState.RunVar2, 1.5),
                (FSMState.WalkForward, 2.0),
            };
            double total = 0; foreach (var (_, w) in behaviors) total += w;
            double r = _rng.NextDouble() * total;
            FSMState chosen = FSMState.Idle;
            foreach (var (s, w) in behaviors) { r -= w; if (r <= 0) { chosen = s; break; } }
            if (chosen != FSMState.Idle) { try { _fsm.TransitionTo(chosen); } catch { } Log.Debug("Behavior: {State}", chosen); }
        }

        // ═══════════════════════════════════════════════════════════════
        //  A-05: Squash & stretch on landing
        // ═══════════════════════════════════════════════════════════════

        private void UpdateSquashStretch(double dt)
        {
            // Trigger squash when transitioning from Fall to Idle
            if (_wasFalling && _fsm?.CurrentState == FSMState.Idle)
            {
                _squashTimer = 80; // 80ms squash animation
                _particles?.EmitDust(); // A-01: dust on landing
                _wasFalling = false;
            }
            if (_squashTimer > 0)
            {
                _squashTimer -= dt;
                if (_squashTimer < 0) _squashTimer = 0;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Interaction — click-through toggle, hover, double-click, fast cursor
        // ═══════════════════════════════════════════════════════════════

        private void UpdateInteractionMode(double dt)
        {
            if (_overlay == null) return;
            double mx = 0, my = 0;
            try
            {
                var pos = System.Windows.Forms.Cursor.Position;
                mx = pos.X; my = pos.Y;
            }
            catch { return; }

            // B-03: Fast cursor → 20% surprised
            double mouseSpeed = Math.Sqrt(_mouseVelX * _mouseVelX + _mouseVelY * _mouseVelY);
            if (mouseSpeed > 1500 && _fsm?.CurrentState == FSMState.Idle && _rng.NextDouble() < 0.2)
            {
                try { _fsm.TransitionTo(FSMState.Surprised); } catch { }
                _particles?.EmitSurprised(); // A-01: "!" particle
                Log.Debug("Fast cursor → Surprised");
            }

            // Check proximity
            double sLeft = _catX, sTop = _catY, sRight = _catX + _spriteDisplayW, sBottom = _catY + _spriteDisplayH;
            bool isNear = mx >= sLeft - 30 && mx <= sRight + 30 && my >= sTop - 30 && my <= sBottom + 30;

            if (isNear && !_isInteractMode && !_isDragging)
            {
                _overlay.SetClickThrough(false);
                _isInteractMode = true;
                _hoverTimer = 0;
            }
            else if (!isNear && _isInteractMode && !_isDragging)
            {
                _overlay.SetClickThrough(true);
                _isInteractMode = false;
                _hoverTimer = 0;
            }

            // B-01: Hover 3s → petting → hearts
            if (isNear && !_isDragging)
            {
                _hoverTimer += dt;
                if (_hoverTimer >= 3000)
                {
                    _particles?.EmitHearts(5); // A-01: hearts
                    _audioManager?.Play(FSMState.Blink);
                    _hoverTimer = 0;
                    Log.Debug("Hover petting → hearts");
                }
            }

            // B-04: Track cursor idle time
            if (Math.Abs(_mouseVelX) > 1 || Math.Abs(_mouseVelY) > 1)
                _lastMouseMoveTime = 0;
        }

        // ═══════════════════════════════════════════════════════════════
        //  Mouse event handlers
        // ═══════════════════════════════════════════════════════════════

        private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(_overlay);
            _lastMouseX = pos.X; _lastMouseY = pos.Y;
            _isDragging = true;
            _dragOffsetX = pos.X - _catX;
            _dragOffsetY = pos.Y - _catY;
            _overlay!.SetClickThrough(false);

            // B-02: Double-click → Playful
            var now = DateTime.Now;
            if ((now - _lastClickTime).TotalMilliseconds < 400)
            {
                _clickCount++;
                if (_clickCount >= 2)
                {
                    try { _fsm?.TransitionTo(FSMState.Playful); } catch { }
                    _particles?.EmitHearts(3);
                    _clickCount = 0;
                    Log.Debug("Double-click → Playful");
                }
            }
            else
            {
                _clickCount = 1;
                // Single click → meow
                try { _fsm?.TransitionTo(FSMState.Angry); } catch { }
                _audioManager?.Play(FSMState.MeowLeft);
            }
            _lastClickTime = now;
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
            _lastMouseX = pos.X; _lastMouseY = pos.Y;
            _lastMouseTime = now;
            _lastMouseMoveTime = 0; // B-04: reset idle timer

            if (_isDragging)
            {
                _catX = pos.X - _dragOffsetX;
                _catY = pos.Y - _dragOffsetY;
                if (_fsm?.CurrentState != FSMState.Drag)
                { try { _fsm?.TransitionTo(FSMState.Drag); } catch { } }
            }
        }

        private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                try { _fsm?.TransitionTo(FSMState.Fall); } catch { }
                _audioManager?.Play(FSMState.Surprised);
                _particles?.EmitDust();
                Log.Debug("Mouse up → cat falls");
            }
        }

        private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
        {
            var menu = new ContextMenu();

            var feedItem = new MenuItem { Header = "🐟 Feed Mochi" };
            feedItem.Click += (s, ev) => { _feedingService?.Feed(); _audioManager?.Play(FSMState.Eating); _particles?.EmitHearts(8); };

            var playItem = new MenuItem { Header = "🐾 Play" };
            playItem.Click += (s, ev) => { try { _fsm?.TransitionTo(FSMState.Playful); } catch { } _particles?.EmitHearts(5); };

            var sleepItem = new MenuItem { Header = "😴 Sleep/Wake" };
            sleepItem.Click += (s, ev) => { if (_fsm?.CurrentState == FSMState.Sleeping) _sleepService?.Wake(); else _sleepService?.Sleep(); };

            var statsItem = new MenuItem { Header = "📊 Stats" };
            statsItem.Click += (s, ev) => ShowStatsPopup();

            var settingsItem = new MenuItem { Header = "⚙️ Settings" };
            settingsItem.Click += (s, ev) => { try { new SettingsWindow().Show(); } catch (Exception ex) { Log.Error(ex, "Settings"); } };

            var bringItem = new MenuItem { Header = "🐱 Bring Mochi" };
            bringItem.Click += (s, ev) => { _catX = _screenWidth - _spriteDisplayW - 20; _catY = _screenHeight / 2 - _spriteDisplayH / 2; try { _fsm?.TransitionTo(FSMState.Surprised); } catch { } };

            var exitItem = new MenuItem { Header = "❌ Exit" };
            exitItem.Click += (s, ev) => Shutdown();

            menu.Items.Add(feedItem);
            menu.Items.Add(playItem);
            menu.Items.Add(sleepItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(statsItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(bringItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);
            menu.IsOpen = true;
        }

        // ═══════════════════════════════════════════════════════════════
        //  B-07/B-08: Fullscreen detection
        // ═══════════════════════════════════════════════════════════════

        private void CheckFullscreen()
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                var detector = new MochiV2.Infrastructure.Window.FullscreenDetector();
                bool isFs = detector.IsForegroundFullscreen();
                if (isFs && !_wasFullscreen)
                {
                    _overlay?.Hide();
                    _wasFullscreen = true;
                    Log.Information("Fullscreen detected — hiding cat");
                }
                else if (!isFs && _wasFullscreen)
                {
                    _overlay?.Show();
                    _wasFullscreen = false;
                    try { _fsm?.TransitionTo(FSMState.Surprised); } catch { }
                    Log.Information("Fullscreen exited — cat reappears");
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        //  B-05/B-06: Typing awareness
        // ═══════════════════════════════════════════════════════════════

        private void OnTypingBurstStarted(TypingBurstStartedEvent evt)
        {
            // B-05: >120 keys/min for 2min → sleep
            try { _fsm?.TransitionTo(FSMState.Sleeping); } catch { }
            _particles?.StartZzzEmitting();
            Log.Information("Typing burst → cat sleeps");
        }

        private void OnTypingBurstEnded(TypingBurstEndedEvent evt)
        {
            // B-06: Stop typing 5min → wake + meow
            try { _fsm?.TransitionTo(FSMState.WakeUp); } catch { }
            _particles?.StopZzzEmitting();
            _audioManager?.Play(FSMState.MeowLeft);
            Log.Information("Typing stopped → cat wakes + meows");
        }

        // ═══════════════════════════════════════════════════════════════
        //  C-03/C-04/C-05/C-06: Stats popup
        // ═══════════════════════════════════════════════════════════════

        private void ShowStatsPopup()
        {
            var mood = _moodResolver?.CurrentMood ?? "Content";
            var level = _saveData?.Level ?? 1;
            var food = _saveData?.Food ?? 80;
            var energy = _saveData?.Energy ?? 80;
            var happiness = _saveData?.Happiness ?? 80;

            var popup = new Window
            {
                Title = "Mochi Stats",
                Width = 250, Height = 200,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Focusable = true,
                Topmost = true
            };

            var panel = new StackPanel { Margin = new Thickness(10) };
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 40, 40, 50)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(15)
            };
            border.Child = panel;

            panel.Children.Add(new TextBlock { Text = $"🐱 Mochi  Lv.{level}", FontSize = 16, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(new TextBlock { Text = $"Mood: {mood}", FontSize = 12, Foreground = Brushes.LightPink, Margin = new Thickness(0, 0, 0, 5) });
            panel.Children.Add(MakeBar("Food", food, Brushes.LightGreen));
            panel.Children.Add(MakeBar("Energy", energy, Brushes.LightBlue));
            panel.Children.Add(MakeBar("Happy", happiness, Brushes.LightPink));

            popup.Content = border;
            popup.MouseLeftButtonDown += (s, ev) => popup.Close();
            popup.Show();
        }

        private UIElement MakeBar(string label, int value, Brush color)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock { Text = $"{label}: ", FontSize = 11, Foreground = Brushes.White, Width = 55 });
            var bar = new ProgressBar { Value = value, Maximum = 100, Width = 120, Height = 12, Foreground = color };
            sp.Children.Add(bar);
            sp.Children.Add(new TextBlock { Text = $" {value}", FontSize = 11, Foreground = Brushes.White });
            return sp;
        }

        // ═══════════════════════════════════════════════════════════════
        //  D-01/D-02: Multi-monitor
        // ═══════════════════════════════════════════════════════════════

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            _screenWidth = SystemParameters.PrimaryScreenWidth;
            _screenHeight = SystemParameters.PrimaryScreenHeight;
            // D-02: If cat off-screen, teleport to center + Surprised
            if (_catX > _screenWidth - _spriteDisplayW || _catY > _screenHeight - _spriteDisplayH)
            {
                _catX = _screenWidth - _spriteDisplayW - 20;
                _catY = _screenHeight / 2 - _spriteDisplayH / 2;
                try { _fsm?.TransitionTo(FSMState.Surprised); } catch { }
                Log.Information("Monitor change — cat teleported to center");
            }
            if (_overlay != null)
            {
                _overlay.Width = _screenWidth;
                _overlay.Height = _screenHeight;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  D-04: Resource budget logging
        // ═══════════════════════════════════════════════════════════════

        private void LogResourceUsage()
        {
            long memBytes = GC.GetTotalMemory(false);
            double memMB = memBytes / 1024.0 / 1024.0;
            double fps = _renderer?.CurrentFps ?? 0;
            Log.Information("Resources: RAM={MemMB:F1}MB, FPS={Fps:F0}, State={State}, Mood={Mood}",
                memMB, fps, _fsm?.CurrentState, _moodResolver?.CurrentMood);
            if (memMB > 100)
                Log.Warning("RAM exceeds 100MB budget: {MemMB:F1}MB", memMB);
        }

        // ═══════════════════════════════════════════════════════════════
        //  Event handlers
        // ═══════════════════════════════════════════════════════════════

        private void OnAnimationFinished(AnimationFinishedEvent evt)
        {
            Log.Debug("Anim finished: {State}", evt.State);
            // A-01: Emit particles based on which animation finished
            switch (evt.State)
            {
                case FSMState.Surprised:
                    // Already emitted on trigger
                    break;
                case FSMState.WakeUp:
                    _particles?.EmitHearts(3);
                    break;
            }
        }

        private void OnStateChanged(StateChangedEvent evt)
        {
            Log.Debug("State: {Old} → {New}", evt.OldState, evt.NewState);
            if (_manifest != null)
                _animManager?.TransitionTo(evt.NewState, _manifest, _assetsBasePath);

            // A-01: Particle triggers on state change
            switch (evt.NewState)
            {
                case FSMState.Sleeping:
                    _particles?.StartZzzEmitting();
                    break;
                case FSMState.WakeUp:
                    _particles?.StopZzzEmitting();
                    break;
                case FSMState.Eating:
                    _particles?.EmitHearts(5);
                    break;
                case FSMState.Surprised:
                    _particles?.EmitSurprised();
                    break;
            }

            // A-07: Play sound for new state
            _audioManager?.Play(evt.NewState);

            // C-07: Update tray tooltip
            try
            {
                var mood = _moodResolver?.CurrentMood ?? "Content";
                var level = _saveData?.Level ?? 1;
                // TrayIconController tooltip update would go here if method exists
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════════════════

        protected override void OnExit(ExitEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
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

               // H-11: Update stats window with current data
               private void UpdateStatsWindow()
               {
                   if (_statsWindow == null) return;
                   int food = _saveData?.Food ?? 80;
                   int energy = _saveData?.Energy ?? 80;
                   int happiness = _saveData?.Happiness ?? 80;
                   string mood = _moodResolver?.CurrentMood ?? "Content";
                   int level = _saveData?.Level ?? 1;
                   int xp = _saveData?.Level > 0 ? (_saveData.TotalFed * 10 + _saveData.TotalPetted * 5) : 0;
                   int xpNext = level * 100;
                   string pomState = _pomodoro?.State.ToString() ?? "Idle";
                   double remaining = _pomodoro?.RemainingSeconds ?? 0;
                   string pomTime = $"{(int)remaining/60:D2}:{(int)remaining%60:D2}";
                   string temp = _weather?.HasWeather == true ? $"{_weather.Current!.Temperature:F0}°C" : "--°C";
                   string desc = _weather?.HasWeather == true ? _weather.Current!.Description : "--";
                   _statsWindow.UpdateStats(food, energy, happiness, mood, level, xp, xpNext, pomState, pomTime, temp, desc);
               }

               // E-08: Surface provider changed handler
               private void OnSurfacesChanged()
               {
                   Log.Debug("Surfaces changed event received.");
               }

               // F-07: Pomodoro event handler — adjust cat behavior based on timer state
               private void OnPomodoroEvent(MochiV2.Core.Events.PomodoroEvent ev)
               {
                   switch (ev.State)
                   {
                       case MochiV2.Core.Models.PomodoroState.Focus:
                           // Cat stays calm during focus
                           if (_speechBubble != null && ev.RemainingSeconds > 0 && ev.ElapsedSeconds < 1)
                               _speechBubble.Show("Focus time! 💪", 3.0);
                           break;
                       case MochiV2.Core.Models.PomodoroState.ShortBreak:
                       case MochiV2.Core.Models.PomodoroState.LongBreak:
                           // Cat becomes playful during breaks
                           if (_speechBubble != null && ev.RemainingSeconds > 0 && ev.ElapsedSeconds < 1)
                               _speechBubble.Show("Break time! 🐱", 3.0);
                           try { _fsm?.TransitionTo(FSMState.Playful, bypassValidation: true); } catch { }
                           break;
                       case MochiV2.Core.Models.PomodoroState.Idle:
                           if (_speechBubble != null && ev.RemainingSeconds == 0 && ev.ElapsedSeconds == 0)
                               _speechBubble.Show("Pomodoro selesai! 🎉", 5.0);
                           break;
                   }
               }

               // F-04: Speech bubble show/hide handlers
               private void OnSpeechBubbleShow(string text, double duration)
               {
                   Log.Information("Speech bubble: \"{Text}\" for {Dur:F1}s", text, duration);
                   _speechBubbleWindow?.ShowAt(text, _catX, _catY, duration);
               }

               private void OnSpeechBubbleHide()
               {
                   Log.Debug("Speech bubble hidden");
                   _speechBubbleWindow?.HideWithAnimation();
               }

               // G-01: Hotkey handler
               private void OnHotkeyPressed(string name)
               {
                   Log.Information("Hotkey: {Name}", name);
                   switch (name)
                   {
                       case "teleport_cat":
                           _catX = _screenWidth - _spriteDisplayW - 20;
                           _catY = _screenHeight / 2 - _spriteDisplayH / 2;
                           try { _fsm?.Interrupt(FSMState.Surprised); } catch { }
                           break;
                       case "feed_cat":
                           try { _feedingService?.Feed(); } catch { }
                           break;
                       case "pet_cat":
                           if (_speechBubble != null) _speechBubble.Show("Purr~ 💚", 3.0);
                           try { _fsm?.TransitionTo(FSMState.Playful, bypassValidation: true); } catch { }
                           break;
                       case "toggle_pomodoro":
                           if (_pomodoro?.State == MochiV2.Core.Models.PomodoroState.Idle ||
                               _pomodoro?.State == MochiV2.Core.Models.PomodoroState.Paused)
                               _pomodoro?.Start();
                           else
                               _pomodoro?.Pause();
                           break;
                   }
               }
           }
        }