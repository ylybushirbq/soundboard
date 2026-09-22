namespace Soundboard.Core;

/// <summary>
/// Flips the sign of <see cref="SoundSlot.SpeedStep"/> (accelerate ↔ decelerate).
/// Last-triggered slot wins; otherwise the selected row when settings are open;
/// otherwise every enabled slot that has speed ramp on.
/// </summary>
public static class SpeedDirectionToggle
{
    public readonly record struct Result(int Flipped, int SkippedZero, float? NewStep);

    public static IReadOnlyList<SoundSlot> ResolveTargets(
        AppConfig config,
        string? lastTriggeredSlotId,
        string? selectedSlotId,
        bool settingsWindowOpen)
    {
        ArgumentNullException.ThrowIfNull(config);

        var last = FindEnabled(config, lastTriggeredSlotId);
        if (last is not null)
        {
            return [last];
        }

        if (settingsWindowOpen)
        {
            var selected = FindEnabled(config, selectedSlotId);
            if (selected is not null)
            {
                return [selected];
            }
        }

        return config.Slots.Where(s => s.Enabled && s.EnableSpeedRamp).ToList();
    }

    public static Result Apply(IReadOnlyList<SoundSlot> targets)
    {
        var flipped = 0;
        var skipped = 0;
        float? newStep = null;

        foreach (var slot in targets)
        {
            if (!float.IsFinite(slot.SpeedStep) || slot.SpeedStep == 0f)
            {
                skipped++;
                continue;
            }

            slot.SpeedStep = -slot.SpeedStep;
            SpeedRamp.Normalize(slot);
            flipped++;
            newStep ??= slot.SpeedStep;
        }

        return new Result(flipped, skipped, newStep);
    }

    /// <summary>「已切换为加速 +0.2」 / 「已切换为减速 -0.2」 / no-op tips.</summary>
    public static string StatusMessage(Result result, int targetCount)
    {
        if (targetCount == 0)
        {
            return "没有可切换加速/减速的槽位。";
        }

        if (result.Flipped == 0)
        {
            return "步进为 0，未切换方向。";
        }

        var direction = FormatDirection(result.NewStep ?? 0f);
        return result.Flipped == 1
            ? $"已切换为{direction}"
            : $"已切换 {result.Flipped} 个槽位为{direction}";
    }

    public static string FormatDirection(float step) =>
        step > 0f
            ? $"加速 {SpeedRamp.FormatStep(step)}"
            : $"减速 {SpeedRamp.FormatStep(step)}";

    private static SoundSlot? FindEnabled(AppConfig config, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return config.Slots.FirstOrDefault(s => s.Enabled && s.Id == id);
    }
}
