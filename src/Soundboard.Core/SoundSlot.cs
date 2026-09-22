namespace Soundboard.Core;

public sealed class SoundSlot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public HotkeyBinding? Hotkey { get; set; }
    public float Volume { get; set; } = 1f;
    public bool Enabled { get; set; } = true;

    /// <summary>When false (default), playback is always 1.0× — old configs unchanged.</summary>
    public bool EnableSpeedRamp { get; set; }

    /// <summary>Speed on the first play of a ramp cycle.</summary>
    public float BaseSpeed { get; set; } = SpeedRamp.DefaultBaseSpeed;

    /// <summary>
    /// Added to the current speed on each subsequent press. Negative values
    /// decelerate toward <see cref="SpeedRamp.MinSpeed"/>.
    /// </summary>
    public float SpeedStep { get; set; } = SpeedRamp.DefaultSpeedStep;

    /// <summary>Upper clamp for the ramp.</summary>
    public float MaxSpeed { get; set; } = SpeedRamp.DefaultMaxSpeed;

    /// <summary>
    /// When true, Stop / StopAll / ToggleStop / natural end reset the next press
    /// back to <see cref="BaseSpeed"/>. Restart (press again while playing) never resets.
    /// </summary>
    public bool ResetOnStop { get; set; } = true;

    public SoundSlot Clone() => new()
    {
        Id = Id,
        Name = Name,
        FilePath = FilePath,
        Hotkey = Hotkey?.Clone(),
        Volume = Volume,
        Enabled = Enabled,
        EnableSpeedRamp = EnableSpeedRamp,
        BaseSpeed = BaseSpeed,
        SpeedStep = SpeedStep,
        MaxSpeed = MaxSpeed,
        ResetOnStop = ResetOnStop
    };
}
