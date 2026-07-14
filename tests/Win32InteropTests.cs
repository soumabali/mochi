using System;
using MochiV2.Infrastructure.Window;
using Xunit;

namespace MochiV2.Tests
{
    /// <summary>
    /// Unit tests for T-002 Win32 interop layer.
    /// Tests verify that constants match PRD §9 / DESIGN §2 values,
    /// wrapper methods are safe on non-Windows, and the fullscreen
    /// detection logic is correct.
    /// </summary>
    public class Win32InteropTests
    {
        // ------------------------------------------------------------------
        //  Constant values  (must match PRD §9 / DESIGN §2 / WinUser.h)
        // ------------------------------------------------------------------

        [Fact]
        public void WS_EX_LAYERED_MatchesWin32SdkValue()
        {
            Assert.Equal(0x00080000, Win32Interop.WS_EX_LAYERED);
        }

        [Fact]
        public void WS_EX_TRANSPARENT_MatchesWin32SdkValue()
        {
            Assert.Equal(0x00000020, Win32Interop.WS_EX_TRANSPARENT);
        }

        [Fact]
        public void WS_EX_TOOLWINDOW_MatchesWin32SdkValue()
        {
            Assert.Equal(0x00000080, Win32Interop.WS_EX_TOOLWINDOW);
        }

        [Fact]
        public void WS_EX_NOACTIVATE_MatchesWin32SdkValue()
        {
            Assert.Equal(0x08000000, Win32Interop.WS_EX_NOACTIVATE);
        }

        [Fact]
        public void WS_EX_APPWINDOW_MatchesWin32SdkValue()
        {
            Assert.Equal(0x00040000, Win32Interop.WS_EX_APPWINDOW);
        }

        [Fact]
        public void GWL_EXSTYLE_MatchesWin32SdkValue()
        {
            Assert.Equal(-20, Win32Interop.GWL_EXSTYLE);
        }

        [Fact]
        public void HWND_TOPMOST_IsMinusOne()
        {
            Assert.Equal(new IntPtr(-1), Win32Interop.HWND_TOPMOST);
        }

        [Fact]
        public void SWP_Flags_MatchWin32SdkValues()
        {
            Assert.Equal(0x0001u, Win32Interop.SWP_NOSIZE);
            Assert.Equal(0x0002u, Win32Interop.SWP_NOMOVE);
            Assert.Equal(0x0010u, Win32Interop.SWP_NOACTIVATE);
        }

        // ------------------------------------------------------------------
        //  Wrapper methods — non-Windows safety
        // ------------------------------------------------------------------

        [Fact]
        public void GetExtendedStyle_OnNonWindows_ReturnsZero()
        {
            // On Linux build, GetExtendedStyle should not throw and return 0.
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(0, Win32Interop.GetExtendedStyle(IntPtr.Zero));
            }
        }

        [Fact]
        public void SetExtendedStyle_OnNonWindows_DoesNotThrow()
        {
            if (!OperatingSystem.IsWindows())
            {
                Win32Interop.SetExtendedStyle(IntPtr.Zero, 0);
            }
        }

        [Fact]
        public void SetWindowClickThrough_OnNonWindows_DoesNotThrow()
        {
            if (!OperatingSystem.IsWindows())
            {
                Win32Interop.SetWindowClickThrough(IntPtr.Zero, true);
                Win32Interop.SetWindowClickThrough(IntPtr.Zero, false);
            }
        }

        [Fact]
        public void SetToolWindow_OnNonWindows_DoesNotThrow()
        {
            if (!OperatingSystem.IsWindows())
            {
                Win32Interop.SetToolWindow(IntPtr.Zero);
            }
        }

        [Fact]
        public void SetNoActivate_OnNonWindows_DoesNotThrow()
        {
            if (!OperatingSystem.IsWindows())
            {
                Win32Interop.SetNoActivate(IntPtr.Zero);
            }
        }

        [Fact]
        public void SetTopMost_OnNonWindows_DoesNotThrow()
        {
            if (!OperatingSystem.IsWindows())
            {
                Win32Interop.SetTopMost(IntPtr.Zero, true);
                Win32Interop.SetTopMost(IntPtr.Zero, false);
            }
        }

        [Fact]
        public void TryGetCursorPos_OnNonWindows_ReturnsFalse()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.False(Win32Interop.TryGetCursorPos(out var pt));
                Assert.Equal(0, pt.X);
                Assert.Equal(0, pt.Y);
            }
        }

        [Fact]
        public void GetForegroundHwnd_OnNonWindows_ReturnsZero()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(IntPtr.Zero, Win32Interop.GetForegroundHwnd());
            }
        }

        [Fact]
        public void TryGetWindowRect_OnNonWindows_ReturnsFalse()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.False(Win32Interop.TryGetWindowRect(IntPtr.Zero, out var rect));
            }
        }

        // ------------------------------------------------------------------
        //  FullscreenDetector — pure logic tests (no P/Invoke needed)
        // ------------------------------------------------------------------

        [Fact]
        public void FullscreenDetector_ExactMatch_ReturnsTrue()
        {
            var win = (0, 0, 1920, 1080);
            var mon = (0, 0, 1920, 1080);
            Assert.True(FullscreenDetector.IsRectFullscreen(win, mon, 0));
        }

        [Fact]
        public void FullscreenDetector_SmallTolerance_ReturnsTrue()
        {
            // Window slightly smaller (e.g. 1px border) within tolerance.
            var win = (0, 0, 1919, 1079);
            var mon = (0, 0, 1920, 1080);
            Assert.True(FullscreenDetector.IsRectFullscreen(win, mon, 8));
        }

        [Fact]
        public void FullscreenDetector_WindowSmallerThanMonitor_ReturnsFalse()
        {
            // Maximized window with visible taskbar — not fullscreen.
            var win = (0, 0, 1920, 1040);
            var mon = (0, 0, 1920, 1080);
            Assert.False(FullscreenDetector.IsRectFullscreen(win, mon, 8));
        }

        [Fact]
        public void FullscreenDetector_WindowOffset_ReturnsFalse()
        {
            // Same size but offset origin — not fullscreen.
            var win = (10, 10, 1930, 1090);
            var mon = (0, 0, 1920, 1080);
            Assert.False(FullscreenDetector.IsRectFullscreen(win, mon, 8));
        }

        [Fact]
        public void FullscreenDetector_HalfScreenWindow_ReturnsFalse()
        {
            var win = (0, 0, 960, 1080);
            var mon = (0, 0, 1920, 1080);
            Assert.False(FullscreenDetector.IsRectFullscreen(win, mon, 8));
        }

        [Fact]
        public void FullscreenDetector_WithinToleranceOffset_ReturnsTrue()
        {
            // Window offset by 5px in each direction — within tolerance of 8,
            // same dimensions — should be considered fullscreen.
            var win = (-5, -5, 1915, 1075);
            var mon = (0, 0, 1920, 1080);
            Assert.True(FullscreenDetector.IsRectFullscreen(win, mon, 8));
        }

        [Fact]
        public void FullscreenDetector_BeyondToleranceOffset_ReturnsFalse()
        {
            // Window offset by 20px — exceeds tolerance of 8.
            var win = (-20, -20, 1900, 1060);
            var mon = (0, 0, 1920, 1080);
            Assert.False(FullscreenDetector.IsRectFullscreen(win, mon, 8));
        }

        [Fact]
        public void FullscreenDetector_IsForegroundFullscreen_OnNonWindows_ReturnsFalse()
        {
            if (!OperatingSystem.IsWindows())
            {
                var detector = new FullscreenDetector();
                Assert.False(detector.IsForegroundFullscreen());
            }
        }

        // ------------------------------------------------------------------
        //  DpiHelper — non-Windows defaults
        // ------------------------------------------------------------------

        [Fact]
        public void DpiHelper_BaseDpi_Is96()
        {
            Assert.Equal(96, DpiHelper.BaseDpi);
        }

        [Fact]
        public void DpiHelper_OnNonWindows_DefaultsTo96And1xScale()
        {
            if (!OperatingSystem.IsWindows())
            {
                var dpi = new DpiHelper();
                dpi.GetDpiForWindow(IntPtr.Zero);
                Assert.Equal(96u, dpi.Dpi);
                Assert.Equal(1.0, dpi.ScaleX, 5);
                Assert.Equal(1.0, dpi.ScaleY, 5);
            }
        }

        [Fact]
        public void DpiHelper_QueryDpiForWindow_OnNonWindows_Returns96()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(96u, DpiHelper.QueryDpiForWindow(IntPtr.Zero));
            }
        }
    }
}