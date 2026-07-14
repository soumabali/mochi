using System;
using SkiaSharp;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Night-mode service (PRD §9.4). Activates a cool/dim tint overlay and
    /// increases Mochi's sleep bias during nighttime hours (22:00–06:00 local
    /// time).
    /// <list type="bullet">
    /// <item><see cref="IsActive"/> is true when
    /// <see cref="DateTime.Now"/> falls inside the night window.</item>
    /// <item><see cref="TintColor"/> returns a subtle cool-blue
    /// <see cref="SKColor"/> (alpha ≈ 30) the renderer blends over the
    /// overlay during night mode.</item>
    /// <item><see cref="SleepBiasMultiplier"/> returns 2.0 when night mode
    /// is active and 1.0 otherwise — the sleep service uses this to weight
    /// sleep-vs-awake decisions.</item>
    /// <item><see cref="CheckLocalTime"/> re-evaluates the current hour and
    /// updates <see cref="IsActive"/>; callers should invoke it each tick
    /// or subscribe to <see cref="NightModeToggledEvent"/> for transitions.</item>
    /// </list>
    /// Fullscreen detection is handled by
    /// <c>MochiV2.Infrastructure.Window.FullscreenDetector</c>; monitor changes
    /// arrive via <see cref="MochiV2.Core.Events.MonitorChangedEvent"/> on the
    /// shared <see cref="EventBus"/> — this service optionally subscribes to
    /// re-check local time on monitor change.
    /// </summary>
    public sealed class NightModeService
    {
        //─────────────────────── Constants ───────────────────────

        /// <summary>Hour (24-h, inclusive) at which night mode begins.</summary>
        public const int NightStartHour = 22;

        /// <summary>Hour (24-h, exclusive) at which night mode ends.</summary>
        public const int NightEndHour = 6;

        /// <summary>
        /// Sleep-weight multiplier applied when night mode is active (PRD §9.4).
        /// </summary>
        public const double ActiveSleepBiasMultiplier = 2.0;

        /// <summary>
        /// Sleep-weight multiplier applied when night mode is inactive.
        /// </summary>
        public const double InactiveSleepBiasMultiplier = 1.0;

        /// <summary>
        /// Subtle cool-blue tint applied to the overlay during night mode
        /// (alpha ≈ 30 / 255 ≈ 12 % opacity, PRD §9.4).
        /// </summary>
        public static readonly SKColor NightTintColor =
            new SKColor(0x6E, 0x8A, 0xB5, 0x1E); // #6E8AB5 @ alpha 30

        private static readonly ILogger Logger =
            Log.ForContext(typeof(NightModeService));

        //─────────────────────── State ──────────────────────────

        private bool _isActive;
        private int _lastHour = -1;

        //─────────────────────── Properties ─────────────────────

        /// <summary>
        /// True when local time is inside the 22:00–06:00 night window.
        /// Updated by <see cref="CheckLocalTime"/>; safe to read from any
        /// render or decision tick.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Cool-blue <see cref="SKColor"/> (alpha ≈ 30) the renderer blends
        /// over the overlay while <see cref="IsActive"/> is true. Returns a
        /// fully transparent color when night mode is inactive so callers can
        /// apply it unconditionally without double-branching.
        /// </summary>
        public SKColor TintColor => _isActive ? NightTintColor : SKColor.Empty;

        /// <summary>
        /// Sleep bias multiplier consumed by the sleep service when weighting
        /// sleep-vs-awake transitions. Returns <see cref="ActiveSleepBiasMultiplier"/>
        /// (2.0) during night mode and <see cref="InactiveSleepBiasMultiplier"/>
        /// (1.0) otherwise.
        /// </summary>
        public double SleepBiasMultiplier =>
            _isActive ? ActiveSleepBiasMultiplier : InactiveSleepBiasMultiplier;

        //─────────────────────── Construction ───────────────────

        /// <summary>
        /// Create the night-mode service and evaluate the current local time
        /// immediately so <see cref="IsActive"/> is correct from first read.
        /// </summary>
        public NightModeService()
        {
            CheckLocalTime();
            Logger.Debug("NightModeService initialised (active={Active}).", _isActive);
        }

        //─────────────────────── Public API ─────────────────────

        /// <summary>
        /// Re-evaluate <see cref="DateTime.Now.Hour"/> against the night
        /// window and update <see cref="IsActive"/>. Logs transitions at
        /// Information level. Safe to call on every tick; only logs when the
        /// state actually changes.
        /// </summary>
        /// <returns>The updated value of <see cref="IsActive"/>.</returns>
        public bool CheckLocalTime()
        {
            int hour = DateTime.Now.Hour;

            // Only recompute / log when the hour changes; within the same hour
            // the active flag cannot flip.
            if (hour == _lastHour && _lastHour != -1)
                return _isActive;

            bool nowActive = IsHourInNightWindow(hour);
            _lastHour = hour;

            if (nowActive != _isActive)
            {
                _isActive = nowActive;
                Logger.Information(
                    "Night mode {State} (hour={Hour:00}:00, tint={Tint}, sleepBias={Bias}).",
                    _isActive ? "activated" : "deactivated",
                    hour,
                    _isActive ? NightTintColor.ToString() : "transparent",
                    SleepBiasMultiplier);
            }

            return _isActive;
        }

        //─────────────────────── Helpers ─────────────────────────

        /// <summary>
        /// Determine whether a 24-hour value falls inside the 22:00–06:00
        /// night window (22 and 23 plus 0–5; 06:00 is the exclusive boundary).
        /// </summary>
        /// <param name="hour">Hour of day in 24-hour notation (0–23).</param>
        /// <returns>True if the hour is within the night window.</returns>
        private static bool IsHourInNightWindow(int hour)
            => hour >= NightStartHour || hour < NightEndHour;
    }

    /// <summary>
    /// Published on the <see cref="EventBus"/> when <see cref="NightModeService"/>
    /// transitions between active and inactive. Lets the renderer and sleep
    /// service react to night-mode changes without polling.
    /// </summary>
    /// <param name="IsActive">The new night-mode state.</param>
    public sealed record NightModeToggledEvent(bool IsActive);
}