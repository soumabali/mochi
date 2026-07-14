using System;
using MochiV2.Core.Behavior;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Mini ball game service. Post-MVP Phase G-3.
    /// A ball appears near the cat. User clicks to throw it.
    /// Cat chases the ball (walks toward it). When cat reaches ball, meows.
    /// No sprites needed — ball is a vector-drawn particle.
    /// </summary>
    public sealed class MiniBallGameService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(MiniBallGameService));

        private readonly IRandom _random;
        private double _ballX, _ballY;
        private double _ballVelX, _ballVelY;
        private bool _ballActive;

        /// <summary>True when a ball is currently in play.</summary>
        public bool IsBallActive => _ballActive;

        /// <summary>Ball position.</summary>
        public (double X, double Y) BallPosition => (_ballX, _ballY);

        /// <summary>Ball velocity for physics.</summary>
        public (double VelX, double VelY) BallVelocity => (_ballVelX, _ballVelY);

        /// <summary>Gravity for ball physics.</summary>
        public double BallGravity { get; set; } = 2000.0;

        /// <summary>Ground level (Y coordinate where ball rests).</summary>
        public double GroundY { get; set; } = 900;

        /// <summary>Fired when cat should chase the ball. Passes target X.</summary>
        public event Action<double>? ChaseBall;

        /// <summary>Fired when cat reaches the ball.</summary>
        public event Action? BallCaught;

        public MiniBallGameService(IRandom random)
        {
            _random = random;
        }

        /// <summary>Throw a ball from cat's position.</summary>
        public void ThrowBall(double catX, double catY, double velX, double velY)
        {
            _ballX = catX;
            _ballY = catY;
            _ballVelX = velX;
            _ballVelY = velY;
            _ballActive = true;
            ChaseBall?.Invoke(_ballX);
            Logger.Debug("Ball thrown from ({X:F1},{Y:F1}) vel=({VX:F1},{VY:F1})", catX, catY, velX, velY);
        }

        /// <summary>Throw a ball randomly from cat position.</summary>
        public void ThrowRandom(double catX, double catY)
        {
            double velX = (_random.NextDouble() - 0.5) * 800;
            double velY = -300 - _random.NextDouble() * 400; // upward
            ThrowBall(catX, catY, velX, velY);
        }

        /// <summary>Tick ball physics — call every frame.</summary>
        public void Tick(double deltaSeconds)
        {
            if (!_ballActive) return;
            double dt = deltaSeconds;
            _ballVelY += BallGravity * dt;
            _ballX += _ballVelX * dt;
            _ballY += _ballVelY * dt;
            _ballVelX *= 0.99; // air resistance

            // Ground bounce
            if (_ballY >= GroundY)
            {
                _ballY = GroundY;
                _ballVelY = -_ballVelY * 0.5; // bounce
                _ballVelX *= 0.7;

                if (Math.Abs(_ballVelY) < 50 && Math.Abs(_ballVelX) < 20)
                {
                    // Ball stopped — cat can catch
                    _ballActive = false;
                    BallCaught?.Invoke();
                    Logger.Debug("Ball stopped at ({X:F1},{Y:F1})", _ballX, _ballY);
                }
            }
        }

        /// <summary>Check if cat is close enough to catch the ball.</summary>
        public bool IsCatNearBall(double catX, double catY, double threshold = 50)
        {
            if (!_ballActive) return false;
            double dx = _ballX - catX;
            double dy = _ballY - catY;
            return Math.Sqrt(dx * dx + dy * dy) < threshold;
        }

        /// <summary>Remove the ball.</summary>
        public void RemoveBall()
        {
            _ballActive = false;
        }
    }
}