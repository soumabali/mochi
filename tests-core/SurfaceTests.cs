using System;
using System.Collections.Generic;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// Tests for Post-MVP Phase E: Window-Top Walking.
    /// Tests WalkableSurface, SurfaceClimber, and MovementService surface mode.
    /// </summary>
    public class SurfaceTests
    {
        //---- WalkableSurface struct tests ----

        [Fact]
        public void WalkableSurface_Width_Calculates_RightMinusLeft()
        {
            var s = new WalkableSurface(100, 200, 500, IntPtr.Zero);
            Assert.Equal(400, s.Width);
        }

        [Fact]
        public void WalkableSurface_Equality_Structural()
        {
            var a = new WalkableSurface(10, 20, 30, new IntPtr(42));
            var b = new WalkableSurface(10, 20, 30, new IntPtr(42));
            var c = new WalkableSurface(10, 20, 31, new IntPtr(42));
            Assert.Equal(a, b);
            Assert.True(a.Equals(b));
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void WalkableSurface_ZeroWidth_Valid()
        {
            var s = new WalkableSurface(100, 200, 100, IntPtr.Zero);
            Assert.Equal(0, s.Width);
        }

        //---- SurfaceClimber tests ----

        [Fact]
        public void CanClimbTo_WithinRange_ReturnsTrue()
        {
            var climber = new SurfaceClimber();
            var pos = new Position(500, 1000, Facing.Right);
            var surface = new WalkableSurface(400, 700, 600, IntPtr.Zero);
            Assert.True(climber.CanClimbTo(surface, pos));
        }

        [Fact]
        public void CanClimbTo_OutOfRange_ReturnsFalse()
        {
            var climber = new SurfaceClimber();
            var pos = new Position(500, 1000, Facing.Right);
            var surface = new WalkableSurface(400, 600, 600, IntPtr.Zero); // 400px above
            Assert.False(climber.CanClimbTo(surface, pos));
        }

        [Fact]
        public void CanClimbTo_BelowCurrent_ReturnsFalse()
        {
            var climber = new SurfaceClimber();
            var pos = new Position(500, 500, Facing.Right);
            var surface = new WalkableSurface(400, 600, 600, IntPtr.Zero); // below
            Assert.False(climber.CanClimbTo(surface, pos));
        }

        [Fact]
        public void CanClimbTo_AtMaxRange_ReturnsTrue()
        {
            var climber = new SurfaceClimber();
            var pos = new Position(500, 1000, Facing.Right);
            var surface = new WalkableSurface(400, 700, 600, IntPtr.Zero); // exactly 300px above
            Assert.True(climber.CanClimbTo(surface, pos));
        }

        [Fact]
        public void GetClimbArc_ReturnsCorrectValues()
        {
            var climber = new SurfaceClimber();
            var from = new Position(500, 1000, Facing.Right);
            var surface = new WalkableSurface(400, 700, 600, IntPtr.Zero);
            var arc = climber.GetClimbArc(from, surface);
            Assert.Equal(500, arc.StartX);
            Assert.Equal(1000, arc.StartY);
            Assert.Equal(500, arc.EndX); // clamped to surface range
            Assert.Equal(700, arc.EndY);
            Assert.True(arc.DurationSeconds > 0);
        }

        [Fact]
        public void GetClimbArc_ClampsXToSurface()
        {
            var climber = new SurfaceClimber();
            var from = new Position(100, 1000, Facing.Right); // far left of surface
            var surface = new WalkableSurface(400, 700, 600, IntPtr.Zero);
            var arc = climber.GetClimbArc(from, surface);
            Assert.Equal(400, arc.EndX); // clamped to surface.Left
        }

        [Fact]
        public void FindNearestClimbableSurface_ReturnsNearest()
        {
            var climber = new SurfaceClimber();
            var pos = new Position(500, 1000, Facing.Right);
            var surfaces = new[]
            {
                new WalkableSurface(100, 700, 300, IntPtr.Zero),    // far away
                new WalkableSurface(450, 750, 550, new IntPtr(1)),  // close
                new WalkableSurface(800, 800, 1000, new IntPtr(2)), // medium
            };
            var nearest = climber.FindNearestClimbableSurface(pos, surfaces);
            Assert.True(nearest.HasValue);
            Assert.Equal(new IntPtr(1), nearest.Value.SurfaceHandle);
        }

        [Fact]
        public void FindNearestClimbableSurface_NoneInRange_ReturnsNull()
        {
            var climber = new SurfaceClimber();
            var pos = new Position(500, 1000, Facing.Right);
            var surfaces = new[]
            {
                new WalkableSurface(100, 500, 300, IntPtr.Zero), // 500px above, too far
            };
            var nearest = climber.FindNearestClimbableSurface(pos, surfaces);
            Assert.Null(nearest);
        }

        [Fact]
        public void FindNearestClimbableSurface_EmptyArray_ReturnsNull()
        {
            var climber = new SurfaceClimber();
            var pos = new Position(500, 1000, Facing.Right);
            var nearest = climber.FindNearestClimbableSurface(pos, Array.Empty<WalkableSurface>());
            Assert.Null(nearest);
        }
    }

    /// <summary>
    /// Tests for MovementService surface walking mode.
    /// </summary>
    public class SurfaceMovementTests
    {
        private class FakeRandom : IRandom
        {
            private readonly double[] _values;
            private int _idx;
            public FakeRandom(double[] values) => _values = values;
            public double NextDouble() => _values[_idx++ % _values.Length];
            public int Next(int maxExclusive) => (int)(_values[_idx++ % _values.Length] * maxExclusive);
            public int Next(int minInclusive, int maxExclusive) => minInclusive + (int)(_values[_idx++ % _values.Length] * (maxExclusive - minInclusive));
        }

        private static (MovementService svc, WorkAreaRect area) Make(
            IRandom rng, double spriteW = 128, double? initialX = null,
            double width = 1920, double height = 1080)
        {
            var area = new WorkAreaRect(0, 0, width, height);
            var svc = new MovementService(area, rng, spriteW, initialX);
            return (svc, area);
        }

        [Fact]
        public void TransitionToSurface_UpdatesYToSurfaceTop()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 500);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            Assert.True(svc.IsOnSurface);
            Assert.Equal(300 - 128, svc.Position.Y); // surface.Top - spriteWidth
        }

        [Fact]
        public void TransitionToSurface_ClampsXToSurface()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 100);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            Assert.Equal(400, svc.Position.X); // clamped to surface.Left
        }

        [Fact]
        public void WalkOnSurface_MovesHorizontally()
        {
            // Use a wider surface so 100px walk doesn't hit edge
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 500);
            var surface = new WalkableSurface(200, 300, 1000, new IntPtr(1));
            svc.TransitionToSurface(surface);
            svc.Walk(100, 1.0); // 100 px/s for 1s
            Assert.Equal(600, svc.Position.X);
            Assert.Equal(300 - 128, svc.Position.Y); // still on surface
        }

        [Fact]
        public void WalkOnSurface_TurnsAtSurfaceEdge()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 470);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            // Walk right — should hit surface right edge (600 - 128 = 472)
            var outcome = svc.Walk(100, 1.0);
            Assert.Equal(EdgeOutcome.TurnedAround, outcome);
            Assert.Equal(Facing.Left, svc.Facing);
        }

        [Fact]
        public void IsAtSurfaceEdge_TrueAtSurfaceBoundary()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 400);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            Assert.True(svc.IsAtSurfaceEdge);
        }

        [Fact]
        public void LeaveSurface_ReturnsToBottom()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 450);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            Assert.True(svc.IsOnSurface);
            svc.LeaveSurface();
            Assert.False(svc.IsOnSurface);
            Assert.Equal(1080 - 128, svc.Position.Y); // bottom anchor
        }

        [Fact]
        public void LeaveSurface_FiresSurfaceLeftEvent()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 450);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            bool fired = false;
            svc.SurfaceLeft += () => fired = true;
            svc.LeaveSurface();
            Assert.True(fired);
        }

        [Fact]
        public void CheckSurfaceExists_RemovesGoneSurface()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 450);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            // Surface is not in the new list
            bool lost = svc.CheckSurfaceExists(Array.Empty<WalkableSurface>());
            Assert.True(lost);
            Assert.False(svc.IsOnSurface);
        }

        [Fact]
        public void CheckSurfaceExists_KeepsExistingSurface()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 450);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            var surfaces = new[] { surface };
            bool lost = svc.CheckSurfaceExists(surfaces);
            Assert.False(lost);
            Assert.True(svc.IsOnSurface);
        }

        [Fact]
        public void CheckSurfaceExists_UpdatesPositionWhenSurfaceMoves()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 450);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            // Surface moved down 50px
            var movedSurface = new WalkableSurface(400, 350, 600, new IntPtr(1));
            svc.CheckSurfaceExists(new[] { movedSurface });
            Assert.Equal(350 - 128, svc.Position.Y);
        }

        [Fact]
        public void CheckSurfaceExists_FiresSurfaceLeftWhenGone()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 450);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            bool fired = false;
            svc.SurfaceLeft += () => fired = true;
            svc.CheckSurfaceExists(Array.Empty<WalkableSurface>());
            Assert.True(fired);
        }

        [Fact]
        public void UpdateSurfaceTimer_AccumulatesTime()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 450);
            var surface = new WalkableSurface(400, 300, 600, new IntPtr(1));
            svc.TransitionToSurface(surface);
            svc.UpdateSurfaceTimer(2.0);
            Assert.False(svc.CanLeaveSurface); // 2 < 3
            svc.UpdateSurfaceTimer(1.5);
            Assert.True(svc.CanLeaveSurface); // 3.5 >= 3
        }

        [Fact]
        public void Walk_OffSurface_UsesWorkAreaBounds()
        {
            var (svc, _) = Make(new FakeRandom(new[] { 0.5 }), initialX: 100);
            // Not on surface, walk should use work area bounds
            svc.Walk(100, 1.0);
            Assert.Equal(200, svc.Position.X);
            Assert.Equal(1080 - 128, svc.Position.Y); // bottom anchor
        }
    }

    /// <summary>
    /// Mock ISurfaceProvider for testing.
    /// </summary>
    public class MockSurfaceProvider : ISurfaceProvider
    {
        private WalkableSurface[] _surfaces = Array.Empty<WalkableSurface>();
        public event Action? SurfacesChanged;

        public WalkableSurface[] GetSurfaces() => _surfaces;

        public void SetSurfaces(WalkableSurface[] surfaces)
        {
            _surfaces = surfaces;
            SurfacesChanged?.Invoke();
        }
    }
}