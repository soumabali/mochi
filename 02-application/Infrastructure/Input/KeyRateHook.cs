using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using MochiV2.Core.Events;
using Serilog;

namespace MochiV2.Infrastructure.Input
{
    /// <summary>
    /// Win32 low-level keyboard hook (WH_KEYBOARD_LL) that counts key
    /// events per minute for typing-rate awareness. PRD §6.6.
    ///
    /// <b>Privacy:</b> ONLY counts the rate of key events — never captures,
    /// stores, or inspects actual key codes, characters, or target windows.
    /// The hook callback increments a counter and returns immediately; no
    ///vk code or scan data is retained.
    ///
    /// Publishes <see cref="TypingBurstStartedEvent"/> when sustained rate
    /// exceeds 120 keys/min for 2 minutes, and <see cref="TypingBurstEndedEvent"/>
    /// when typing stops for 5 minutes. On burst end: downstream subscribers
    /// wake Mochi + meow.
    ///
    /// Guarded for non-Windows: on Linux/headless builds the hook is a no-op
    /// so unit tests and CI builds succeed without native calls.
    /// </summary>
    public sealed class KeyRateHook : IDisposable
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext(typeof(KeyRateHook));

        //──────── Configuration (PRD §6.6) ────────

        /// <summary>Keys/min threshold for sustained typing burst.</summary>
        public const int BurstThresholdKeysPerMin = 120;

        /// <summary>Seconds of sustained above-threshold rate before burst start.</summary>
        public const double BurstStartSeconds = 120.0; // 2 min

        /// <summary>Seconds of zero typing before burst end.</summary>
        public const double BurstEndSeconds = 300.0; // 5 min

        //──────── Win32 constants ────────

        private const int WH_KEYBOARD_LL = 13;
        private const int HC_ACTION = 0;

        //──────── State ────────

        private readonly EventBus _eventBus;
        private IntPtr _hookHandle = IntPtr.Zero;
        private HookProc? _hookProc;
        private bool _disposed;

        // Key-count ring buffer: count per 1-second bucket, 60 buckets = 1 min window.
        private readonly int[] _secondBuckets = new int[60];
        private int _currentBucket;
        private double _lastBucketTime;
        private double _burstStartTime;
        private bool _burstActive;
        private double _lastKeyTime;

        // Elapsed seconds source (Environment.TickCount64 for monotonic clock).
        private static double NowSeconds => Environment.TickCount64 / 1000.0;

        /// <summary>
        /// Current rolling key rate (keys/min) over the last 60-second window.
        /// </summary>
        public int CurrentKeysPerMin
        {
            get
            {
                lock (_secondBuckets)
                {
                    int sum = 0;
                    for (int i = 0; i < 60; i++)
                        sum += _secondBuckets[i];
                    return sum;
                }
            }
        }

        /// <summary>True when a typing burst is currently active.</summary>
        public bool IsBurstActive => _burstActive;

        /// <summary>
        /// Create the hook bound to <paramref name="eventBus"/> for publishing
        /// typing burst events. Does not start hooking until <see cref="Start"/>
        /// is called.
        /// </summary>
        /// <param name="eventBus">EventBus for publishing typing events.</param>
        /// <exception cref="ArgumentNullException">eventBus is null.</exception>
        public KeyRateHook(EventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _lastBucketTime = NowSeconds;
            _lastKeyTime = -1;
        }

        /// <summary>
        /// Install the low-level keyboard hook. Safe to call once; subsequent
        /// calls are no-ops. On non-Windows hosts logs and returns.
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KeyRateHook));
            if (_hookHandle != IntPtr.Zero)
                return;

            if (!OperatingSystem.IsWindows())
            {
                Logger.Debug("KeyRateHook.Start skipped (non-Windows).");
                return;
            }

            StartCore();
        }

        /// <summary>
        /// Remove the hook if installed. Safe to call multiple times.
        /// </summary>
        public void Stop()
        {
            if (_hookHandle == IntPtr.Zero)
                return;

            StopCore();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Stop();
            GC.SuppressFinalize(this);
        }

        /// <summary>Destructor ensures hook is uninstalled if Dispose not called.</summary>
        ~KeyRateHook()
        {
            Stop();
        }

        //───────────── Rate evaluation (called from hook or test) ─────────────

        /// <summary>
        /// Record one key event and evaluate burst state transitions.
        /// Internal so tests can simulate key presses without a real hook.
        /// </summary>
        internal void RecordKey()
        {
            double now = NowSeconds;
            _lastKeyTime = now;

            // Advance bucket index, clearing elapsed seconds.
            lock (_secondBuckets)
            {
                double elapsed = now - _lastBucketTime;
                while (elapsed >= 1.0)
                {
                    _lastBucketTime += 1.0;
                    _currentBucket = (_currentBucket + 1) % 60;
                    _secondBuckets[_currentBucket] = 0;
                    elapsed = now - _lastBucketTime;
                }
                _secondBuckets[_currentBucket]++;
            }

            EvaluateBurstState(now);
        }

        /// <summary>
        /// Check burst start/end conditions. Call periodically (e.g. from a
        /// timer or after each key) to detect burst-end even when no keys arrive.
        /// </summary>
        public void Evaluate()
        {
            EvaluateBurstState(NowSeconds);
        }

        private void EvaluateBurstState(double now)
        {
            int rate = CurrentKeysPerMin;
            bool aboveThreshold = rate >= BurstThresholdKeysPerMin;

            if (aboveThreshold && !_burstActive)
            {
                if (_burstStartTime <= 0)
                    _burstStartTime = now;

                if (now - _burstStartTime >= BurstStartSeconds)
                {
                    _burstActive = true;
                    Logger.Information(
                        "Typing burst started (rate={Rate} keys/min sustained {Seconds:F0}s).",
                        rate, BurstStartSeconds);
                    PublishSafe(new TypingBurstStartedEvent());
                }
            }
            else if (aboveThreshold && _burstActive)
            {
                // Still in burst; reset the idle timer.
                _lastKeyTime = now;
            }
            else if (!aboveThreshold)
            {
                _burstStartTime = 0;

                if (_burstActive)
                {
                    // Burst ends after BurstEndSeconds of no typing (rate dropped).
                    if (_lastKeyTime > 0 && (now - _lastKeyTime) >= BurstEndSeconds)
                    {
                        _burstActive = false;
                        _lastKeyTime = -1;
                        Logger.Information(
                            "Typing burst ended (no typing for {Seconds:F0}s). Wake + meow.",
                            BurstEndSeconds);
                        PublishSafe(new TypingBurstEndedEvent());
                    }
                }
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

        //───────────── Win32 P/Invoke (Windows only) ─────────────

#if WINDOWS
        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
#endif

        private void StartCore()
        {
#if WINDOWS
            _hookProc = HookCallback;
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            using var module = process.MainModule;
            IntPtr hMod = module is not null
                ? GetModuleHandle(module.ModuleName ?? string.Empty)
                : IntPtr.Zero;
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, hMod, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Logger.Error("SetWindowsHookEx failed (Win32 error {Error}).", err);
                throw new InvalidOperationException(
                    $"SetWindowsHookEx failed with Win32 error {err}.");
            }
            Logger.Debug("KeyRateHook installed WH_KEYBOARD_LL handle={Handle}.", _hookHandle);
#else
            Logger.Debug("KeyRateHook.StartCore no-op (WINDOWS not defined).");
#endif
        }

        private void StopCore()
        {
#if WINDOWS
            if (_hookHandle != IntPtr.Zero)
            {
                if (UnhookWindowsHookEx(_hookHandle))
                    Logger.Debug("KeyRateHook removed handle={Handle}.", _hookHandle);
                else
                    Logger.Warning("UnhookWindowsHookEx failed (Win32 error {Error}).",
                        Marshal.GetLastWin32Error());
                _hookHandle = IntPtr.Zero;
                _hookProc = null;
            }
#else
            _hookHandle = IntPtr.Zero;
#endif
        }

#if WINDOWS
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION)
            {
                // wParam is WM_KEYDOWN (0x0100) or WM_SYSKEYDOWN (0x0104).
                // We ONLY count — never inspect lParam (vk/scan) for privacy.
                int msg = wParam.ToInt32();
                if (msg == 0x0100 || msg == 0x0104)
                {
                    try
                    {
                        RecordKey();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "KeyRateHook RecordKey threw.");
                    }
                }
            }
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
#endif
    }
}