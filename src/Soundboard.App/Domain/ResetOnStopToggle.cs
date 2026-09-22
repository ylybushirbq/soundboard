namespace Soundboard.Core;

/// <summary>
/// Chooses which slots a global 「停止后重置」toggle should affect.
/// Last-triggered slot wins; otherwise the selected row when settings are open;
/// otherwise every enabled slot.
/// </summary>
public static class ResetOnStopToggle
{
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

        return config.Slots.Where(s => s.Enabled).ToList();
    }

    /// <summary>
    /// Applies the toggle. One target: flip it. Several: if all were on, turn
    /// them off; otherwise turn them all on. Returns the new value, or null if
    /// nothing was targeted.
    /// </summary>
    public static bool? Apply(IReadOnlyList<SoundSlot> targets)
    {
        if (targets.Count == 0)
        {
            return null;
        }

        var newValue = !targets.All(s => s.ResetOnStop);
        foreach (var slot in targets)
        {
            slot.ResetOnStop = newValue;
        }

        return newValue;
    }

    private static SoundSlot? FindEnabled(AppConfig config, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return config.Slots.FirstOrDefault(s => s.Enabled && s.Id == id);
    }
}
