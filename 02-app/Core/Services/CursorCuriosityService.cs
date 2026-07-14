using System;
using MochiV2.Core.Behavior;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Cursor curiosity behavior for Mochi. PRD §6.5.
    ///
    /// <list type="bullet">
    /// <item>Cursor idle 30s → Mochi walks toward cursor position.</item>
    /// <item>Fast cursor &gt;1500 px/s → 20% chance Mochi startled (Surprised state).</item>
    /// </list>
    ///
    /// Subscribes to <see cref="MouseMovedEvent"/> from the <see cref="EventBus"/>.
    /// Uses <see cref="ITimeProvider"/> for idle detection and <see cref="IRandom"/>
    /// for the Surprised probability roll.
    /// </summary>
    public sealed class CursorCuriosityService
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(CursorCuriosityService));

        //──────── Configuration (PRD §6.5) ────────

        /// <summary>Seconds cursor must be idle before Mochi walks toward it.</summary>
        public const double IdleThresholdSeconds = 30.0;

        /// <summary>Cursor velocity (px/s) considered "fast" for startled chance.</summary>
        public const double FastCursorVelocity = 1500.0;

        /// <summary>Chance Surprised is triggered on fast cursor (PRD: 20%).</summary>
        public const double SurprisedChance = 0.20;

        //──────── Dependencies ────────

        private readonly EventBus _eventBus;
        private readonly FSM _fsm;
        private readonly ITimeProvider _time;
        private readonly IRandom _random;

        //──────── State ────────

        private double _lastMoveTime;
        private double _lastCursorX;
        private double _lastCursorY;
        private bool _idleCuriosityTriggered;

        // Cat position for direction determination.
        private double _catX;
        private double _catY;

        /// <summary>
        /// Create the cursor curiosity service.
        /// </summary>
        /// <param name="eventBus">EventBus to subscribe MouseMovedEvent.</param>
        /// <param name="fsm">FSM to drive state transitions (Surprised, Walk).</param>
        /// <param name="time">Time provider for idle detection.</param>
        /// <param name="random">RNG for 20% Surprised roll.</param>
        /// <exception cref="ArgumentNullException">Any dependency null.</exception>
        public CursorCuriosityService(
            EventBus eventBus,
            FSM fsm,
            ITimeProvider time,
            IRandom random)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _lastMoveTime = _time.GetElapsedSeconds();
            _eventBus.Subscribe<MouseMovedEvent>(OnMouseMoved);
            Logger.Debug("CursorCuriosityService subscribed MouseMovedEvent.");
        }

        /// <summary>
        /// Update cat sprite position for walk-direction determination.
        /// Called by layout/MovementService each layout pass.
        /// </summary>
        public void UpdateCatPosition(double x, double y)
        {
            _catX = x;
            _catY = y;
        }

        /// <summary>
        /// Periodic tick to evaluate idle curiosity. Should be called every
        /// second or so (e.g. from a timer) to detect idle even when no
        /// mouse events arrive.
        /// </summary>
        public void Tick()
        {
            double now = _time.GetElapsedSeconds();
            double idleDuration = now - _lastMoveTime;

            if (idleDuration >= IdleThresholdSeconds && !_idleCuriosityTriggered)
            {
                _idleCuriosityTriggered = true;
                Logger.Information(
                    "Cursor idle {Seconds:F0}s — Mochi curious, walking toward ({X:F0}, {Y:F0}).",
                    idleDuration, _lastCursorX, _lastCursorY);
                WalkTowardCursor();
            }
        }

        //───────────────────── MouseMoved ─────────────────────

        private void OnMouseMoved(MouseMovedEvent e)
        {
            double now = _time.GetElapsedSeconds();
            _lastMoveTime = now;
            _lastCursorX = e.X;
            _lastCursorY = e.Y;
            _idleCuriosityTriggered = false;

            // Fast cursor → 20% chance Surprised.
            if (e.Velocity > FastCursorVelocity)
            {
                double roll = _random.NextDouble();
                if (roll < SurprisedChance)
                {
                    Logger.Information(
                        "Fast cursor {Vel:F0}px/s — Mochi startled (roll={Roll:F2}).",
                        e.Velocity, roll);
                    TryInterrupt(FSMState.Surprised);
                }
                else
                {
                    Logger.Debug(
                        "Fast cursor {Vel:F0}px/s — not startled (roll={Roll:F2}).",
                        e.Velocity, roll);
                }
            }
        }

        private void WalkTowardCursor()
        {
            // Determine walk direction based on cursor relative to cat.
            FSMState walkState = _lastCursorX < _catX
                ? FSMState.WalkLeft
                : FSMState.WalkRight;

            TryInterrupt(walkState);

            // Publish a MouseMovedEvent so MovementService/layout can pick up
            // the target position if needed downstream.
            PublishSafe(new CursorNearCatEvent(0.0));
        }

        private void TryInterrupt(FSMState state)
        {
            try
            {
                if (_fsm.CurrentState == state)
                    return;
                _fsm.Interrupt(state);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "FSM interrupt {State} failed.", state);
            }
        }

        private void PublishSafe<T>(T evt) where T : notnull
        {
            try
            {
                _eventBus.Publish(evt);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "{Type} publish handler threw.", typeof(T).Name);
            }
        }
    }
}