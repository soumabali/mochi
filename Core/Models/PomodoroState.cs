namespace MochiV2.Core.Models
{
    /// <summary>
    /// Pomodoro timer states. Post-MVP Phase F.
    /// </summary>
    public enum PomodoroState
    {
        /// <summary>Timer not running.</summary>
        Idle,

        /// <summary>Focus work period (default 25 min).</summary>
        Focus,

        /// <summary>Short break between focus rounds (default 5 min).</summary>
        ShortBreak,

        /// <summary>Long break after N rounds (default 15 min).</summary>
        LongBreak,

        /// <summary>Timer paused (resumes from same point).</summary>
        Paused
    }
}