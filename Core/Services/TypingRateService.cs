using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Typing-rate awareness for Mochi. PRD §6.6.
    ///
    /// Receives key counts from <see cref="Infrastructure.Input.KeyRateHook"/>
    /// and drives Mochi's state:
    /// <list type="bullet">
    /// <item>&gt;120 keys/min sustained for 2 min → Mochi sleeps (Sleeping state).</item>
    /// <item>Typing stops for 5 min → wake + meow (WakeUp state, then MeowLeft/MeowRight).</item>
    /// </list>
    ///
    /// Subscribes to <see cref="TypingBurstStartedEvent"/> / <see cref="TypingBurstEndedEvent"/>
    /// from the <see cref="EventBus"/>. Uses <see cref="ITimeProvider"/> for timing.
    /// </summary>
    public sealed class TypingRateService
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(TypingRateService));

        //──────── Configuration (PRD §6.6) ────────

        /// <summary>Keys/min threshold for sustained typing (matches KeyRateHook).</summary>
        public const int BurstThresholdKeysPerMin = 120;

        /// <summary>Seconds of sustained above-threshold rate before sleep.</summary>
        public const double BurstStartSeconds = 120.0; // 2 min

        /// <summary>Seconds of no typing before wake + meow.</summary>
        public const double BurstEndSeconds = 300.0; // 5 min

        //──────── Dependencies ────────

        private readonly EventBus _eventBus;
        private readonly FSM _fsm;
        private readonly ITimeProvider _time;
        private readonly IRandom _random;

        //──────── State ────────

        private bool _isSleeping;
        private double _lastBurstEnd;

        /// <summary>
        /// Create the typing-rate service.
        /// </summary>
        /// <param name="eventBus">EventBus to subscribe typing events.</param>
        /// <param name="fsm">FSM to drive Sleeping / WakeUp / Meow states.</param>
        /// <param name="time">Time provider for timing.</param>
        /// <param name="random">RNG for meow-left/meow-right selection.</param>
        /// <exception cref="ArgumentNullException">Any dependency null.</exception>
        public TypingRateService(
            EventBus eventBus,
            FSM fsm,
            ITimeProvider time,
            IRandom random)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _lastBurstEnd = -1;
            _eventBus.Subscribe<TypingBurstStartedEvent>(OnBurstStarted);
            _eventBus.Subscribe<TypingBurstEndedEvent>(OnBurstEnded);
            Logger.Debug("TypingRateService subscribed TypingBurst events.");
        }

        /// <summary>True when Mochi is currently sleeping due to typing burst.</summary>
        public bool IsSleeping => _isSleeping;

        //───────────────────── Event handlers ─────────────────────

        private void OnBurstStarted(TypingBurstStartedEvent e)
        {
            Logger.Information("Typing burst started — Mochi going to sleep.");
            if (!_isSleeping)
            {
                _isSleeping = true;
                TryInterrupt(FSMState.Sleeping);
            }
        }

        private void OnBurstEnded(TypingBurstEndedEvent e)
        {
            Logger.Information("Typing burst ended — waking Mochi + meow.");
            _lastBurstEnd = _time.GetElapsedSeconds();

            if (_isSleeping)
            {
                _isSleeping = false;
                // Wake up (reverse yawn), then meow.
                TryInterrupt(FSMState.WakeUp);
                // Meow after wake — pick left or right randomly.
                FSMState meow = _random.Next(2) == 0
                    ? FSMState.MeowLeft
                    : FSMState.MeowRight;
                TryInterrupt(meow);
            }
            else
            {
                // Not sleeping but still meow on burst end.
                FSMState meow = _random.Next(2) == 0
                    ? FSMState.MeowLeft
                    : FSMState.MeowRight;
                TryInterrupt(meow);
            }
        }

        /// <summary>
        /// Periodic tick. Currently the service is event-driven via
        /// TypingBurstStarted/Ended, but Tick() is provided for future
        /// time-based evaluation and testability.
        /// </summary>
        public void Tick()
        {
            // Event-driven; no periodic action needed currently.
        }

        private void TryInterrupt(FSMState state)
        {
            try
            {
                if (_fsm.CurrentState == state)
                    return;
                _fsm.Interrupt(state);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "FSM interrupt {State} failed.", state);
            }
        }
    }
}