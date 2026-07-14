using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using MochiV2.Core.Particles;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Sleep service (PRD §6.8). Manages Mochi's sleeping cycle:
    /// <list type="bullet">
    /// <item>Auto-sleep when Energy ≤ 20 (publishes
    /// <see cref="SleepStartedEvent"/>).</item>
    /// <item>Manual sleep/wake via tray menu or direct method call.</item>
    /// <item>Wake plays the reversed-yawn <see cref="FSMState.WakeUp"/>
    /// animation (playOnceReversed, PRD §10).</item>
    /// <item>Zzz particles emitted periodically while sleeping
    /// (PRD §7.4 / FR-18).</item>
    /// <item>Energy recovery (+1 / 30 min) handled by
    /// <see cref="NeedsTicker"/> while asleep.</item>
    /// </list>
    /// Subscribes to <see cref="NeedsTickEvent"/> to monitor energy and
    /// trigger auto-sleep. Publishes <see cref="SleepStartedEvent"/> and
    /// <see cref="SleepEndedEvent"/> on the <see cref="EventBus"/>.
    /// </summary>
    public sealed class SleepService : IDisposable
    {
        // ─────────────────────── Constants ───────────────────────

        /// <summary>Energy threshold at or below which Mochi auto-sleeps (PRD §6.8).</summary>
        public const int AutoSleepThreshold = 20;

        /// <summary>
        /// Seconds between Zzz particle emissions while sleeping (PRD §7.4).
        /// </summary>
        public const double ZzzIntervalSeconds = 2.0;

        ///<summary>Auto-wake after this many seconds asleep (default: 10 minutes).</summary>
        public const double SleepDurationSeconds = 600.0;

        // ─────────────────────── Dependencies ────────────────────

        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly NeedsTicker _needs;
        private readonly FSM _fsm;
        private readonly ParticleSystem _particles;
        private readonly ILogger _log;

        // ─────────────────────── State ───────────────────────────

        private bool _asleep;
        private double _sleepStartSeconds;
        private double _lastZzzSeconds;
        private bool _disposed;

        /// <summary>
        /// Constructs the sleep service and subscribes to
        /// <see cref="NeedsTickEvent"/> on <paramref name="bus"/> for energy
        /// monitoring.
        /// </summary>
        /// <param name="time">Monotonic time source for Zzz interval timing.</param>
        /// <param name="bus">Event bus for sleep/wake event publication.</param>
        /// <param name="needs">Needs ticker to toggle sleep/recovery mode.</param>
        /// <param name="fsm">FSM to trigger Sleeping/WakeUp state transitions.</param>
        /// <param name="particles">Particle system for Zzz emission.</param>
        public SleepService(
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
            _log = Log.ForContext<SleepService>();

            _bus.Subscribe<NeedsTickEvent>(OnNeedsTick);
            _log.Debug("SleepService initialised (autoSleepThreshold={Threshold})", AutoSleepThreshold);
        }

        // ─────────────────────── Public API ──────────────────────

        /// <summary>Whether Mochi is currently asleep.</summary>
        public bool IsAsleep => _asleep;

        /// <summary>
        /// Put Mochi to sleep manually (tray menu or event). Transitions to
        /// <see cref="FSMState.Sleeping"/>, starts Zzz particle emission, and
        /// publishes <see cref="SleepStartedEvent"/>. No-op if already asleep.
        /// </summary>
        public void Sleep()
        {
            if (_asleep)
            {
                _log.Debug("Sleep requested but already asleep — ignoring");
                return;
            }

            _asleep = true;
            _sleepStartSeconds = _time.GetElapsedSeconds();
            _lastZzzSeconds = _time.GetElapsedSeconds();

            _needs.OnSleepStarted();
            TryTransitionTo(FSMState.Sleeping);
            _particles.StartZzzEmitting();

            _bus.Publish(new SleepStartedEvent());
            _log.Information("Mochi went to sleep (manual)");
        }

        /// <summary>
        /// Wake Mochi up manually (tray menu or event). Plays the reversed-
        /// yawn <see cref="FSMState.WakeUp"/> animation (playOnceReversed),
        /// stops Zzz particles, and publishes <see cref="SleepEndedEvent"/>.
        /// No-op if already awake.
        /// </summary>
        public void Wake()
        {
            if (!_asleep)
            {
                _log.Debug("Wake requested but already awake — ignoring");
                return;
            }

            _asleep = false;

            _needs.OnSleepEnded();
            _particles.StopZzzEmitting();
            TryTransitionTo(FSMState.WakeUp);

            _bus.Publish(new SleepEndedEvent());
            _log.Information("Mochi woke up (manual)");
        }

        /// <summary>
        /// Periodic update call (e.g. once per second from the host loop).
        /// While asleep, emits Zzz particles at
        /// <see cref="ZzzIntervalSeconds"/> intervals.
        /// </summary>
        public void Update()
        {
            if (!_asleep)
                return;

            double now = _time.GetElapsedSeconds();
            if ((now - _lastZzzSeconds) >= ZzzIntervalSeconds)
            {
                _lastZzzSeconds = now;
                _particles.EmitZzz();
            }
        }

        // ─────────────────────── Event handler ───────────────────

        /// <summary>
        /// Handle <see cref="NeedsTickEvent"/>: monitors energy and triggers
        /// auto-sleep when Energy ≤ <see cref="AutoSleepThreshold"/> (PRD §6.8).
        /// Auto-wake is not triggered by energy alone (energy recovers while
        /// sleeping); waking is manual or handled by a separate wake trigger.
        /// </summary>
        private void OnNeedsTick(NeedsTickEvent evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));

            // Auto-wake after sleep duration elapsed
            if (_asleep)
            {
                double elapsed = _time.GetElapsedSeconds() - _sleepStartSeconds;
                if (elapsed >= SleepDurationSeconds)
                {
                    _log.Information("Auto-wake after {Seconds:F0}s sleep", elapsed);
                    Wake();
                    return;
                }
            }

            if (!_asleep && evt.Energy <= AutoSleepThreshold)
            {
                _asleep = true;
                _lastZzzSeconds = _time.GetElapsedSeconds();

                _needs.OnSleepStarted();
                TryTransitionTo(FSMState.Sleeping);
                _particles.StartZzzEmitting();

                _bus.Publish(new SleepStartedEvent());
                _log.Information("Mochi auto-sleep triggered (energy={Energy})", evt.Energy);
            }
        }

        // ─────────────────────── Helpers ─────────────────────────

        /// <summary>
        /// Attempt an FSM transition to <paramref name="target"/>. Logs a
        /// warning if the transition is not possible from the current state
        /// rather than crashing.
        /// </summary>
        private void TryTransitionTo(FSMState target)
        {
            try
            {
                _fsm.TransitionTo(target, bypassValidation: true);
            }
            catch (InvalidOperationException ex)
            {
                _log.Warning(ex, "Could not transition to {State} for sleep/wake; staying {Current}",
                    target, _fsm.CurrentState);
            }
        }

        // ─────────────────────── IDisposable ─────────────────────

        /// <summary>Unsubscribes from the event bus and stops Zzz emission.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Unsubscribe<NeedsTickEvent>(OnNeedsTick);
            if (_asleep)
                _particles.StopZzzEmitting();
            _log.Debug("SleepService disposed");
        }
    }
}