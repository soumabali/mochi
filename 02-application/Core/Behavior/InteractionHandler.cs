using System;
using MochiV2.Core.Events;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Behavior
{
    ///<summary>
    ///Determines mouse interactions from <see cref="MouseMovedEvent"/> /
    ///<see cref="MouseClickedEvent"/> and drives the FSM + EventBus per
    ///PRD §7.1. Publishes domain events (proximity, petting, click, drag)
    ///and triggers FSM state transitions (Meow, Surprised, Playful, Drag).
    ///
    ///Delegates downstream effects (meow sound, hearts particles, fall
    ///physics) other subscribers via EventBus — this handler is purely
    ///detection + state orchestration.
    ///</summary>
    public sealed class InteractionHandler
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(InteractionHandler));

        //──────── Configuration (PRD §7.1 thresholds) ────────

        ///<summary>Distance (px) cursor considered "near cat face".</summary>
        public const double NearThreshold = 80.0;

        ///<summary>Hover seconds before petting triggers.</summary>
        public const double PetHoverSeconds = 3.0;

        ///<summary>Cursor velocity (px/s) "fast" → Surprised chance.</summary>
        public const double FastCursorVelocity = 1500.0;

        ///<summary>Chance Surprised on fast cursor (PRD: 20%).</summary>
        public const double SurprisedChance = 0.20;

        ///<summary>Double-click interval (s).</summary>
        public const double DoubleClickSeconds = 0.5;

        ///<summary>Sprite hit-test half-extent (px) for click/drag detection.</summary>
        public const double SpriteHitRadius = 64.0;

        //──────── Dependencies ────────

        private readonly EventBus _eventBus;
        private readonly FSM _fsm;
        private readonly ITimeProvider _time;
        private readonly IRandom _random;

        //──────── Hover tracking ────────

        private double _hoverStart = -1;
        private bool _pettingFired;
        private bool _nearCat;

        //──────── Drag tracking ────────

        private bool _dragging;
        private double _lastMoveX, _lastMoveY, _lastMoveTime;

        //──────── Double-click tracking ────────

        private double _lastClickTime = -1;

        //──────── Cached cursor ────────

        private double _cursorX, _cursorY, _cursorVel;

        ///<summary>
        ///Current cat sprite screen position (center). Updated
        ///<see cref="UpdateCatPosition"/> MovementService / layout.
        ///</summary>
        public double CatX { get; private set; }

        ///<summary>Current cat sprite screen Y (center).</summary>
        public double CatY { get; private set; }

        ///<summary>
        ///Create handler subscribing <paramref name="eventBus"/> events.
        ///</summary>
        ///<param name="eventBus">EventBus subscribe/publish.</param>
        ///<param name="fsm">Finite State Machine drive transitions.</param>
        ///<param name="time">Time provider hover/double-click timing.</param>
        ///<param name="random">RNG Surprised 20% roll.</param>
        ///<exception cref="ArgumentNullException">Any dependency null.</exception>
        public InteractionHandler(
            EventBus eventBus,
            FSM fsm,
            ITimeProvider time,
            IRandom random)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _eventBus.Subscribe<MouseMovedEvent>(OnMouseMoved);
            _eventBus.Subscribe<MouseClickedEvent>(OnMouseClicked);
            Logger.Debug("InteractionHandler subscribed MouseMoved + MouseClicked.");
        }

        ///<summary>
        ///Update cat sprite center position for hit-testing & proximity.
        ///Called by layout/MovementService each layout pass.
        ///</summary>
        public void UpdateCatPosition(double x, double y)
        {
            CatX = x;
            CatY = y;
        }

        //───────────────────── MouseMoved ─────────────────────

        private void OnMouseMoved(MouseMovedEvent e)
        {
            _cursorX = e.X;
            _cursorY = e.Y;
            _cursorVel = e.Velocity;

            double now = _time.GetElapsedSeconds();
            double dx = e.X - CatX;
            double dy = e.Y - CatY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            HandleNearCat(dist, now);
            HandleFastCursor();

            if (_dragging)
            {
                _lastMoveX = e.X;
                _lastMoveY = e.Y;
                _lastMoveTime = now;
            }
        }

        private void HandleNearCat(double dist, double now)
        {
            bool near = dist <= NearThreshold;

            if (near && !_nearCat)
            {
                _nearCat = true;
                _hoverStart = now;
                _pettingFired = false;
                PublishSafe(new CursorNearCatEvent(dist));
                Logger.Debug("Cursor near cat (dist {Dist:F0}).", dist);
            }
            else if (near && _nearCat)
            {
                //Continue hover; emit proximity update.
                PublishSafe(new CursorNearCatEvent(dist));

                if (!_pettingFired && (now - _hoverStart) >= PetHoverSeconds)
                {
                    _pettingFired = true;
                    PublishSafe(new CatPettedEvent());
                    Logger.Information("Cat petted (hover {Sec:F1}s) → hearts.", PetHoverSeconds);
                }
            }
            else if (!near && _nearCat)
            {
                _nearCat = false;
                _hoverStart = -1;
                _pettingFired = false;
            }
        }

        private void HandleFastCursor()
        {
            if (_cursorVel <= FastCursorVelocity) return;

            if (_random.NextDouble() < SurprisedChance)
            {
                TryInterrupt(FSMState.Surprised);
                Logger.Debug("Fast cursor {Vel:F0}px/s → Surprised.", _cursorVel);
            }
        }

        //───────────────────── MouseClicked ─────────────────────

        private void OnMouseClicked(MouseClickedEvent e)
        {
            double now = _time.GetElapsedSeconds();
            bool onSprite = HitTestSprite(e.X, e.Y);

            //Double-click detection (on sprite)
            if (onSprite && _lastClickTime >= 0 &&
                (now - _lastClickTime) <= DoubleClickSeconds)
            {
                _lastClickTime = -1;
                TryInterrupt(FSMState.Playful);
                PublishSafe(new CatClickedEvent(e.X, e.Y));
                Logger.Information("Double-click → Playful.");
                return;
            }

            if (onSprite)
            {
                _lastClickTime = now;
                PublishSafe(new CatClickedEvent(e.X, e.Y));

                //Meow + MeowLeft/Right based on cursor side relative cat.
                var meowState = e.X < CatX ? FSMState.MeowLeft : FSMState.MeowRight;
                TryInterrupt(meowState);
                Logger.Information("Cat clicked → {State}.", meowState);

                //Begin potential drag.
                if (!_dragging)
                {
                    _dragging = true;
                    _lastMoveX = e.X;
                    _lastMoveY = e.Y;
                    _lastMoveTime = now;
                    PublishSafe(new MouseDragStartEvent(e.X, e.Y));
                    TryInterrupt(FSMState.Drag);
                    Logger.Debug("Drag start ({X:F0},{Y:F0}).", e.X, e.Y);
                }
                return;
            }

            //Click off sprite while dragging → release.
            if (_dragging) EndDrag(e.X, e.Y, now);
        }

        ///<summary>
        ///Called on mouse button release (drag end). Public so input
        ///wiring (PreviewMouseUp) can signal release even off-sprite.
        ///</summary>
        public void OnMouseReleased(double x, double y)
        {
            if (_dragging) EndDrag(x, y, _time.GetElapsedSeconds());
        }

        private void EndDrag(double x, double y, double now)
        {
            _dragging = false;
            double dt = now - _lastMoveTime;
            double vx = x - _lastMoveX;
            double vy = y - _lastMoveY;
            double dist = Math.Sqrt(vx * vx + vy * vy);
            double vel = dt > 0 ? dist / dt : 0;

            PublishSafe(new MouseDragEndEvent(x, y, vel));
            Logger.Information("Drag end → Fall (vel {Vel:F0}px/s).", vel);

            //Release → Fall sequence.
            TryInterrupt(FSMState.Fall);
        }

        //───────────────────── Helpers ─────────────────────

        private bool HitTestSprite(double x, double y)
        {
            double dx = x - CatX;
            double dy = y - CatY;
            return Math.Sqrt(dx * dx + dy * dy) <= SpriteHitRadius;
        }

        private void TryInterrupt(FSMState state)
        {
            try
            {
                if (_fsm.CurrentState == state) return;
                _fsm.Interrupt(state);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "FSM interrupt → {State} failed.", state);
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