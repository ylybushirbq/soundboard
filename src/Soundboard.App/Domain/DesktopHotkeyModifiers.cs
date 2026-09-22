namespace Soundboard.Core;

/// <summary>
/// Win32 <c>RegisterHotKey</c> modifier bits. Always includes <see cref="ModNoRepeat"/>
/// (Windows 7+) so held keys do not auto-repeat as extra WM_HOTKEY messages.
/// </summary>
public static class DesktopHotkeyModifiers
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    public static uint Build(bool control, bool alt, bool shift, bool win)
    {
        var mods = ModNoRepeat;
        if (control) mods |= ModControl;
        if (alt) mods |= ModAlt;
        if (shift) mods |= ModShift;
        if (win) mods |= ModWin;
        return mods;
    }

    public static uint FromBinding(HotkeyBinding binding) =>
        Build(binding.Control, binding.Alt, binding.Shift, binding.Win);
}
