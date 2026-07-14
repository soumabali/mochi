using System;
using System.Collections.Generic;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// PRD §6.1 procedural micro-motion: breathing, idle fidgets, happy hop.
    /// All motion is time-based and procedural — no animation assets.
    /// Uses injected <see cref="ITimeProvider"/> and <see cref="IRandom"/>
    /// for deterministic testing.
    /// </summary>
    public sealed class MicroMotionService
    {
        // Breathing: sinusoidal vertical scale ±1.5% at ~0.4 Hz (PRD §6.1)
        public const double BreathingAmplitude = 0.015; // ±1.5%
        public const double BreathingFrequencyHz = 0.4; // ~0.4 Hz

        // Idle fidget interval bounds (seconds) — PRD §6.1: every 6–20s
        public const double FidgetIntervalMin = 6.0;
        public const double FidgetIntervalMax = 20.0;

        // Happy hop: 2–3 sine bounces with squash & stretch
        public const int HopBounceCount = 3;
        public const double HopDurationSeconds = 0.6;

        private readonly ITimeProvider _time;
        private readonly IRandom _random;

        private double _nextFidgetAt;
        private FidgetType? _pendingFidget;

        // Published when a fidget should be played by the renderer/FSM.
        public event Action<FidgetType>? FidgetRaised;

        // Pool of idle fidgets (PRD §6.1)
        private static readonly FidgetType[] FidgetPool =
        {
            FidgetType.Blink,
            FidgetType.HeadBob,
            FidgetType.Sway,
            FidgetType.Glance
        };

        public MicroMotionService(ITimeProvider time, IRandom random)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _nextFidgetAt = time.GetElapsedSeconds() + SampleFidgetInterval();
        }

        /// <summary>
        /// Apply sinusoidal breathing scale to a vertical scale factor.
        /// PRD §6.1: vertical scale oscillates ±1.5% at ~0.4 Hz.
        /// </summary>
        /// <param name="timeSeconds">Elapsed seconds (or injected time).</param>
        /// <param name="scaleY">Reference to the current Y scale; modified in place.</param>
        public static void ApplyBreathing(double timeSeconds, ref double scaleY)
        {
            double phase = 2.0 * Math.PI * BreathingFrequencyHz * timeSeconds;
            // Base scale 1.0, oscillate ±amplitude
            scaleY = 1.0 + BreathingAmplitude * Math.Sin(phase);
        }

        /// <summary>Instance helper using the injected time provider.</summary>
        public double CurrentBreathingScaleY()
        {
            double s = 1.0;
            ApplyBreathing(_time.GetElapsedSeconds(), ref s);
            return s;
        }

        /// <summary>
        /// Happy hop translation and squash/stretch over [0,1] progress.
        /// PRD §6.1: 2–3 sine bounces on translate-Y with squash & stretch.
        /// </summary>
        /// <param name="progress">0..1 fraction of the hop duration.</param>
        /// <returns>Delta Y (pixels, negative = up) and scale (X,Y) for squash/stretch.</returns>
        public static (double dY, double scaleX, double scaleY) HappyHop(double progress)
        {
            if (progress < 0) progress = 0;
            if (progress > 1) progress = 1;

            // Translate-Y: series of sine bounces. Each bounce is a half-sine
            // lobe of decreasing amplitude. dY < 0 is upward.
            double total = 0;
            double maxBounceHeight = 60.0; // px, peak of first bounce
            for (int i = 0; i < HopBounceCount; i++)
            {
                double amp = maxBounceHeight * Math.Pow(0.6, i); // decay
                double segStart = (double)i / HopBounceCount;
                double segEnd = (double)(i + 1) / HopBounceCount;
                if (progress < segStart) break;
                double segLen = segEnd - segStart;
                double local = progress < segEnd
                    ? (progress - segStart) / segLen
                    : 1.0;
                // Sine lobe: sin(pi * local) peaks at local=0.5
                total -= amp * Math.Sin(Math.PI * local) * (progress < segEnd ? 1.0 : 0.0);
            }

            // Squash/stretch: at launch and land (progress near 0 or 1) squash
            // vertically and stretch horizontally; at apex (mid-bounce) stretch
            // vertically. Use first bounce for the canonical envelope.
            double apex = Math.Sin(Math.PI * progress); // 0..1..0
            double squash = 0.12 * (1.0 - apex);        // squash at ends
            double scaleY = 1.0 - squash;               // squash vertically
            double scaleX = 1.0 + squash;               // stretch horizontally (area-preserving-ish)
            // At the apex, add a vertical stretch
            scaleY += 0.08 * apex;

            return (total, scaleX, scaleY);
        }

        /// <summary>
        /// Returns the next due fidget or null if the interval has not
        /// elapsed. When a fidget is returned, the next interval is sampled.
        /// Call this every tick (e.g. 30–60 Hz).
        /// </summary>
        public FidgetType? GetFidgetEvent()
        {
            double now = _time.GetElapsedSeconds();
            if (_pendingFidget.HasValue)
            {
                var f = _pendingFidget;
                _pendingFidget = null;
                _nextFidgetAt = now + SampleFidgetInterval();
                FidgetRaised?.Invoke(f.Value);
                Log.Debug("Fidget raised: {Fidget} (next @ {Next:F1}s)", f, _nextFidgetAt);
                return f;
            }

            if (now >= _nextFidgetAt)
            {
                _pendingFidget = FidgetPool[_random.Next(FidgetPool.Length)];
                return GetFidgetEvent();
            }
            return null;
        }

        /// <summary>Sample the next fidget interval in [min, max) seconds.</summary>
        public double SampleFidgetInterval()
        {
            // Uniform over [Min, Max)
            return FidgetIntervalMin + _random.NextDouble() * (FidgetIntervalMax - FidgetIntervalMin);
        }

        /// <summary>For tests: peek at the scheduled next-fidget time.</summary>
        public double PeekNextFidgetAt() => _nextFidgetAt;
    }

    /// <summary>
    /// Idle fidget types from the PRD §6.1 pool.
    /// </summary>
    public enum FidgetType
    {
        /// <summary>Quick eye blink.</summary>
        Blink,

        /// <summary>Small vertical head bob.</summary>
        HeadBob,

        /// <summary>Gentle horizontal sway.</summary>
        Sway,

        /// <summary>Glance to one side.</summary>
        Glance
    }
}