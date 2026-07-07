using System.Text.Json.Serialization;

namespace MochiV2.Core.Models
{
    /// <summary>
    /// Pomodoro timer configuration. Post-MVP Phase F.
    /// All durations in minutes. Defaults: 25/5/15, 4 rounds.
    /// </summary>
    public sealed class PomodoroSettings
    {
        /// <summary>Focus duration in minutes.</summary>
        [JsonPropertyName("focusMinutes")]
        public double FocusMinutes { get; set; } = 25.0;

        /// <summary>Short break duration in minutes.</summary>
        [JsonPropertyName("shortBreakMinutes")]
        public double ShortBreakMinutes { get; set; } = 5.0;

        /// <summary>Long break duration in minutes.</summary>
        [JsonPropertyName("longBreakMinutes")]
        public double LongBreakMinutes { get; set; } = 15.0;

        /// <summary>Number of focus rounds before a long break.</summary>
        [JsonPropertyName("roundsBeforeLongBreak")]
        public int RoundsBeforeLongBreak { get; set; } = 4;

        /// <summary>Whether pomodoro auto-starts the next round.</summary>
        [JsonPropertyName("autoContinue")]
        public bool AutoContinue { get; set; } = true;

        public static PomodoroSettings Default => new();
    }
}