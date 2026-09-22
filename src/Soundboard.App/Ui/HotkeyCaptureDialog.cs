using Soundboard.Core;
using Soundboard.Hotkeys;

namespace Soundboard.Ui;

internal sealed class HotkeyCaptureDialog : Form, IMessageFilter
{
    internal static HotkeyCaptureDialog? ActiveInstance { get; private set; }

    /// <summary>
    /// Raised when a capture dialog is shown so the host can drop RegisterHotKey
    /// / pause polling, otherwise desktop mode would steal the combo from this window.
    /// </summary>
    internal static event Action? CaptureBegan;

    internal static event Action? CaptureEnded;

    private readonly Label _preview;
    private HotkeyBinding? _value;

    public HotkeyBinding? Value => _value?.Clone();

    public HotkeyCaptureDialog(HotkeyBinding? current, HotkeyMode mode = HotkeyMode.Game)
    {
        _value = current?.Clone();
        Text = "设置热键";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 250);
        KeyPreview = true;
        Font = UiTheme.Font;

        var hint = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 96,
            Padding = new Padding(16, 12, 16, 0),
            Text = HintText(mode)
        };

        _preview = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 56,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(UiTheme.Font.FontFamily, 14f, FontStyle.Bold),
            Text = _value?.ToDisplayString() ?? "（等待按键）"
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 8, 8, 8)
        };

        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 88, Height = 28 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 88, Height = 28 };
        var clear = new Button { Text = "清除", Width = 88, Height = 28 };
        clear.Click += (_, _) =>
        {
            _value = null;
            _preview.Text = "（未设置）";
        };

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(clear);

        Controls.Add(buttons);
        Controls.Add(_preview);
        Controls.Add(hint);

        AcceptButton = ok;
        CancelButton = cancel;

        KeyDown += OnCaptureKeyDown;
        MouseDown += OnCaptureMouseDown;
    }

    public void AcceptMouseSideButton(bool xButton1)
    {
        NativeMethods.ReadModifiers(out var control, out var alt, out var shift, out var win);
        Bind(new HotkeyBinding
        {
            Control = control,
            Alt = alt,
            Shift = shift,
            Win = win,
            Key = xButton1 ? HotkeyBinding.KeyXButton1 : HotkeyBinding.KeyXButton2
        });
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (RawInputMouse.TryReadXButtonDownFromClientMessage(m, out var xButton1))
        {
            AcceptMouseSideButton(xButton1);
            return true;
        }

        return false;
    }

    protected override void OnShown(EventArgs e)
    {
        ActiveInstance = this;
        Application.AddMessageFilter(this);
        CaptureBegan?.Invoke();
        base.OnShown(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Application.RemoveMessageFilter(this);
        if (ReferenceEquals(ActiveInstance, this))
        {
            ActiveInstance = null;
        }

        CaptureEnded?.Invoke();
        base.OnFormClosed(e);
    }

    private static string HintText(HotkeyMode mode) =>
        mode == HotkeyMode.Desktop
            ? "请按下组合键，或按下鼠标侧键（可同时按 Ctrl / Alt / Shift / Win）。\n桌面模式保存后会 RegisterHotKey，该组合键会被本程序抢走，其他软件收不到。捕获时会暂时放开注册。鼠标侧键始终用 Raw Input，无法 RegisterHotKey。"
            : "请按下组合键，或按下鼠标侧键（可同时按 Ctrl / Alt / Shift / Win）。\n游戏模式只观察、不拦截：前台程序仍会收到同一按键。推荐鼠标侧键，避免 WASD 等常用键。";

    private void OnCaptureMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.XButton1)
        {
            AcceptMouseSideButton(xButton1: true);
        }
        else if (e.Button == MouseButtons.XButton2)
        {
            AcceptMouseSideButton(xButton1: false);
        }
    }

    private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu
            or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey
            or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin or Keys.None
            or Keys.XButton1 or Keys.XButton2)
        {
            return;
        }

        if (e.KeyCode is Keys.Escape)
        {
            return;
        }

        Bind(new HotkeyBinding
        {
            Control = e.Control,
            Alt = e.Alt,
            Shift = e.Shift,
            Win = NativeMethods.IsWinDown(),
            Key = (e.KeyCode & Keys.KeyCode).ToString()
        });
    }

    private void Bind(HotkeyBinding value)
    {
        _value = value;
        _preview.Text = _value.ToDisplayString();
    }
}
