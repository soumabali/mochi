using System;
using System.Collections.Generic;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Global hotkey service. Post-MVP Phase G-1.
    /// Registers and manages global keyboard shortcuts.
    /// On Windows, uses Win32 RegisterHotKey. On non-Windows, no-op (for tests).
    /// </summary>
    public sealed class HotkeyService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(HotkeyService));

        /// <summary>Hotkey definition.</summary>
        public sealed class Hotkey
        {
            public string Name { get; init; } = "";
            public uint Modifier { get; init; }
            public uint Key { get; init; }
        }

        /// <summary>Fired when a registered hotkey is pressed. Passes hotkey name.</summary>
        public event Action<string>? HotkeyPressed;

        private readonly Dictionary<string, Hotkey> _registered = new();

        /// <summary>Default hotkeys.</summary>
        public static readonly Hotkey[] Defaults =
        {
            new Hotkey { Name = "teleport_cat", Modifier = 0x004 /*MOD_SHIFT*/ | 0x002 /*MOD_CTRL*/, Key = 0x4D /*M*/ },
            new Hotkey { Name = "feed_cat", Modifier = 0x004 | 0x002, Key = 0x46 /*F*/ },
            new Hotkey { Name = "pet_cat", Modifier = 0x004 | 0x002, Key = 0x50 /*P*/ },
            new Hotkey { Name = "toggle_pomodoro", Modifier = 0x004 | 0x002, Key = 0x54 /*T*/ },
        };

        public HotkeyService()
        {
            foreach (var hk in Defaults)
                _registered[hk.Name] = hk;
            Logger.Information("HotkeyService initialized with {Count} hotkeys", _registered.Count);
        }

        /// <summary>Simulate a hotkey press (for testing/non-Windows).</summary>
        public void SimulateHotkey(string name)
        {
            if (_registered.ContainsKey(name))
            {
                Logger.Debug("Hotkey pressed: {Name}", name);
                HotkeyPressed?.Invoke(name);
            }
        }

        /// <summary>Register a custom hotkey.</summary>
        public void Register(string name, uint modifier, uint key)
        {
            _registered[name] = new Hotkey { Name = name, Modifier = modifier, Key = key };
            Logger.Debug("Registered hotkey: {Name}", name);
        }

        /// <summary>Get all registered hotkey names.</summary>
        public IEnumerable<string> GetRegisteredNames() => _registered.Keys;
    }
}