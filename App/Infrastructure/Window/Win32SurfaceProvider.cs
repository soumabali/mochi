using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Infrastructure.Window
{
    /// <summary>
    /// Win32-backed implementation of <see cref="ISurfaceProvider"/>.
    /// Polls visible desktop windows via <see cref="Win32Interop.EnumerateVisibleWindows"/>
    /// every 5 seconds and on display settings changes. Caches the surface
    /// list and fires <see cref="SurfacesChanged"/> when the set changes.
    /// Post-MVP Phase E (PRD §5: window-top walking).
    /// </summary>
    public sealed class Win32SurfaceProvider : ISurfaceProvider, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(Win32SurfaceProvider));

        private readonly Timer _pollTimer;
        private WalkableSurface[] _cachedSurfaces = Array.Empty<WalkableSurface>();
        private bool _disposed;

        /// <summary>
        /// Raised when the surface set changes (window opened, closed, moved,
        /// or display settings changed). Subscribers re-query <see cref="GetSurfaces"/>.
        /// </summary>
        public event Action? SurfacesChanged;

        /// <summary>
        /// Creates the surface provider. On Windows, starts a 5-second poll
        /// timer. On non-Windows, returns empty surfaces (for compile/test).
        /// </summary>
        public Win32SurfaceProvider()
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("Win32SurfaceProvider: non-Windows, no polling.");
                return;
            }

#if WINDOWS
            // Initial enumeration
            RefreshSurfaces();

            // Poll every 5 seconds
            _pollTimer = new Timer(_ => RefreshSurfaces(), null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            // Subscribe to display settings changes
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
#endif
        }

        /// <summary>
        /// Current snapshot of walkable surfaces, ordered left-to-right by
        /// <see cref="WalkableSurface.Left"/>.
        /// </summary>
        public WalkableSurface[] GetSurfaces()
        {
            return _cachedSurfaces;
        }

        /// <summary>
        /// Force a refresh of the surface list. Called by the poll timer and
        /// display settings change handler.
        /// </summary>
        public void RefreshSurfaces()
        {
            if (!OperatingSystem.IsWindows())
            {
                _cachedSurfaces = Array.Empty<WalkableSurface>();
                return;
            }

#if WINDOWS
            var windowList = Win32Interop.EnumerateVisibleWindows();
            var surfaces = new List<WalkableSurface>();

            foreach (var (hwnd, left, top, right, _) in windowList)
            {
                // Surface = title bar top edge. Cat stands on top.
                surfaces.Add(new WalkableSurface(left, top, right, hwnd));
            }

            // Order left-to-right
            var newSurfaces = surfaces.OrderBy(s => s.Left).ToArray();

            if (!SurfacesEqual(_cachedSurfaces, newSurfaces))
            {
                _cachedSurfaces = newSurfaces;
                Logger.Debug("Surfaces changed: {Count} surfaces.", newSurfaces.Length);
                SurfacesChanged?.Invoke();
            }
#endif
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            RefreshSurfaces();
        }

        private static bool SurfacesEqual(WalkableSurface[] a, WalkableSurface[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

#if WINDOWS
            _pollTimer?.Dispose();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
#endif
        }
    }
}