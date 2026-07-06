using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Serilog;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using MochiV2.Infrastructure.Window;
using MochiV2.Core.Animation;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// WPF transparent overlay window hosting SkiaSharp SKElement.
    /// Win32 extended styles applied for click-through, topmost, no-activate.
    /// CompositionTarget.Rendering loop drives 60fps invalidation.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext<OverlayWindow>();

        private readonly MochiRenderer _renderer;

        /// <summary>Constructs overlay window with default renderer.</summary>
        public OverlayWindow()
            : this(new MochiRenderer())
        {
        }

        /// <summary>
        /// Internal constructor allowing tests to inject renderer.
        /// </summary>
        internal OverlayWindow(MochiRenderer renderer)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            InitializeComponent();
        }

        /// <summary>
        /// Sets the animation manager so renderer can access current sprite frame.
        /// </summary>
        public void SetAnimationManager(AnimationManager animManager)
        {
            _renderer.AnimationManager = animManager;
        }

        /// <summary>True while CompositionTarget render loop subscribed.</summary>
        public bool IsRenderLoopActive { get; private set; }

        /// <summary>Exposes renderer (frame-rate counter, future sprite API).</summary>
        internal MochiRenderer Renderer => _renderer;

        //------------------------------------------------------------------
        // Window lifecycle
        //------------------------------------------------------------------

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            ApplyOverlayWin32Styles(hwnd);

            RenderElement.PaintSurface += OnPaintSurface;
            StartRenderLoop();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopRenderLoop();
            RenderElement.PaintSurface -= OnPaintSurface;
            base.OnClosed(e);
        }

        //------------------------------------------------------------------
        // Win32 style application (guarded — compile on Linux, run on Windows)
        //------------------------------------------------------------------

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

            Logger.Information("Overlay Win32 styles applied HWND {Hwnd}.", hwnd);
        }

        //------------------------------------------------------------------
        // Click-through toggle
        //------------------------------------------------------------------

        public void SetClickThrough(bool enabled)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.SetWindowClickThrough(hwnd, enabled);
        }

        //------------------------------------------------------------------
        // Render loop (CompositionTarget.Rendering → InvalidateVisual)
        //------------------------------------------------------------------

        public void StartRenderLoop()
        {
            if (IsRenderLoopActive)
                return;

            CompositionTarget.Rendering += OnCompositionTargetRendering;
            IsRenderLoopActive = true;
            Logger.Debug("Render loop started.");
        }

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
            RenderElement.InvalidateVisual();
        }

        //------------------------------------------------------------------
        // SkiaSharp paint
        //------------------------------------------------------------------

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            SKSizeI size = e.Info.Size;
            _renderer.Draw(canvas, new SKSize(size.Width, size.Height));
        }
    }
}