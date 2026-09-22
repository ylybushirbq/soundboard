namespace Soundboard.Core;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public bool StartMinimized { get; set; } = true;
    public bool RunAtStartup { get; set; }
    public float GlobalVolume { get; set; } = 0.8f;
    public bool Muted { get; set; }
    public HotkeyMode HotkeyMode { get; set; } = HotkeyMode.Game;
    public HotkeyBehavior HotkeyBehavior { get; set; } = HotkeyBehavior.Restart;
    public HotkeyBinding? StopAllHotkey { get; set; }
    public HotkeyBinding? MuteAllHotkey { get; set; }
    public HotkeyBinding? ToggleResetOnStopHotkey { get; set; }
    public HotkeyBinding? ResetSpeedHotkey { get; set; }
    public HotkeyBinding? ToggleSpeedDirectionHotkey { get; set; }
    public List<SoundSlot> Slots { get; set; } = [];

    public static AppConfig CreateDefault() => new()
    {
        Version = 1,
        StartMinimized = true,
        RunAtStartup = false,
        GlobalVolume = 0.8f,
        Muted = false,
        HotkeyMode = HotkeyMode.Game,
        HotkeyBehavior = HotkeyBehavior.Restart,
        StopAllHotkey = new HotkeyBinding { Control = true, Shift = true, Key = "F8" },
        MuteAllHotkey = new HotkeyBinding { Control = true, Shift = true, Key = "F9" },
        ToggleResetOnStopHotkey = null,
        ResetSpeedHotkey = null,
        ToggleSpeedDirectionHotkey = null,
        Slots = []
    };

    public AppConfig Clone() => new()
    {
        Version = Version,
        StartMinimized = StartMinimized,
        RunAtStartup = RunAtStartup,
        GlobalVolume = GlobalVolume,
        Muted = Muted,
        HotkeyMode = HotkeyMode,
        HotkeyBehavior = HotkeyBehavior,
        StopAllHotkey = StopAllHotkey?.Clone(),
        MuteAllHotkey = MuteAllHotkey?.Clone(),
        ToggleResetOnStopHotkey = ToggleResetOnStopHotkey?.Clone(),
        ResetSpeedHotkey = ResetSpeedHotkey?.Clone(),
        ToggleSpeedDirectionHotkey = ToggleSpeedDirectionHotkey?.Clone(),
        Slots = Slots.Select(s => s.Clone()).ToList()
    };

    public void Normalize()
    {
        Version = Version <= 0 ? 1 : Version;
        GlobalVolume = Clamp01(GlobalVolume);
        HotkeyMode = HotkeyModeParser.Normalize(HotkeyMode);
        if (!Enum.IsDefined(HotkeyBehavior))
        {
            HotkeyBehavior = HotkeyBehavior.Restart;
        }

        foreach (var slot in Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
            {
                slot.Id = Guid.NewGuid().ToString("N");
            }

            slot.Volume = Clamp01(slot.Volume);
            slot.Name = slot.Name?.Trim() ?? "";
            slot.FilePath = slot.FilePath?.Trim() ?? "";
            SpeedRamp.Normalize(slot);
            if (slot.Hotkey is { IsEmpty: true })
            {
                slot.Hotkey = null;
            }
        }

        if (StopAllHotkey is { IsEmpty: true })
        {
            StopAllHotkey = null;
        }

        if (MuteAllHotkey is { IsEmpty: true })
        {
            MuteAllHotkey = null;
        }

        if (ToggleResetOnStopHotkey is { IsEmpty: true })
        {
            ToggleResetOnStopHotkey = null;
        }

        if (ResetSpeedHotkey is { IsEmpty: true })
        {
            ResetSpeedHotkey = null;
        }

        if (ToggleSpeedDirectionHotkey is { IsEmpty: true })
        {
            ToggleSpeedDirectionHotkey = null;
        }
    }

    public IReadOnlyList<string> FindHotkeyConflicts()
    {
        var used = new Dictionary<HotkeyBinding, string>();
        var conflicts = new List<string>();

        void Check(HotkeyBinding? binding, string owner)
        {
            if (binding is null || binding.IsEmpty)
            {
                return;
            }

            if (used.TryGetValue(binding, out var existing))
            {
                conflicts.Add($"{owner} 与 {existing} 使用了相同热键 {binding.ToDisplayString()}");
            }
            else
            {
                used[binding] = owner;
            }
        }

        Check(StopAllHotkey, "「停止全部」");
        Check(MuteAllHotkey, "「全部静音」");
        Check(ToggleResetOnStopHotkey, "「切换停止后重置」");
        Check(ResetSpeedHotkey, "「重置倍速」");
        Check(ToggleSpeedDirectionHotkey, "「切换加速/减速」");
        foreach (var slot in Slots)
        {
            var name = string.IsNullOrWhiteSpace(slot.Name) ? slot.Id : slot.Name;
            Check(slot.Hotkey, $"音效「{name}」");
        }

        return conflicts;
    }

    public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}
