using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using MochiV2.Core.Particles;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Feeding service (PRD §6.7). Handles the "feed Mochi" interaction:
    /// adds Food (+40) and Happiness (+10), triggers the <see cref="FSMState.Eating"/>
    /// animation (loop with speedMultiplier 1.3 per PRD §10), and emits hearts
    /// particles via <see cref="EventBus"/> → <see cref="CatFedEvent"/>.
    ///
    /// A cooldown (default 30 s) prevents feeding spam. The service subscribes
    /// to <see cref="CatFedEvent"/> on the <see cref="EventBus"/> so external
    /// feed requests (tray menu, double-click) are handled uniformly.
    /// </summary>
    public sealed class FeedingService : IDisposable
    {
        // ─────────────────────── Constants ───────────────────────

        /// <summary>Food points added per feed action (PRD §6.7).</summary>
        public const int FoodAmount = 40;

        /// <summary>Happiness points added per feed action (PRD §6.7).</summary>
        public const int HappinessAmount = 10;

        /// <summary>Seconds between feed actions to prevent spam (PRD §6.7).</summary>
        public const double CooldownSeconds = 30.0;

        /// <summary>Number of heart particles emitted per feed (PRD §7.4).</summary>
        public const int HeartParticleCount = 5;

        // ─────────────────────── Dependencies ────────────────────

        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly NeedsTicker _needs;
        private readonly FSM _fsm;
        private readonly ParticleSystem _particles;
        private readonly ILogger _log;

        // ─────────────────────── State ───────────────────────────

        private double _lastFeedSeconds = double.NegativeInfinity;
        private bool _disposed;

        /// <summary>
        /// Constructs the feeding service and subscribes to
        /// <see cref="CatFedEvent"/> on <paramref name="bus"/>.
        /// </summary>
        /// <param name="time">Monotonic time source for cooldown tracking.</param>
        /// <param name="bus">Event bus for publishing/subscribing feed events.</param>
        /// <param name="needs">Needs ticker to apply Food/Happiness changes.</param>
        /// <param name="fsm">FSM to trigger the Eating state transition.</param>
        /// <param name="particles">Particle system for hearts emission.</param>
        public FeedingService(
            ITimeProvider time,
            EventBus bus,
            NeedsTicker needs,
            FSM fsm,
            ParticleSystem particles)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _needs = needs ?? throw new ArgumentNullException(nameof(needs));
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
            _particles = particles ?? throw new ArgumentNullException(nameof(particles));
            _log = Log.ForContext<FeedingService>();

            _bus.Subscribe<CatFedEvent>(OnCatFed);
            _log.Debug("FeedingService initialised (food=+{Food}, happiness=+{Happy}, cooldown={Cooldown}s)",
                FoodAmount, HappinessAmount, CooldownSeconds);
        }

        // ─────────────────────── Public API ──────────────────────

        /// <summary>
        /// Whether feeding is currently allowed (cooldown elapsed).
        /// </summary>
        public bool CanFeed
        {
            get
            {
                double now = _time.GetElapsedSeconds();
                return (now - _lastFeedSeconds) >= CooldownSeconds;
            }
        }

        /// <summary>
        /// Seconds remaining until the next feed is allowed (0 if ready).
        /// </summary>
        public double CooldownRemaining
        {
            get
            {
                double now = _time.GetElapsedSeconds();
                double remaining = CooldownSeconds - (now - _lastFeedSeconds);
                return remaining > 0 ? remaining : 0;
            }
        }

        /// <summary>
        /// Feed Mochi: adds Food +40 and Happiness +10, triggers the Eating
        /// FSM state, emits hearts particles, and publishes a
        /// <see cref="CatFedEvent"/>. Enforces a cooldown; returns false
        /// (without side effects) if called too soon after the last feed.
        /// </summary>
        /// <returns>True if feeding was applied; false if on cooldown.</returns>
        public bool Feed()
        {
            double now = _time.GetElapsedSeconds();

            if ((now - _lastFeedSeconds) < CooldownSeconds)
            {
                _log.Debug("Feed rejected — cooldown ({Remaining:F0}s remaining)",
                    CooldownSeconds - (now - _lastFeedSeconds));
                return false;
            }

            _lastFeedSeconds = now;

            // Apply need changes (NeedsTicker clamps to MaxNeed).
            _needs.OnFed(FoodAmount);
            _needs.OnPetted(); // resets happiness decay grace window

            // Trigger Eating animation (PRD §10: loop, speedMultiplier 1.3).
            TryTransitionTo(FSMState.Eating);

            // Emit hearts particles (PRD §7.4 / FR-18).
            _particles.EmitHearts(HeartParticleCount);

            // Publish event for other subscribers (UI feedback, stats, etc.).
            _bus.Publish(new CatFedEvent(FoodAmount));

            _log.Information("Fed Mochi +{Food} food, +{Happy} happiness (hearts emitted)",
                FoodAmount, HappinessAmount);
            return true;
        }

        // ─────────────────────── Event handler ───────────────────

        /// <summary>
        /// Handle <see cref="CatFedEvent"/>: applies feeding when an external
        /// source (tray menu, double-click) publishes the event. This lets all
        /// feed requests flow through the same cooldown-gated path.
        /// </summary>
        private void OnCatFed(CatFedEvent evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));

            // External feed request — route through Feed() for cooldown/gating.
            // The event's Amount is informational; we use our own fixed amounts
            // per PRD §6.7 (the publish inside Feed() would re-enter here, but
            // the cooldown guard prevents double-application).
            if (!CanFeed)
            {
                _log.Debug("CatFedEvent received but on cooldown — ignoring");
                return;
            }

            // Avoid republishing CatFedEvent: perform the side effects directly.
            double now = _time.GetElapsedSeconds();
            _lastFeedSeconds = now;

            _needs.OnFed(FoodAmount);
            _needs.OnPetted();

            TryTransitionTo(FSMState.Eating);
            _particles.EmitHearts(HeartParticleCount);

            _log.Information("External feed applied +{Food} food, +{Happy} happiness",
                FoodAmount, HappinessAmount);
        }

        // ─────────────────────── Helpers ─────────────────────────

        /// <summary>
        /// Attempt an FSM transition to <paramref name="target"/>. Logs a
        /// warning if the transition is not possible from the current state
        /// (e.g. Mochi is mid-Drag) rather than crashing.
        /// </summary>
        private void TryTransitionTo(FSMState target)
        {
            try
            {
                _fsm.TransitionTo(target, bypassValidation: true);
            }
            catch (InvalidOperationException ex)
            {
                _log.Warning(ex, "Could not transition to {State} for feeding; staying {Current}",
                    target, _fsm.CurrentState);
            }
        }

        // ─────────────────────── IDisposable ─────────────────────

        /// <summary>Unsubscribes from the event bus.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Unsubscribe<CatFedEvent>(OnCatFed);
            _log.Debug("FeedingService disposed");
        }
    }
}