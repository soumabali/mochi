using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MochiV2.Core.Models
{
    /// <summary>
    /// Animation playback mode for a sprite folder.
    /// Matches PRD §5 manifest "mode" field (camelCase JSON).
    /// </summary>
    public enum SpriteMode
    {
        /// <summary>Hold the first frame indefinitely (Idle).</summary>
        HoldFirstFrame,

        /// <summary>Play frames forward once, then stop.</summary>
        PlayOnce,

        /// <summary>Loop frames forward continuously.</summary>
        Loop,

        /// <summary>Play frames in reverse once, then stop.</summary>
        PlayOnceReversed,

        /// <summary>Play frames forward once, then hold the last frame.</summary>
        PlayOnceThenHoldLast
    }

    /// <summary>
    /// A single sprite entry in the asset manifest.
    /// Maps an FSM state name to a sprite folder and playback mode.
    /// </summary>
    public sealed class SpriteEntry
    {
        /// <summary>
        /// Relative path to the sprite folder (e.g. "Sprite/cat_blinking_left").
        /// The typo folder "Sprite/cat_surpised" is used as-is — never auto-corrected.
        /// </summary>
        [JsonPropertyName("folder")]
        public string Folder { get; set; } = string.Empty;

        /// <summary>
        /// Playback mode for this sprite.
        /// </summary>
        [JsonPropertyName("mode")]
        public SpriteMode Mode { get; set; }

        /// <summary>
        /// Playback speed multiplier. Defaults to 1.0 when omitted from JSON.
        /// </summary>
        [JsonPropertyName("speedMultiplier")]
        public double SpeedMultiplier { get; set; } = 1.0;
    }

    /// <summary>
    /// Sound playback settings. Matches PRD §5 "soundSettings" block.
    /// </summary>
    public sealed class SoundSettings
    {
        /// <summary>Default master volume (0.0–1.0). PRD default: 0.35.</summary>
        [JsonPropertyName("masterVolumeDefault")]
        public double MasterVolumeDefault { get; set; } = 0.35;

        /// <summary>Probability of playing a blink sound on blink (0.0–1.0).</summary>
        [JsonPropertyName("blinkSoundProbability")]
        public double BlinkSoundProbability { get; set; } = 0.1;

        /// <summary>Whether walk sounds loop while walking.</summary>
        [JsonPropertyName("walkSoundLoop")]
        public bool WalkSoundLoop { get; set; }

        /// <summary>Interval between walk sound triggers in milliseconds.</summary>
        [JsonPropertyName("walkSoundIntervalMs")]
        public int WalkSoundIntervalMs { get; set; } = 4000;

        /// <summary>Cooldown per sound type in milliseconds.</summary>
        [JsonPropertyName("cooldownPerSoundMs")]
        public int CooldownPerSoundMs { get; set; } = 8000;
    }

    /// <summary>
    /// The asset manifest — the sole mapping layer between FSM state names
    /// and physical sprite/sound file paths (PRD §5, AGENTS.md §3.3).
    /// </summary>
    public sealed class AssetManifest
    {
        /// <summary>
        /// Maps state name → sprite entry (folder + mode + speed).
        /// </summary>
        [JsonPropertyName("sprites")]
        public Dictionary<string, SpriteEntry> Sprites { get; set; } = new();

        /// <summary>
        /// Maps sound key → relative sound file path (e.g. "Sound/cat_meowing.wav").
        /// </summary>
        [JsonPropertyName("sounds")]
        public Dictionary<string, string> Sounds { get; set; } = new();

        /// <summary>
        /// FSM states that should never play a sound.
        /// </summary>
        [JsonPropertyName("statesWithoutSound")]
        public List<string> StatesWithoutSound { get; set; } = new();

        /// <summary>
        /// Global sound settings.
        /// </summary>
        [JsonPropertyName("soundSettings")]
        public SoundSettings SoundSettings { get; set; } = new();
    }
}