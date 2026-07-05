using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Serilog;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using MochiV2.Infrastructure.Window;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// WPF transparent overlay window hosting a SkiaSharp <see cref="SKElement"/>.
    /// On <see cref="SourceInitialized"/> the Win32 extended styles
    /// (WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)
    /// and HWND_TOPMOST are applied via
    /// <see cref="MochiV2.Infrastructure.Window.Win32Interop"/> (T-002).
    /// A <see cref="CompositionTarget.Rendering"/> loop drives 60fps
    /// invalidation of the render element.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext<OverlayWindow>();

        private readonly MochiRenderer _renderer;

        /// <summary>Constructs the overlay window with a default renderer.</summary>
        public OverlayWindow()
            : this(new MochiRenderer())
        {
        }

        /// <summary>
        /// Internal constructor allowing tests / DI to inject a renderer.
        /// </summary>
        internal OverlayWindow(MochiRenderer renderer)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            InitializeComponent();
        }

        /// <summary>True while the CompositionTarget render loop is subscribed.</summary>
        public bool IsRenderLoopActive { get; private set; }

        /// <summary>Exposes the renderer (frame-rate counter, future sprite API).</summary>
        internal MochiRenderer Renderer => _renderer;

        // ------------------------------------------------------------------
        // Window lifecycle
        // ------------------------------------------------------------------

        /// <summary>
        /// Applies Win32 overlay styles (topmost, layered, click-through,
        /// tool-window, no-activate) once the HWND exists, then wires the
        /// SkiaSharp paint surface handler and starts the render loop.
        /// All Win32 calls are guarded by Win32Interop (no-op on non-Windows).
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Obtain the HWND (valid only after source initialized).
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Apply WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW |
            // WS_EX_NOACTIVATE + HWND_TOPMOST via T-002 Win32Interop.
            ApplyOverlayWin32Styles(hwnd);

            // Wire SkiaSharp paint surface.
            RenderElement.PaintSurface += OnPaintSurface;

            // Start 60fps invalidation loop.
            StartRenderLoop();
        }

        /// <summary>
        /// Unsubscribes the render loop and paint handler on close.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            StopRenderLoop();
            RenderElement.PaintSurface -= OnPaintSurface;
            base.OnClosed(e);
        }

        // ------------------------------------------------------------------
        // Win32 style application (guarded — compile on Linux, run on Windows)
        // ------------------------------------------------------------------

        /// <summary>
        /// Applies the Roam-state extended window styles from PRD §9 / DESIGN §2:
        /// WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
        /// plus HWND_TOPMOST. Uses T-002 <see cref="Win32Interop"/> which is a
        /// no-op on non-Windows platforms.
        /// </summary>
        private void ApplyOverlayWin32Styles(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("ApplyOverlayWin32Styles skipped (non-Windows).");
                return;
            }

            int style = Win32Interop.GetExtendedStyle(hwnd);
            style |= Win32Interop.WS_EX_LAYERED
                   | Win32Interop.WS_EX_TRANSPARENT
                   | Win32Interop.WS_EX_TOOLWINDOW
                   | Win32Interop.WS_EX_NOACTIVATE;
            Win32Interop.SetExtendedStyle(hwnd, style);

            Win32Interop.SetTopMost(hwnd, topmost: true);

            Logger.Information("Overlay Win32 styles applied to HWND {Hwnd}.", hwnd);
        }

        // ------------------------------------------------------------------
        // Click-through toggle (used by FSM / interact-mode later)
        // ------------------------------------------------------------------

        /// <summary>
        /// Toggles WS_EX_TRANSPARENT on the window via T-002 Win32Interop.
        /// When <paramref name="enabled"/> is true mouse events pass through
        /// (Roam). When false the window receives input (Interact / Drag).
        /// Guarded: no-op on non-Windows.
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.SetWindowClickThrough(hwnd, enabled);
        }

        // ------------------------------------------------------------------
        // Render loop (CompositionTarget.Rendering → InvalidateVisual)
        // ------------------------------------------------------------------

        /// <summary>
        /// Subscribes to <see cref="CompositionTarget.Rendering"/> to drive
        /// 60fps invalidation of <see cref="RenderElement"/>.
        /// </summary>
        public void StartRenderLoop()
        {
            if (IsRenderLoopActive)
                return;

            CompositionTarget.Rendering += OnCompositionTargetRendering;
            IsRenderLoopActive = true;
            Logger.Debug("Render loop started.");
        }

        /// <summary>
        /// Unsubscribes from <see cref="CompositionTarget.Rendering"/>.
        /// </summary>
        public void StopRenderLoop()
        {
            if (!IsRenderLoopActive)
                return;

            CompositionTarget.Rendering -= OnCompositionTargetRendering;
            IsRenderLoopActive = false;
            Logger.Debug("Render loop stopped.");
        }

        private void OnCompositionTargetRendering(object? sender, EventArgs e)
        {
            // InvalidateVisual forces SKElement.PaintSurface to fire next layout.
            RenderElement.InvalidateVisual();
        }

        // ------------------------------------------------------------------
        // SkiaSharp paint
        // ------------------------------------------------------------------

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;

            // Clear to transparent so the window background shows through.
            canvas.Clear(SKColors.Transparent);

            SKSizeI size = e.Info.Size;
            _renderer.Draw(canvas, new SKSize(size.Width, size.Height));
        }
    }
}