using Soundboard.Audio;
using Soundboard.Core;
using Soundboard.Hotkeys;
using Soundboard.Startup;

namespace Soundboard.Ui;

internal sealed class MainForm : Form
{
    public const string WindowTitle = "音板设置";

    private enum GlobalHotkeyKind
    {
        StopAll,
        MuteAll,
        ToggleResetOnStop,
        ResetSpeed,
        ToggleSpeedDirection
    }

    private readonly ConfigStore _store;
    private readonly AudioEngine _audio;
    private readonly HotkeyManager _hotkeys;
    private readonly HotkeyPoller _poller;
    private readonly Func<string> _exePath;
    private AppConfig _config;

    private readonly ListView _list;
    private readonly TrackBar _globalVolume;
    private readonly Label _globalVolumeLabel;
    private readonly CheckBox _muted;
    private readonly CheckBox _startMinimized;
    private readonly CheckBox _runAtStartup;
    private readonly RadioButton _toggleStop;
    private readonly RadioButton _restart;
    private readonly RadioButton _modeGame;
    private readonly RadioButton _modeDesktop;
    private readonly Label _rampHint;
    private readonly Label _fortniteHint;
    private readonly Label _desktopHint;
    private readonly Label _stopAllLabel;
    private readonly Label _muteAllLabel;
    private readonly Label _toggleResetLabel;
    private readonly Label _resetSpeedLabel;
    private readonly Label _toggleSpeedDirLabel;
    private readonly Label _status;
    private readonly NotifyIcon _tray;

    private bool _allowVisible;
    private bool _reallyExit;
    private bool _suppressSave;
    private int _lastHotkeyId;
    private long _lastHotkeyTick;
    private string? _lastTriggeredSlotId;
    private bool _pendingDesktopSwitchTip;

    public MainForm(
        ConfigStore store,
        AppConfig config,
        AudioEngine audio,
        HotkeyManager hotkeys,
        NotifyIcon tray,
        Func<string> exePath,
        bool startHidden)
    {
        _store = store;
        _config = config;
        _audio = audio;
        _hotkeys = hotkeys;
        _tray = tray;
        _exePath = exePath;
        _allowVisible = !startHidden;
        _poller = new HotkeyPoller(hotkeys, OnPolledHotkey);
        _hotkeys.HotkeyPressed += OnDesktopHotkey;
        HotkeyCaptureDialog.CaptureBegan += PauseHotkeysForCapture;
        HotkeyCaptureDialog.CaptureEnded += ResumeHotkeysAfterCapture;

        Text = WindowTitle;
        Font = UiTheme.Font;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(980, 800);
        Size = new Size(1040, 860);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = IconFactory.CreateAppIcon();
        Padding = new Padding(0);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            GridLines = true
        };
        _list.Columns.Add("名称", 150);
        _list.Columns.Add("热键", 160);
        _list.Columns.Add("音量", 70);
        _list.Columns.Add("状态", 110);
        _list.Columns.Add("文件", 320);
        _list.DoubleClick += (_, _) => EditSelected();
        _list.KeyDown += OnListKeyDown;

        _globalVolume = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, Width = 160, Height = 32 };
        _globalVolumeLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 8, 0, 0) };
        _globalVolume.ValueChanged += (_, _) =>
        {
            _globalVolumeLabel.Text = $"{_globalVolume.Value}%";
            if (_suppressSave) return;
            _config.GlobalVolume = _globalVolume.Value / 100f;
            _audio.GlobalVolume = _config.GlobalVolume;
        };
        _globalVolume.MouseUp += (_, _) => Persist();
        _globalVolume.KeyUp += (_, _) => Persist();

        _muted = new CheckBox { Text = "全部静音", AutoSize = true, Padding = new Padding(12, 6, 0, 0) };
        _muted.CheckedChanged += (_, _) =>
        {
            if (_suppressSave) return;
            _config.Muted = _muted.Checked;
            _audio.Muted = _config.Muted;
            Persist();
            RefreshList();
        };

        _startMinimized = new CheckBox { Text = "启动后最小化到托盘", AutoSize = true, Margin = new Padding(0, 4, 16, 4) };
        _startMinimized.CheckedChanged += (_, _) =>
        {
            if (_suppressSave) return;
            _config.StartMinimized = _startMinimized.Checked;
            Persist();
        };

        _runAtStartup = new CheckBox { Text = "开机时自动启动（当前用户）", AutoSize = true, Margin = new Padding(0, 4, 16, 4) };
        _runAtStartup.CheckedChanged += (_, _) =>
        {
            if (_suppressSave) return;
            _config.RunAtStartup = _runAtStartup.Checked;
            try
            {
                StartupManager.SetEnabled(_config.RunAtStartup, _exePath());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法写入开机启动项：\n" + ex.Message, WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Persist();
        };

        _toggleStop = new RadioButton { Text = "播放 / 停止切换", AutoSize = true, Margin = new Padding(0, 4, 16, 4) };
        _restart = new RadioButton { Text = "再次按下从头播放", AutoSize = true, Margin = new Padding(0, 4, 16, 4) };
        _toggleStop.CheckedChanged += OnBehaviorChanged;
        _restart.CheckedChanged += OnBehaviorChanged;

        _modeGame = new RadioButton
        {
            Text = "游戏模式（观察按键，不抢键，推荐打游戏）",
            AutoSize = true,
            Margin = new Padding(0, 2, 16, 2)
        };
        _modeDesktop = new RadioButton
        {
            Text = "桌面模式（RegisterHotKey，适合日常切歌，会抢走该组合键）",
            AutoSize = true,
            Margin = new Padding(0, 2, 16, 2)
        };
        _modeGame.CheckedChanged += OnHotkeyModeChanged;
        _modeDesktop.CheckedChanged += OnHotkeyModeChanged;

        _rampHint = HintLabel(RampHintText(false));
        _desktopHint = HintLabel(
            "桌面模式类似 QQ 音乐的全局快捷键：Ctrl+Alt 组合通常更稳。请先启动音板再开堡垒之夜，并尽量使用 QQ 音乐能用热键时的同一显示模式（推荐无边框）。独占全屏仍不保证。");
        _fortniteHint = HintLabel(
            "堡垒之夜：请用无边框窗口。独占全屏时 EAC 经常让后台收不到键；桌面模式也不会魔法般修好独占全屏。打游戏也可改用游戏模式（不抢键），不要绑 WASD / 建造键；推荐鼠标侧键。");

        _stopAllLabel = HotkeyValueLabel();
        _muteAllLabel = HotkeyValueLabel();
        _toggleResetLabel = HotkeyValueLabel();
        _resetSpeedLabel = HotkeyValueLabel();
        _toggleSpeedDirLabel = HotkeyValueLabel();
        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        BuildLayout();
        LoadFromConfig(_config, persist: false);
        _audio.Changed += () => BeginInvokeIfNeeded(RefreshList);
        _audio.PlaybackFailed += (id, ex) => BeginInvokeIfNeeded(() => ShowPlaybackError(id, ex));

        Shown += (_, _) =>
        {
            ResizeLastColumn();
            ApplyHotkeys();
        };
        Resize += (_, _) =>
        {
            ResizeLastColumn();
            var wrap = Math.Max(400, ClientSize.Width - 80);
            _rampHint.MaximumSize = new Size(wrap, 0);
            _fortniteHint.MaximumSize = new Size(wrap, 0);
            _desktopHint.MaximumSize = new Size(wrap, 0);
        };
    }

    public void Reveal()
    {
        _allowVisible = true;
        Show();
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Activate();
        NativeMethods.SetForegroundWindow(Handle);
    }

    public void RequestExit()
    {
        _reallyExit = true;
        _allowVisible = true;
        Close();
    }

    public void ApplyHotkeys(bool userInitiated = false)
    {
        // Drop the other path first so game poll + desktop RegisterHotKey never overlap.
        _poller.Enabled = false;
        _poller.ResetHeldState();
        _hotkeys.UnregisterAll();

        var results = _hotkeys.ReplaceAll(_config);
        var desktop = _config.HotkeyMode == HotkeyMode.Desktop;
        if (!desktop)
        {
            _poller.Enabled = true;
            _pendingDesktopSwitchTip = false;
        }

        var failed = results.Where(r => !r.Success).ToList();
        var keyboardOk = results.Count(r => r.Success && !r.Binding.IsMouseButton);
        var mouseOk = results.Count(r => r.Success && r.Binding.IsMouseButton);
        var data = $"数据目录：{_store.ConfigDirectory}";
        if (!desktop)
        {
            var modeLabel = "游戏模式";
            if (failed.Count == 0)
            {
                if (keyboardOk == 0 && mouseOk == 0)
                {
                    _status.Text = $"{modeLabel}（观察按键，不抢键）。尚未设置热键。{data}";
                }
                else
                {
                    var parts = new List<string>();
                    if (keyboardOk > 0)
                    {
                        parts.Add($"GetAsyncKeyState 观察 {keyboardOk} 个键盘热键（不抢键）");
                    }

                    if (mouseOk > 0)
                    {
                        parts.Add($"Raw Input 观察 {mouseOk} 个鼠标侧键");
                    }

                    _status.Text = $"{modeLabel}：{string.Join("；", parts)}。{data}";
                }
            }
            else
            {
                _status.Text = $"{modeLabel} · 部分热键失败：" +
                               string.Join("；", failed.Select(f => $"{f.Owner}（{f.Binding.ToDisplayString()}）：{f.Error}")) +
                               $"  {data}";
            }
        }
        else
        {
            _status.Text = DesktopHotkeyReport.StatusBar(keyboardOk, failed.Count, mouseOk, _store.ConfigDirectory);
            if (failed.Count > 0)
            {
                var body = DesktopHotkeyReport.FailureSummary(
                    failed.Select(f => (f.Owner, f.Binding.ToDisplayString(), f.Error ?? "注册失败")));
                if (userInitiated && (_allowVisible || Visible))
                {
                    MessageBox.Show(this, body, WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    var balloon = body.Length <= 240 ? body : body[..237] + "…";
                    _tray.ShowBalloonTip(2800, "桌面模式热键注册失败", balloon, ToolTipIcon.Warning);
                }

                _pendingDesktopSwitchTip = false;
            }
            else if (keyboardOk > 0 && _pendingDesktopSwitchTip)
            {
                _tray.ShowBalloonTip(1800, "音板", DesktopHotkeyReport.SuccessBalloon(keyboardOk), ToolTipIcon.Info);
                _pendingDesktopSwitchTip = false;
            }
            else
            {
                _pendingDesktopSwitchTip = false;
            }
        }

        RefreshList();
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(value && _allowVisible);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _allowVisible = false;
            Hide();
            _tray.ShowBalloonTip(1800, "音板", "窗口已隐藏到托盘。退出请用托盘图标右键菜单。", ToolTipIcon.Info);
            return;
        }

        Persist();
        if (_reallyExit)
        {
            HotkeyCaptureDialog.CaptureBegan -= PauseHotkeysForCapture;
            HotkeyCaptureDialog.CaptureEnded -= ResumeHotkeysAfterCapture;
            _hotkeys.HotkeyPressed -= OnDesktopHotkey;
            _poller.Dispose();
            _hotkeys.Dispose();
            RawInputMouse.Unregister();
        }

        base.OnFormClosing(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RawInputMouse.Register(Handle) && IsHandleCreated)
        {
            BeginInvokeIfNeeded(() =>
            {
                if (!IsDisposed)
                {
                    _status.Text = "无法注册 Raw Input：将改用 GetAsyncKeyState 观察鼠标侧键。";
                }
            });
        }

        _poller.Enabled = _config.HotkeyMode == HotkeyMode.Game;
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        RawInputMouse.Unregister();
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmInput)
        {
            HandleRawMouse(m.LParam);
        }

        if (m.Msg == TrayAppContext.ShowMessageId)
        {
            Reveal();
            return;
        }

        base.WndProc(ref m);
    }

    private void HandleRawMouse(IntPtr hRawInput)
    {
        if (!RawInputMouse.TryReadXButtonDown(hRawInput, out var xButton1))
        {
            return;
        }

        if (HotkeyCaptureDialog.ActiveInstance is { } capture)
        {
            capture.AcceptMouseSideButton(xButton1);
            return;
        }

        NativeMethods.ReadAsyncModifiers(out var control, out var alt, out var shift, out var win);
        if (_hotkeys.TryMatchMouse(xButton1, control, alt, shift, win, out var id))
        {
            HandleHotkey(id);
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16, 12, 16, 8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 470));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 8)
        };
        var title = new Label
        {
            Text = "音板",
            Font = new Font(UiTheme.Font.FontFamily, 18f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        var subtitle = new Label
        {
            Text = "独立托盘程序：全局热键播放音频。游戏模式只观察不抢键；桌面模式用 RegisterHotKey（会抢走该组合键）。不会注入游戏、不读写其他进程内存。音频保存在本机数据目录。",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 4)
        };
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(subtitle, 0, 1);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 4, 0, 8)
        };
        toolbar.Controls.Add(Button("添加", (_, _) => AddSlot()));
        toolbar.Controls.Add(Button("编辑", (_, _) => EditSelected()));
        toolbar.Controls.Add(Button("删除", (_, _) => RemoveSelected()));
        toolbar.Controls.Add(Button("试听", (_, _) => PreviewSelected()));
        toolbar.Controls.Add(Button("停止全部", (_, _) => _audio.StopAll(), 108));
        toolbar.Controls.Add(Button("重置倍速", (_, _) => ResetAllSpeeds(), 108));
        toolbar.Controls.Add(Button("保存", (_, _) => Persist(announce: true)));
        toolbar.Controls.Add(Button("重新加载", (_, _) => Reload(), 108));

        var settings = new GroupBox
        {
            Text = "设置",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 8, 10, 8)
        };
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 4, 0, 4)
        };
        var settingsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 12,
            Padding = new Padding(4, 4, 20, 8)
        };

        var volumeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
        volumeRow.Controls.Add(new Label { Text = "全局音量", AutoSize = true, Padding = new Padding(0, 8, 8, 0) });
        volumeRow.Controls.Add(_globalVolume);
        volumeRow.Controls.Add(_globalVolumeLabel);
        volumeRow.Controls.Add(_muted);

        var hotkeyRow = FlowRow();
        hotkeyRow.Controls.Add(new Label { Text = "停止全部：", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        hotkeyRow.Controls.Add(_stopAllLabel);
        hotkeyRow.Controls.Add(Button("更改", (_, _) => EditGlobalHotkey(GlobalHotkeyKind.StopAll), 72));
        hotkeyRow.Controls.Add(new Label { Text = "全部静音：", AutoSize = true, Padding = new Padding(16, 6, 0, 0) });
        hotkeyRow.Controls.Add(_muteAllLabel);
        hotkeyRow.Controls.Add(Button("更改", (_, _) => EditGlobalHotkey(GlobalHotkeyKind.MuteAll), 72));

        var extraHotkeyRow = FlowRow();
        extraHotkeyRow.Controls.Add(new Label { Text = "切换停止后重置：", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        extraHotkeyRow.Controls.Add(_toggleResetLabel);
        extraHotkeyRow.Controls.Add(Button("更改", (_, _) => EditGlobalHotkey(GlobalHotkeyKind.ToggleResetOnStop), 72));
        extraHotkeyRow.Controls.Add(new Label { Text = "重置倍速：", AutoSize = true, Padding = new Padding(16, 6, 0, 0) });
        extraHotkeyRow.Controls.Add(_resetSpeedLabel);
        extraHotkeyRow.Controls.Add(Button("更改", (_, _) => EditGlobalHotkey(GlobalHotkeyKind.ResetSpeed), 72));

        var directionHotkeyRow = FlowRow();
        directionHotkeyRow.Controls.Add(new Label { Text = "切换加速/减速：", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        directionHotkeyRow.Controls.Add(_toggleSpeedDirLabel);
        directionHotkeyRow.Controls.Add(Button("更改", (_, _) => EditGlobalHotkey(GlobalHotkeyKind.ToggleSpeedDirection), 72));

        var toggleHint = HintLabel("「切换停止后重置」和「切换加速/减速」优先作用于最近一次触发的槽位；若还没有触发过，则设置窗口打开时用当前选中行，否则作用于所有符合条件的启用槽位。步进为 0 时不切换方向。");

        var modeRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill
        };
        modeRow.Controls.Add(new Label
        {
            Text = "热键获取方式：",
            AutoSize = true,
            Padding = new Padding(0, 2, 0, 2)
        });
        modeRow.Controls.Add(_modeGame);
        modeRow.Controls.Add(_modeDesktop);
        modeRow.Controls.Add(_desktopHint);

        var flagsRow = FlowRow();
        flagsRow.Controls.Add(_startMinimized);
        flagsRow.Controls.Add(_runAtStartup);

        var behaviorRow = FlowRow();
        behaviorRow.Controls.Add(new Label { Text = "槽位热键行为：", AutoSize = true, Padding = new Padding(0, 4, 8, 0) });
        behaviorRow.Controls.Add(_toggleStop);
        behaviorRow.Controls.Add(_restart);

        settingsLayout.Controls.Add(volumeRow, 0, 0);
        settingsLayout.Controls.Add(hotkeyRow, 0, 1);
        settingsLayout.Controls.Add(extraHotkeyRow, 0, 2);
        settingsLayout.Controls.Add(directionHotkeyRow, 0, 3);
        settingsLayout.Controls.Add(toggleHint, 0, 4);
        settingsLayout.Controls.Add(modeRow, 0, 5);
        settingsLayout.Controls.Add(_fortniteHint, 0, 6);
        settingsLayout.Controls.Add(flagsRow, 0, 7);
        settingsLayout.Controls.Add(behaviorRow, 0, 8);
        settingsLayout.Controls.Add(_rampHint, 0, 9);
        scroll.Controls.Add(settingsLayout);
        settings.Controls.Add(scroll);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(toolbar, 0, 1);
        root.Controls.Add(_list, 0, 2);
        root.Controls.Add(settings, 0, 3);
        root.Controls.Add(_status, 0, 4);
        Controls.Add(root);
    }

    private static FlowLayoutPanel FlowRow() => new()
    {
        AutoSize = true,
        WrapContents = true,
        Dock = DockStyle.Fill
    };

    private static Label HintLabel(string text) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(920, 0),
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 4, 8, 8),
        Text = text
    };

    private static Label HotkeyValueLabel() => new()
    {
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(0, 6, 8, 0)
    };

    private static Button Button(string text, EventHandler onClick, int minWidth = 88)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 28,
            MinimumSize = new Size(minWidth, 28),
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0, 0, 8, 4)
        };
        b.Click += onClick;
        return b;
    }

    private void LoadFromConfig(AppConfig config, bool persist)
    {
        _suppressSave = true;
        _config = config;
        _config.Normalize();
        _audio.GlobalVolume = _config.GlobalVolume;
        _audio.Muted = _config.Muted;
        _globalVolume.Value = (int)Math.Round(_config.GlobalVolume * 100);
        _globalVolumeLabel.Text = $"{_globalVolume.Value}%";
        _muted.Checked = _config.Muted;
        _startMinimized.Checked = _config.StartMinimized;
        _runAtStartup.Checked = _config.RunAtStartup;
        _toggleStop.Checked = _config.HotkeyBehavior != HotkeyBehavior.Restart;
        _restart.Checked = _config.HotkeyBehavior == HotkeyBehavior.Restart;
        _modeGame.Checked = _config.HotkeyMode != HotkeyMode.Desktop;
        _modeDesktop.Checked = _config.HotkeyMode == HotkeyMode.Desktop;
        _stopAllLabel.Text = _config.StopAllHotkey?.ToDisplayString() ?? "（未设置）";
        _muteAllLabel.Text = _config.MuteAllHotkey?.ToDisplayString() ?? "（未设置）";
        _toggleResetLabel.Text = _config.ToggleResetOnStopHotkey?.ToDisplayString() ?? "（未设置）";
        _resetSpeedLabel.Text = _config.ResetSpeedHotkey?.ToDisplayString() ?? "（未设置）";
        _toggleSpeedDirLabel.Text = _config.ToggleSpeedDirectionHotkey?.ToDisplayString() ?? "（未设置）";
        UpdateRampHint();
        _suppressSave = false;
        RefreshList();
        if (persist)
        {
            Persist();
        }
    }

    private void RefreshList()
    {
        var selectedId = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as string : null;
        _list.BeginUpdate();
        _list.Items.Clear();
        var failed = _hotkeys.LastResults.Where(r => !r.Success)
            .ToDictionary(r => r.Binding, r => r.Error ?? "失败");

        foreach (var slot in _config.Slots)
        {
            var status = StatusText(slot, failed);
            var item = new ListViewItem(string.IsNullOrWhiteSpace(slot.Name) ? "未命名" : slot.Name)
            {
                Tag = slot.Id
            };
            item.SubItems.Add(slot.Hotkey?.ToDisplayString() ?? "（未设置）");
            item.SubItems.Add($"{Math.Round(slot.Volume * 100)}%");
            item.SubItems.Add(status);
            item.SubItems.Add(slot.FilePath);
            if (_audio.IsPlaying(slot.Id))
            {
                item.ForeColor = Color.FromArgb(16, 96, 72);
            }

            _list.Items.Add(item);
            if (slot.Id == selectedId)
            {
                item.Selected = true;
            }
        }

        _list.EndUpdate();
        _muted.Checked = _audio.Muted;
        _tray.Text = TrayText();
        UpdateRampHint();
    }

    private string StatusText(SoundSlot slot, Dictionary<HotkeyBinding, string> failed)
    {
        if (!slot.Enabled)
        {
            return "已禁用";
        }

        if (_audio.IsPlaying(slot.Id))
        {
            var speed = _audio.PlayingSpeed(slot.Id);
            return speed is { } s && Math.Abs(s - 1f) > 0.001f
                ? $"播放中 {SpeedRamp.Format(s)}"
                : "播放中";
        }

        if (slot.Hotkey is { IsEmpty: false } hk && failed.ContainsKey(hk))
        {
            return "热键失败";
        }

        if (slot.Hotkey is null || slot.Hotkey.IsEmpty)
        {
            return "无热键";
        }

        return slot.Hotkey.IsMouseButton
            ? "侧键观察"
            : _config.HotkeyMode == HotkeyMode.Desktop ? "已注册" : "键盘观察";
    }

    private string TrayText()
    {
        var playing = _audio.PlayingIds.Count;
        var muted = _audio.Muted ? " · 已静音" : "";
        var text = playing > 0 ? $"音板 · 正在播放 {playing} 个{muted}" : $"音板 · 后台运行{muted}";
        return text.Length <= 63 ? text : text[..63];
    }

    private SoundSlot? SelectedSlot()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return null;
        }

        var id = _list.SelectedItems[0].Tag as string;
        return _config.Slots.FirstOrDefault(s => s.Id == id);
    }

    private bool SettingsWindowOpen => Visible && _allowVisible && WindowState != FormWindowState.Minimized;

    private void AddSlot()
    {
        var slot = new SoundSlot { Name = "新音效", Volume = 1f, Enabled = true };
        using var dlg = new SlotEditDialog(slot, _store.ConfigDirectory, _store.ExeDirectory, _config.HotkeyMode);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.Slots.Add(dlg.Slot);
        if (!ValidateAndSave())
        {
            _config.Slots.RemoveAll(s => s.Id == dlg.Slot.Id);
        }
    }

    private void EditSelected()
    {
        var slot = SelectedSlot();
        if (slot is null)
        {
            return;
        }

        using var dlg = new SlotEditDialog(slot, _store.ConfigDirectory, _store.ExeDirectory, _config.HotkeyMode);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var index = _config.Slots.FindIndex(s => s.Id == slot.Id);
        if (index < 0)
        {
            return;
        }

        var previous = _config.Slots[index];
        _config.Slots[index] = dlg.Slot;
        if (!ValidateAndSave())
        {
            _config.Slots[index] = previous;
        }
    }

    private void RemoveSelected()
    {
        var slot = SelectedSlot();
        if (slot is null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(slot.Name) ? "该音效" : $"「{slot.Name}」";
        if (MessageBox.Show(this, $"确定删除{name}？", WindowTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            != DialogResult.Yes)
        {
            return;
        }

        _audio.Stop(slot.Id);
        if (_lastTriggeredSlotId == slot.Id)
        {
            _lastTriggeredSlotId = null;
        }

        _config.Slots.RemoveAll(s => s.Id == slot.Id);
        Persist();
        ApplyHotkeys(userInitiated: true);
    }

    private void PreviewSelected()
    {
        var slot = SelectedSlot();
        if (slot is null)
        {
            return;
        }

        TriggerSlot(slot);
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            RemoveSelected();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            EditSelected();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Space)
        {
            PreviewSelected();
            e.Handled = true;
        }
    }

    private void EditGlobalHotkey(GlobalHotkeyKind kind)
    {
        var current = kind switch
        {
            GlobalHotkeyKind.StopAll => _config.StopAllHotkey,
            GlobalHotkeyKind.MuteAll => _config.MuteAllHotkey,
            GlobalHotkeyKind.ToggleResetOnStop => _config.ToggleResetOnStopHotkey,
            GlobalHotkeyKind.ResetSpeed => _config.ResetSpeedHotkey,
            GlobalHotkeyKind.ToggleSpeedDirection => _config.ToggleSpeedDirectionHotkey,
            _ => null
        };
        using var dlg = new HotkeyCaptureDialog(current, _config.HotkeyMode);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        switch (kind)
        {
            case GlobalHotkeyKind.StopAll:
                _config.StopAllHotkey = dlg.Value;
                _stopAllLabel.Text = _config.StopAllHotkey?.ToDisplayString() ?? "（未设置）";
                break;
            case GlobalHotkeyKind.MuteAll:
                _config.MuteAllHotkey = dlg.Value;
                _muteAllLabel.Text = _config.MuteAllHotkey?.ToDisplayString() ?? "（未设置）";
                break;
            case GlobalHotkeyKind.ToggleResetOnStop:
                _config.ToggleResetOnStopHotkey = dlg.Value;
                _toggleResetLabel.Text = _config.ToggleResetOnStopHotkey?.ToDisplayString() ?? "（未设置）";
                break;
            case GlobalHotkeyKind.ResetSpeed:
                _config.ResetSpeedHotkey = dlg.Value;
                _resetSpeedLabel.Text = _config.ResetSpeedHotkey?.ToDisplayString() ?? "（未设置）";
                break;
            case GlobalHotkeyKind.ToggleSpeedDirection:
                _config.ToggleSpeedDirectionHotkey = dlg.Value;
                _toggleSpeedDirLabel.Text = _config.ToggleSpeedDirectionHotkey?.ToDisplayString() ?? "（未设置）";
                break;
        }

        ValidateAndSave();
    }

    private void OnBehaviorChanged(object? sender, EventArgs e)
    {
        if (_suppressSave || sender is RadioButton { Checked: false })
        {
            return;
        }

        _config.HotkeyBehavior = _restart.Checked ? HotkeyBehavior.Restart : HotkeyBehavior.ToggleStop;
        UpdateRampHint();
        Persist();
    }

    private void OnHotkeyModeChanged(object? sender, EventArgs e)
    {
        if (_suppressSave || sender is RadioButton { Checked: false })
        {
            return;
        }

        _config.HotkeyMode = _modeDesktop.Checked ? HotkeyMode.Desktop : HotkeyMode.Game;
        _pendingDesktopSwitchTip = _config.HotkeyMode == HotkeyMode.Desktop;
        Persist();
        ApplyHotkeys(userInitiated: true);
    }

    private void PauseHotkeysForCapture()
    {
        _poller.Enabled = false;
        _poller.ResetHeldState();
        _hotkeys.UnregisterAll();
    }

    private void ResumeHotkeysAfterCapture()
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvokeIfNeeded(() => ApplyHotkeys());
    }

    private void UpdateRampHint()
    {
        var anyRamp = _config.Slots.Any(s => s.EnableSpeedRamp);
        _rampHint.Text = RampHintText(_restart.Checked || _config.HotkeyBehavior == HotkeyBehavior.Restart);
        _rampHint.ForeColor = anyRamp && (_restart.Checked || _config.HotkeyBehavior == HotkeyBehavior.Restart)
            ? Color.FromArgb(16, 96, 72)
            : SystemColors.GrayText;
    }

    private static string RampHintText(bool restart)
    {
        if (restart)
        {
            return "再次按下从头播放：正在播时再按会从开头重播。若该槽开启了速度变化，每次重播按步进加快或减速（步进可填负数），不会因为重播而回到起始速度。停止全部、点停止、或自然播完（且勾选「停止后重置」）才会回到起始速度。「重置倍速」只清掉变化记忆，不停播。「切换加速/减速」会翻转步进正负号。";
        }

        return "播放/停止切换：正在播时再按是停止，不会改变速度。开启速度变化的槽位，只有下一次从停止状态再播放才会用下一档速度。「重置倍速」只清掉变化记忆，不停播。「切换加速/减速」会翻转步进正负号。";
    }

    private bool ValidateAndSave()
    {
        var conflicts = _config.FindHotkeyConflicts();
        if (conflicts.Count > 0)
        {
            MessageBox.Show(this, "热键冲突：\n" + string.Join("\n", conflicts), WindowTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            LoadFromConfig(_store.Load(), persist: false);
            ApplyHotkeys(userInitiated: true);
            return false;
        }

        Persist();
        ApplyHotkeys(userInitiated: true);
        return true;
    }

    private void Persist(bool announce = false)
    {
        try
        {
            _store.Save(_config);
            if (announce)
            {
                _status.Text = $"已保存。数据目录：{_store.ConfigDirectory}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存配置失败：\n" + ex.Message, WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Reload()
    {
        try
        {
            LoadFromConfig(_store.Load(), persist: false);
            ApplyHotkeys(userInitiated: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "读取配置失败：\n" + ex.Message, WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnPolledHotkey(int id) => BeginInvokeIfNeeded(() => HandleHotkey(id));

    private void OnDesktopHotkey(int id)
    {
        if (_config.HotkeyMode != HotkeyMode.Desktop)
        {
            return;
        }

        BeginInvokeIfNeeded(() => HandleHotkey(id));
    }

    private void HandleHotkey(int id)
    {
        if (HotkeyCaptureDialog.ActiveInstance is not null)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (id == _lastHotkeyId && now - _lastHotkeyTick < 80)
        {
            return;
        }

        _lastHotkeyId = id;
        _lastHotkeyTick = now;

        if (id == HotkeyManager.IdStopAll)
        {
            _audio.StopAll();
            return;
        }

        if (id == HotkeyManager.IdMuteAll)
        {
            _audio.Muted = !_audio.Muted;
            _config.Muted = _audio.Muted;
            _suppressSave = true;
            _muted.Checked = _config.Muted;
            _suppressSave = false;
            Persist();
            RefreshList();
            return;
        }

        if (id == HotkeyManager.IdToggleResetOnStop)
        {
            ToggleResetOnStop();
            return;
        }

        if (id == HotkeyManager.IdResetSpeed)
        {
            ResetAllSpeeds();
            return;
        }

        if (id == HotkeyManager.IdToggleSpeedDirection)
        {
            ToggleSpeedDirection();
            return;
        }

        var slotId = _hotkeys.GetSlotId(id);
        var slot = _config.Slots.FirstOrDefault(s => s.Id == slotId);
        if (slot is null)
        {
            return;
        }

        TriggerSlot(slot);
    }

    private void ToggleResetOnStop()
    {
        var targets = ResetOnStopToggle.ResolveTargets(
            _config,
            _lastTriggeredSlotId,
            SelectedSlot()?.Id,
            SettingsWindowOpen);
        var newValue = ResetOnStopToggle.Apply(targets);
        if (newValue is null)
        {
            Announce("没有可切换的启用槽位。", balloon: false);
            return;
        }

        Persist();
        RefreshList();
        var state = newValue.Value ? "开" : "关";
        string who;
        if (targets.Count == 1)
        {
            var name = string.IsNullOrWhiteSpace(targets[0].Name) ? targets[0].Id : targets[0].Name;
            who = $"音效「{name}」";
        }
        else
        {
            who = $"全部 {targets.Count} 个启用槽位";
        }

        Announce($"{who} 停止后重置：{state}", balloon: true);
    }

    private void ToggleSpeedDirection()
    {
        var targets = SpeedDirectionToggle.ResolveTargets(
            _config,
            _lastTriggeredSlotId,
            SelectedSlot()?.Id,
            SettingsWindowOpen);
        var result = SpeedDirectionToggle.Apply(targets);
        var text = SpeedDirectionToggle.StatusMessage(result, targets.Count);
        if (result.Flipped > 0)
        {
            Persist();
            RefreshList();
        }

        Announce(text, balloon: result.Flipped > 0);
    }

    private void ResetAllSpeeds()
    {
        _audio.ResetAllRamps();
        RefreshList();
        Announce("已重置全部槽位倍速（下次播放从起始速度开始，当前播放未停止）。", balloon: true);
    }

    private void Announce(string text, bool balloon)
    {
        _status.Text = $"{text}  数据目录：{_store.ConfigDirectory}";
        if (balloon)
        {
            _tray.ShowBalloonTip(1800, "音板", text, ToolTipIcon.Info);
        }
    }

    private void TriggerSlot(SoundSlot slot)
    {
        _lastTriggeredSlotId = slot.Id;
        var path = PathResolver.Resolve(slot.FilePath, _store.ConfigDirectory, _store.ExeDirectory);
        try
        {
            _audio.ToggleOrRestart(slot, path, _config.HotkeyBehavior);
        }
        catch (Exception ex)
        {
            ShowPlaybackError(slot.Id, ex);
        }
    }

    private void ShowPlaybackError(string slotId, Exception ex)
    {
        var slot = _config.Slots.FirstOrDefault(s => s.Id == slotId);
        var name = slot?.Name ?? slotId;
        _tray.ShowBalloonTip(2500, "无法播放", $"{name}：{ex.Message}", ToolTipIcon.Warning);
        _status.Text = $"播放失败（{name}）：{ex.Message}  数据目录：{_store.ConfigDirectory}";
    }

    private void ResizeLastColumn()
    {
        if (_list.Columns.Count == 0)
        {
            return;
        }

        var used = 0;
        for (var i = 0; i < _list.Columns.Count - 1; i++)
        {
            used += _list.Columns[i].Width;
        }

        var remaining = _list.ClientSize.Width - used - 8;
        _list.Columns[_list.Columns.Count - 1].Width = Math.Max(120, remaining);
    }

    private void BeginInvokeIfNeeded(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try { BeginInvoke(action); }
            catch (ObjectDisposedException) { /* shutting down */ }
            return;
        }

        action();
    }
}
