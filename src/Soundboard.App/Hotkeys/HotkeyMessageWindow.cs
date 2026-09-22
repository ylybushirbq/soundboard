namespace Soundboard.Hotkeys;

/// <summary>
/// Message-only HWND (<c>HWND_MESSAGE</c>) that keeps a UI-thread message pump
/// for <c>WM_HOTKEY</c> while the settings form is hidden in the tray.
/// Not a WH_* hook.
/// </summary>
internal sealed class HotkeyMessageWindow : NativeWindow, IDisposable
{
    public event Action<int>? HotkeyPressed;

    public HotkeyMessageWindow()
    {
        try
        {
            CreateHandle(new CreateParams
            {
                Caption = "Soundboard.HotkeySink",
                Parent = NativeMethods.HwndMessage
            });
        }
        catch
        {
            // Fallback: still a hidden NativeWindow on this thread (not shown in the taskbar).
        }

        if (Handle == IntPtr.Zero)
        {
            try
            {
                CreateHandle(new CreateParams { Caption = "Soundboard.HotkeySink" });
            }
            catch
            {
                // RegisterHotKey will report 没有可用于注册的窗口.
            }
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotkey)
        {
            HotkeyPressed?.Invoke(unchecked((int)(m.WParam.ToInt64() & 0xFFFFFFFF)));
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }
}
