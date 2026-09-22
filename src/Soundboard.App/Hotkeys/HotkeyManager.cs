using System.Runtime.InteropServices;
using Soundboard.Core;

namespace Soundboard.Hotkeys;

internal readonly record struct HotkeyRegistration
{
    public int Id { get; init; }
    public string Owner { get; init; }
    public HotkeyBinding Binding { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Catalogues slot and global hotkeys, then applies one of two exclusive paths:
/// <list type="bullet">
/// <item><see cref="HotkeyMode.Game"/> — observe only (GetAsyncKeyState + Raw Input). No RegisterHotKey.</item>
/// <item><see cref="HotkeyMode.Desktop"/> — RegisterHotKey (MOD_NOREPEAT) for keyboard on a message-only HWND. Mouse side buttons stay Raw Input.</item>
/// </list>
/// Switching modes always unregisters the other path. Never uses WH_* low-level hooks.
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    public const int IdStopAll = 1;
    public const int IdMuteAll = 2;
    public const int IdToggleResetOnStop = 3;
    public const int IdResetSpeed = 4;
    public const int IdToggleSpeedDirection = 5;
    public const int IdSlotBase = 100;

    public const int VkXButton1 = 0x05;
    public const int VkXButton2 = 0x06;

    private readonly HotkeyMessageWindow _sink;
    private readonly Dictionary<int, string> _slotIds = [];
    private readonly List<int> _registered = [];
    private IntPtr _hotkeyHwnd;
    private bool _disposed;

    public HotkeyManager()
    {
        _sink = new HotkeyMessageWindow();
        _sink.HotkeyPressed += id => HotkeyPressed?.Invoke(id);
    }

    /// <summary>WM_HOTKEY from the message-only sink (desktop mode only).</summary>
    public event Action<int>? HotkeyPressed;

    public IReadOnlyList<HotkeyRegistration> LastResults { get; private set; } = [];

    public IntPtr MessageWindowHandle => _sink.Handle;

    public string? GetSlotId(int hotkeyId) =>
        _slotIds.TryGetValue(hotkeyId, out var id) ? id : null;

    /// <summary>
    /// Rebuilds the catalog. Always drops any previous RegisterHotKey ids first
    /// so game mode cannot double-fire with leftover OS registrations.
    /// </summary>
    public IReadOnlyList<HotkeyRegistration> ReplaceAll(AppConfig config)
    {
        UnregisterAll();
        _slotIds.Clear();
        var results = new List<HotkeyRegistration>();
        var desktop = config.HotkeyMode == HotkeyMode.Desktop;
        var hwnd = _sink.Handle;
        _hotkeyHwnd = hwnd;

        void Add(int id, HotkeyBinding? binding, string owner, string? slotId = null)
        {
            if (binding is null || binding.IsEmpty)
            {
                return;
            }

            if (binding.IsMouseButton)
            {
                if (slotId is not null)
                {
                    _slotIds[id] = slotId;
                }

                results.Add(new HotkeyRegistration
                {
                    Id = id,
                    Owner = owner,
                    Binding = binding,
                    Success = true
                });
                return;
            }

            if (!TryGetVirtualKey(binding.Key, out var vk))
            {
                results.Add(new HotkeyRegistration
                {
                    Id = id,
                    Owner = owner,
                    Binding = binding,
                    Success = false,
                    Error = $"无法识别按键「{binding.Key}」"
                });
                return;
            }

            if (!desktop)
            {
                if (slotId is not null)
                {
                    _slotIds[id] = slotId;
                }

                results.Add(new HotkeyRegistration
                {
                    Id = id,
                    Owner = owner,
                    Binding = binding,
                    Success = true
                });
                return;
            }

            if (hwnd == IntPtr.Zero)
            {
                results.Add(new HotkeyRegistration
                {
                    Id = id,
                    Owner = owner,
                    Binding = binding,
                    Success = false,
                    Error = RegisterHotKeyError.ToChinese(RegisterHotKeyError.NoWindow)
                });
                return;
            }

            var mods = DesktopHotkeyModifiers.FromBinding(binding);
            var ok = NativeMethods.RegisterHotKey(hwnd, id, mods, vk);
            if (ok)
            {
                _registered.Add(id);
                if (slotId is not null)
                {
                    _slotIds[id] = slotId;
                }
            }

            results.Add(new HotkeyRegistration
            {
                Id = id,
                Owner = owner,
                Binding = binding,
                Success = ok,
                Error = ok ? null : RegisterHotKeyError.ToChinese(Marshal.GetLastWin32Error())
            });
        }

        Add(IdStopAll, config.StopAllHotkey, "停止全部");
        Add(IdMuteAll, config.MuteAllHotkey, "全部静音");
        Add(IdToggleResetOnStop, config.ToggleResetOnStopHotkey, "切换停止后重置");
        Add(IdResetSpeed, config.ResetSpeedHotkey, "重置倍速");
        Add(IdToggleSpeedDirection, config.ToggleSpeedDirectionHotkey, "切换加速/减速");

        for (var i = 0; i < config.Slots.Count; i++)
        {
            var slot = config.Slots[i];
            if (!slot.Enabled)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(slot.Name) ? $"音效 {i + 1}" : slot.Name;
            Add(IdSlotBase + i, slot.Hotkey, name, slot.Id);
        }

        LastResults = results;
        return results;
    }

    /// <summary>
    /// Drops every RegisterHotKey id on the HWND that originally registered them.
    /// Safe to call in game mode (no-op if none).
    /// </summary>
    public void UnregisterAll()
    {
        var hwnd = _hotkeyHwnd != IntPtr.Zero ? _hotkeyHwnd : _sink.Handle;
        foreach (var id in _registered)
        {
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(hwnd, id);
            }
        }

        _registered.Clear();
    }

    public bool TryMatchMouse(bool xButton1, bool control, bool alt, bool shift, bool win, out int hotkeyId)
    {
        var key = xButton1 ? HotkeyBinding.KeyXButton1 : HotkeyBinding.KeyXButton2;
        return TryMatchBinding(key, control, alt, shift, win, mouse: true, out hotkeyId);
    }

    private bool TryMatchBinding(
        string key,
        bool control,
        bool alt,
        bool shift,
        bool win,
        bool mouse,
        out int hotkeyId)
    {
        hotkeyId = 0;
        var pressed = new HotkeyBinding
        {
            Control = control,
            Alt = alt,
            Shift = shift,
            Win = win,
            Key = key
        };

        foreach (var result in LastResults)
        {
            if (!result.Success || result.Binding.IsMouseButton != mouse)
            {
                continue;
            }

            if (result.Binding.Equals(pressed))
            {
                hotkeyId = result.Id;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetVirtualKey(string keyName, out uint vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(keyName) || HotkeyBinding.IsMouseKey(keyName))
        {
            return false;
        }

        if (!Enum.TryParse<Keys>(keyName.Trim(), ignoreCase: true, out var keys)
            || keys is Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu
            or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey
            or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin)
        {
            return false;
        }

        vk = (uint)(keys & Keys.KeyCode);
        return vk != 0;
    }

    public static int MouseVirtualKey(bool xButton1) =>
        xButton1 ? VkXButton1 : VkXButton2;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnregisterAll();
        LastResults = [];
        _slotIds.Clear();
        _sink.Dispose();
        _hotkeyHwnd = IntPtr.Zero;
        _disposed = true;
    }
}
