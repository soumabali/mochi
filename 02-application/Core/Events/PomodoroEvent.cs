using MochiV2.Core.Models;

namespace MochiV2.Core.Events
{
    /// <summary>
    /// EventBus event for Pomodoro timer state changes and ticks.
    /// Post-MVP Phase F.
    /// </summary>
    public readonly struct PomodoroEvent
    {
        public PomodoroState State { get; }
        public double ElapsedSeconds { get; }
        public double RemainingSeconds { get; }
        public int Round { get; }

        public PomodoroEvent(PomodoroState state, double elapsedSeconds, double remainingSeconds, int round)
        {
            State = state;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
            Round = round;
        }
    }
}