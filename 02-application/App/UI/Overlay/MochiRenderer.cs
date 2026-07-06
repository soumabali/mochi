using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using MochiV2.Core.Animation;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// SkiaSharp drawing surface for Mochi overlay.
    /// Renders current sprite frame from AnimationManager.
    /// Falls back to placeholder pink circle if no frame available.
    /// </summary>
    public sealed class MochiRenderer
    {
        internal static readonly SKColor PlaceholderColor = new SKColor(0xE8, 0xA0, 0xBF);

        private int _frameCount;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private double _lastFps;

        /// <summary>Frames-per-second measured by counting Draw calls.</summary>
        public double CurrentFps => _lastFps;

        /// <summary>Current animation manager (set by App).</summary>
        public AnimationManager? AnimationManager { get; set; }

        /// <summary>Sprite display scale (1.0 = native 256px).</summary>
        public float Scale { get; set; } = 1.0f;

        /// <summary>
        /// Draws current overlay frame: sprite from AnimationManager or placeholder.
        /// </summary>
        public void Draw(SKCanvas canvas, SKSize dimensions)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            TickFps();

            // Try to render current sprite frame
            string? framePath = AnimationManager?.ActiveController?.CurrentFramePath;
            if (!string.IsNullOrEmpty(framePath) && File.Exists(framePath))
            {
                try
                {
                    using var bitmap = SKBitmap.Decode(framePath);
                    if (bitmap != null)
                    {
                        // Position cat at bottom-center of screen
                        float spriteSize = 256f * Scale;
                        float x = dimensions.Width / 2f - spriteSize / 2f;
                        float y = dimensions.Height - spriteSize - 50f; // 50px from bottom

                        // Account for taskbar (rough estimate)
                        y -= 40f;

                        var destRect = new SKRect(x, y, x + spriteSize, y + spriteSize);
                        canvas.DrawBitmap(bitmap, destRect);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Log and fall through to placeholder
                    System.Diagnostics.Debug.WriteLine($"Failed to decode frame: {ex.Message}");
                }
            }

            // Placeholder: filled pink circle
            float radius = Math.Min(dimensions.Width, dimensions.Height) * 0.05f;
            float cx = dimensions.Width / 2f;
            float cy = dimensions.Height - radius - 50f;

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