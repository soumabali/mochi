using System;
using System.Collections.Generic;
using Serilog;

namespace MochiV2.UI.Settings
{
    /// <summary>
    /// In-memory model for all user-configurable settings shown in the
    /// Settings window (S-2). Mirrors PRD §9.2 / DESIGN S-2 sections.
    /// Persistence is handled by <c>SaveManager</c> (T-019); until then
    /// <see cref="SavePlaceholder"/> logs the payload so the UI contract
    /// is stable.
    /// </summary>
    public sealed class MochiSettings
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext<MochiSettings>();

        /// <summary>Default master volume (PRD: 0.35).</summary>
        public const double DefaultVolume = 0.35;

        /// <summary>Default sprite scale (1.0x).</summary>
        public const double DefaultScale = 1.0;

        /// <summary>Default personality coefficient (0.0 = Calm).</summary>
        public const double DefaultPersonality = 0.0;

        // --- General -------------------------------------------------------
        /// <summary>Launch Mochi when Windows starts.</summary>
        public bool StartWithWindows { get; set; }

        // --- Personality ---------------------------------------------------
        /// <summary>0.0 = Calm, 1.0 = Chaotic.</summary>
        public double Personality { get; set; } = DefaultPersonality;

        // --- Volume --------------------------------------------------------
        /// <summary>Master volume 0.0–1.0.</summary>
        public double MasterVolume { get; set; } = DefaultVolume;

        // --- Behavior toggles ---------------------------------------------
        /// <summary>Play sound effects.</summary>
        public bool EnableSound { get; set; } = true;

        /// <summary>React to user typing rate (typing awareness).</summary>
        public bool EnableTypingAwareness { get; set; } = true;

        /// <summary>Dim/quiet Mochi during night hours.</summary>
        public bool EnableNightMode { get; set; }

        // --- Scale ---------------------------------------------------------
        /// <summary>Sprite scale 0.5x–2.0x.</summary>
        public double SpriteScale { get; set; } = DefaultScale;

        // --- Language ------------------------------------------------------
        /// <summary>UI language code. EN only for MVP; ID structure ready.</summary>
        public string Language { get; set; } = "EN";

        /// <summary>
        /// Languages available in the dropdown. EN shipped for MVP; ID
        /// (Indonesian) placeholder so the structure is ready.
        /// </summary>
        public static readonly IReadOnlyList<LanguageOption> AvailableLanguages =
            new[]
            {
                new LanguageOption("EN", "English"),
                new LanguageOption("ID", "Bahasa Indonesia (soon)"),
            };

        /// <summary>
        /// Placeholder persistence hook. Real save goes through
        /// <c>SaveManager</c> (T-019). For now we deep-clone + log so the
        /// UI→model→persistence contract is exercised end-to-end.
        /// </summary>
        public void SavePlaceholder()
        {
            Logger.Information(
                "Settings.SavePlaceholder (T-019 not wired): " +
                "StartWithWindows={StartWithWindows} Personality={Personality:F2} " +
                "MasterVolume={MasterVolume:F2} EnableSound={EnableSound} " +
                "EnableTypingAwareness={EnableTypingAwareness} " +
                "EnableNightMode={EnableNightMode} SpriteScale={SpriteScale:F2} " +
                "Language={Language}",
                StartWithWindows, Personality, MasterVolume, EnableSound,
                EnableTypingAwareness, EnableNightMode, SpriteScale, Language);
        }

        /// <summary>Creates a deep copy so Cancel can discard edits.</summary>
        public MochiSettings Clone() => new()
        {
            StartWithWindows = StartWithWindows,
            Personality = Personality,
            MasterVolume = MasterVolume,
            EnableSound = EnableSound,
            EnableTypingAwareness = EnableTypingAwareness,
            EnableNightMode = EnableNightMode,
            SpriteScale = SpriteScale,
            Language = Language,
        };

        /// <summary>Copies values from <paramref name="other"/> onto this.</summary>
        public void CopyFrom(MochiSettings other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            StartWithWindows = other.StartWithWindows;
            Personality = other.Personality;
            MasterVolume = other.MasterVolume;
            EnableSound = other.EnableSound;
            EnableTypingAwareness = other.EnableTypingAwareness;
            EnableNightMode = other.EnableNightMode;
            SpriteScale = other.SpriteScale;
            Language = other.Language;
        }
    }

    /// <summary>A language dropdown entry (code + display name).</summary>
    public readonly record struct LanguageOption(string Code, string DisplayName);
}