using System;
using System.Windows;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Serilog;
using MochiV2.Core.Events;

namespace MochiV2.UI.Tray
{
    /// <summary>
    /// System tray icon + context menu for Mochi v2 (PRD §9.1).
    /// Uses <see cref="TaskbarIcon"/> from H.NotifyIcon.Wpf to show a
    /// notify icon in the Windows taskbar notification area.
    /// </summary>
    /// <remarks>
    /// Menu items:
    /// <list type="bullet">
    ///   <item>Feed      → publishes <see cref="CatFedEvent"/>(40)</item>
    ///   <item>Play      → publishes <see cref="CatPettedEvent"/></item>
    ///   <item>Sleep/Wake → toggles <see cref="SleepStartedEvent"/> / <see cref="SleepEndedEvent"/></item>
    ///   <item>Stats     → opens stats popup (placeholder)</item>
    ///   <item>Settings  → opens settings window (placeholder)</item>
    ///   <item>Bring Mochi → teleports Mochi to center of screen (placeholder)</item>
    ///   <item>Exit      → closes the application</item>
    /// </list>
    /// </remarks>
    public sealed class TrayIconController : IDisposable
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext<TrayIconController>();

        private readonly EventBus _bus;
        private readonly TaskbarIcon _taskbarIcon;
        private bool _isSleeping;
        private bool _disposed;

        /// <summary>Fired when pomodoro menu item clicked. Passes "start"/"pause"/"reset".</summary>
        public event Action<string>? PomodoroAction;

        /// <summary>Fired when quick launch menu item clicked.</summary>
        public event Action? QuickLaunchAction;

        /// <summary>Fired when mood check-in menu item clicked.</summary>
        public event Action? MoodCheckInAction;

        /// <summary>Fired when hydration acknowledgement clicked.</summary>
        public event Action? HydrationAction;

        /// <summary>
        /// Creates the tray icon controller.
        /// </summary>
        /// <param name="bus">EventBus used to publish care events.</param>
        public TrayIconController(EventBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _isSleeping = false;

            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = "Mochi 💚",
                IconSource = CreateDefaultIcon(),
                Visibility = Visibility.Visible,
            };
            _taskbarIcon.TrayLeftMouseUp += OnTrayLeftClick;

            BuildContextMenu();

            Logger.Information("TrayIconController initialised");
        }

        //─────────────────────── Context menu ───────────────────────

        /// <summary>
        /// Builds the tray context menu and assigns it to the taskbar icon.
        /// </summary>
        private void BuildContextMenu()
        {
            var menu = new System.Windows.Controls.ContextMenu();

            var feedItem = new System.Windows.Controls.MenuItem { Header = "Feed" };
            feedItem.Click += OnFeedClick;
            menu.Items.Add(feedItem);

            var playItem = new System.Windows.Controls.MenuItem { Header = "Play" };
            playItem.Click += OnPlayClick;
            menu.Items.Add(playItem);

            var sleepItem = new System.Windows.Controls.MenuItem { Header = "Sleep" };
            sleepItem.Click += OnSleepWakeClick;
            menu.Items.Add(sleepItem);
            // Keep reference via Tag so we can swap Sleep/Wake label.
            sleepItem.Tag = "SleepWake";

            menu.Items.Add(new System.Windows.Controls.Separator());

            var statsItem = new System.Windows.Controls.MenuItem { Header = "Stats" };
            statsItem.Click += OnStatsClick;
            menu.Items.Add(statsItem);

            var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
            settingsItem.Click += OnSettingsClick;
            menu.Items.Add(settingsItem);

            var bringItem = new System.Windows.Controls.MenuItem { Header = "Bring Mochi" };
            bringItem.Click += OnBringMochiClick;
            menu.Items.Add(bringItem);

            // Post-MVP: Pomodoro submenu
            menu.Items.Add(new System.Windows.Controls.Separator());

            var pomodoroItem = new System.Windows.Controls.MenuItem { Header = "🍅 Pomodoro" };
            var pomStart = new System.Windows.Controls.MenuItem { Header = "Start" };
            pomStart.Click += (s, e) => PomodoroAction?.Invoke("start");
            pomodoroItem.Items.Add(pomStart);
            var pomPause = new System.Windows.Controls.MenuItem { Header = "Pause" };
            pomPause.Click += (s, e) => PomodoroAction?.Invoke("pause");
            pomodoroItem.Items.Add(pomPause);
            var pomReset = new System.Windows.Controls.MenuItem { Header = "Reset" };
            pomReset.Click += (s, e) => PomodoroAction?.Invoke("reset");
            pomodoroItem.Items.Add(pomReset);
            menu.Items.Add(pomodoroItem);

            // Post-MVP: Quick Launch submenu
            var launchItem = new System.Windows.Controls.MenuItem { Header = "🚀 Quick Launch" };
            launchItem.Click += (s, e) => QuickLaunchAction?.Invoke();
            menu.Items.Add(launchItem);

            // Post-MVP: Mood Check-In
            var moodItem = new System.Windows.Controls.MenuItem { Header = "😊 How are you?" };
            moodItem.Click += (s, e) => MoodCheckInAction?.Invoke();
            menu.Items.Add(moodItem);

            // Post-MVP: Hydration reminder
            var hydrateItem = new System.Windows.Controls.MenuItem { Header = "💧 I drank water!" };
            hydrateItem.Click += (s, e) => HydrationAction?.Invoke();
            menu.Items.Add(hydrateItem);

            menu.Items.Add(new System.Windows.Controls.Separator());

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += OnExitClick;
            menu.Items.Add(exitItem);

            _taskbarIcon.ContextMenu = menu;
        }

        //─────────────────────── Menu handlers ──────────────────────

        private void OnFeedClick(object sender, RoutedEventArgs e)
        {
            Logger.Information("Tray: Feed clicked — publishing CatFedEvent(40)");
            _bus.Publish(new CatFedEvent(40));
        }

        private void OnPlayClick(object sender, RoutedEventArgs e)
        {
            Logger.Information("Tray: Play clicked — publishing CatPettedEvent");
            _bus.Publish(new CatPettedEvent());
        }

        private void OnSleepWakeClick(object sender, RoutedEventArgs e)
        {
            if (_isSleeping)
            {
                Logger.Information("Tray: Wake clicked — publishing SleepEndedEvent");
                _bus.Publish(new SleepEndedEvent());
                _isSleeping = false;
            }
            else
            {
                Logger.Information("Tray: Sleep clicked — publishing SleepStartedEvent");
                _bus.Publish(new SleepStartedEvent());
                _isSleeping = true;
            }

            // Update menu label
            if (sender is System.Windows.Controls.MenuItem item && item.Tag?.ToString() == "SleepWake")
            {
                item.Header = _isSleeping ? "Wake" : "Sleep";
            }
        }

        private void OnStatsClick(object sender, RoutedEventArgs e)
        {
            Logger.Information("Tray: Stats clicked");
            OpenStatsPopup();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Logger.Information("Tray: Settings clicked");
            OpenSettingsWindow();
        }

        private void OnBringMochiClick(object sender, RoutedEventArgs e)
        {
            Logger.Information("Tray: Bring Mochi clicked");
            BringMochiToCenter();
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            Logger.Information("Tray: Exit clicked — shutting down application");
            try
            {
                _taskbarIcon.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error disposing taskbar icon during exit");
            }

            Application.Current?.Shutdown();
        }

        private void OnTrayLeftClick(object sender, RoutedEventArgs e)
        {
            // Left-click brings Mochi to center (PRD §9.1 convenience).
            BringMochiToCenter();
        }

        //─────────────────────── Placeholders ───────────────────────

        /// <summary>
        /// Opens the stats popup. Placeholder — to be wired by the
        /// stats UI task.
        /// </summary>
        private void OpenStatsPopup()
        {
            // TODO: T-0xx StatsPopup — show needs/mood/uptime overlay.
            Logger.Debug("OpenStatsPopup placeholder invoked");
        }

        /// <summary>
        /// Opens the settings window. Placeholder — to be wired by the
        /// settings UI task.
        /// </summary>
        private void OpenSettingsWindow()
        {
            // TODO: T-0xx SettingsWindow — show configuration dialog.
            Logger.Debug("OpenSettingsWindow placeholder invoked");
        }

        /// <summary>
        /// Teleports Mochi to the center of the primary screen.
        /// Placeholder — to be wired once the overlay window is available.
        /// </summary>
        private void BringMochiToCenter()
        {
            // TODO: once OverlayWindow is accessible via DI, set its
            // Left/Top to screen center. For now we just log.
            Logger.Debug("BringMochiToCenter placeholder invoked");
        }

        //─────────────────────── Icon ───────────────────────────────

        /// <summary>
        /// Creates a simple default icon source so the tray displays
        /// something even before an asset path is configured. Returns
        /// null on non-Windows / design-time where icon creation may fail.
        /// </summary>
        private static System.Windows.Media.ImageSource? CreateDefaultIcon()
        {
            try
            {
                // Use the built-in application icon if available, otherwise null.
                // The actual cat icon asset will be assigned by the asset-loading task.
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to create default tray icon");
                return null;
            }
        }

        /// <summary>
        /// Sets the tray icon image source. Called by the asset-loading
        /// task once the cat icon is available.
        /// </summary>
        public void SetIcon(System.Windows.Media.ImageSource iconSource)
        {
            _taskbarIcon.IconSource = iconSource;
        }

        /// <summary>
        /// Updates the Sleep/Wake menu label based on the current sleeping
        /// state. Can be called externally when state changes come from
        /// sources other than the tray menu (e.g. auto-sleep).
        /// </summary>
        public void SetSleepingState(bool isSleeping)
        {
            _isSleeping = isSleeping;
            if (_taskbarIcon.ContextMenu is { } menu)
            {
                foreach (var item in menu.Items)
                {
                    if (item is System.Windows.Controls.MenuItem mi &&
                        mi.Tag?.ToString() == "SleepWake")
                    {
                        mi.Header = _isSleeping ? "Wake" : "Sleep";
                        break;
                    }
                }
            }
        }

        //─────────────────────── IDisposable ────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _taskbarIcon.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error disposing taskbar icon");
            }

            Logger.Debug("TrayIconController disposed");
        }
    }
}