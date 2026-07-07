using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Quick launcher service. Post-MVP Phase G-1.
    /// Manages a list of app shortcuts that can be launched from tray menu.
    /// </summary>
    public sealed class QuickLauncherService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(QuickLauncherService));

        /// <summary>Launcher entry: display name + command/path.</summary>
        public sealed class LaunchEntry
        {
            public string Name { get; init; } = "";
            public string Command { get; init; } = "";
            public string? Args { get; init; }
        }

        /// <summary>Default quick-launch entries.</summary>
        public List<LaunchEntry> Entries { get; set; } = new()
        {
            new LaunchEntry { Name = "VS Code", Command = "code" },
            new LaunchEntry { Name = "Browser", Command = "https://google.com" },
            new LaunchEntry { Name = "Terminal", Command = "wt" },
            new LaunchEntry { Name = "Notepad", Command = "notepad" },
            new LaunchEntry { Name = "Calculator", Command = "calc" },
        };

        /// <summary>Launch an app by entry index. Returns true on success.</summary>
        public bool Launch(int index)
        {
            if (index < 0 || index >= Entries.Count) return false;
            return Launch(Entries[index]);
        }

        /// <summary>Launch an app by entry. Returns true on success.</summary>
        public bool Launch(LaunchEntry entry)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = entry.Command,
                    UseShellExecute = true,
                };
                if (!string.IsNullOrEmpty(entry.Args))
                    psi.Arguments = entry.Args;

                Process.Start(psi);
                Logger.Information("Launched: {Name} ({Command})", entry.Name, entry.Command);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to launch {Name}: {Command}", entry.Name, entry.Command);
                return false;
            }
        }

        /// <summary>Launch by name (case-insensitive). Returns true on success.</summary>
        public bool LaunchByName(string name)
        {
            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                    return Launch(entry);
            }
            return false;
        }
    }
}