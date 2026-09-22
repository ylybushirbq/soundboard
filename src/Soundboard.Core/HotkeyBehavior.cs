namespace Soundboard.Core;

/// <summary>
/// What happens when a slot's hotkey is pressed while that slot is already playing.
/// </summary>
public enum HotkeyBehavior
{
    /// <summary>Playing → stop; stopped → play from the start.</summary>
    ToggleStop = 0,

    /// <summary>Always restart the sound from the beginning (default). Speed ramp steps up on each press.</summary>
    Restart = 1
}
