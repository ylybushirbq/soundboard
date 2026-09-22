using Soundboard.Core;
using Soundboard.Ui;

namespace Soundboard.Hotkeys;

/// <summary>
/// Game-mode observer: <c>GetAsyncKeyState</c> on a short UI timer.
/// Does not register or swallow keys — the foreground game still receives them.
/// Disabled entirely in desktop (RegisterHotKey) mode to avoid double-fire.
/// Edge-triggered: fires once per press, ignores key-repeat while held.
/// </summary>
internal sealed class HotkeyPoller : IDisposable
{
    public const int IntervalMs = 12;

    private readonly HotkeyManager _hotkeys;
    private readonly Action<int> _onHotkey;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Dictionary<int, bool> _wasDown = [];

    public HotkeyPoller(HotkeyManager hotkeys, Action<int> onHotkey)
    {
        _hotkeys = hotkeys;
        _onHotkey = onHotkey;
        _timer = new System.Windows.Forms.Timer { Interval = IntervalMs };
        _timer.Tick += (_, _) => Poll();
    }

    public bool Enabled
    {
        get => _timer.Enabled;
        set => _timer.Enabled = value;
    }

    public void ResetHeldState() => _wasDown.Clear();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _wasDown.Clear();
    }

    private void Poll()
    {
        if (HotkeyCaptureDialog.ActiveInstance is not null)
        {
            SnapshotHeldWithoutFiring();
            return;
        }

        NativeMethods.ReadAsyncModifiers(out var control, out var alt, out var shift, out var win);

        foreach (var result in _hotkeys.LastResults)
        {
            if (!result.Success)
            {
                continue;
            }

            var down = IsBindingDown(result.Binding, control, alt, shift, win);
            var was = _wasDown.GetValueOrDefault(result.Id);
            if (down && !was)
            {
                _onHotkey(result.Id);
            }

            _wasDown[result.Id] = down;
        }
    }

    private void SnapshotHeldWithoutFiring()
    {
        NativeMethods.ReadAsyncModifiers(out var control, out var alt, out var shift, out var win);
        foreach (var result in _hotkeys.LastResults)
        {
            if (!result.Success)
            {
                continue;
            }

            _wasDown[result.Id] = IsBindingDown(result.Binding, control, alt, shift, win);
        }
    }

    private static bool IsBindingDown(
        HotkeyBinding binding,
        bool control,
        bool alt,
        bool shift,
        bool win)
    {
        if (binding.Control != control
            || binding.Alt != alt
            || binding.Shift != shift
            || binding.Win != win)
        {
            return false;
        }

        int vk;
        if (binding.IsXButton1)
        {
            vk = HotkeyManager.VkXButton1;
        }
        else if (binding.IsXButton2)
        {
            vk = HotkeyManager.VkXButton2;
        }
        else if (!HotkeyManager.TryGetVirtualKey(binding.Key, out var parsed))
        {
            return false;
        }
        else
        {
            vk = (int)parsed;
        }

        return NativeMethods.IsAsyncKeyDown(vk);
    }
}
