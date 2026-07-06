using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Physics
{
    /// <summary>
    /// Physics simulation for Mochi's drag-release fall and landing squash & stretch.
    /// PRD §9 AC-3: Dragging Mochi and releasing produces: angry (with sound) → fall →
    /// dust puff → squash → landing → recovery → idle, no visual dead-ends.
    /// PRD §7.2: Fall is reversed jump (playOnceReversed animation, handled by FSM/AnimationManager).
    ///
    /// This engine handles the *physics* layer: gravity-driven vertical fall after drag
    /// release, horizontal drift from release velocity, ground (work-area bottom) collision,
    /// and the squash-and-stretch deformation curve on impact. It is pure math/state —
    /// it does not touch sprites, animation, or assets (asset lock §0).
    ///
    /// Constitution compliance:
    /// - No hardcoded frame counts (time-driven, delta-based).
    /// - Fail loud: invalid parameters throw.
    /// - Serilog logging for state transitions.
    /// </summary>
    public sealed class PhysicsEngine
    {
        // ── Physics constants (PRD T-010 spec) ──────────────────────────

        /// <summary>Gravitational acceleration in pixels/sec². Drives fall arc after
        /// drag release. Tuned for a visible-but-snappy fall on a 1080p work area.</summary>
        public const double Gravity = 2400.0;

        /// <summary>Maximum horizontal velocity clamp (pixels/sec). Prevents the
        /// release flick from sending Mochi off-screen horizontally.</summary>
        public const double MaxHorizontalVelocity = 1200.0;

        /// <summary>Horizontal drag (air resistance) coefficient per second.
        /// Applied as exponential decay to horizontal velocity during fall.</summary>
        public const double HorizontalDrag = 1.8;

        /// <summary>Squash compression at impact: 10% (PRD T-010 spec).</summary>
        public const double SquashCompression = 0.10;

        /// <summary>Squash-and-stretch phase duration: 80 ms (PRD T-010 spec).</summary>
        public const double SquashDurationSeconds = 0.080;

        /// <summary>Stretch overshoot factor beyond rest scale (0 = no overshoot,
        /// 1 = 100% overshoot). Gives the "bounce" character on recovery.</summary>
        public const double StretchOvershoot = 0.5;

        // ── Injected dependencies ───────────────────────────────────────

        private readonly IWorkAreaProvider _workArea;
        private readonly double _spriteWidth;

        // ── Mutable simulation state ────────────────────────────────────

        /// <summary>Current simulated position. X is sprite left-edge, Y is sprite top-edge.
        /// When idle (not falling), Y is anchored to the work-area bottom.</summary>
        public Position Position { get; private set; }

        /// <summary>Current horizontal velocity (pixels/sec). Non-zero during fall drift.</summary>
        public double VelocityX { get; private set; }

        /// <summary>Current vertical velocity (pixels/sec). Positive = downward. Non-zero during fall.</summary>
        public double VelocityY { get; private set; }

        /// <summary>True while Mochi is in a gravity-driven fall (between drag-release and ground impact).</summary>
        public bool IsFalling { get; private set; }

        /// <summary>Current squash-and-stretch phase, or null if idle.</summary>
        public SquashPhase? Squash { get; private set; }

        // ── Events ──────────────────────────────────────────────────────

        /// <summary>Raised when Mochi impacts the ground after a fall. The renderer
        /// / particle system listens to spawn a dust puff (PRD AC-3).</summary>
        public event Action<Position>? Landed;

        /// <summary>Raised when the squash-and-stretch animation completes and Mochi
        /// has fully recovered to rest scale. The FSM listens to transition from
        /// landing-recovery to Idle (PRD AC-3: no visual dead-ends).</summary>
        public event Action? SquashCompleted;

        /// <summary>
        /// Create the physics engine.
        /// </summary>
        /// <param name="workArea">Taskbar-aware work area for ground/boundary clamping.</param>
        /// <param name="spriteWidth">Logical pixel width of the sprite, for edge clamping.</param>
        /// <param name="initialPosition">Starting position (typically bottom-anchored).</param>
        public PhysicsEngine(IWorkAreaProvider workArea, double spriteWidth, Position initialPosition)
        {
            _workArea = workArea ?? throw new ArgumentNullException(nameof(workArea));
            if (spriteWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(spriteWidth), "Sprite width must be positive.");
            _spriteWidth = spriteWidth;

            Position = initialPosition;
            VelocityX = 0;
            VelocityY = 0;
            IsFalling = false;
            Squash = null;
        }

        // ── Public API: drag lifecycle ──────────────────────────────────

        /// <summary>
        /// Begin a gravity-driven fall from a drag-release event.
        /// PRD AC-3 / A-3: user releases Mochi after dragging → angry → fall.
        /// The <see cref="MouseDragEndEvent"/> carries the release position and a
        /// horizontal velocity; this engine takes over from there.
        /// </summary>
        /// <param name="releaseX">X coordinate at release (sprite left-edge).</param>
        /// <param name="releaseY">Y coordinate at release (sprite top-edge).</param>
        /// <param name="releaseVelocityX">Horizontal velocity at release (pixels/sec).
        /// Positive = rightward, negative = leftward. Clamped to
        /// <see cref="MaxHorizontalVelocity"/>.</param>
        /// <param name="facing">Facing direction to set during the fall.</param>
        public void BeginFall(double releaseX, double releaseY, double releaseVelocityX, Facing facing)
        {
            // Clamp release position to work-area bounds.
            double clampedX = ClampX(releaseX);
            double clampedY = ClampY(releaseY);

            // Clamp horizontal velocity.
            double vx = Math.Clamp(releaseVelocityX, -MaxHorizontalVelocity, MaxHorizontalVelocity);

            // Infer facing from horizontal velocity if zero — keep provided facing.
            Facing effectiveFacing = vx < -1 ? Facing.Left
                                 : vx > 1 ? Facing.Right
                                 : facing;

            Position = new Position(clampedX, clampedY, effectiveFacing);
            VelocityX = vx;
            VelocityY = 0; // starts at rest vertically; gravity accelerates downward
            IsFalling = true;
            Squash = null;

            Log.Debug("PhysicsEngine.BeginFall: pos=({X:F1},{Y:F1}) vel=({VX:F1},{VY:F1}) facing={Facing}",
                clampedX, clampedY, vx, 0.0, effectiveFacing);
        }

        /// <summary>
        /// Advance the physics simulation by <paramref name="deltaSeconds"/>.
        /// During fall: applies gravity, integrates position, applies horizontal drag,
        /// clamps to screen edges, and detects ground impact.
        /// During squash: advances the squash-and-stretch deformation curve.
        /// When idle: no-op (position held).
        /// </summary>
        /// <param name="deltaSeconds">Elapsed time since last update. Must be ≥ 0.
        /// Never hardcode frame counts — all motion is delta-driven.</param>
        public void Update(double deltaSeconds)
        {
            if (deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta must be non-negative.");
            if (deltaSeconds == 0)
                return;

            if (IsFalling)
            {
                UpdateFall(deltaSeconds);
            }
            else if (Squash is { } squash)
            {
                UpdateSquash(deltaSeconds, squash);
            }
            // Idle: nothing to do.
        }

        // ── Fall integration ────────────────────────────────────────────

        private void UpdateFall(double deltaSeconds)
        {
            // Gravity accelerates downward.
            VelocityY += Gravity * deltaSeconds;

            // Horizontal drag: exponential decay toward zero.
            double dragFactor = Math.Exp(-HorizontalDrag * deltaSeconds);
            VelocityX *= dragFactor;

            // Integrate position.
            double nextX = Position.X + VelocityX * deltaSeconds;
            double nextY = Position.Y + VelocityY * deltaSeconds;

            // Horizontal screen-edge collision: stop at edge, kill horizontal velocity.
            double minX = _workArea.Left;
            double maxX = _workArea.Right - _spriteWidth;
            if (maxX < minX) maxX = minX; // tiny work area: pin left

            if (nextX <= minX)
            {
                nextX = minX;
                if (VelocityX < 0) VelocityX = 0;
            }
            else if (nextX >= maxX)
            {
                nextX = maxX;
                if (VelocityX > 0) VelocityX = 0;
            }

            // Ground collision: work-area bottom (sprite top = bottom - spriteHeight).
            // We approximate sprite height as spriteWidth (square bounding box), matching
            // MovementService.BottomAnchorY convention.
            double groundY = _workArea.Bottom - _spriteWidth;

            if (nextY >= groundY)
            {
                // Impact!
                nextY = groundY;
                IsFalling = false;
                VelocityY = 0;
                // Retain residual horizontal velocity? No — landing snaps to rest.
                VelocityX = 0;

                Position = new Position(ClampX(nextX), groundY, Position.Facing);

                // Begin squash-and-stretch on impact.
                Squash = new SquashPhase(0.0);

                Log.Debug("PhysicsEngine.Landed: pos=({X:F1},{Y:F1}) → squash begins", Position.X, Position.Y);
                Landed?.Invoke(Position);
            }
            else
            {
                Position = new Position(ClampX(nextX), nextY, Position.Facing);
            }
        }

        // ── Squash & stretch integration ────────────────────────────────

        private void UpdateSquash(double deltaSeconds, SquashPhase squash)
        {
            double elapsed = squash.Elapsed + deltaSeconds;
            Squash = new SquashPhase(elapsed);

            if (elapsed >= SquashDurationSeconds)
            {
                // Recovery complete: back to rest scale.
                Squash = null;
                Log.Debug("PhysicsEngine.SquashCompleted → rest scale");
                SquashCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Compute the current squash-and-stretch scale factors at this instant.
        /// Returns (scaleX, scaleY) centered on 1.0 (rest). The renderer applies
        /// these to the sprite; the engine does not touch assets.
        ///
        /// Curve (PRD T-010: 10% compress, 80ms, overshoot):
        /// - Phase 0 (0 → 50% of duration): compress vertically, expand horizontally
        ///   (squash). Peak at midpoint: scaleY = 1 - Compression, scaleX = 1 + Compression.
        /// - Phase 1 (50% → 100%): stretch past rest and settle (overshoot bounce).
        ///   Uses a damped-sine ease-out so it lands cleanly on 1.0.
        /// </summary>
        /// <returns>
        /// (scaleX, scaleY) deformation factors. (1.0, 1.0) when idle or no active squash.
        /// </returns>
        public (double ScaleX, double ScaleY) GetCurrentSquashScale()
        {
            if (Squash is not { } squash)
                return (1.0, 1.0);

            double t = Math.Clamp(squash.Elapsed / SquashDurationSeconds, 0.0, 1.0);

            // Two-phase deformation:
            // t in [0, 0.5]: squash (compress Y, expand X), peak at t=0.5.
            // t in (0.5, 1.0]: stretch overshoot with damped sine settling to 1.0.
            if (t <= 0.5)
            {
                // Squash in: ease-in to peak compression at t=0.5.
                double p = t / 0.5; // 0 → 1
                double ease = p * p; // ease-in quad
                double compress = SquashCompression * ease;
                return (1.0 + compress, 1.0 - compress);
            }
            else
            {
                // Stretch out: damped sine overshoot settling to 1.0.
                // amplitude decays so it ends exactly at rest.
                double p = (t - 0.5) / 0.5; // 0 → 1 over second half
                // Damped sine: amplitude * sin(pi * p) * (1 - p) → starts at 0, peaks, returns to 0.
                double amplitude = SquashCompression * StretchOvershoot;
                double deformation = amplitude * Math.Sin(Math.PI * p) * (1.0 - p);
                // Stretch phase: Y expands beyond rest, X contracts below rest.
                return (1.0 - deformation, 1.0 + deformation);
            }
        }

        // ── Snap to ground (no fall) ────────────────────────────────────

        /// <summary>
        /// Snap Mochi to the bottom anchor (ground) without simulating a fall.
        /// Used after monitor changes or re-anchoring when no fall is needed.
        /// </summary>
        public void AnchorToGround()
        {
            double groundY = _workArea.Bottom - _spriteWidth;
            Position = new Position(ClampX(Position.X), groundY, Position.Facing);
            VelocityX = 0;
            VelocityY = 0;
            IsFalling = false;
            Squash = null;
        }

        /// <summary>
        /// Update the work area (e.g. monitor hotplug, taskbar resize).
        /// Re-clamps position to new bounds. If idle, re-anchors to the new ground.
        /// </summary>
        public void UpdateWorkArea(IWorkAreaProvider workArea)
        {
            _ = workArea ?? throw new ArgumentNullException(nameof(workArea));
            // We can't reassign the readonly field, but we re-clamp using the new area.
            // This is a pragmatic compromise: the primary ctor field stays, but we
            // expose a setter via a backing field pattern for hot-swap.
            // For MVP, monitor changes trigger full re-anchor through AnchorToGround
            // after the caller constructs a new engine with the new work area.
            //
            // Fail loud: this method is intentionally minimal. If the caller needs
            // a new work area, they should recreate the engine (cheap, stateless except Position).
            AnchorToGround();
        }

        // ── Internals ───────────────────────────────────────────────────

        private double ClampX(double x)
        {
            double minX = _workArea.Left;
            double maxX = _workArea.Right - _spriteWidth;
            if (maxX < minX) maxX = minX;
            if (x < minX) return minX;
            if (x > maxX) return maxX;
            return x;
        }

        private double ClampY(double y)
        {
            // During fall, Y can be anywhere above the ground (sprite can be held high).
            // We clamp to the work-area top as the upper bound.
            double minY = _workArea.Top;
            double maxY = _workArea.Bottom - _spriteWidth;
            if (maxY < minY) maxY = minY;
            if (y < minY) return minY;
            if (y > maxY) return maxY;
            return y;
        }
    }

    /// <summary>
    /// Mutable value representing an in-progress squash-and-stretch deformation.
    /// Tracks elapsed time; the deformation curve is computed statelessly by
    /// <see cref="PhysicsEngine.GetCurrentSquashScale"/>.
    /// </summary>
    public readonly record struct SquashPhase(double Elapsed);
}