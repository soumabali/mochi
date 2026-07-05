using System;
using System.Diagnostics;
using SkiaSharp;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// SkiaSharp drawing surface for the Mochi overlay.
    /// T-003 implements only a placeholder (filled pink circle) so the
    /// render loop is visible. Later tasks replace <see cref="Draw"/>
    /// with sprite rendering (T-00x) and particle effects (T-011).
    /// </summary>
    public sealed class MochiRenderer
    {
        /// <summary>Placeholder pink color (#E8A0BF) from PRD §1 design tokens.</summary>
        internal static readonly SKColor PlaceholderColor =
            new SKColor(0xE8, 0xA0, 0xBF);

        // --- Frame-rate counter (counts Draw calls per second) ------------
        private int _frameCount;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private double _lastFps;

        /// <summary>
        /// Frames-per-second measured by counting <see cref="Draw"/> calls.
        /// Updated once per second; valid between updates.
        /// </summary>
        public double CurrentFps => _lastFps;

        /// <summary>
        /// Draws the current overlay frame to <paramref name="canvas"/>.
        /// T-003: draws a placeholder filled circle centered in the surface.
        /// </summary>
        /// <param name="canvas">Target Skia canvas (already cleared by host).</param>
        /// <param name="dimensions">Surface dimensions in pixels.</param>
        public void Draw(SKCanvas canvas, SKSize dimensions)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            // Tick the frame-rate counter first so every successful call is counted.
            TickFps();

            // ----- Placeholder: filled pink circle centered in the window -----
            float radius = Math.Min(dimensions.Width, dimensions.Height) * 0.35f;
            float cx = dimensions.Width * 0.5f;
            float cy = dimensions.Height * 0.5f;

            using (var paint = new SKPaint
            {
                Color = PlaceholderColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            })
            {
                canvas.DrawCircle(cx, cy, radius, paint);
            }
        }

        /// <summary>
        /// Counts Draw invocations and recomputes FPS once per second.
        /// </summary>
        private void TickFps()
        {
            _frameCount++;
            if (_fpsStopwatch.Elapsed.TotalSeconds >= 1.0)
            {
                _lastFps = _frameCount / _fpsStopwatch.Elapsed.TotalSeconds;
                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
        }
    }
}