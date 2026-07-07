using System;
using System.Text.Json.Serialization;

namespace MochiV2.Core.Models
{
    /// <summary>
    /// Serializable snapshot of all Mochi v2 persistent state. PRD §12.
    /// Written to <c>save.json</c> by <see cref="MochiV2.Infrastructure.Storage.SaveManager"/>
    /// and loaded on startup. Uses <see cref="JsonPropertyName"/> camelCase
    /// attributes for stable on-disk naming independent of C# conventions.
    /// </summary>
    public sealed class SaveData
    {
        //───────────────────────── Needs (0–100) ─────────────────────────

        /// <summary>Current food level (0–100). PRD §6.1.</summary>
        [JsonPropertyName("food")]
        public int Food { get; set; } = 80;

        /// <summary>Current energy level (0–100). PRD §6.1.</summary>
        [JsonPropertyName("energy")]
        public int Energy { get; set; } = 80;

        /// <summary>Current happiness level (0–100). PRD §6.1.</summary>
        [JsonPropertyName("happiness")]
        public int Happiness { get; set; } = 80;

        //───────────────────────── Position ──────────────────────────────

        /// <summary>Screen X position (logical pixels, DPI-aware). PRD §7.2.</summary>
        [JsonPropertyName("x")]
        public double X { get; set; }

        /// <summary>Screen Y position (logical pixels, DPI-aware). PRD §7.2.</summary>
        [JsonPropertyName("y")]
        public double Y { get; set; }

        /// <summary>Current facing direction ("Left", "Right", "Forward").</summary>
        [JsonPropertyName("facing")]
        public string Facing { get; set; } = "Right";

        //───────────────────────── Stats ─────────────────────────────────

        /// <summary>Current level (XP threshold = 100 × level).</summary>
        [JsonPropertyName("level")]
        public int Level { get; set; } = 1;

        /// <summary>Accumulated experience points toward next level.</summary>
        [JsonPropertyName("xp")]
        public int XP { get; set; }

        /// <summary>Lifetime total times fed.</summary>
        [JsonPropertyName("totalFed")]
        public int TotalFed { get; set; }

        /// <summary>Lifetime total times petted.</summary>
        [JsonPropertyName("totalPetted")]
        public int TotalPetted { get; set; }

        /// <summary>Lifetime total play time in minutes.</summary>
        [JsonPropertyName("totalPlayTimeMinutes")]
        public int TotalPlayTimeMinutes { get; set; }

        //───────────────────────── Settings ──────────────────────────────

        /// <summary>Personality trait value (0.0–1.0 scale). PRD §5.</summary>
        [JsonPropertyName("personality")]
        public double Personality { get; set; } = 0.5;

        /// <summary>Master volume (0.0–1.0). PRD §8.</summary>
        [JsonPropertyName("volume")]
        public double Volume { get; set; } = 0.35;

        /// <summary>Sprite render scale (1.0 = native). PRD §7.1.</summary>
        [JsonPropertyName("scale")]
        public double Scale { get; set; } = 1.0;

        /// <summary>Whether sound effects are enabled.</summary>
        [JsonPropertyName("enableSound")]
        public bool EnableSound { get; set; } = true;

        /// <summary>Whether typing-awareness behavior is enabled. PRD §9.</summary>
        [JsonPropertyName("enableTypingAwareness")]
        public bool EnableTypingAwareness { get; set; } = true;

        /// <summary>Whether night-mode (dimmed/quiet) is enabled. PRD §9.</summary>
        [JsonPropertyName("enableNightMode")]
        public bool EnableNightMode { get; set; } = false;

        //───────────────────────── Pomodoro (Post-MVP Phase F) ──────────

        /// <summary>Pomodoro focus duration in minutes.</summary>
        [JsonPropertyName("pomodoroFocusMinutes")]
        public double PomodoroFocusMinutes { get; set; } = 25.0;

        /// <summary>Pomodoro short break duration in minutes.</summary>
        [JsonPropertyName("pomodoroShortBreakMinutes")]
        public double PomodoroShortBreakMinutes { get; set; } = 5.0;

        /// <summary>Pomodoro long break duration in minutes.</summary>
        [JsonPropertyName("pomodoroLongBreakMinutes")]
        public double PomodoroLongBreakMinutes { get; set; } = 15.0;

        /// <summary>Pomodoro rounds before long break.</summary>
        [JsonPropertyName("pomodoroRounds")]
        public int PomodoroRounds { get; set; } = 4;

        /// <summary>Whether pomodoro auto-continues to next round.</summary>
        [JsonPropertyName("pomodoroAutoContinue")]
        public bool PomodoroAutoContinue { get; set; } = true;

        //───────────────────────── Chat/LLM (Post-MVP Phase I) ───────────

        /// <summary>Chat API URL (OpenAI, Ollama, Groq, etc.).</summary>
        [JsonPropertyName("chatApiUrl")]
        public string ChatApiUrl { get; set; } = "http://localhost:11434/v1";

        /// <summary>Chat API key (empty for local Ollama).</summary>
        [JsonPropertyName("chatApiKey")]
        public string ChatApiKey { get; set; } = "";

        /// <summary>Chat model name.</summary>
        [JsonPropertyName("chatModel")]
        public string ChatModel { get; set; } = "llama3.2";

        /// <summary>Whether chat is enabled.</summary>
        [JsonPropertyName("chatEnabled")]
        public bool ChatEnabled { get; set; } = true;

        //───────────────────────── Meta ──────────────────────────────────

        /// <summary>UTC timestamp of the last successful save. Used for offline decay.</summary>
        [JsonPropertyName("lastSaved")]
        public DateTime LastSaved { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Returns a fresh <see cref="SaveData"/> with sensible defaults for a
        /// new Mochi (first launch or missing/corrupt save file).
        /// </summary>
        public static SaveData CreateDefault() => new()
        {
            Food = 80,
            Energy = 80,
            Happiness = 80,
            X = 0,
            Y = 0,
            Facing = "Right",
            Level = 1,
            XP = 0,
            TotalFed = 0,
            TotalPetted = 0,
            TotalPlayTimeMinutes = 0,
            Personality = 0.5,
            Volume = 0.35,
            Scale = 1.0,
            EnableSound = true,
            EnableTypingAwareness = true,
            EnableNightMode = false,
            LastSaved = DateTime.UtcNow,
        };
    }
}