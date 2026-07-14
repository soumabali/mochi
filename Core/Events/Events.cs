using MochiV2.Core.Models;

namespace MochiV2.Core.Events
{
    // ───────────────────────── Input events ─────────────────────────

    /// <summary>Mouse cursor moved. Published by CursorPoller (~30 Hz).</summary>
    /// <param name="X">Screen X.</param>
    /// <param name="Y">Screen Y.</param>
    /// <param name="Velocity">Pixels/sec since last poll (0 on first).</param>
    public sealed record MouseMovedEvent(double X, double Y, double Velocity);

    /// <summary>Mouse button clicked (anywhere).</summary>
    /// <param name="X">Screen X.</param>
    /// <param name="Y">Screen Y.</param>
    public sealed record MouseClickedEvent(double X, double Y);

    /// <summary>User started dragging Mochi (button down on sprite).</summary>
    public sealed record MouseDragStartEvent(double X, double Y);

    /// <summary>User released Mochi (button up after drag).</summary>
    /// <param name="X">Release X.</param>
    /// <param name="Y">Release Y.</param>
    /// <param name="Velocity">Release velocity for fall arc.</param>
    public sealed record MouseDragEndEvent(double X, double Y, double Velocity);

    /// <summary>Cursor entered the near-cat zone (proximity threshold).</summary>
    public sealed record CursorNearCatEvent(double Distance);

    /// <summary>Click landed on the cat sprite (hit-test passed).</summary>
    public sealed record CatClickedEvent(double X, double Y);

    // ───────────────────────── Care events ─────────────────────────

    /// <summary>User fed Mochi (tray menu / double-click on food).</summary>
    /// <param name="Amount">Food points added.</param>
    public sealed record CatFedEvent(int Amount);

    /// <summary>User petted Mochi (hover/click).</summary>
    public sealed record CatPettedEvent();

    // ───────────────────────── State / mood events ─────────────────────────

    /// <summary>Mood resolved from needs changed (with 60 s hysteresis).</summary>
    /// <param name="OldMood">Previous mood name.</param>
    /// <param name="NewMood">New mood name.</param>
    public sealed record MoodChangedEvent(string OldMood, string NewMood);

    /// <summary>Periodic needs tick (food/energy/happiness decay).</summary>
    /// <param name="Food">0–100.</param>
    /// <param name="Energy">0–100.</param>
    /// <param name="Happiness">0–100.</param>
    public sealed record NeedsTickEvent(int Food, int Energy, int Happiness);

    /// <summary>Mochi started sleeping.</summary>
    public sealed record SleepStartedEvent();

    /// <summary>Mochi woke up.</summary>
    public sealed record SleepEndedEvent();

    /// <summary>
    /// FSM state changed. Published by <c>FSM</c> after a successful transition.
    /// </summary>
    /// <param name="OldState">Previous FSM state.</param>
    /// <param name="NewState">New FSM state.</param>
    public sealed record StateChangedEvent(FSMState OldState, FSMState NewState);

    // ───────────────────────── Asset events ─────────────────────────

    /// <summary>
    /// Manifest-referenced asset missing on disk. PRD §0 rule 5: fail loud,
    /// log via Serilog, fall back to Idle — never crash.
    /// </summary>
    /// <param name="StateName">FSM state name whose asset is missing.</param>
    /// <param name="MissingPath">Folder/file path that was absent.</param>
    public sealed record AssetMissingEvent(string StateName, string MissingPath);

    // ───────────────────────── Environment events ─────────────────────────

    /// <summary>A fullscreen app was detected (Mochi should hide).</summary>
    /// <param name="ProcessName">Foreground process name if known.</param>
    public sealed record FullscreenDetectedEvent(string? ProcessName);

    /// <summary>Fullscreen app exited (Mochi may reappear).</summary>
    public sealed record FullscreenExitedEvent();

    /// <summary>Active display monitor changed (Mochi may need repositioning).</summary>
    /// <param name="MonitorId">New monitor identifier.</param>
    public sealed record MonitorChangedEvent(string MonitorId);

    // ───────────────────────── Typing events ─────────────────────────

    /// <summary>User started a burst of typing (key-rate above threshold).</summary>
    public sealed record TypingBurstStartedEvent();

    /// <summary>User stopped typing (rate dropped below threshold for N seconds).</summary>
    public sealed record TypingBurstEndedEvent();

    // ───────────────────────── Progression events ─────────────────────────

    /// <summary>Mochi leveled up.</summary>
    /// <param name="NewLevel">New level reached.</param>
    /// <param name="Xp">Current total XP.</param>
    public sealed record LevelUpEvent(int NewLevel, int Xp);
}