using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using MochiV2.Core.Animation;
using MochiV2.Core.Behavior;
using MochiV2.Core.Particles;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// SkiaSharp drawing surface for Mochi overlay.
    /// Renders: sprite frame + particles + micro-motion transforms.
    /// </summary>
    public sealed class MochiRenderer
    {
        internal static readonly SKColor PlaceholderColor = new SKColor(0xE8, 0xA0, 0xBF);

        private int _frameCount;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private double _lastFps;

        public double CurrentFps => _lastFps;

        // Set by App each frame
        public AnimationManager? AnimationManager { get; set; }
        public ParticleSystem? Particles { get; set; }
        public MicroMotionService? MicroMotion { get; set; }
        public float Scale { get; set; } = 2.0f;
        public double CatX { get; set; }
        public double CatY { get; set; }

        // Bitmap cache
        private SKBitmap? _cachedBitmap;
        private string? _cachedFramePath;

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
                    if (_cachedBitmap == null || _cachedFramePath != framePath)
                    {
                        _cachedBitmap?.Dispose();
                        // PNG sprites already have proper alpha — simple decode
                        _cachedBitmap = SKBitmap.Decode(framePath);
                        _cachedFramePath = framePath;
                    }

                    if (_cachedBitmap != null)
                    {
                        float nativeW = _cachedBitmap.Width;
                        float nativeH = _cachedBitmap.Height;
                        float displayW = nativeW * Scale;
                        float displayH = nativeH * Scale;

                        float x = (float)CatX;
                        float y = (float)CatY;

                        // Apply micro-motion (breathing scale)
                        float scaleY = 1f;
                        if (MicroMotion != null)
                        {
                            scaleY = (float)MicroMotion.CurrentBreathingScaleY();
                        }

                        float adjustedH = displayH * scaleY;
                        float adjustedW = displayW;
                        float dy = (displayH - adjustedH) / 2f;

                        var destRect = new SKRect(x, y + dy, x + adjustedW, y + dy + adjustedH);
                        var srcRect = new SKRect(0, 0, nativeW, nativeH);

                        using var paint = new SKPaint
                        {
                            IsAntialias = true,
                            FilterQuality = SKFilterQuality.Medium,
                            Color = SKColors.White.WithAlpha(255) // opaque, bitmap alpha drives transparency
                        };
                        canvas.DrawBitmap(_cachedBitmap, srcRect, destRect, paint);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to decode frame: {ex.Message}");
                }
            }
            else
            {
                // Placeholder: pink circle
                float radius = 64f * Scale * 0.3f;
                float cx = (float)CatX + 64f * Scale * 0.5f;
                float cy = (float)CatY + 64f * Scale * 0.5f;
                using var p = new SKPaint { Color = PlaceholderColor, IsAntialias = true, Style = SKPaintStyle.Fill };
                canvas.DrawCircle(cx, cy, radius, p);
            }

            // Draw particles
            if (Particles != null)
            {
                Particles.Draw(canvas);
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