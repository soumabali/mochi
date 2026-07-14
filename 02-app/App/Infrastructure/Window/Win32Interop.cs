using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Serilog;

namespace MochiV2.Infrastructure.Window
{
    /// <summary>
    /// Win32 P/Invoke declarations and high-level wrapper methods for the
    /// transparent overlay window (T-002).
    ///
    /// All P/Invoke signatures match Windows SDK (WinUser.h, WinDef.h).
    /// Declarations compiled inside <c>#if WINDOWS</c>; the
    /// <c>net9.0-windows</c> TFM includes them on every build host; runtime
    /// calls guarded by <see cref="OperatingSystem.IsWindows"/>() so that
    /// unit tests on Linux builds execute non-native code paths.
    /// </summary>
    public static class Win32Interop
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(Win32Interop));

        //------------------------------------------------------------------
        // Extended window-style constants (WinUser.h)
        // Values match PRD §9 / DESIGN §2 Window States.
        //------------------------------------------------------------------

        /// <summary>Layered window — required for per-pixel alpha / transparency.</summary>
        public const int WS_EX_LAYERED = 0x00080000;

        /// <summary>Window does not receive mouse input (click-through).</summary>
        public const int WS_EX_TRANSPARENT = 0x00000020;

        /// <summary>Tool window — hidden from Alt-Tab and taskbar.</summary>
        public const int WS_EX_TOOLWINDOW = 0x00000080;

        /// <summary>Window cannot be activated by click or Alt-Tab.</summary>
        public const int WS_EX_NOACTIVATE = 0x08000000;

        /// <summary>Force top-level taskbar button (removed for overlay).</summary>
        public const int WS_EX_APPWINDOW = 0x00040000;

        /// <summary>Offset to retrieve/set extended window styles via Get/SetWindowLong.</summary>
        public const int GWL_EXSTYLE = -20;

        //------------------------------------------------------------------
        // SetWindowPos HWND special handles (WinUser.h)
        //------------------------------------------------------------------

        /// <summary>Places window above all non-topmost windows.</summary>
        public static readonly IntPtr HWND_TOPMOST = new(-1);

        /// <summary>Places window above all windows (including topmost).</summary>
        public static readonly IntPtr HWND_TOP = new(0);

        //------------------------------------------------------------------
        // SetWindowPos flags (WinUser.h)
        //------------------------------------------------------------------

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_HIDEWINDOW = 0x0080;

        //------------------------------------------------------------------
        // Monitor-from-window flags (WinUser.h)
        //------------------------------------------------------------------

        /// <summary>Return primary monitor if window doesn't intersect any.</summary>
        public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

        /// <summary>Return nearest monitor if window doesn't intersect any.</summary>
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        //==================================================================
        // P/Invoke declarations (compiled on WINDOWS TFM)
        //==================================================================

        //--- GetWindowLong / SetWindowLong -------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        //--- SetWindowPos ------------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        //--- GetCursorPos ------------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        //--- GetForegroundWindow -----------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr GetForegroundWindow();

        //--- GetWindowRect -----------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        //--- MonitorFromWindow -------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        //--- GetMonitorInfo ----------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        //--- GetDpiForWindow (PerMonitorV2, Windows 10 1607+) ------------
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetDpiForWindow")]
        [SupportedOSPlatform("windows")]
        private static extern uint GetDpiForWindowNative(IntPtr hwnd);

        //--- EnumWindows -------------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        //--- IsWindowVisible ---------------------------------------------
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "IsWindowVisible")]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisibleNative(IntPtr hWnd);

        //--- GetWindowTextLength -----------------------------------------
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowTextLengthW")]
        [SupportedOSPlatform("windows")]
        private static extern int GetWindowTextLengthNative(IntPtr hWnd);

        //--- GetWindowText -----------------------------------------------
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
        [SupportedOSPlatform("windows")]
        private static extern int GetWindowTextNative(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        //--- GetClassName ------------------------------------------------
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
        [SupportedOSPlatform("windows")]
        private static extern int GetClassNameNative(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        //--- EnumWindows delegate ----------------------------------------
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        //--- Structs -----------------------------------------------------
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

        //==================================================================
        // High-level wrapper methods
        //==================================================================

        /// <summary>
        /// Reads current extended window style flags.
        /// </summary>
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
        /// Sets extended window style flags.
        /// </summary>
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
        /// Toggles <see cref="WS_EX_TRANSPARENT"/> (click-through) extended
        /// style. When <paramref name="enabled"/> is <c>true</c> the window is
        /// click-through (Roam state); when <c>false</c> the window receives
        /// mouse input (Interact / Drag states). See DESIGN §2.
        /// </summary>
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
        /// taskbar and Alt-Tab. See PRD §10 RA-1 / DESIGN §2.
        /// </summary>
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
        /// Adds <see cref="WS_EX_NOACTIVATE"/> so clicks don't steal focus
        /// and the window doesn't appear in Alt-Tab. See DESIGN §2.
        /// </summary>
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
        /// above all non-topmost windows.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static void SetTopMost(IntPtr hwnd, bool topmost)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("SetTopMost skipped (non-Windows).");
                return;
            }
#if WINDOWS
            var after = topmost ? HWND_TOPMOST : HWND_TOP;
            SetWindowPos(hwnd, after, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            Logger.Debug("TopMost {State} HWND {Hwnd}.", topmost ? "ON" : "OFF", hwnd);
#endif
        }

        /// <summary>
        /// Retrieves cursor position in screen coordinates.
        /// </summary>
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
        /// Returns foreground window handle, <see cref="IntPtr.Zero"/> on
        /// non-Windows.
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
        /// Retrieves bounding rectangle of <paramref name="hwnd"/>.
        /// </summary>
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
        /// Returns handle of the monitor the given window is on
        /// (nearest monitor with intersection).
        /// </summary>
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
        /// Retrieves full monitor rectangle of the monitor that
        /// <paramref name="hwnd"/> is on.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool TryGetMonitorRect(IntPtr hwnd, out (int Left, int Top, int Right, int Bottom) rect)
        {
            rect = default;
            if (!OperatingSystem.IsWindows())
                return false;
#if WINDOWS
            IntPtr hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
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

        /// <summary>
        /// Enumerates visible top-level windows with title bars suitable for
        /// surface walking. Returns (hwnd, left, top, right, bottom) tuples.
        /// Filters: visible, has title, width &gt; 50px. Returns empty on non-Windows.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static List<(IntPtr Hwnd, int Left, int Top, int Right, int Bottom)> EnumerateVisibleWindows()
        {
            var result = new List<(IntPtr, int, int, int, int)>();

            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("EnumerateVisibleWindows skipped (non-Windows).");
                return result;
            }

#if WINDOWS
            var hwnds = new List<IntPtr>();
            EnumWindowsProc callback = (hWnd, _) =>
            {
                hwnds.Add(hWnd);
                return true;
            };
            EnumWindows(callback, IntPtr.Zero);

            foreach (var hWnd in hwnds)
            {
                if (!IsWindowVisibleNative(hWnd))
                    continue;

                if (GetWindowTextLengthNative(hWnd) <= 0)
                    continue;

                if (!GetWindowRect(hWnd, out RECT r))
                    continue;

                if (r.Right - r.Left <= 50)
                    continue;

                int exStyle = unchecked((int)GetWindowLongPtr(hWnd, GWL_EXSTYLE));
                if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_LAYERED) != 0)
                    continue;

                result.Add((hWnd, r.Left, r.Top, r.Right, r.Bottom));
            }

            Logger.Debug("EnumerateVisibleWindows: {Count} surfaces found.", result.Count);
#endif
            return result;
        }

        /// <summary>
        /// Checks if a window handle is still valid and visible.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool IsWindowStillValid(IntPtr hWnd)
        {
            if (!OperatingSystem.IsWindows())
                return false;
#if WINDOWS
            return IsWindowVisibleNative(hWnd);
#else
            return false;
#endif
        }
    }
}