using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Periodic needs decay engine (PRD §6.1–6.2). Tracks Food, Energy, and
    /// Happiness over real (or injected) time and publishes
    /// <see cref="NeedsTickEvent"/> via <see cref="EventBus"/> whenever a need
    /// value changes. Uses <see cref="ITimeProvider"/> for deterministic testing.
    ///
    /// Decay rates (PRD §6.1):
    /// <list type="bullet">
    ///   <item>Food: -1 per 4 minutes (240 s).</item>
    ///   <item>Energy: -1 per 6 minutes (360 s) awake, +1 per 30 minutes
    ///       (1800 s) asleep.</item>
    ///   <item>Happiness: -1 per 5 minutes (300 s) unless recently petted
    ///       or fed.</item>
    /// </list>
    /// </summary>
    public sealed class NeedsTicker : IDisposable
    {
        // ── Decay constants (seconds per point) ──────────────────────────

        /// <summary>Seconds for Food to drop by 1 (PRD §6.1: 4 min).</summary>
        public const double FoodDecaySeconds = 240.0;

        /// <summary>Seconds awake for Energy to drop by 1 (PRD §6.1: 6 min).</summary>
        public const double EnergyDecaySeconds = 360.0;

        /// <summary>Seconds asleep for Energy to rise by 1 (PRD §6.1: 30 min).</summary>
        public const double EnergyRecoverySeconds = 1800.0;

        /// <summary>Seconds for Happiness to drop by 1 without interaction (PRD §6.1: 5 min).</summary>
        public const double HappinessDecaySeconds = 300.0;

        /// <summary>Window after pet/fed during which Happiness does not decay.</summary>
        public const double HappinessGraceSeconds = 60.0;

        // ── Bounds ───────────────────────────────────────────────────────

        public const int MinNeed = 0;
        public const int MaxNeed = 100;

        // ── State ────────────────────────────────────────────────────────

        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly ILogger _log;

        private int _food = MaxNeed;
        private int _energy = MaxNeed;
        private int _happiness = MaxNeed;

        // Fractional accumulators — carry sub-point remainders across ticks
        // so that calling Update() every second still produces correct decay.
        private double _foodAccumulator;
        private double _energyAccumulator;
        private double _happinessAccumulator;

        private double _lastTickSeconds;
        private double _lastInteractionSeconds = double.NegativeInfinity;
        private bool _asleep;

        private bool _disposed;

        /// <summary>
        /// Constructs the ticker. Initial need values default to 100 (full).
        /// Call <see cref="Update"/> periodically (e.g. every second) to
        /// accumulate decay and emit tick events.
        /// </summary>
        /// <param name="time">Monotonic time source.</param>
        /// <param name="bus">Event bus for publishing <see cref="NeedsTickEvent"/>.</param>
        public NeedsTicker(ITimeProvider time, EventBus bus)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _log = Log.ForContext<NeedsTicker>();
            _lastTickSeconds = _time.GetElapsedSeconds();
            _log.Debug("NeedsTicker initialised (Food={Food}, Energy={Energy}, Happiness={Happiness})",
                _food, _energy, _happiness);
        }

        // ── Current values (read-only snapshot) ──────────────────────────

        /// <summary>Current Food 0–100.</summary>
        public int Food => _food;

        /// <summary>Current Energy 0–100.</summary>
        public int Energy => _energy;

        /// <summary>Current Happiness 0–100.</summary>
        public int Happiness => _happiness;

        /// <summary>Whether Mochi is currently asleep (energy recovers).</summary>
        public bool IsAsleep => _asleep;

        // ── Interaction hooks ────────────────────────────────────────────

        /// <summary>
        /// Notify that Mochi was petted. Resets the happiness decay grace
        /// window so Happiness stops decaying for
        /// <see cref="HappinessGraceSeconds"/>.
        /// </summary>
        public void OnPetted()
        {
            _lastInteractionSeconds = _time.GetElapsedSeconds();
            _happinessAccumulator = 0;
            _log.Debug("Petted — happiness grace window reset");
        }

        /// <summary>
        /// Notify that Mochi was fed. Adds <paramref name="amount"/> to Food
        /// (clamped to <see cref="MaxNeed"/>) and resets the happiness decay
        /// grace window.
        /// </summary>
        /// <param name="amount">Food points added (must be &gt; 0).</param>
        public void OnFed(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Fed amount must be positive");

            int prev = _food;
            _food = Math.Min(MaxNeed, _food + amount);
            _lastInteractionSeconds = _time.GetElapsedSeconds();
            _happinessAccumulator = 0;
            _log.Debug("Fed +{Amount} (Food {Prev}→{Food})", amount, prev, _food);
        }

        /// <summary>
        /// Mark Mochi as having started sleeping. Energy will recover instead
        /// of decaying on subsequent <see cref="Update"/> calls.
        /// </summary>
        public void OnSleepStarted()
        {
            if (_asleep) return;
            _asleep = true;
            _energyAccumulator = 0;
            _log.Debug("Sleep started — energy recovery mode");
        }

        /// <summary>Mark Mochi as awake; energy resumes decaying.</summary>
        public void OnSleepEnded()
        {
            if (!_asleep) return;
            _asleep = false;
            _energyAccumulator = 0;
            _log.Debug("Sleep ended — energy decay resumed");
        }

        // ── Core tick ────────────────────────────────────────────────────

        /// <summary>
        /// Accumulate elapsed time, apply decay/recovery, and publish a
        /// <see cref="NeedsTickEvent"/> if any need value changed.
        /// Should be called regularly (e.g. once per second from a host loop).
        /// </summary>
        public void Update()
        {
            double now = _time.GetElapsedSeconds();
            double dt = now - _lastTickSeconds;
            _lastTickSeconds = now;

            if (dt <= 0)
                return; // no forward time — nothing to do

            bool changed = false;

            // Food always decays (PRD §6.1).
            _foodAccumulator += dt / FoodDecaySeconds;
            int foodPoints = (int)_foodAccumulator;
            if (foodPoints > 0)
            {
                _foodAccumulator -= foodPoints;
                int newFood = Clamp(_food - foodPoints);
                if (newFood != _food)
                {
                    _food = newFood;
                    changed = true;
                }
            }

            // Energy: decay when awake, recover when asleep.
            double energyRate = _asleep ? 1.0 / EnergyRecoverySeconds : -1.0 / EnergyDecaySeconds;
            _energyAccumulator += dt * energyRate;
            int energyPoints = (int)Math.Abs(_energyAccumulator);
            if (energyPoints > 0)
            {
                _energyAccumulator -= Math.Sign(_energyAccumulator) * energyPoints;
                int newEnergy = Clamp(_energy + (_asleep ? energyPoints : -energyPoints));
                if (newEnergy != _energy)
                {
                    _energy = newEnergy;
                    changed = true;
                }
            }

            // Happiness: decay only if outside the grace window after
            // petting/feeding (PRD §6.1 "if not petted/fed recently").
            double sinceInteraction = now - _lastInteractionSeconds;
            if (sinceInteraction >= HappinessGraceSeconds)
            {
                // If this tick straddles the grace boundary, only decay the
                // portion beyond the grace window.
                double decayDt = dt;
                double beforeGrace = sinceInteraction - dt;
                if (beforeGrace < HappinessGraceSeconds)
                    decayDt = sinceInteraction - HappinessGraceSeconds;

                if (decayDt > 0)
                {
                    _happinessAccumulator += decayDt / HappinessDecaySeconds;
                    int happinessPoints = (int)_happinessAccumulator;
                    if (happinessPoints > 0)
                    {
                        _happinessAccumulator -= happinessPoints;
                        int newHappiness = Clamp(_happiness - happinessPoints);
                        if (newHappiness != _happiness)
                        {
                            _happiness = newHappiness;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                _log.Debug("Needs tick (Food={Food}, Energy={Energy}, Happiness={Happiness})",
                    _food, _energy, _happiness);
                _bus.Publish(new NeedsTickEvent(_food, _energy, _happiness));
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static int Clamp(int v) => Math.Max(MinNeed, Math.Min(MaxNeed, v));

        // ── IDisposable ──────────────────────────────────────────────────

        /// <summary>Disposes the ticker. No unmanaged resources; safe to call.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _log.Debug("NeedsTicker disposed");
        }
    }
}