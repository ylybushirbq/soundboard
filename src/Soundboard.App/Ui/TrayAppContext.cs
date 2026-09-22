using Soundboard.Audio;
using Soundboard.Core;
using Soundboard.Hotkeys;
using Soundboard.Ui;

namespace Soundboard;

internal sealed class TrayAppContext : ApplicationContext
{
    public const string ShowMessageName = "Soundboard.TrayApp.ShowSettings";
    public static uint ShowMessageId { get; private set; }

    private readonly NotifyIcon _tray;
    private readonly MainForm _form;
    private readonly AudioEngine _audio;
    private readonly HotkeyManager _hotkeys;
    private readonly Icon _icon;

    private static string SettingsFormTitle => Ui.MainForm.WindowTitle;

    public TrayAppContext(bool forceMinimized)
    {
        ShowMessageId = NativeMethods.RegisterWindowMessage(ShowMessageName);

        var exeDir = ConfigStore.PersistentExeDirectory();
        ConfigMigrationResult? migration = null;
        try
        {
            var appData = ConfigStore.GetAppDataDirectory();
            Directory.CreateDirectory(appData);
            migration = ConfigStore.TryMigrateFromExeDirectory(exeDir, appData);
        }
        catch
        {
            migration = null;
        }

        var store = ConfigStore.OpenAppData(exeDir);
        AppConfig config;
        try
        {
            config = store.LoadOrCreate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "配置文件无法读取，将使用默认设置。\n" + ex.Message,
                SettingsFormTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            config = AppConfig.CreateDefault();
        }

        _icon = IconFactory.CreateAppIcon();
        _audio = new AudioEngine
        {
            GlobalVolume = config.GlobalVolume,
            Muted = config.Muted
        };

        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = "音板",
            Visible = true
        };

        MainForm? form = null;
        _hotkeys = new HotkeyManager();

        var startHidden = forceMinimized || config.StartMinimized;
        form = new MainForm(store, config, _audio, _hotkeys, _tray, () => Application.ExecutablePath, startHidden);
        _form = form;

        _tray.ContextMenuStrip = BuildMenu();
        _tray.DoubleClick += (_, _) => _form.Reveal();

        // Create the HWND while still hidden so Raw Input has a target.
        _ = _form.Handle;
        _form.ApplyHotkeys();

        if (!startHidden)
        {
            _form.Reveal();
        }
        else
        {
            _tray.ShowBalloonTip(1600, "音板", "已在托盘运行。双击图标打开设置。", ToolTipIcon.Info);
        }

        if (migration is { Migrated: true })
        {
            var copied = migration.FilesCopied;
            var missing = migration.FilesMissing;
            MessageBox.Show(
                _form,
                "已将旧配置从程序目录迁移到稳定的本机数据目录：\n" +
                $"{store.ConfigDirectory}\n\n" +
                $"已导入 {copied} 个仍能找到的音频到 library 资料库" +
                (missing > 0 ? $"，有 {missing} 个原路径已找不到。" : "。") +
                "\n\n之后请以该目录为准。把 exe 放在桌面或 OneDrive 也不会再把配置和音频弄丢。",
                SettingsFormTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        _form.FormClosed += (_, _) => Exit();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => _form.Reveal());
        menu.Items.Add("停止全部声音", null, (_, _) => _audio.StopAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => _form.RequestExit());
        return menu;
    }

    private void Exit()
    {
        _hotkeys.Dispose();
        _audio.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _icon.Dispose();
        ExitThread();
    }
}
