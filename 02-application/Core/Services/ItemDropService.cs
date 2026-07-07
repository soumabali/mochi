using System;
using System.Collections.Generic;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Item drop service. Post-MVP Phase G-2.
    /// Cat occasionally drops items (fish, coin, heart) as particle effects.
    /// User can click items to collect them — adds XP.
    /// No new sprites needed — uses particle vector drawing (same as hearts/Zzz).
    /// </summary>
    public sealed class ItemDropService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ItemDropService));

        private readonly ITimeProvider _timeProvider;
        private readonly IRandom _random;
        private double _lastDropTime;
        private double _dropInterval = 600; // 10 min average
        private double _dropChance = 0.3;   // 30% chance per interval check

        public double DropIntervalSeconds
        {
            get => _dropInterval;
            set => _dropInterval = Math.Max(60, value);
        }

        public bool Enabled { get; set; } = true;

        /// <summary>Fired when an item is dropped. Passes item type and position.</summary>
        public event Action<ItemType, double, double>? ItemDropped;

        /// <summary>Available item types (all vector-drawn, no sprites).</summary>
        public enum ItemType
        {
            Fish,
            Coin,
            Heart,
            Star
        }

        public ItemDropService(ITimeProvider timeProvider, IRandom random)
        {
            _timeProvider = timeProvider;
            _random = random;
            _lastDropTime = _timeProvider.GetElapsedSeconds();
        }

        /// <summary>Tick — call every frame. Cat position needed for drop location.</summary>
        public void Tick(double catX, double catY)
        {
            if (!Enabled) return;
            double now = _timeProvider.GetElapsedSeconds();
            if (now - _lastDropTime < _dropInterval) return;

            _lastDropTime = now;
            if (_random.NextDouble() > _dropChance) return;

            // Pick random item type
            var types = Enum.GetValues<ItemType>();
            var itemType = types[_random.Next(types.Length)];

            // Drop slightly below cat
            double dropX = catX + _random.Next(-20, 21);
            double dropY = catY + 30;

            ItemDropped?.Invoke(itemType, dropX, dropY);
            Logger.Information("Item dropped: {Type} at ({X:F1}, {Y:F1})", itemType, dropX, dropY);
        }

        /// <summary>XP value for each item type.</summary>
        public static int GetItemXP(ItemType type) => type switch
        {
            ItemType.Fish => 10,
            ItemType.Coin => 5,
            ItemType.Heart => 15,
            ItemType.Star => 20,
            _ => 1
        };
    }
}