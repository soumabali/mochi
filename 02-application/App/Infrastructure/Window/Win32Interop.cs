using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;

namespace MochiV2.Infrastructure.Window
{
    /// <summary>
    /// Win32 P/Invoke declarations and high-level wrapper methods for the
    /// transparent overlay window (T-002).
    ///
    /// All P/Invoke signatures match the Windows SDK (WinUser.h, WinDef.h).
    /// Declarations are compiled inside <c>#if WINDOWS</c> so the
    /// <c>net9.0-windows</c> TFM includes them on every build host; runtime
    /// calls are guarded by <see cref="OperatingSystem.IsWindows"/> so that
    /// unit tests and Linux builds never execute native code.
    /// </summary>
    public static class Win32Interop
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(Win32Interop));

        // ------------------------------------------------------------------
        //  Extended window-style constants  ( WinUser.h )
        //  Values match PRD §9 / DESIGN §2 Window States.
        // ------------------------------------------------------------------

        /// <summary>Layered window — required for per-pixel alpha / transparency.</summary>
        public const int WS_EX_LAYERED = 0x00080000;

        /// <summary>Window does not receive mouse input (click-through).</summary>
        public const int WS_EX_TRANSPARENT = 0x00000020;

        /// <summary>Tool window — hidden from Alt-Tab and the taskbar.</summary>
        public const int WS_EX_TOOLWINDOW = 0x00000080;

        /// <summary>Window cannot be activated by a click or Alt-Tab.</summary>
        public const int WS_EX_NOACTIVATE = 0x08000000;

        /// <summary>Force a top-level taskbar button (removed for overlay).</summary>
        public const int WS_EX_APPWINDOW = 0x00040000;

        /// <summary>Offset to retrieve/set extended window styles via Get/SetWindowLong.</summary>
        public const int GWL_EXSTYLE = -20;

        // ------------------------------------------------------------------
        //  SetWindowPos HWND special handles  ( WinUser.h )
        // ------------------------------------------------------------------

        /// <summary>Places the window above all non-topmost windows.</summary>
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        /// <summary>Places the window above all windows (including topmost).</summary>
        public static readonly IntPtr HWND_TOP = new IntPtr(0);

        // ------------------------------------------------------------------
        //  SetWindowPos flags  ( WinUser.h )
        // ------------------------------------------------------------------

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_HIDEWINDOW = 0x0080;

        // ------------------------------------------------------------------
        //  Monitor-from-window flags  ( WinUser.h )
        // ------------------------------------------------------------------

        /// <summary>Return primary monitor if window doesn't intersect any.</summary>
        public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

        /// <summary>Return nearest monitor if window doesn't intersect any.</summary>
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // ==================================================================
        //  P/Invoke declarations  (compiled on WINDOWS TFM)
        // ==================================================================
#if WINDOWS
        // --- GetWindowLong / SetWindowLong -------------------------------
        //  Use IntPtr-based overloads so the same code works on 32/64-bit.
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // --- SetWindowPos ------------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        // --- GetCursorPos ------------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // --- GetForegroundWindow -----------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr GetForegroundWindow();

        // --- GetWindowRect -----------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // --- MonitorFromWindow -------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        // --- GetMonitorInfo ----------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        // --- GetDpiForWindow  (PerMonitorV2, Windows 10 1607+) -----------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern uint GetDpiForWindowNative(IntPtr hwnd);

        // --- Structs -----------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
#endif

        // ==================================================================
        //  High-level wrapper methods
        // ==================================================================

        /// <summary>
        /// Reads the current extended window style flags.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <returns>Extended style bit-field, or 0 on non-Windows.</returns>
        [SupportedOSPlatform("windows")]
        public static int GetExtendedStyle(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("GetExtendedStyle skipped (non-Windows).");
                return 0;
            }
#if WINDOWS
            return unchecked((int)GetWindowLongPtr(hwnd, GWL_EXSTYLE));
#else
            return 0;
#endif
        }

        /// <summary>
        /// Sets the extended window style flags.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="style">New extended style bit-field.</param>
        [SupportedOSPlatform("windows")]
        public static void SetExtendedStyle(IntPtr hwnd, int style)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("SetExtendedStyle skipped (non-Windows).");
                return;
            }
#if WINDOWS
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
#endif
        }

        /// <summary>
        /// Toggles the <see cref="WS_EX_TRANSPARENT"/> (click-through) extended
        /// style.  When <paramref name="enabled"/> is <c>true</c> the window is
        /// click-through (Roam state); when <c>false</c> the window receives
        /// mouse input (Interact / Drag states).  See DESIGN §2.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="enabled">True to enable click-through, false to disable.</param>
        [SupportedOSPlatform("windows")]
        public static void SetWindowClickThrough(IntPtr hwnd, bool enabled)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("SetWindowClickThrough({Enabled}) skipped (non-Windows).", enabled);
                return;
            }
#if WINDOWS
            int ex = unchecked((int)GetWindowLongPtr(hwnd, GWL_EXSTYLE));
            if (enabled)
                ex |= WS_EX_TRANSPARENT;
            else
                ex &= ~WS_EX_TRANSPARENT;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
            Logger.Debug("Click-through {State} HWND {Hwnd}.", enabled ? "ON" : "OFF", hwnd);
#endif
        }

        /// <summary>
        /// Adds <see cref="WS_EX_TOOLWINDOW"/> and removes
        /// <see cref="WS_EX_APPWINDOW"/> so the window is hidden from the
        /// taskbar and Alt-Tab.  See PRD §10 RA-1 / DESIGN §2.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        [SupportedOSPlatform("windows")]
        public static void SetToolWindow(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("SetToolWindow skipped (non-Windows).");
                return;
            }
#if WINDOWS
            int ex = unchecked((int)GetWindowLongPtr(hwnd, GWL_EXSTYLE));
            ex |= WS_EX_TOOLWINDOW;
            ex &= ~WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
            Logger.Debug("ToolWindow applied HWND {Hwnd}.", hwnd);
#endif
        }

        /// <summary>
        /// Adds <see cref="WS_EX_NOACTIVATE"/> so clicks do not steal focus
        /// and the window never appears in Alt-Tab.  See DESIGN §2.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        [SupportedOSPlatform("windows")]
        public static void SetNoActivate(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("SetNoActivate skipped (non-Windows).");
                return;
            }
#if WINDOWS
            int ex = unchecked((int)GetWindowLongPtr(hwnd, GWL_EXSTYLE));
            ex |= WS_EX_NOACTIVATE;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
            Logger.Debug("NoActivate applied HWND {Hwnd}.", hwnd);
#endif
        }

        /// <summary>
        /// Adds or removes <c>HWND_TOPMOST</c> via <c>SetWindowPos</c>.
        /// When <paramref name="topmost"/> is <c>true</c> the window stays
        /// above all non-topmost windows (Roam / Interact / Drag states).
        /// When <c>false</c> the window becomes non-topmost.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="topmost">True for topmost, false for not-topmost.</param>
        [SupportedOSPlatform("windows")]
        public static void SetTopMost(IntPtr hwnd, bool topmost)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("SetTopMost({Topmost}) skipped (non-Windows).", topmost);
                return;
            }
#if WINDOWS
            IntPtr after = topmost ? HWND_TOPMOST : new IntPtr(-2); // HWND_NOTOPMOST
            SetWindowPos(hwnd, after, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            Logger.Debug("TopMost {State} HWND {Hwnd}.", topmost ? "ON" : "OFF", hwnd);
#endif
        }

        /// <summary>
        /// Retrieves the cursor position in screen coordinates.
        /// </summary>
        /// <param name="point">Receives the cursor position.</param>
        /// <returns><c>true</c> on success; <c>false</c> on non-Windows or failure.</returns>
        [SupportedOSPlatform("windows")]
        public static bool TryGetCursorPos(out (int X, int Y) point)
        {
            point = default;
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }
#if WINDOWS
            if (GetCursorPos(out POINT pt))
            {
                point = (pt.X, pt.Y);
                return true;
            }
            return false;
#else
            return false;
#endif
        }

        /// <summary>
        /// Returns the foreground window handle, or <see cref="IntPtr.Zero"/>
        /// on non-Windows.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static IntPtr GetForegroundHwnd()
        {
            if (!OperatingSystem.IsWindows())
                return IntPtr.Zero;
#if WINDOWS
            return GetForegroundWindow();
#else
            return IntPtr.Zero;
#endif
        }

        /// <summary>
        /// Retrieves the bounding rectangle of <paramref name="hwnd"/>.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="rect">Receives (left, top, right, bottom).</param>
        /// <returns><c>true</c> on success.</returns>
        [SupportedOSPlatform("windows")]
        public static bool TryGetWindowRect(IntPtr hwnd, out (int Left, int Top, int Right, int Bottom) rect)
        {
            rect = default;
            if (!OperatingSystem.IsWindows())
                return false;
#if WINDOWS
            if (GetWindowRect(hwnd, out RECT r))
            {
                rect = (r.Left, r.Top, r.Right, r.Bottom);
                return true;
            }
            return false;
#else
            return false;
#endif
        }

        /// <summary>
        /// Returns the handle of the monitor that the given window is on
        /// (nearest monitor if no intersection).
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <returns>Monitor handle, or <see cref="IntPtr.Zero"/> on non-Windows.</returns>
        [SupportedOSPlatform("windows")]
        public static IntPtr GetMonitorFromWindow(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows())
                return IntPtr.Zero;
#if WINDOWS
            return MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
#else
            return IntPtr.Zero;
#endif
        }

        /// <summary>
        /// Retrieves the full monitor rectangle for the monitor that
        /// <paramref name="hwnd"/> is on.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="rect">Receives (left, top, right, bottom).</param>
        /// <returns><c>true</c> on success.</returns>
        [SupportedOSPlatform("windows")]
        public static bool TryGetMonitorRect(IntPtr hwnd, out (int Left, int Top, int Right, int Bottom) rect)
        {
            rect = default;
            if (!OperatingSystem.IsWindows())
                return false;
#if WINDOWS
            IntPtr hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            MONITORINFO mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMon, ref mi))
            {
                rect = (mi.rcMonitor.Left, mi.rcMonitor.Top,
                        mi.rcMonitor.Right, mi.rcMonitor.Bottom);
                return true;
            }
            return false;
#else
            return false;
#endif
        }
    }
}