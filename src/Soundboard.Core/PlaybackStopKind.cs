namespace Soundboard.Core;

/// <summary>
/// Why a slot's playback ended. Restart must not reset the speed ramp.
/// </summary>
public enum PlaybackStopKind
{
    /// <summary>The file played through to the end.</summary>
    NaturalEnd = 0,

    /// <summary>Stop, StopAll, or ToggleStop while playing.</summary>
    ExplicitStop = 1,

    /// <summary>User pressed the slot hotkey again to play from the start.</summary>
    Restart = 2
}
