using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Pomodoro timer state machine. Post-MVP Phase F.
    /// Cycles: Focus → ShortBreak → Focus → ... → LongBreak (after N rounds).
    /// Uses <see cref="ITimeProvider"/> for testable time progression.
    /// Fires <see cref="PomodoroEvent"/> via <see cref="EventBus"/> on state
    /// changes and periodic ticks.
    /// </summary>
    public sealed class PomodoroService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(PomodoroService));

        private readonly ITimeProvider _timeProvider;
        private readonly EventBus _eventBus;

        private PomodoroSettings _settings = PomodoroSettings.Default;
        private PomodoroState _state = PomodoroState.Idle;
        private PomodoroState _stateBeforePause = PomodoroState.Idle;

        private double _phaseStartTime;     // ITimeProvider seconds when phase began
        private double _pausedAtTime;       // when pause was pressed
        private double _pausedAccumulated;  // total paused seconds in current phase
        private int _round = 0;             // completed focus rounds (0-based)

        public PomodoroState State => _state;
        /// <summary>Current round number (1-based when running, 0 when idle).</summary>
        public int Round => _state == PomodoroState.Idle ? 0 : _round + 1;
        public PomodoroSettings Settings => _settings;

        /// <summary>Current phase duration in seconds.</summary>
        public double CurrentPhaseDurationSeconds => GetPhaseDuration(_state);

        /// <summary>Elapsed seconds in current phase (excluding paused time).</summary>
        public double ElapsedSeconds
        {
            get
            {
                if (_state == PomodoroState.Idle) return 0;
                if (_state == PomodoroState.Paused) return _pausedAtTime - _phaseStartTime - _pausedAccumulated;
                return _timeProvider.GetElapsedSeconds() - _phaseStartTime - _pausedAccumulated;
            }
        }

        /// <summary>Remaining seconds in current phase.</summary>
        public double RemainingSeconds => Math.Max(0, CurrentPhaseDurationSeconds - ElapsedSeconds);

        public PomodoroService(ITimeProvider timeProvider, EventBus eventBus)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>Update settings (takes effect on next phase).</summary>
        public void UpdateSettings(PomodoroSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Logger.Debug("Pomodoro settings updated: focus={Focus}min", settings.FocusMinutes);
        }

        /// <summary>Start or resume the timer from Idle/Paused.</summary>
        public void Start()
        {
            if (_state == PomodoroState.Paused)
            {
                // Resume: add pause duration to accumulated
                double pauseDuration = _timeProvider.GetElapsedSeconds() - _pausedAtTime;
                _pausedAccumulated += pauseDuration;
                _state = _stateBeforePause;
                Logger.Debug("Pomodoro resumed: {State}", _state);
                PublishEvent();
            }
            else if (_state == PomodoroState.Idle)
            {
                _round = 0;
                BeginPhase(PomodoroState.Focus); // BeginPhase publishes
            }
            // else: already running, do nothing
        }

        /// <summary>Pause the timer.</summary>
        public void Pause()
        {
            if (_state == PomodoroState.Idle || _state == PomodoroState.Paused) return;
            _stateBeforePause = _state;
            _pausedAtTime = _timeProvider.GetElapsedSeconds();
            _state = PomodoroState.Paused;
            Logger.Debug("Pomodoro paused at {Elapsed:F1}s", ElapsedSeconds);
            PublishEvent();
        }

        /// <summary>Reset to Idle, clear rounds.</summary>
        public void Reset()
        {
            _state = PomodoroState.Idle;
            _stateBeforePause = PomodoroState.Idle;
            _round = 0;
            _pausedAccumulated = 0;
            Logger.Debug("Pomodoro reset");
            PublishEvent();
        }

        /// <summary>
        /// Tick — call every frame. Checks if current phase is complete
        /// and auto-transitions if so.
        /// </summary>
        public void Tick()
        {
            if (_state == PomodoroState.Idle || _state == PomodoroState.Paused) return;

            if (ElapsedSeconds >= CurrentPhaseDurationSeconds)
            {
                AdvancePhase();
            }
        }

        /// <summary>Advance to the next phase.</summary>
        private void AdvancePhase()
        {
            if (_state == PomodoroState.Focus)
            {
                _round++;
                if (_round >= _settings.RoundsBeforeLongBreak)
                {
                    _round = 0;
                    BeginPhase(PomodoroState.LongBreak);
                }
                else
                {
                    BeginPhase(PomodoroState.ShortBreak);
                }
            }
            else if (_state == PomodoroState.ShortBreak || _state == PomodoroState.LongBreak)
            {
                if (_settings.AutoContinue)
                {
                    BeginPhase(PomodoroState.Focus);
                }
                else
                {
                    _state = PomodoroState.Idle;
                    Logger.Debug("Pomodoro stopped (autoContinue=false)");
                }
            }
        }

        private void BeginPhase(PomodoroState newState)
        {
            _state = newState;
            _phaseStartTime = _timeProvider.GetElapsedSeconds();
            _pausedAccumulated = 0;
            Logger.Information("Pomodoro phase: {State} (round {Round})", newState, _round + 1);
            PublishEvent();
        }

        private double GetPhaseDuration(PomodoroState state)
        {
            return state switch
            {
                PomodoroState.Focus => _settings.FocusMinutes * 60,
                PomodoroState.ShortBreak => _settings.ShortBreakMinutes * 60,
                PomodoroState.LongBreak => _settings.LongBreakMinutes * 60,
                _ => 0
            };
        }

        private void PublishEvent()
        {
            _eventBus.Publish(new PomodoroEvent(_state, ElapsedSeconds, RemainingSeconds, _round + 1));
        }
    }
}