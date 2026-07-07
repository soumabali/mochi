using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using MochiV2.Core.Animation;
using MochiV2.Core.Behavior;
using MochiV2.Core.Particles;
using MochiV2.Core.Services;

namespace MochiV2.UI.Overlay
{
    /// <summary>
    /// SkiaSharp drawing surface for Mochi overlay.
    /// Renders: sprite + particles + micro-motion + night tint + squash/stretch + sad filter.
    /// </summary>
    public sealed class MochiRenderer
    {
        internal static readonly SKColor PlaceholderColor = new SKColor(0xE8, 0xA0, 0xBF);
        private static readonly SKColor NightTint = new SKColor(0x20, 0x30, 0x60, 0x20); // cool blue, low alpha

        private int _frameCount;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private double _lastFps;

        public double CurrentFps => _lastFps;

        // Set by App each frame
        public AnimationManager? AnimationManager { get; set; }
        public ParticleSystem? Particles { get; set; }
        public MicroMotionService? MicroMotion { get; set; }
        public NightModeService? NightMode { get; set; }
        public float Scale { get; set; } = 1.5f;
        public double CatX { get; set; }
        public double CatY { get; set; }
        public double SquashAmount { get; set; } // 0-1, squash on landing
        public string CurrentMood { get; set; } = "Content";

        // H-18/H-19: Post-MVP rendering properties
        public (double X, double Y)? BallPosition { get; set; }
        public System.Collections.Generic.List<(double X, double Y, int Type)>? DroppedItems { get; set; }

        // Bitmap cache
        private SKBitmap? _cachedBitmap;
        private string? _cachedFramePath;

        public void Draw(SKCanvas canvas, SKSize dimensions)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            TickFps();

            // Try render sprite
            string? framePath = AnimationManager?.ActiveController?.CurrentFramePath;
            if (!string.IsNullOrEmpty(framePath) && File.Exists(framePath))
            {
                try
                {
                    if (_cachedBitmap == null || _cachedFramePath != framePath)
                    {
                        _cachedBitmap?.Dispose();
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

                        // A-03/A-04: Micro-motion breathing
                        float breathScaleY = 1f;
                        if (MicroMotion != null)
                            breathScaleY = (float)MicroMotion.CurrentBreathingScaleY();

                        // A-05: Squash & stretch on landing
                        float squashY = 1f - (float)SquashAmount * 0.1f; // 10% compress
                        float stretchX = 1f + (float)SquashAmount * 0.05f; // 5% stretch

                        float finalScaleY = breathScaleY * squashY;
                        float finalScaleX = stretchX;

                        float adjustedH = displayH * finalScaleY;
                        float adjustedW = displayW * finalScaleX;
                        float dy = (displayH - adjustedH) / 2f; // bottom-anchor

                        var destRect = new SKRect(x, y + dy, x + adjustedW, y + dy + adjustedH);
                        var srcRect = new SKRect(0, 0, nativeW, nativeH);

                        using var paint = new SKPaint
                        {
                            IsAntialias = true,
                            FilterQuality = SKFilterQuality.Medium,
                        };

                        // A-08: Sad mood → desaturate
                        if (CurrentMood == "Sad")
                        {
                            paint.ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                            {
                                0.3f, 0.6f, 0.1f, 0, 0,  // R
                                0.3f, 0.6f, 0.1f, 0, 0,  // G
                                0.3f, 0.6f, 0.1f, 0, 0,  // B
                                0, 0, 0, 1, 0            // A
                            });
                        }

                        canvas.DrawBitmap(_cachedBitmap, srcRect, destRect, paint);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Decode error: {ex.Message}");
                }
            }
            else
            {
                // Placeholder
                float r = 40f * Scale * 0.3f;
                float cx = (float)CatX + 100f * Scale * 0.5f;
                float cy = (float)CatY + 100f * Scale * 0.5f;
                using var p = new SKPaint { Color = PlaceholderColor, IsAntialias = true, Style = SKPaintStyle.Fill };
                canvas.DrawCircle(cx, cy, r, p);
            }

            // A-02: Position particles at cat location
            if (Particles != null)
            {
                // Set emit origin to cat center before drawing
                float catCenterX = (float)CatX + (float)(100 * Scale * 0.5);
                float catCenterY = (float)CatY + (float)(100 * Scale * 0.5);
                Particles.SetEmitOrigin(new SKPoint(catCenterX, catCenterY));
                Particles.Draw(canvas);
            }

            // A-06: Night mode tint overlay
            if (NightMode != null && NightMode.IsActive)
            {
                using var tintPaint = new SKPaint { Color = NightTint, Style = SKPaintStyle.Fill };
                canvas.DrawRect(new SKRect(0, 0, dimensions.Width, dimensions.Height), tintPaint);
            }

            // H-18: Mini ball game rendering (red circle)
            if (BallPosition != null)
            {
                var (bx, by) = BallPosition.Value;
                using var ballPaint = new SKPaint { Color = new SKColor(0xFF, 0x6B, 0x6B), IsAntialias = true, Style = SKPaintStyle.Fill };
                canvas.DrawCircle((float)bx, (float)by, 12f * Scale, ballPaint);
            }

            // H-19: Item drop rendering (simple colored shapes)
            if (DroppedItems != null)
            {
                foreach (var item in DroppedItems)
                {
                    var (ix, iy, type) = item;
                    var color = type switch
                    {
                        0 => new SKColor(0xFF, 0xC0, 0x7A), // Fish = orange
                        1 => new SKColor(0xFF, 0xD7, 0x00), // Coin = gold
                        2 => new SKColor(0xFF, 0x69, 0xB4), // Heart = pink
                        3 => new SKColor(0xFF, 0xFF, 0x00), // Star = yellow
                        _ => new SKColor(0xFF, 0xFF, 0xFF),
                    };
                    using var itemPaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
                    canvas.DrawCircle((float)ix, (float)iy, 10f * Scale, itemPaint);
                }
            }

            // H-17: Night mode dream Zzz particles
            if (NightMode != null && NightMode.IsActive && _frameCount % 60 == 0)
            {
                // Subtle dream effect handled by particle system
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