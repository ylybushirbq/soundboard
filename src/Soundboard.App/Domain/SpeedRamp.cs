namespace Soundboard.Core;

/// <summary>
/// Per-slot playback-rate ramp. Disabled slots always play at 1.0× so old
/// configs keep their previous behaviour.
/// </summary>
public static class SpeedRamp
{
    public const float MinSpeed = 0.25f;
    public const float AbsoluteMax = 8f;
    public const float DefaultBaseSpeed = 1f;
    public const float DefaultSpeedStep = 0.2f;
    public const float DefaultMaxSpeed = 3f;

    public static void Normalize(SoundSlot slot)
    {
        if (!float.IsFinite(slot.BaseSpeed) || slot.BaseSpeed <= 0f)
        {
            slot.BaseSpeed = DefaultBaseSpeed;
        }

        slot.BaseSpeed = Math.Clamp(slot.BaseSpeed, MinSpeed, AbsoluteMax);

        if (!float.IsFinite(slot.SpeedStep))
        {
            slot.SpeedStep = DefaultSpeedStep;
        }
        else
        {
            slot.SpeedStep = Math.Clamp(slot.SpeedStep, -AbsoluteMax, AbsoluteMax);
        }

        if (!float.IsFinite(slot.MaxSpeed) || slot.MaxSpeed <= 0f)
        {
            slot.MaxSpeed = DefaultMaxSpeed;
        }

        slot.MaxSpeed = Math.Clamp(slot.MaxSpeed, slot.BaseSpeed, AbsoluteMax);
    }

    /// <summary>
    /// Speed used on the first press of a ramp cycle (or after a reset).
    /// </summary>
    public static float InitialSpeed(SoundSlot slot)
    {
        if (!slot.EnableSpeedRamp)
        {
            return 1f;
        }

        return Math.Clamp(slot.BaseSpeed, MinSpeed, slot.MaxSpeed);
    }

    /// <summary>
    /// Speed to use on the next press after <paramref name="usedSpeed"/> was just played.
    /// </summary>
    public static float NextSpeed(SoundSlot slot, float usedSpeed)
    {
        if (!slot.EnableSpeedRamp)
        {
            return 1f;
        }

        var ceiling = Math.Clamp(slot.MaxSpeed, MinSpeed, AbsoluteMax);
        return Math.Clamp(usedSpeed + slot.SpeedStep, MinSpeed, ceiling);
    }

    public static string Format(float speed) => $"{speed:0.##}×";

    /// <summary>「+0.2」 / 「-0.2」 for status text.</summary>
    public static string FormatStep(float step)
    {
        if (!float.IsFinite(step) || step == 0f)
        {
            return "0";
        }

        var abs = Math.Abs(step).ToString("0.##");
        return step > 0f ? $"+{abs}" : $"-{abs}";
    }
}
