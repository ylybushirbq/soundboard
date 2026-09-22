using Soundboard.Core;

namespace Soundboard.Ui;

internal sealed class SlotEditDialog : Form
{
    private readonly string _configDirectory;
    private readonly string _exeDirectory;
    private readonly HotkeyMode _hotkeyMode;
    private readonly TextBox _name;
    private readonly TextBox _path;
    private readonly Label _hotkeyLabel;
    private readonly TrackBar _volume;
    private readonly Label _volumeLabel;
    private readonly CheckBox _enabled;
    private readonly CheckBox _enableRamp;
    private readonly NumericUpDown _baseSpeed;
    private readonly NumericUpDown _speedStep;
    private readonly NumericUpDown _maxSpeed;
    private readonly CheckBox _resetOnStop;
    private HotkeyBinding? _hotkey;

    public SoundSlot Slot { get; }

    public SlotEditDialog(SoundSlot slot, string configDirectory, string exeDirectory, HotkeyMode hotkeyMode = HotkeyMode.Game)
    {
        Slot = slot.Clone();
        _configDirectory = configDirectory;
        _exeDirectory = exeDirectory;
        _hotkeyMode = hotkeyMode;
        _hotkey = Slot.Hotkey?.Clone();
        SpeedRamp.Normalize(Slot);

        Text = string.IsNullOrWhiteSpace(slot.Name) ? "添加音效" : "编辑音效";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(760, 660);
        Font = UiTheme.Font;
        Padding = new Padding(20);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _name = new TextBox { Dock = DockStyle.Fill, Text = Slot.Name, MinimumSize = new Size(360, 0) };
        _path = new TextBox { Dock = DockStyle.Fill, Text = Slot.FilePath, ReadOnly = true };
        var browse = AutoButton("导入到音板");
        browse.Click += (_, _) => ImportAudio();

        _hotkeyLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 8, 6),
            Text = _hotkey?.ToDisplayString() ?? "（未设置）"
        };
        var capture = AutoButton("捕获");
        capture.Click += (_, _) => CaptureHotkey();

        _volume = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Height = 40,
            Value = (int)Math.Round(AppConfig.Clamp01(Slot.Volume) * 100)
        };
        _volumeLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 8, 0, 0),
            Text = $"{_volume.Value}%"
        };
        _volume.ValueChanged += (_, _) => _volumeLabel.Text = $"{_volume.Value}%";

        _enabled = WrapCheck("启用该音效（观察热键；不会从游戏里抢走按键）");
        _enableRamp = WrapCheck("启用速度变化（再次按下从头播放并按步进加快或减速；音调会随速率变化）");
        _baseSpeed = SpeedSpinner(Slot.BaseSpeed);
        _speedStep = SpeedSpinner(Slot.SpeedStep, minimum: -(decimal)SpeedRamp.AbsoluteMax);
        _maxSpeed = SpeedSpinner(Slot.MaxSpeed);
        _resetOnStop = WrapCheck("停止、停止全部或自然结束后重置回起始速度（再次按下重播不会重置）");

        _enableRamp.Checked = Slot.EnableSpeedRamp;
        _enabled.Checked = Slot.Enabled;
        _resetOnStop.Checked = Slot.ResetOnStop;
        _enableRamp.CheckedChanged += (_, _) => SyncRampEnabled();
        SyncRampEnabled();

        AddRow(layout, 0, FieldLabel("名称"), _name, span: 2);
        AddRow(layout, 1, FieldLabel("音频"), _path, browse);

        var importHint = WrapLabel("导入会复制到本机资料库（%AppData%\\Soundboard\\library），可重复导入以替换。");
        layout.Controls.Add(importHint, 1, 2);
        layout.SetColumnSpan(importHint, 2);

        AddRow(layout, 3, FieldLabel("热键"), _hotkeyLabel, capture);
        AddRow(layout, 4, FieldLabel("音量"), _volume, _volumeLabel);

        layout.Controls.Add(_enabled, 1, 5);
        layout.SetColumnSpan(_enabled, 2);
        layout.Controls.Add(_enableRamp, 1, 6);
        layout.SetColumnSpan(_enableRamp, 2);

        AddRow(layout, 7, FieldLabel("起始速度"), _baseSpeed, null);
        AddRow(layout, 8, FieldLabel("每次变化"), _speedStep, null);
        var stepHint = WrapLabel("可填负数表示减速，例如 -0.2。播放速度仍夹在最低 0.25× 与「最高速度」之间。");
        layout.Controls.Add(stepHint, 1, 9);
        layout.SetColumnSpan(stepHint, 2);
        AddRow(layout, 10, FieldLabel("最高速度"), _maxSpeed, null);

        layout.Controls.Add(_resetOnStop, 1, 11);
        layout.SetColumnSpan(_resetOnStop, 2);

        for (var i = 0; i <= 11; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 12, 0, 0),
            WrapContents = false
        };
        var ok = AutoButton("确定");
        ok.DialogResult = DialogResult.OK;
        var cancel = AutoButton("取消");
        cancel.DialogResult = DialogResult.Cancel;
        ok.Click += (_, _) => Apply();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static void AddRow(TableLayoutPanel layout, int row, Control label, Control field, Control? extra = null, int span = 1)
    {
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(field, 1, row);
        if (extra is not null)
        {
            layout.Controls.Add(extra, 2, row);
        }
        else if (span > 1)
        {
            layout.SetColumnSpan(field, span);
        }
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(0, 8, 12, 8),
        Margin = new Padding(0, 0, 8, 0)
    };

    private static Label WrapLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(620, 0),
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 4, 0, 8)
    };

    private static CheckBox WrapCheck(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(620, 0),
        Margin = new Padding(0, 6, 8, 8),
        Padding = new Padding(0, 2, 0, 2)
    };

    private static Button AutoButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(88, 32),
        Padding = new Padding(14, 4, 14, 4),
        Margin = new Padding(0, 4, 0, 4)
    };

    private void SyncRampEnabled()
    {
        var on = _enableRamp.Checked;
        _baseSpeed.Enabled = on;
        _speedStep.Enabled = on;
        _maxSpeed.Enabled = on;
        _resetOnStop.Enabled = on;
    }

    private static NumericUpDown SpeedSpinner(float value, decimal minimum = 0.25m)
    {
        var n = new NumericUpDown
        {
            Width = 140,
            DecimalPlaces = 2,
            Increment = 0.1m,
            Minimum = minimum,
            Maximum = (decimal)SpeedRamp.AbsoluteMax,
            Value = Math.Clamp((decimal)value, minimum, (decimal)SpeedRamp.AbsoluteMax),
            Margin = new Padding(0, 6, 0, 6)
        };
        return n;
    }

    private void ImportAudio()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "导入到音板",
            Filter = "音频文件|*.mp3;*.wav;*.ogg;*.aiff;*.aif;*.wma;*.flac;*.m4a|所有文件|*.*",
            CheckFileExists = true
        };

        var initial = PathResolver.Resolve(_path.Text, _configDirectory, _exeDirectory);
        if (File.Exists(initial))
        {
            dlg.InitialDirectory = Path.GetDirectoryName(initial);
            dlg.FileName = Path.GetFileName(initial);
        }

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var previous = _path.Text;
            var stored = MediaLibrary.Import(dlg.FileName, _configDirectory, Slot.Id);
            if (!string.Equals(previous, stored, StringComparison.OrdinalIgnoreCase))
            {
                MediaLibrary.TryDeleteStoredFile(_configDirectory, previous);
            }

            _path.Text = stored;
            if (string.IsNullOrWhiteSpace(_name.Text) || _name.Text == "新音效")
            {
                _name.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导入音频失败：\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CaptureHotkey()
    {
        using var dlg = new HotkeyCaptureDialog(_hotkey, _hotkeyMode);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _hotkey = dlg.Value;
            _hotkeyLabel.Text = _hotkey?.ToDisplayString() ?? "（未设置）";
        }
    }

    private void Apply()
    {
        Slot.Name = _name.Text.Trim();
        Slot.FilePath = _path.Text.Trim();
        Slot.Hotkey = _hotkey?.IsEmpty == false ? _hotkey : null;
        Slot.Volume = _volume.Value / 100f;
        Slot.Enabled = _enabled.Checked;
        Slot.EnableSpeedRamp = _enableRamp.Checked;
        Slot.BaseSpeed = (float)_baseSpeed.Value;
        Slot.SpeedStep = (float)_speedStep.Value;
        Slot.MaxSpeed = (float)_maxSpeed.Value;
        Slot.ResetOnStop = _resetOnStop.Checked;
        SpeedRamp.Normalize(Slot);
        if (string.IsNullOrWhiteSpace(Slot.Name))
        {
            Slot.Name = string.IsNullOrWhiteSpace(Slot.FilePath)
                ? "未命名音效"
                : Path.GetFileNameWithoutExtension(Slot.FilePath);
        }
    }
}
