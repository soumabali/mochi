using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MochiV2.Core.Models;
using NAudio.Wave;
using Serilog;

namespace MochiV2.Infrastructure.Audio
{
    /// <summary>
    /// NAudio-backed sound playback for the Mochi v2 desktop pet. PRD §8.
    /// Resolves an <see cref="FSMState"/> to a sound clip via the
    /// <c>manifest.json</c> <see cref="AssetManifest.Sounds"/> mapping,
    /// honours <see cref="AssetManifest.StatesWithoutSound"/>, enforces a
    /// per-sound cooldown (default 8 s) to prevent audio spam, trims playback
    /// to 1.5–3 s, applies a configurable master volume (default 0.35), and
    /// gates the blink sound behind a 10 % probability roll.
    /// </summary>
    public sealed class AudioManager : IDisposable
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(AudioManager));

        // ── Defaults (PRD §8) ──────────────────────────────────────────
        private const float DefaultMasterVolume = 0.35f;
        private const int DefaultCooldownMs = 8000;
        private const double DefaultBlinkProbability = 0.1;
        private const double DefaultTrimSeconds = 2.0;
        private const double MinTrimSeconds = 1.5;
        private const double MaxTrimSeconds = 3.0;

        // ── Runtime state ──────────────────────────────────────────────
        private readonly Dictionary<string, DateTime> _lastPlayedTimes = new();
        private readonly Dictionary<string, double> _trimOverrides = new();
        private readonly Random _random;
        private readonly object _playLock = new();

        private IWavePlayer? _waveOut;
        private AudioFileReader? _reader;
        private Timer? _stopTimer;

        private float _masterVolume = DefaultMasterVolume;
        private int _cooldownMs = DefaultCooldownMs;
        private double _blinkProbability = DefaultBlinkProbability;
        private HashSet<string> _statesWithoutSound = new();
        private Dictionary<string, string> _sounds = new();
        private string _assetsBasePath = string.Empty;
        private bool _disposed;

        /// <summary>
        /// Create an <see cref="AudioManager"/> seeded with settings drawn
        /// from <paramref name="manifest"/> (if non-null). Passing null is
        /// legal; call <see cref="Configure"/> later before playing.
        /// </summary>
        /// <param name="manifest">Parsed manifest (may be null).</param>
        /// <param name="assetsBasePath">Absolute path to the Assets/ folder.</param>
        /// <param name="random">Injectable RNG (for deterministic tests).</param>
        public AudioManager(AssetManifest? manifest = null,
                            string? assetsBasePath = null,
                            Random? random = null)
        {
            _random = random ?? new Random();
            if (manifest is not null && assetsBasePath is not null)
                Configure(manifest, assetsBasePath);
        }

        // ── Configuration ──────────────────────────────────────────────

        /// <summary>
        /// (Re)load sound mappings and settings from a parsed manifest.
        /// </summary>
        /// <param name="manifest">Manifest with sounds + soundSettings.</param>
        /// <param name="assetsBasePath">Absolute path to Assets/ folder.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="manifest"/> is null.
        /// </exception>
        public void Configure(AssetManifest manifest, string assetsBasePath)
        {
            if (manifest is null) throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrWhiteSpace(assetsBasePath))
                throw new ArgumentException("assetsBasePath must be non-empty", nameof(assetsBasePath));

            _assetsBasePath = assetsBasePath;
            _sounds = manifest.Sounds ?? new Dictionary<string, string>();
            _statesWithoutSound = new HashSet<string>(
                manifest.StatesWithoutSound ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            var s = manifest.SoundSettings ?? new SoundSettings();
            _masterVolume = ClampVolume((float)s.MasterVolumeDefault);
            _cooldownMs = Math.Max(0, s.CooldownPerSoundMs);
            _blinkProbability = ClampProbability(s.BlinkSoundProbability);

            Logger.Information(
                "AudioManager configured: {SoundCount} sounds, {NoSoundCount} silent states, " +
                "vol={Volume:F2}, cooldown={CooldownMs}ms, blinkProb={BlinkProb:F2}",
                _sounds.Count, _statesWithoutSound.Count, _masterVolume, _cooldownMs, _blinkProbability);
        }

        /// <summary>Master volume 0.0–1.0 (clamped). Settable at runtime.</summary>
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = ClampVolume(value);
                lock (_playLock)
                {
                    if (_waveOut is not null)
                        _waveOut.Volume = _masterVolume;
                }
                Logger.Debug("Master volume set to {Volume:F2}", _masterVolume);
            }
        }

        /// <summary>Per-sound cooldown in milliseconds (≥ 0).</summary>
        public int CooldownMs
        {
            get => _cooldownMs;
            set => _cooldownMs = Math.Max(0, value);
        }

        /// <summary>
        /// Override the trim duration for a specific sound key
        /// (clamped to <see cref="MinTrimSeconds"/>–<see cref="MaxTrimSeconds"/>).
        /// </summary>
        public void SetTrimDuration(string soundKey, double seconds)
        {
            if (string.IsNullOrWhiteSpace(soundKey)) return;
            _trimOverrides[soundKey] = ClampTrim(seconds);
        }

        /// <summary>Current trim seconds for <paramref name="soundKey"/>.</summary>
        public double GetTrimDuration(string soundKey)
        {
            return _trimOverrides.TryGetValue(soundKey, out var t)
                ? t : DefaultTrimSeconds;
        }

        // ── Playback ───────────────────────────────────────────────────

        /// <summary>
        /// Attempt to play the sound associated with <paramref name="state"/>.
        /// Checks (in order): statesWithoutSound, per-sound cooldown, and —
        /// for <see cref="FSMState.Blink"/> — the configured probability.
        /// Returns true if a clip was actually queued for playback.
        /// </summary>
        /// <param name="state">Current FSM state.</param>
        /// <returns>True if sound was played; false if suppressed or unavailable.</returns>
        public bool Play(FSMState state)
        {
            if (_disposed) return false;

            string stateName = state.ToString();

            // 1. statesWithoutSound — manifest lists sprite-alias names
            //    (e.g. IdleLeft, RunVar1). Check both the enum name and the
            //    canonical alias set.
            if (IsSilentState(state, stateName))
            {
                Logger.Debug("Sound suppressed for silent state {State}", stateName);
                return false;
            }

            // 2. Resolve sound key from the manifest "sounds" mapping.
            string? soundKey = ResolveSoundKey(state);
            if (soundKey is null || !_sounds.TryGetValue(soundKey!, out var relPath))
            {
                Logger.Debug("No sound mapping for state {State} (key={Key})", stateName, soundKey);
                return false;
            }

            // 3. Blink probability gate.
            if (state == FSMState.Blink)
            {
                double roll = _random.NextDouble();
                if (roll >= _blinkProbability)
                {
                    Logger.Debug("Blink sound skipped (roll={Roll:F2} ≥ prob={Prob:F2})", roll, _blinkProbability);
                    return false;
                }
            }

            // 4. Per-sound cooldown.
            if (IsOnCooldown(soundKey))
            {
                Logger.Debug("Sound {Key} on cooldown ({CooldownMs}ms)", soundKey, _cooldownMs);
                return false;
            }

            // 5. Play.
            string fullPath = Path.Combine(_assetsBasePath, relPath);
            return PlayFile(soundKey, fullPath);
        }

        /// <summary>
        /// Play a sound file directly by absolute path, bypassing the
        /// state/cooldown logic. Used for ad-hoc one-shots.
        /// </summary>
        public bool PlayRaw(string soundKey, string fullPath)
        {
            if (_disposed) return false;
            if (string.IsNullOrWhiteSpace(soundKey) || !File.Exists(fullPath))
            {
                Logger.Warning("PlayRaw: missing file {Path}", fullPath);
                return false;
            }
            return PlayFile(soundKey, fullPath);
        }

        /// <summary>
        /// Core playback routine. Loads the file with NAudio, sets volume,
        /// starts playback, and schedules an auto-stop after the trim window.
        /// Thread-safe via <see cref="_playLock"/>.
        /// </summary>
        private bool PlayFile(string soundKey, string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Logger.Warning("Sound file missing: {Path}", fullPath);
                return false;
            }

            try
            {
                lock (_playLock)
                {
                    // Tear down any previous playback.
                    StopInternal();

                    _reader = new AudioFileReader(fullPath);
                    // Master volume applied at the wave-out device level.
                    _waveOut = CreateWavePlayer();
                    _waveOut.Volume = _masterVolume;
                    _waveOut.Init(_reader);

                    // Trim: schedule a stop after min(trim, fileDuration).
                    double trim = GetTrimDuration(soundKey);
                    double fileDuration = _reader.TotalTime.TotalSeconds;
                    double effectiveTrim = Math.Min(trim, fileDuration);
                    if (effectiveTrim < MinTrimSeconds) effectiveTrim = Math.Min(fileDuration, MinTrimSeconds);

                    _waveOut.PlaybackStopped += (_, _) =>
                    {
                        Logger.Debug("Sound {Key} finished naturally", soundKey);
                    };

                    _waveOut.Play();
                    _lastPlayedTimes[soundKey] = DateTime.UtcNow;

                    // Auto-stop timer enforces the trim window even if the
                    // clip is longer than `effectiveTrim`.
                    int stopAfterMs = (int)Math.Ceiling(effectiveTrim * 1000);
                    _stopTimer?.Dispose();
                    _stopTimer = new Timer(_ => StopInternal(), null, stopAfterMs, Timeout.Infinite);

                    Logger.Information(
                        "Playing sound {Key} from {Path} (trim={Trim:F2}s, vol={Vol:F2})",
                        soundKey, fullPath, effectiveTrim, _masterVolume);
                }
                return true;
            }
            catch (Exception ex)
            {
                // NAudio depends on a working audio subsystem; on headless
                // Linux this will fail. Log loud but don't crash the pet.
                Logger.Warning(ex, "Failed to play sound {Key} from {Path}", soundKey, fullPath);
                lock (_playLock) { StopInternal(); }
                return false;
            }
        }

        /// <summary>
        /// Create the underlying NAudio wave-out device. Wrapped so the
        /// platform-specific failure (no audio device on Linux/CI) is caught
        /// by the caller.
        /// </summary>
        private static IWavePlayer CreateWavePlayer()
        {
            // WaveOutEvent uses a background thread (no STA requirement),
            // making it suitable for a desktop pet that also runs a WPF UI.
            return new WaveOutEvent();
        }

        /// <summary>Stop and dispose the active reader + wave-out device.</summary>
        private void StopInternal()
        {
            try { _stopTimer?.Dispose(); } catch { /* ignored */ }
            _stopTimer = null;

            try
            {
                if (_waveOut is not null)
                {
                    if (_waveOut.PlaybackState != PlaybackState.Stopped)
                        _waveOut.Stop();
                    _waveOut.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error disposing wave-out device");
            }
            finally
            {
                _waveOut = null;
            }

            try
            {
                _reader?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error disposing audio reader");
            }
            finally
            {
                _reader = null;
            }
        }

        /// <summary>Immediately stop any in-flight playback.</summary>
        public void Stop()
        {
            lock (_playLock) { StopInternal(); }
        }

        // ── Cooldown ───────────────────────────────────────────────────

        /// <summary>True if <paramref name="soundKey"/> is within its cooldown window.</summary>
        public bool IsOnCooldown(string soundKey)
        {
            if (_cooldownMs <= 0) return false;
            if (!_lastPlayedTimes.TryGetValue(soundKey, out var last)) return false;
            return (DateTime.UtcNow - last).TotalMilliseconds < _cooldownMs;
        }

        /// <summary>Remaining cooldown milliseconds for a key (0 if none).</summary>
        public int RemainingCooldownMs(string soundKey)
        {
            if (_cooldownMs <= 0) return 0;
            if (!_lastPlayedTimes.TryGetValue(soundKey, out var last)) return 0;
            double elapsed = (DateTime.UtcNow - last).TotalMilliseconds;
            if (elapsed >= _cooldownMs) return 0;
            return (int)(_cooldownMs - elapsed);
        }

        // ── State → sound-key resolution ───────────────────────────────

        /// <summary>
        /// Map an <see cref="FSMState"/> to a manifest sound key. The
        /// manifest "sounds" dict uses canonical keys (Meow, Walk, Scratch…)
        /// while the enum has directional variants (MeowLeft/MeowRight,
        /// WalkLeft/WalkRight, …). This collapses those aliases.
        /// </summary>
        private static string? ResolveSoundKey(FSMState state) => state switch
        {
            FSMState.Angry => "Angry",
            FSMState.Blink => "Blink",
            FSMState.Playful => "Playful",
            FSMState.MeowLeft => "Meow",
            FSMState.MeowRight => "Meow",
            FSMState.ScratchLeft => "Scratch",
            FSMState.ScratchRight => "Scratch",
            FSMState.WalkLeft => "Walk",
            FSMState.WalkRight => "Walk",
            FSMState.WalkForward => "WalkForward",
            FSMState.Sleeping => "Sleep",
            FSMState.Surprised => "Surprised",
            FSMState.HungryStandard => "Begging",
            FSMState.HungryCritical => "Begging",
            FSMState.Eating => "Eating",
            _ => null
        };

        /// <summary>
        /// Check whether a state should produce no sound. The manifest lists
        /// sprite-alias names (IdleLeft, RunVar1, …) in statesWithoutSound,
        /// so we test both the canonical enum name and known aliases.
        /// </summary>
        private bool IsSilentState(FSMState state, string stateName)
        {
            if (_statesWithoutSound.Count == 0) return false;
            if (_statesWithoutSound.Contains(stateName)) return true;

            // Canonical aliases: Idle↔IdleLeft/IdleRight, Fall↔FallVar1/2, etc.
            switch (state)
            {
                case FSMState.Idle:
                    return _statesWithoutSound.Contains("IdleLeft") ||
                           _statesWithoutSound.Contains("IdleRight");
                case FSMState.Fall:
                    return _statesWithoutSound.Contains("FallVar1") ||
                           _statesWithoutSound.Contains("FallVar2");
                case FSMState.WakeUp:
                    return _statesWithoutSound.Contains("WakeUp");
                default:
                    return false;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static float ClampVolume(float v) => Math.Clamp(v, 0f, 1f);

        private static double ClampProbability(double p) => Math.Clamp(p, 0.0, 1.0);

        private static double ClampTrim(double s) => Math.Clamp(s, MinTrimSeconds, MaxTrimSeconds);

        // ── IDisposable ────────────────────────────────────────────────

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_playLock) { StopInternal(); }
            Logger.Debug("AudioManager disposed");
        }
    }
}