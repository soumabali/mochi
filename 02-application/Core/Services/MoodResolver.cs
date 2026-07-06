using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Deterministic priority-based mood resolver (PRD §6.3). Subscribes to
    /// <see cref="NeedsTickEvent"/> and resolves a mood string from the
    /// current need values using a fixed priority order:
    /// <code>
    ///   HungryCritical (food &lt; 20)
    ///     &gt; HungryStandard (food &lt; 40)
    ///     &gt; Tired (energy &lt; 20)
    ///     &gt; Sad (happiness &lt; 30)
    ///     &gt; Content (default)
    /// </code>
    /// A 60-second hysteresis prevents mood flapping: the mood will not change
    /// more than once per <see cref="HysteresisSeconds"/>. When a new mood is
    /// resolved and the hysteresis window has elapsed, a
    /// <see cref="MoodChangedEvent"/> is published via <see cref="EventBus"/>.
    /// </summary>
    public sealed class MoodResolver : IDisposable
    {
        /// <summary>Minimum seconds between mood changes (PRD §6.3).</summary>
        public const double HysteresisSeconds = 60.0;

        // Mood name constants (match FSMState names where applicable).
        public const string MoodHungryCritical = nameof(MochiV2.Core.Models.FSMState.HungryCritical);
        public const string MoodHungryStandard = nameof(MochiV2.Core.Models.FSMState.HungryStandard);
        public const string MoodTired = "Tired";
        public const string MoodSad = "Sad";
        public const string MoodContent = "Content";

        // Threshold constants (PRD §6.3).
        public const int FoodCriticalThreshold = 20;
        public const int FoodStandardThreshold = 40;
        public const int EnergyTiredThreshold = 20;
        public const int HappinessSadThreshold = 30;

        private readonly EventBus _bus;
        private readonly ITimeProvider _time;
        private readonly ILogger _log;

        private string _currentMood = MoodContent;
        private string? _pendingMood;
        private double _lastChangeSeconds;
        private bool _disposed;

        /// <summary>
        /// Constructs the resolver and subscribes to
        /// <see cref="NeedsTickEvent"/> on <paramref name="bus"/>.
        /// </summary>
        /// <param name="time">Monotonic time source for hysteresis.</param>
        /// <param name="bus">Event bus for subscription and publication.</param>
        public MoodResolver(ITimeProvider time, EventBus bus)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _log = Log.ForContext<MoodResolver>();
            _lastChangeSeconds = _time.GetElapsedSeconds();
            _bus.Subscribe<NeedsTickEvent>(OnNeedsTick);
            _log.Debug("MoodResolver initialised (mood={Mood})", _currentMood);
        }

        /// <summary>Current resolved mood name.</summary>
        public string CurrentMood => _currentMood;

        /// <summary>
        /// Pure, deterministic mood resolution from need values.
        /// Does not apply hysteresis or publish events — used internally
        /// and safe to call from tests.
        /// </summary>
        public static string ResolveMood(int food, int energy, int happiness)
        {
            if (food < FoodCriticalThreshold)
                return MoodHungryCritical;
            if (food < FoodStandardThreshold)
                return MoodHungryStandard;
            if (energy < EnergyTiredThreshold)
                return MoodTired;
            if (happiness < HappinessSadThreshold)
                return MoodSad;
            return MoodContent;
        }

        /// <summary>
        /// Handle a <see cref="NeedsTickEvent"/>: resolve the candidate mood
        /// and publish <see cref="MoodChangedEvent"/> when the mood changes
        /// and the hysteresis window has elapsed.
        /// </summary>
        private void OnNeedsTick(NeedsTickEvent evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));

            string candidate = ResolveMood(evt.Food, evt.Energy, evt.Happiness);

            if (candidate == _currentMood)
            {
                // Same mood — clear any pending change.
                if (_pendingMood != null)
                {
                    _pendingMood = null;
                    _log.Debug("Mood stabilised back to {Mood}; pending change cleared", _currentMood);
                }
                return;
            }

            // Mood wants to change. Enforce hysteresis.
            double now = _time.GetElapsedSeconds();
            double elapsedSinceChange = now - _lastChangeSeconds;

            if (elapsedSinceChange >= HysteresisSeconds)
            {
                // Hysteresis satisfied — apply immediately.
                ApplyMoodChange(candidate, now);
            }
            else
            {
                // Within hysteresis window — remember the candidate; it will
                // be applied once the window elapses (see <see cref="Tick"/>).
                if (_pendingMood != candidate)
                {
                    _pendingMood = candidate;
                    _log.Debug("Mood wants to change {Old}→{New} but hysteresis ({Remaining:F0}s remaining); pending",
                        _currentMood, candidate, HysteresisSeconds - elapsedSinceChange);
                }
            }
        }

        /// <summary>
        /// Drive hysteresis timing. Call periodically (e.g. once per second)
        /// so that a pending mood change is applied as soon as the hysteresis
        /// window elapses, even without a new <see cref="NeedsTickEvent"/>.
        /// </summary>
        public void Tick()
        {
            if (_pendingMood == null)
                return;

            double now = _time.GetElapsedSeconds();
            if (now - _lastChangeSeconds >= HysteresisSeconds)
            {
                ApplyMoodChange(_pendingMood, now);
                _pendingMood = null;
            }
        }

        /// <summary>
        /// Force an immediate mood recalculation from the given need values,
        /// bypassing hysteresis. Intended for testing or initialisation.
        /// </summary>
        public void Recalculate(int food, int energy, int happiness)
        {
            string candidate = ResolveMood(food, energy, happiness);
            double now = _time.GetElapsedSeconds();
            if (candidate != _currentMood)
                ApplyMoodChange(candidate, now);
        }

        private void ApplyMoodChange(string newMood, double now)
        {
            string oldMood = _currentMood;
            _currentMood = newMood;
            _lastChangeSeconds = now;
            _pendingMood = null;
            _log.Information("Mood changed {Old}→{New}", oldMood, newMood);
            _bus.Publish(new MoodChangedEvent(oldMood, newMood));
        }

        /// <summary>Unsubscribes from the event bus.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Unsubscribe<NeedsTickEvent>(OnNeedsTick);
            _log.Debug("MoodResolver disposed");
        }
    }
}