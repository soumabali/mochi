using System;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// Abstraction over Win32 interop calls needed by the overlay window.
    /// T-002 (MochiV2.Infrastructure.Window.Win32Interop) will provide the
    /// real implementation; until then a minimal stub lives here so the
    /// overlay window can compile and run on Windows while keeping Linux
    /// builds clean.
    /// </summary>
    internal interface IWin32OverlayInterop
    {
        /// <summary>
        /// Applies the extended window styles required for a transparent,
        /// non-activating, click-through topmost overlay
        /// (WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW |
        /// WS_EX_NOACTIVATE) and sets the window to HWND_TOPMOST.
        /// No-op on non-Windows platforms.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        void ApplyOverlayStyles(IntPtr hwnd);

        /// <summary>
        /// Toggles the WS_EX_TRANSPARENT (click-through) extended style.
        /// When <paramref name="enabled"/> is true the window ignores mouse
        /// input (Roam state); when false the window receives clicks
        /// (Interact / Drag states).
        /// No-op on non-Windows platforms.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="enabled">True to enable click-through, false to disable.</param>
        void SetClickThrough(IntPtr hwnd, bool enabled);
    }
}