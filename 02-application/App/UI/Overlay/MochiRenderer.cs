using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using MochiV2.Core.Animation;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// SkiaSharp drawing surface for Mochi overlay.
    /// Renders current sprite frame from AnimationManager with transparency.
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

        /// <summary>Sprite display scale (1.0 = native pixel size).</summary>
        public float Scale { get; set; } = 2.0f; // 128px native * 2 = 256px display

        // Cache last decoded bitmap to avoid re-decoding same frame every 60fps
        private SKBitmap? _cachedBitmap;
        private string? _cachedFramePath;

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
                    // Cache bitmap — only decode if frame path changed
                    if (_cachedBitmap == null || _cachedFramePath != framePath)
                    {
                        _cachedBitmap?.Dispose();
                        _cachedBitmap = SKBitmap.Decode(framePath);
                        _cachedFramePath = framePath;
                    }

                    if (_cachedBitmap != null)
                    {
                        // Use native bitmap size, scaled by Scale factor
                        float nativeW = _cachedBitmap.Width;
                        float nativeH = _cachedBitmap.Height;
                        float displayW = nativeW * Scale;
                        float displayH = nativeH * Scale;

                        // Position cat at bottom-center of screen
                        float x = dimensions.Width / 2f - displayW / 2f;
                        float y = dimensions.Height - displayH - 60f; // 60px from bottom (taskbar)

                        var destRect = new SKRect(x, y, x + displayW, y + displayH);
                        var srcRect = new SKRect(0, 0, nativeW, nativeH);

                        // Draw with alpha blending for transparency
                        using var paint = new SKPaint
                        {
                            IsAntialias = true,
                            FilterQuality = SKFilterQuality.Medium
                        };
                        canvas.DrawBitmap(_cachedBitmap, srcRect, destRect, paint);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to decode frame: {ex.Message}");
                }
            }

            // Placeholder: filled pink circle
            float radius = Math.Min(dimensions.Width, dimensions.Height) * 0.05f;
            float cx = dimensions.Width / 2f;
            float cy = dimensions.Height - radius - 60f;

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