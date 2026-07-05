using System;
using System.Runtime.Versioning;
using Serilog;

namespace MochiV2.Infrastructure.Window
{
    /// <summary>
    /// Detects whether the current foreground window occupies the entire
    /// monitor — i.e. a fullscreen game or video player is active.
    ///
    /// Used by the Fullscreen overlay state (DESIGN §2) to auto-hide Mochi
    /// and by event A-12: FullscreenDetected / FullscreenExited (PRD §8).
    /// </summary>
    public sealed class FullscreenDetector
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(FullscreenDetector));

        // Allow a tolerance (in pixels) so maximized-but-not-fullscreen
        // windows with a thin taskbar border are not misclassified.
        private const int TolerancePixels = 8;

        /// <summary>
        /// Determines whether the foreground window is fullscreen by comparing
        /// its bounding rectangle to the monitor rectangle.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the foreground window covers its monitor (within
        /// <see cref="TolerancePixels"/>); <c>false</c> otherwise or when not
        /// on Windows.
        /// </returns>
        [SupportedOSPlatform("windows")]
        public bool IsForegroundFullscreen()
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("IsForegroundFullscreen skipped (non-Windows).");
                return false;
            }

            IntPtr fg = Win32Interop.GetForegroundHwnd();
            if (fg == IntPtr.Zero)
            {
                Logger.Debug("IsForegroundFullscreen: no foreground window.");
                return false;
            }

            if (!Win32Interop.TryGetWindowRect(fg, out var winRect))
            {
                Logger.Debug("IsForegroundFullscreen: GetWindowRect failed.");
                return false;
            }

            if (!Win32Interop.TryGetMonitorRect(fg, out var monRect))
            {
                Logger.Debug("IsForegroundFullscreen: GetMonitorRect failed.");
                return false;
            }

            return IsRectFullscreen(winRect, monRect, TolerancePixels);
        }

        /// <summary>
        /// Pure logic: checks whether a window rect covers a monitor rect
        /// within the given tolerance.  Exposed for unit testing.
        /// </summary>
        /// <param name="winRect">(left, top, right, bottom) of the window.</param>
        /// <param name="monRect">(left, top, right, bottom) of the monitor.</param>
        /// <param name="tolerance">Pixel tolerance for border/taskbar offsets.</param>
        /// <returns><c>true</c> if the window covers the monitor.</returns>
        public static bool IsRectFullscreen(
            (int Left, int Top, int Right, int Bottom) winRect,
            (int Left, int Top, int Right, int Bottom) monRect,
            int tolerance)
        {
            int winWidth = winRect.Right - winRect.Left;
            int winHeight = winRect.Bottom - winRect.Top;
            int monWidth = monRect.Right - monRect.Left;
            int monHeight = monRect.Bottom - monRect.Top;

            // The window must be approximately the same size as the monitor.
            if (Math.Abs(winWidth - monWidth) > tolerance)
                return false;
            if (Math.Abs(winHeight - monHeight) > tolerance)
                return false;

            // The window origin must be approximately at the monitor origin.
            if (Math.Abs(winRect.Left - monRect.Left) > tolerance)
                return false;
            if (Math.Abs(winRect.Top - monRect.Top) > tolerance)
                return false;

            return true;
        }
    }
}