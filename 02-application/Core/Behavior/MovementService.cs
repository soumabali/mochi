using System;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Behavior
{
    /// <summary>
    /// PRD §7.2 movement physics: horizontal walking with screen-edge
    /// turn-around. PRD §6.5: 15% chance to sit+blink before turning.
    ///
    /// MVP: bottom-edge only, anchored to bottom of work area.
    /// Post-MVP Phase E: also supports walking on WalkableSurface
    /// (window title-bar tops). When CurrentSurface is non-null,
    /// Y is anchored to the surface top.
    /// </summary>
    public sealed class MovementService
    {
        private readonly IWorkAreaProvider _workArea;
        private readonly IRandom _random;
        private readonly double _spriteWidth;

        public event Action<Facing>? TurnedAround;
        public event Action? SitAndBlinkTriggered;

        /// <summary>Raised when the current surface disappears.</summary>
        public event Action? SurfaceLeft;

        public Position Position { get; private set; }

        public Facing Facing => Position.Facing;

        /// <summary>Surface the cat is on, or null if bottom edge.</summary>
        public WalkableSurface? CurrentSurface { get; private set; }

        public bool IsOnSurface => CurrentSurface.HasValue;

        public bool IsAtScreenEdge =>
            Position.X <= _workArea.Left ||
            Position.X + _spriteWidth >= _workArea.Right;

        public bool IsAtSurfaceEdge
        {
            get
            {
                if (!CurrentSurface.HasValue) return false;
                var s = CurrentSurface.Value;
                return Position.X <= s.Left || Position.X + _spriteWidth >= s.Right;
            }
        }

        public double SpriteWidth => _spriteWidth;

        public double SitAndBlinkChance { get; set; } = 0.15;

        public double SurfaceHysteresisSeconds { get; set; } = 3.0;

        private double _surfaceTimer;

        public MovementService(IWorkAreaProvider workArea, IRandom random, double spriteWidth, double? initialX = null)
        {
            _workArea = workArea;
            _random = random;
            _spriteWidth = spriteWidth;
            if (initialX.HasValue)
                Position = new Position(ClampX(initialX.Value), BottomAnchorY(), Facing.Right);
        }

        public void SetInitialPosition()
        {
            double initialX = ClampX((_workArea.Width - _spriteWidth) / 2.0);
            double y = BottomAnchorY();
            Position = new Position(initialX, y, Facing.Right);
        }

        public EdgeOutcome Walk(double speed, double deltaSeconds, Facing? direction = null)
        {
            if (speed < 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            Facing dir = direction ?? Facing;
            double dx = speed * deltaSeconds;
            double nextX = Position.X + (dir == Facing.Left ? -dx : dx);

            double minX, maxX;
            if (CurrentSurface.HasValue)
            {
                var s = CurrentSurface.Value;
                minX = s.Left;
                maxX = s.Right - _spriteWidth;
            }
            else
            {
                minX = _workArea.Left;
                maxX = _workArea.Right - _spriteWidth;
            }

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

        public void TurnAround()
        {
            Facing newFacing = Position.Facing == Facing.Left ? Facing.Right : Facing.Left;
            Position = Position.WithFacing(newFacing);
            TurnedAround?.Invoke(newFacing);
            Log.Debug("MovementService.TurnAround {Facing}", newFacing);
        }

        public void ReanchorToBottom()
        {
            double clampedX = ClampX(Position.X);
            Position = new Position(clampedX, BottomAnchorY(), Position.Facing);
        }

        public void TransitionToSurface(WalkableSurface surface)
        {
            CurrentSurface = surface;
            _surfaceTimer = 0;
            double surfaceY = surface.Top - _spriteWidth;
            double clampedX = Math.Clamp(Position.X, surface.Left, surface.Right - _spriteWidth);
            Position = new Position(clampedX, surfaceY, Position.Facing);
            Log.Debug("Transitioned to surface at ({X:F1}, {Y:F1})", clampedX, surfaceY);
        }

        public void LeaveSurface()
        {
            if (!CurrentSurface.HasValue) return;
            CurrentSurface = null;
            _surfaceTimer = 0;
            ReanchorToBottom();
            SurfaceLeft?.Invoke();
            Log.Debug("Left surface, reanchored to bottom.");
        }

        public bool CheckSurfaceExists(WalkableSurface[] currentSurfaces)
        {
            if (!CurrentSurface.HasValue) return false;

            var current = CurrentSurface.Value;
            foreach (var s in currentSurfaces)
            {
                if (s.SurfaceHandle == current.SurfaceHandle)
                {
                    if (s.Left != current.Left || s.Top != current.Top || s.Right != current.Right)
                    {
                        double surfaceY = s.Top - _spriteWidth;
                        double clampedX = Math.Clamp(Position.X, s.Left, s.Right - _spriteWidth);
                        Position = new Position(clampedX, surfaceY, Position.Facing);
                        CurrentSurface = s;
                        Log.Debug("Surface moved, updated cat to ({X:F1}, {Y:F1})", clampedX, surfaceY);
                    }
                    return false;
                }
            }

            Log.Debug("Surface gone (HWND {Handle}), leaving.", current.SurfaceHandle);
            CurrentSurface = null;
            _surfaceTimer = 0;
            SurfaceLeft?.Invoke();
            return true;
        }

        public void UpdateSurfaceTimer(double deltaSeconds)
        {
            if (CurrentSurface.HasValue)
                _surfaceTimer += deltaSeconds;
        }

        public bool CanLeaveSurface => _surfaceTimer >= SurfaceHysteresisSeconds;

        //---- internals -------------------------------------------------

        private EdgeOutcome HandleEdgeReached()
        {
            double roll = _random.NextDouble();
            if (roll < SitAndBlinkChance)
            {
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
            if (maxX < minX) maxX = minX;
            if (x < minX) return minX;
            if (x > maxX) return maxX;
            return x;
        }

        private double BottomAnchorY()
        {
            return _workArea.Bottom - _spriteWidth;
        }
    }

    public enum EdgeOutcome
    {
        None,
        TurnedAround,
        SitAndBlink
    }
}