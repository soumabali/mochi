using System;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// PRD §7.2 movement physics: bottom-edge horizontal walking with
    /// screen-edge turn-around. PRD §6.5 behavioral depth: 15% chance
    /// of sit+blink before turning at an edge.
    ///
    /// MVP is bottom-edge only (PRD §5 out-of-scope: window-top walking,
    /// window collision). Y is anchored to the bottom of the work area
    /// (taskbar-aware via <see cref="IWorkAreaProvider"/>).
    /// </summary>
    public sealed class MovementService
    {
        private readonly IWorkAreaProvider _workArea;
        private readonly IRandom _random;
        private readonly double _spriteWidth;

        // Cached edge outcome published to FSM/EventBus callers
        public event Action<Facing>? TurnedAround;
        public event Action? SitAndBlinkTriggered;

        /// <summary>
        /// Current position. X is the sprite's left edge; Y is the sprite's
        /// top edge such that the sprite sits on the bottom of the work area.
        /// </summary>
        public Position Position { get; private set; }

        public Facing Facing => Position.Facing;

        /// <summary>True when the sprite is flush against a horizontal screen edge.</summary>
        public bool IsAtScreenEdge =>
            Position.X <= _workArea.Left ||
            Position.X + _spriteWidth >= _workArea.Right;

        /// <summary>
        /// Marginal sprite width used for edge clamping. The sprite's left
        /// edge X is clamped to [WorkArea.Left, WorkArea.Right - SpriteWidth].
        /// </summary>
        public double SpriteWidth => _spriteWidth;

        /// <summary>
        /// Probability (0..1) that reaching a screen edge triggers a
        /// sit+blink before turning around. PRD §6.5: 15%.
        /// </summary>
        public double SitAndBlinkChance { get; set; } = 0.15;

        /// <param name="workArea">Taskbar-aware work area.</param>
        /// <param name="random">RNG for sit+blink roll.</param>
        /// <param name="spriteWidth">Logical pixel width of the sprite, for edge clamping.</param>
        /// <param name="initialX">Optional initial X (defaults to centered on the work area).</param>
        public MovementService(IWorkAreaProvider workArea, IRandom random, double spriteWidth, double? initialX = null)
        {
            _workArea = workArea ?? throw new ArgumentNullException(nameof(workArea));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (spriteWidth <= 0) throw new ArgumentOutOfRangeException(nameof(spriteWidth));
            _spriteWidth = spriteWidth;

            double x = initialX ?? ClampX((workArea.Width - spriteWidth) / 2.0);
            double y = BottomAnchorY();
            Position = new Position(ClampX(x), y, Facing.Right);
        }

        /// <summary>
        /// Walk horizontally for one tick.
        /// <paramref name="speed"/> is pixels/second; <paramref name="deltaSeconds"/>
        /// is the elapsed time since the last update. Direction is inferred
        /// from <see cref="Facing"/> when <paramref name="direction"/> is null.
        /// </summary>
        /// <returns>
        /// The edge outcome for this tick: <c>None</c>, <c>TurnedAround</c>,
        /// or <c>SitAndBlink</c> (caller drives FSM state accordingly).
        /// </returns>
        public EdgeOutcome Walk(double speed, double deltaSeconds, Facing? direction = null)
        {
            if (speed < 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            Facing dir = direction ?? Facing;
            double dx = speed * deltaSeconds;
            double nextX = Position.X + (dir == Facing.Left ? -dx : dx);

            // Edge reached?
            double minX = _workArea.Left;
            double maxX = _workArea.Right - _spriteWidth;

            if (nextX <= minX && dir == Facing.Left)
            {
                Position = new Position(minX, Position.Y, dir);
                return HandleEdgeReached();
            }
            if (nextX >= maxX && dir == Facing.Right)
            {
                Position = new Position(maxX, Position.Y, dir);
                return HandleEdgeReached();
            }

            Position = new Position(nextX, Position.Y, dir);
            return EdgeOutcome.None;
        }

        /// <summary>
        /// Explicit turn-around: flips <see cref="Facing"/> and raises
        /// <see cref="TurnedAround"/>. Use when the caller has already
        /// played a sit+blink and wants to turn after it completes.
        /// </summary>
        public void TurnAround()
        {
            Facing newFacing = Position.Facing == Facing.Left ? Facing.Right : Facing.Left;
            Position = Position.WithFacing(newFacing);
            TurnedAround?.Invoke(newFacing);
            Log.Debug("MovementService.TurnAround → {Facing}", newFacing);
        }

        /// <summary>
        /// Reposition Mochi at the bottom edge of the work area. Call after
        /// the work area changes (monitor hotplug, taskbar resize).
        /// </summary>
        public void ReanchorToBottom()
        {
            double clampedX = ClampX(Position.X);
            Position = new Position(clampedX, BottomAnchorY(), Position.Facing);
        }

        // ---- internals -------------------------------------------------

        private EdgeOutcome HandleEdgeReached()
        {
            double roll = _random.NextDouble();
            if (roll < SitAndBlinkChance)
            {
                // Sit + blink first; caller turns around after blink completes.
                SitAndBlinkTriggered?.Invoke();
                Log.Debug("Edge reached: sit+blink (roll={Roll:F2})", roll);
                return EdgeOutcome.SitAndBlink;
            }

            TurnAround();
            return EdgeOutcome.TurnedAround;
        }

        private double ClampX(double x)
        {
            double minX = _workArea.Left;
            double maxX = _workArea.Right - _spriteWidth;
            if (maxX < minX) maxX = minX; // tiny work area: pin to left
            if (x < minX) return minX;
            if (x > maxX) return maxX;
            return x;
        }

        private double BottomAnchorY()
        {
            // Sprite sits so its bottom edge rests on the work-area bottom.
            // Sprite height is assumed to equal spriteWidth * AspectRatio; we
            // approximate with spriteWidth to keep the service sprite-agnostic.
            // The renderer adjusts final Y; here we provide a stable anchor.
            return _workArea.Bottom - _spriteWidth;
        }
    }

    /// <summary>Outcome of a Walk tick at a screen edge.</summary>
    public enum EdgeOutcome
    {
        /// <summary>No edge event this tick.</summary>
        None,

        /// <summary>Turned around immediately (no sit+blink).</summary>
        TurnedAround,

        /// <summary>Decided to sit+blink; caller must call TurnAround() later.</summary>
        SitAndBlink
    }
}