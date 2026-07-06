using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Infrastructure.Storage
{
    /// <summary>
    /// Debounced save/load manager for Mochi v2 persistent state. PRD §12.
    /// Persists <see cref="SaveData"/> to <c>%APPDATA%/NekoCompanion/save.json</c>
    /// using <see cref="System.Text.Json"/>. Multiple <see cref="NotifyChanged"/>
    /// calls within the debounce window (5 s) are coalesced into a single write.
    /// On <see cref="Load"/>, applies offline decay (capped, never below 20 and
    /// never critical) based on elapsed wall-clock time since <see cref="SaveData.LastSaved"/>.
    /// Publishes <see cref="LevelUpEvent"/> via <see cref="EventBus"/> when XP
    /// crosses the level threshold (100 × current level).
    /// </summary>
    public sealed class SaveManager : IDisposable
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(SaveManager));

        //── Paths / serialization ──────────────────────────────────────

        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NekoCompanion");

        private static readonly string SavePath = Path.Combine(AppDataDir, "save.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        //── Debounce timing ────────────────────────────────────────────

        /// <summary>Debounce delay before a pending save is flushed to disk.</summary>
        private const int SaveDebounceMs = 5000;

        //── Offline decay constants (PRD §6.4) ─────────────────────────

        /// <summary>Needs are never decayed below this floor during offline period.</summary>
        private const int DecayFloor = 20;

        /// <summary>Threshold for the welcome-back dialog (hours offline).</summary>
        private const double WelcomeBackHours = 24.0;

        // Food: -1 per 4 min (240 s) → per-hour rate = 3600/240 = 15
        private const double FoodDecayPerHour = 15.0;
        // Energy: -1 per 6 min (360 s) → per-hour rate = 3600/360 = 10
        private const double EnergyDecayPerHour = 10.0;
        // Happiness: -1 per 5 min (300 s) → per-hour rate = 3600/300 = 12
        private const double HappinessDecayPerHour = 12.0;

        //── Runtime state ──────────────────────────────────────────────

        private readonly EventBus _bus;
        private readonly Timer? _debounceTimer;
        private readonly object _saveLock = new();

        private SaveData _data;
        private bool _disposed;
        private bool _savePending;

        /// <summary>
        /// Construct the save manager. Loads existing save (or default) and
        /// applies offline decay immediately.
        /// </summary>
        /// <param name="bus">Event bus for publishing <see cref="LevelUpEvent"/>.</param>
        public SaveManager(EventBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _data = LoadFromDisk();
            _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        //───────────────────────── Public API ───────────────────────────

        /// <summary>
        /// Current in-memory save data (post offline-decay on load). Mutations
        /// should be followed by <see cref="NotifyChanged"/> to persist.
        /// </summary>
        public SaveData Data => _data;

        /// <summary>
        /// True when the elapsed time since <see cref="SaveData.LastSaved"/>
        /// exceeds 24 hours, indicating a welcome-back dialog should be shown.
        /// </summary>
        public bool WelcomeBackNeeded =>
            (DateTime.UtcNow - _data.LastSaved).TotalHours > WelcomeBackHours;

        /// <summary>
        /// Load <see cref="SaveData"/> from disk. If the file is missing or
        /// corrupt, returns <see cref="SaveData.CreateDefault"/>. Applies
        /// offline decay (capped at <see cref="DecayFloor"/>) before returning.
        /// </summary>
        public SaveData Load()
        {
            _data = LoadFromDisk();
            ApplyOfflineDecay();
            return _data;
        }

        /// <summary>
        /// Notify that the in-memory <see cref="Data"/> has been mutated and
        /// should be persisted. Coalesces calls within a 5-second window so
        /// rapid mutations trigger a single disk write.
        /// </summary>
        public void NotifyChanged()
        {
            if (_disposed)
            {
                Logger.Warning("NotifyChanged called after dispose; ignoring.");
                return;
            }

            lock (_saveLock)
            {
                _savePending = true;
            }

            // Reset the debounce timer — coalesces repeated calls.
            _debounceTimer?.Change(SaveDebounceMs, Timeout.Infinite);
            Logger.Debug("Save debounced; will flush in {Ms} ms.", SaveDebounceMs);
        }

        /// <summary>
        /// Add experience points and level up (publishing
        /// <see cref="LevelUpEvent"/>) when XP crosses the threshold
        /// (100 × current level). Triggers a debounced save.
        /// </summary>
        /// <param name="amount">XP to add (clamped to non-negative).</param>
        public void AddXP(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _data.XP += amount;
            Logger.Information("Added {Amount} XP (total {Xp}).", amount, _data.XP);

            // Loop: multiple level-ups possible with a large single award.
            while (_data.XP >= LevelThreshold(_data.Level))
            {
                _data.XP -= LevelThreshold(_data.Level);
                _data.Level++;
                Logger.Information("Level up! New level {Level}.", _data.Level);
                _bus.Publish(new LevelUpEvent(_data.Level, _data.XP));
            }

            NotifyChanged();
        }

        /// <summary>
        /// Flush any pending save immediately (bypassing the debounce window).
        /// Safe to call from the debounce timer callback or explicitly on shutdown.
        /// </summary>
        public void Flush()
        {
            SaveToDisk();
        }

        //───────────────────────── Internals ────────────────────────────

        /// <summary>XP required to advance from <paramref name="level"/> to the next.</summary>
        private static int LevelThreshold(int level) => 100 * Math.Max(1, level);

        /// <summary>Timer callback: flush pending save.</summary>
        private void OnDebounceElapsed(object? state)
        {
            bool pending;
            lock (_saveLock)
            {
                pending = _savePending;
                _savePending = false;
            }

            if (pending)
            {
                SaveToDisk();
            }
        }

        /// <summary>
        /// Read and deserialize <see cref="SaveData"/> from <see cref="SavePath"/>.
        /// Returns defaults on missing file or deserialization error.
        /// </summary>
        private static SaveData LoadFromDisk()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    Logger.Information("Save file not found at {Path}; using defaults.", SavePath);
                    return SaveData.CreateDefault();
                }

                var json = File.ReadAllText(SavePath);
                var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
                if (data is null)
                {
                    Logger.Warning("Save file deserialized to null; using defaults.");
                    return SaveData.CreateDefault();
                }

                Logger.Information("Save loaded from {Path}.", SavePath);
                return data;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load save from {Path}; using defaults.", SavePath);
                return SaveData.CreateDefault();
            }
        }

        /// <summary>
        /// Serialize current <see cref="_data"/> to <see cref="SavePath"/>,
        /// creating <see cref="AppDataDir"/> if necessary. Updates
        /// <see cref="SaveData.LastSaved"/> to current UTC.
        /// </summary>
        private void SaveToDisk()
        {
            try
            {
                _data.LastSaved = DateTime.UtcNow;
                Directory.CreateDirectory(AppDataDir);
                var json = JsonSerializer.Serialize(_data, JsonOptions);
                File.WriteAllText(SavePath, json);
                Logger.Information("Save written to {Path}.", SavePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to write save to {Path}.", SavePath);
            }
        }

        /// <summary>
        /// Apply offline decay to Food, Energy, and Happiness based on elapsed
        /// time since <see cref="SaveData.LastSaved"/>. Needs are clamped to
        /// <see cref="DecayFloor"/> (never below 20, never critical).
        /// </summary>
        private void ApplyOfflineDecay()
        {
            var elapsed = DateTime.UtcNow - _data.LastSaved;
            if (elapsed <= TimeSpan.Zero)
            {
                return;
            }

            var hours = elapsed.TotalHours;
            Logger.Information(
                "Applying offline decay for {Hours:F2} h since {LastSaved}.",
                hours,
                _data.LastSaved);

            _data.Food = ClampDecay(_data.Food, hours * FoodDecayPerHour);
            _data.Energy = ClampDecay(_data.Energy, hours * EnergyDecayPerHour);
            _data.Happiness = ClampDecay(_data.Happiness, hours * HappinessDecayPerHour);

            Logger.Information(
                "Decay applied: Food={Food}, Energy={Energy}, Happiness={Happiness}.",
                _data.Food,
                _data.Energy,
                _data.Happiness);
        }

        /// <summary>
        /// Subtract <paramref name="decay"/> from <paramref name="current"/>
        /// and clamp to [<see cref="DecayFloor"/>, 100]. Never goes below
        /// <see cref="DecayFloor"/> (20) so Mochi is never critical on return.
        /// </summary>
        private static int ClampDecay(double current, double decay)
        {
            var newValue = (int)Math.Round(current - decay);
            return Math.Clamp(newValue, DecayFloor, 100);
        }

        //───────────────────────── IDisposable ──────────────────────────

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Flush any pending save before releasing resources.
            try
            {
                _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                Flush();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during SaveManager dispose flush.");
            }
            finally
            {
                _debounceTimer?.Dispose();
            }
        }
    }
}