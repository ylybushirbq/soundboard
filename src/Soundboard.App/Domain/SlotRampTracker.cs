namespace Soundboard.Core;

/// <summary>
/// Remembers the next playback speed per slot. Restarting a sound must
/// <see cref="Consume"/> the next step without calling <see cref="Reset"/>.
/// </summary>
public sealed class SlotRampTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, float> _next = new(StringComparer.Ordinal);

    public float Consume(SoundSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        lock (_gate)
        {
            if (!slot.EnableSpeedRamp)
            {
                return 1f;
            }

            if (!_next.TryGetValue(slot.Id, out var next))
            {
                next = SpeedRamp.InitialSpeed(slot);
            }

            var used = Math.Clamp(next, SpeedRamp.MinSpeed, slot.MaxSpeed);
            _next[slot.Id] = SpeedRamp.NextSpeed(slot, used);
            return used;
        }
    }

    public float? PeekNext(string slotId)
    {
        lock (_gate)
        {
            return _next.TryGetValue(slotId, out var next) ? next : null;
        }
    }

    public void Reset(string slotId)
    {
        lock (_gate)
        {
            _next.Remove(slotId);
        }
    }

    public void ResetAll()
    {
        lock (_gate)
        {
            _next.Clear();
        }
    }

    /// <summary>
    /// Restart (hotkey pressed again while playing) never resets the ramp.
    /// Natural end, StopAll, ToggleStop, and explicit Stop reset only when
    /// <paramref name="resetOnStop"/> is true.
    /// </summary>
    public static bool ShouldReset(PlaybackStopKind kind, bool resetOnStop) =>
        kind != PlaybackStopKind.Restart && resetOnStop;

    /// <summary>
    /// Applies the same stop/restart policy <c>AudioEngine.ToggleOrRestart</c> uses,
    /// so tests can cover ramp-across-restart without playing audio.
    /// Returns the speed that would be used, or <c>null</c> when the press stops playback.
    /// </summary>
    public float? ApplyHotkeyPress(SoundSlot slot, bool isPlaying, HotkeyBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (isPlaying)
        {
            if (behavior == HotkeyBehavior.ToggleStop)
            {
                if (ShouldReset(PlaybackStopKind.ExplicitStop, slot.ResetOnStop))
                {
                    Reset(slot.Id);
                }

                return null;
            }

            if (ShouldReset(PlaybackStopKind.Restart, slot.ResetOnStop))
            {
                Reset(slot.Id);
            }
        }

        return Consume(slot);
    }
}
