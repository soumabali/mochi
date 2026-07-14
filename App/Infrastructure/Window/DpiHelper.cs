using System;
using System.Runtime.Versioning;
using Serilog;

namespace MochiV2.Infrastructure.Window
{
    /// <summary>
    /// PerMonitorV2 DPI utilities (PRD §5: Multi-monitor DPI awareness).
    /// The app.manifest already declares
    /// <c>&lt;dpiAwareness&gt;PerMonitorV2&lt;/dpiAwareness&gt;</c> (T-001);
    /// this class provides per-window DPI queries and scale factors for
    /// SkiaSharp rendering and layout.
    /// </summary>
    public sealed class DpiHelper
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(DpiHelper));

        /// <summary>Standard Windows DPI at 100% scale.</summary>
        public const int BaseDpi = 96;

        private uint _dpi = BaseDpi;

        /// <summary>
        /// Current DPI value (96, 120, 144, 168, 192, …). Defaults to 96
        /// (100% scale) on non-Windows platforms.
        /// </summary>
        public uint Dpi => _dpi;

        /// <summary>
        /// Horizontal scale factor relative to 96-DPI baseline.
        /// (e.g. 1.5 at 144 DPI / 150% scale.)
        /// </summary>
        public double ScaleX => (double)_dpi / BaseDpi;

        /// <summary>
        /// Vertical scale factor relative to 96-DPI baseline.
        /// PerMonitorV2 uses uniform scaling so this equals <see cref="ScaleX"/>.
        /// </summary>
        public double ScaleY => ScaleX;

        /// <summary>
        /// Queries the DPI for <paramref name="hwnd"/> and caches it.
        /// On non-Windows platforms the DPI remains at <see cref="BaseDpi"/>.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <returns>The DPI value for the window.</returns>
        [SupportedOSPlatform("windows")]
        public uint GetDpiForWindow(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("GetDpiForWindow skipped (non-Windows), using {Dpi}.", BaseDpi);
                _dpi = BaseDpi;
                return _dpi;
            }

#if WINDOWS
            _dpi = GetDpiForWindowCore(hwnd);
            Logger.Debug("DPI for HWND {Hwnd}: {Dpi} ({Scale:F2}x).", hwnd, _dpi, ScaleX);
            return _dpi;
#else
            _dpi = BaseDpi;
            return _dpi;
#endif
        }

        /// <summary>
        /// Convenience static method: returns the DPI for a window without
        /// creating an instance.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static uint QueryDpiForWindow(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows())
                return BaseDpi;
#if WINDOWS
            return GetDpiForWindowCore(hwnd);
#else
            return BaseDpi;
#endif
        }

#if WINDOWS
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, EntryPoint = "GetDpiForWindow")]
        [SupportedOSPlatform("windows")]
        private static extern uint GetDpiForWindowNative(IntPtr hwnd);

        private static uint GetDpiForWindowCore(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return BaseDpi;
            try
            {
                uint dpi = GetDpiForWindowNative(hwnd);
                // GetDpiForWindow returns 0 on failure; fall back to 96.
                return dpi == 0 ? BaseDpi : dpi;
            }
            catch (Exception ex) when (ex is not PlatformNotSupportedException)
            {
                Logger.Warning(ex, "GetDpiForWindow failed, falling back to {Dpi}.", BaseDpi);
                return BaseDpi;
            }
        }
#endif
    }
}