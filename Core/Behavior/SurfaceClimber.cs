using System;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// Calculates climb trajectories for Mochi to jump from the bottom edge
    /// (or lower surface) up to a window-top surface.
    /// Post-MVP Phase E (PRD §5: window-top walking).
    /// </summary>
    public sealed class SurfaceClimber
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(SurfaceClimber));

        /// <summary>Maximum vertical jump distance in pixels.</summary>
        public const double MaxClimbDistance = 300.0;

        /// <summary>Gravity used for arc trajectory (pixels/sec², matches PhysicsEngine).</summary>
        public const double ClimbGravity = 2400.0;

        /// <summary>
        /// Checks if Mochi can climb to the given surface from current position.
        /// True when surface.Top is above current position (lower Y) and within
        /// <see cref="MaxClimbDistance"/> pixels vertically.
        /// </summary>
        public bool CanClimbTo(WalkableSurface surface, Position currentPos)
        {
            double verticalDistance = currentPos.Y - surface.Top;
            Logger.Debug("CanClimbTo: vertical={Vert:F1} (from Y={FromY:F1} to top={TopY:F1})",
                verticalDistance, currentPos.Y, surface.Top);
            return verticalDistance > 0 && verticalDistance <= MaxClimbDistance;
        }

        /// <summary>
        /// Calculates the parabolic climb arc from current position to the
        /// nearest horizontal point on the target surface.
        /// </summary>
        public ClimbArc GetClimbArc(Position from, WalkableSurface to)
        {
            // Target X: clamp from.X to surface horizontal range
            double targetX = Math.Clamp(from.X, to.Left, to.Right);

            // Target Y: surface top (cat's feet rest here)
            double targetY = to.Top;

            // Horizontal distance
            double dx = targetX - from.X;
            // Vertical distance (negative = going up)
            double dy = targetY - from.Y; // targetY < from.Y when going up

            // Calculate duration using parabolic motion:
            // y(t) = y0 + v0y * t + 0.5 * g * t²
            // For a simple arc, we'll use a fixed initial velocity that
            // gets the cat to the target height, then compute duration.
            // Simplified: use a fixed arc time based on distance.
            double verticalDistance = Math.Abs(dy);
            double duration = Math.Sqrt(2.0 * verticalDistance / ClimbGravity);

            // Ensure minimum duration
            if (duration < 0.2)
                duration = 0.2;

            Logger.Debug("ClimbArc: ({FromX:F1},{FromY:F1}) → ({ToX:F1},{ToY:F1}), dur={Dur:F2}s",
                from.X, from.Y, targetX, targetY, duration);

            return new ClimbArc(from.X, from.Y, targetX, targetY, duration);
        }

        /// <summary>
        /// Finds the nearest surface within climb range from the current position.
        /// Returns null if none available.
        /// </summary>
        public WalkableSurface? FindNearestClimbableSurface(
            Position currentPos, WalkableSurface[] surfaces)
        {
            WalkableSurface? best = null;
            double bestDist = double.MaxValue;

            foreach (var surface in surfaces)
            {
                if (!CanClimbTo(surface, currentPos))
                    continue;

                // Horizontal distance to surface center
                double surfaceCenter = (surface.Left + surface.Right) / 2.0;
                double hDist = Math.Abs(surfaceCenter - currentPos.X);
                double vDist = currentPos.Y - surface.Top;
                double totalDist = Math.Sqrt(hDist * hDist + vDist * vDist);

                if (totalDist < bestDist)
                {
                    bestDist = totalDist;
                    best = surface;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Immutable climb trajectory data: start and end positions with duration.
    /// </summary>
    public readonly struct ClimbArc
    {
        /// <summary>Start X (sprite left edge).</summary>
        public double StartX { get; }

        /// <summary>Start Y (sprite top edge).</summary>
        public double StartY { get; }

        /// <summary>End X (sprite left edge on surface).</summary>
        public double EndX { get; }

        /// <summary>End Y (sprite top edge on surface).</summary>
        public double EndY { get; }

        /// <summary>Total climb duration in seconds.</summary>
        public double DurationSeconds { get; }

        public ClimbArc(double startX, double startY, double endX, double endY, double durationSeconds)
        {
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
            DurationSeconds = durationSeconds;
        }
    }
}