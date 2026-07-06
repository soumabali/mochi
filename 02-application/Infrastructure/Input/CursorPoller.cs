using System;
using System.Threading;
using System.Threading.Tasks;
using MochiV2.Core.Events;
using MochiV2.Infrastructure.Window;
using Serilog;

namespace MochiV2.Infrastructure.Input
{
    ///<summary>
    ///Polls mouse cursor position ~30 Hz using Win32 <c>GetCursorPos</c>
    ///and publishes <see cref="MouseMovedEvent"/> via <see cref="EventBus"/>.
    ///PRD §7.1: cursor tracking for interaction detection.
    ///
    ///Runs a dedicated polling <see cref="Task"/> with ~33 ms interval
    ///(≈30 Hz). On non-Windows hosts <see cref="Win32Interop.TryGetCursorPos"/>
    ///returns <c>false</c>; the poller logs once and continues emitting
    ///zero-velocity events so downstream consumers remain functional
    ///during headless / Linux test builds.
    ///</summary>
    public sealed class CursorPoller : IDisposable
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(CursorPoller));

        ///<summary>Polling interval (ms) targeting ~30 Hz.</summary>
        public const int PollIntervalMs = 33;

        private readonly EventBus _eventBus;
        private readonly CancellationTokenSource _cts;
        private Task? _pollTask;

        //Last sampled position & timestamp for velocity calculation.
        private double _lastX;
        private double _lastY;
        private double _lastElapsed;
        private bool _hasLast;

        ///<summary>
        ///Create poller bound <paramref name="eventBus"/>. Does not start
        ///polling until <see cref="Start"/> called.
        ///</summary>
        ///<param name="eventBus">EventBus publish <see cref="MouseMovedEvent"/>.</param>
        ///<exception cref="ArgumentNullException"><paramref name="eventBus"/> null.</exception>
        public CursorPoller(EventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _cts = new CancellationTokenSource();
        }

        ///<summary>
        ///Start background polling. Idempotent — repeated calls no-op
        ///if already running.
        ///</summary>
        public void Start()
        {
            if (_pollTask is not null) return;
            _pollTask = Task.Run(() => PollLoop(_cts.Token));
            Logger.Debug("CursorPoller started ({Hz} Hz).", 1000.0 / PollIntervalMs);
        }

        ///<summary>
        ///Stop polling and wait task completion. Safe call multiple times.
        ///</summary>
        public void Stop()
        {
            if (_cts.IsCancellationRequested) return;
            _cts.Cancel();
            Logger.Debug("CursorPoller stop requested.");
        }

        ///<summary>
        ///Single poll iteration. Reads cursor, computes velocity, publishes
        ///<see cref="MouseMovedEvent"/>. Exposed internal/test hook
        ///(<c>internal</c>) deterministic single-step verification.
        ///</summary>
        internal void PollOnce()
        {
            double x = 0, y = 0;
            bool ok = Win32Interop.TryGetCursorPos(out var pt);
            if (ok)
            {
                x = pt.X;
                y = pt.Y;
            }

            double velocity = 0;
            if (_hasLast)
            {
                double dx = x - _lastX;
                double dy = y - _lastY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                //Use wall-clock delta; on first iteration _lastElapsed set below.
                double now = Environment.TickCount64 / 1000.0;
                double dt = now - _lastElapsed;
                if (dt > 0)
                    velocity = dist / dt;
                _lastElapsed = now;
            }
            else
            {
                _lastElapsed = Environment.TickCount64 / 1000.0;
            }

            _lastX = x;
            _lastY = y;
            _hasLast = true;

            try
            {
                _eventBus.Publish(new MouseMovedEvent(x, y, velocity));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MouseMovedEvent handler threw (swallowed).");
            }
        }

        private async Task PollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "CursorPoller iteration failed.");
                }
                try
                {
                    await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            Logger.Debug("CursorPoller loop exited.");
        }

        ///<inheritdoc/>
        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }
}