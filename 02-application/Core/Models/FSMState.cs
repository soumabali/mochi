namespace MochiV2.Core.Models
{
    /// <summary>
    /// All FSM states from PRD §10. Maps 1:1 to AssetManifest sprite entries
    /// (manifest.json keys). Manifest aliases some states (e.g. Idle↔IdleLeft/
    /// IdleRight, Blink↔BlinkLeft/BlinkRight) — the manifest is the only mapping
    /// layer between this enum and on-disk folder names (per PRD §0 rule 3).
    /// </summary>
    public enum FSMState
    {
        /// <summary>Default resting state (manifest IdleLeft/IdleRight alias).</summary>
        Idle,

        /// <summary>Walk left (loop).</summary>
        WalkLeft,

        /// <summary>Walk right (loop).</summary>
        WalkRight,

        /// <summary>Walk toward viewer (loop).</summary>
        WalkForward,

        /// <summary>Run variant 1 (loop).</summary>
        RunVar1,

        /// <summary>Run variant 2 (loop).</summary>
        RunVar2,

        /// <summary>Jump variant 1 (playOnce).</summary>
        JumpVar1,

        /// <summary>Jump variant 2 (playOnce).</summary>
        JumpVar2,

        /// <summary>Sleeping (yawn→hold).</summary>
        Sleeping,

        /// <summary>Playful — chasing wand (loop).</summary>
        Playful,

        /// <summary>Hungry standard begging (loop).</summary>
        HungryStandard,

        /// <summary>Hungry critical begging (loop).</summary>
        HungryCritical,

        /// <summary>Scratch left (playOnce).</summary>
        ScratchLeft,

        /// <summary>Scratch right (playOnce).</summary>
        ScratchRight,

        /// <summary>Meow left (playOnce).</summary>
        MeowLeft,

        /// <summary>Meow right (playOnce).</summary>
        MeowRight,

        /// <summary>Blink fidget (playOnce).</summary>
        Blink,

        /// <summary>Angry — drag reaction (loop).</summary>
        Angry,

        /// <summary>Surprised — fast cursor "!" (playOnce).</summary>
        Surprised,

        /// <summary>Drag — follows cursor with elastic lag (loop/interrupt).</summary>
        Drag,

        /// <summary>Fall — reversed jump on drag release (playOnceReversed).</summary>
        Fall,

        /// <summary>Wake up — reversed yawn (playOnceReversed).</summary>
        WakeUp,

        /// <summary>Eating after being fed (loop, speedMultiplier 1.3).</summary>
        Eating
    }
}