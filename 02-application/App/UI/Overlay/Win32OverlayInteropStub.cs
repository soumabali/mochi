using System;
using Serilog;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// Minimal stub implementation of <see cref="IWin32OverlayInterop"/>.
    /// This keeps the overlay window compilable and runnable on Windows
    /// before T-002 delivers the full
    /// <c>MochiV2.Infrastructure.Window.Win32Interop</c>. All Win32 P/Invoke
    /// calls are guarded with <c>OperatingSystem.IsWindows()</c> so Linux
    /// builds and unit tests succeed.
    /// T-002 will replace this stub with the real interop class.
    /// </summary>
    internal sealed class Win32OverlayInteropStub : IWin32OverlayInterop
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext<Win32OverlayInteropStub>();

        /// <summary>
        /// Applies WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW |
        /// WS_EX_NOACTIVATE + HWND_TOPMOST to <paramref name="hwnd"/>.
        /// Guarded: compiles on Linux, runs only on Windows.
        /// </summary>
        public void ApplyOverlayStyles(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("ApplyOverlayStyles skipped (non-Windows).");
                return;
            }

            ApplyOverlayStylesCore(hwnd);
        }

        /// <summary>
        /// Toggles WS_EX_TRANSPARENT for click-through behavior.
        /// Guarded: compiles on Linux, runs only on Windows.
        /// </summary>
        public void SetClickThrough(IntPtr hwnd, bool enabled)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("SetClickThrough({Enabled}) skipped (non-Windows).", enabled);
                return;
            }

            SetClickThroughCore(hwnd, enabled);
        }

#if WINDOWS
        // ----- Win32 P/Invoke (only compiled on Windows) -----------------

        // Extended window styles ( WinUser.h )
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        // SetWindowPos HWND / flags
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private static void ApplyOverlayStylesCore(IntPtr hwnd)
        {
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            ex |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, ex);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            Logger.Information("Overlay Win32 styles applied to HWND {Hwnd}.", hwnd);
        }

        private static void SetClickThroughCore(IntPtr hwnd, bool enabled)
        {
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (enabled)
                ex |= WS_EX_TRANSPARENT;
            else
                ex &= ~WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, ex);
            Logger.Debug("Click-through {State} for HWND {Hwnd}.",
                enabled ? "ON" : "OFF", hwnd);
        }
#else
        // Non-Windows build: keep methods as no-ops so the class compiles.
        private static void ApplyOverlayStylesCore(IntPtr hwnd) { }
        private static void SetClickThroughCore(IntPtr hwnd, bool enabled) { }
#endif
    }
}