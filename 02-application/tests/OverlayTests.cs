using SkiaSharp;
using MochiV2.UI.Overlay;
using Xunit;

namespace MochiV2.Tests
{
    /// <summary>
    /// T-003: Overlay renderer tests. Validates that
    /// <see cref="MochiRenderer.Draw"/> executes without throwing on a
    /// dummy <see cref="SKCanvas"/> backed by a small surface.
    /// </summary>
    public class OverlayTests
    {
        [Fact]
        public void MochiRenderer_Draw_DoesNotThrow_OnDummyCanvas()
        {
            // Create a real SKSurface + canvas (no window needed).
            using var surface = SKSurface.Create(
                new SKImageInfo(64, 64, SKColorType.Bgra8888, SKAlphaType.Premul));
            SKCanvas canvas = surface.Canvas;

            var renderer = new MochiRenderer();
            var dims = new SKSize(64, 64);

            // Should not throw.
            renderer.Draw(canvas, dims);

            // Sanity: FPS counter should be initialized to zero before first tick.
            // After one Draw call the counter ticks, but CurrentFps stays 0 until 1s.
            Assert.True(renderer.CurrentFps >= 0d);
        }

        [Fact]
        public void MochiRenderer_Draw_AcceptsVariousDimensions()
        {
            using var surface = SKSurface.Create(
                new SKImageInfo(320, 200, SKColorType.Bgra8888, SKAlphaType.Premul));
            SKCanvas canvas = surface.Canvas;

            var renderer = new MochiRenderer();

            // Draw at the nominal overlay size (matches OverlayWindow XAML).
            renderer.Draw(canvas, new SKSize(320, 200));
            renderer.Draw(canvas, new SKSize(1, 1));
            renderer.Draw(canvas, new SKSize(1024, 768));

            // No exception thrown → pass.
            Assert.True(true);
        }
    }
}