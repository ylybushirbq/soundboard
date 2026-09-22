using Soundboard.Core;
using Xunit;

namespace Soundboard.Core.Tests;

public class HotkeyBindingTests
{
    [Fact]
    public void DisplayString_IncludesModifiersAndPrettyKey()
    {
        var hk = new HotkeyBinding { Control = true, Alt = true, Key = "F1" };
        Assert.Equal("Ctrl + Alt + F1", hk.ToDisplayString());
    }

    [Fact]
    public void DisplayString_DigitKeysDropDPrefix()
    {
        var hk = new HotkeyBinding { Shift = true, Key = "D3" };
        Assert.Equal("Shift + 3", hk.ToDisplayString());
    }

    [Fact]
    public void DisplayString_MouseSideButtonsAreChinese()
    {
        var a = new HotkeyBinding { Key = "XButton1" };
        var b = new HotkeyBinding { Control = true, Key = "XButton2" };
        Assert.Equal("鼠标侧键1", a.ToDisplayString());
        Assert.Equal("Ctrl + 鼠标侧键2", b.ToDisplayString());
        Assert.True(a.IsMouseButton);
        Assert.True(a.IsXButton1);
        Assert.True(b.IsXButton2);
        Assert.True(HotkeyBinding.IsMouseKey("xbutton1"));
    }

    [Fact]
    public void EmptyHotkey_ShowsPlaceholder()
    {
        Assert.Equal("（未设置）", new HotkeyBinding().ToDisplayString());
        Assert.True(new HotkeyBinding().IsEmpty);
    }

    [Fact]
    public void Equality_IsCaseInsensitiveOnKey()
    {
        var a = new HotkeyBinding { Control = true, Key = "f1" };
        var b = new HotkeyBinding { Control = true, Key = "F1" };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}

public class AppConfigTests
{
    [Fact]
    public void Normalize_ClampsVolumeAndFillsMissingIds()
    {
        var config = new AppConfig
        {
            GlobalVolume = 4f,
            Slots =
            [
                new SoundSlot { Id = "", Volume = -1f, Name = "  测试  ", FilePath = " a.wav " }
            ]
        };

        config.Normalize();
        Assert.Equal(1f, config.GlobalVolume);
        Assert.Equal(0f, config.Slots[0].Volume);
        Assert.False(string.IsNullOrWhiteSpace(config.Slots[0].Id));
        Assert.Equal("测试", config.Slots[0].Name);
        Assert.Equal("a.wav", config.Slots[0].FilePath);
    }

    [Fact]
    public void FindHotkeyConflicts_DetectsDuplicateBindings()
    {
        var shared = new HotkeyBinding { Control = true, Key = "F1" };
        var config = new AppConfig
        {
            StopAllHotkey = shared.Clone(),
            Slots =
            [
                new SoundSlot { Name = "欢呼", Hotkey = shared.Clone() }
            ]
        };

        var conflicts = config.FindHotkeyConflicts();
        Assert.NotEmpty(conflicts);
        Assert.Contains("欢呼", conflicts[0]);
    }

    [Fact]
    public void ExampleConfig_Deserializes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config.example.json");
        Assert.True(File.Exists(path), path);
        var config = ConfigStore.Deserialize(File.ReadAllText(path));
        Assert.Equal(2, config.Slots.Count);
        Assert.Equal(HotkeyMode.Game, config.HotkeyMode);
        Assert.Equal(HotkeyBehavior.Restart, config.HotkeyBehavior);
        Assert.Null(config.ToggleResetOnStopHotkey);
        Assert.Null(config.ResetSpeedHotkey);
        Assert.Null(config.ToggleSpeedDirectionHotkey);
        Assert.Equal("F1", config.Slots[0].Hotkey?.Key);
        Assert.Equal("samples/beep.wav", config.Slots[0].FilePath);
        Assert.Equal("Ctrl + Shift + F8", config.StopAllHotkey?.ToDisplayString());
        Assert.False(config.Slots[0].EnableSpeedRamp);
        Assert.Equal(1f, config.Slots[0].BaseSpeed);
        Assert.Equal("XButton1", config.Slots[1].Hotkey?.Key);
        Assert.True(config.Slots[1].EnableSpeedRamp);
        Assert.Equal(0.2f, config.Slots[1].SpeedStep);
        Assert.Equal(3f, config.Slots[1].MaxSpeed);
        Assert.True(config.Slots[1].ResetOnStop);
        Assert.Equal("鼠标侧键1", config.Slots[1].Hotkey?.ToDisplayString());
    }

    [Fact]
    public void OldJson_MissingSpeedAndMouseFields_UsesBackwardCompatibleDefaults()
    {
        const string json = """
            {
              "version": 1,
              "slots": [
                {
                  "id": "legacy",
                  "name": "旧槽",
                  "filePath": "a.wav",
                  "hotkey": { "key": "F1" },
                  "volume": 1.0,
                  "enabled": true
                }
              ]
            }
            """;

        var config = ConfigStore.Deserialize(json);
        var slot = Assert.Single(config.Slots);
        Assert.False(slot.EnableSpeedRamp);
        Assert.Equal(1f, slot.BaseSpeed);
        Assert.Equal(0.2f, slot.SpeedStep);
        Assert.Equal(3f, slot.MaxSpeed);
        Assert.True(slot.ResetOnStop);
        Assert.Equal("F1", slot.Hotkey?.Key);
        Assert.False(slot.Hotkey?.IsMouseButton);
        Assert.Equal(1f, SpeedRamp.InitialSpeed(slot));
        Assert.Equal(HotkeyMode.Game, config.HotkeyMode);
        Assert.Equal(HotkeyBehavior.Restart, config.HotkeyBehavior);
        Assert.Null(config.ToggleResetOnStopHotkey);
        Assert.Null(config.ResetSpeedHotkey);
        Assert.Null(config.ToggleSpeedDirectionHotkey);
    }
}

public class ConfigStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "soundboard-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new ConfigStore(dir, Path.Combine(dir, "config.json"));
            var original = AppConfig.CreateDefault();
            original.Slots.Add(new SoundSlot
            {
                Name = "测试",
                FilePath = "samples/beep.wav",
                Hotkey = new HotkeyBinding { Control = true, Alt = true, Key = "A" },
                Volume = 0.4f
            });
            store.Save(original);

            var loaded = store.Load();
            Assert.Single(loaded.Slots);
            Assert.Equal("测试", loaded.Slots[0].Name);
            Assert.Equal("A", loaded.Slots[0].Hotkey?.Key);
            Assert.Equal(0.4f, loaded.Slots[0].Volume);
            Assert.True(loaded.Slots[0].Hotkey?.Control);
            Assert.True(loaded.Slots[0].Hotkey?.Alt);
            Assert.False(loaded.Slots[0].EnableSpeedRamp);
            Assert.Equal(1f, loaded.Slots[0].BaseSpeed);
            Assert.True(loaded.Slots[0].ResetOnStop);
            Assert.Equal(HotkeyMode.Game, loaded.HotkeyMode);
            Assert.Equal(HotkeyBehavior.Restart, loaded.HotkeyBehavior);
            Assert.Null(loaded.ToggleResetOnStopHotkey);
            Assert.Null(loaded.ResetSpeedHotkey);
            Assert.Null(loaded.ToggleSpeedDirectionHotkey);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_CopiesExampleWhenPresent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "soundboard-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "config.example.json"),
                Path.Combine(dir, ConfigStore.ExampleFileName));
            var store = new ConfigStore(dir, Path.Combine(dir, "config.json"));
            var config = store.LoadOrCreate();
            Assert.Equal(2, config.Slots.Count);
            Assert.True(File.Exists(store.ConfigPath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class PathResolverTests
{
    [Fact]
    public void Resolve_PrefersConfigDirectoryForRelativePaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "soundboard-tests-" + Guid.NewGuid().ToString("N"));
        var samples = Path.Combine(dir, "samples");
        Directory.CreateDirectory(samples);
        var file = Path.Combine(samples, "beep.wav");
        File.WriteAllText(file, "x");
        try
        {
            var resolved = PathResolver.Resolve("samples/beep.wav", dir, exeDirectory: "/does/not/exist");
            Assert.Equal(Path.GetFullPath(file), resolved);
            Assert.True(PathResolver.IsSupportedAudio(resolved));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_FallsBackToExeDirectory()
    {
        var configDir = Path.Combine(Path.GetTempPath(), "soundboard-cfg-" + Guid.NewGuid().ToString("N"));
        var exeDir = Path.Combine(Path.GetTempPath(), "soundboard-exe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(Path.Combine(exeDir, "samples"));
        var file = Path.Combine(exeDir, "samples", "beep.wav");
        File.WriteAllText(file, "x");
        try
        {
            var resolved = PathResolver.Resolve("samples/beep.wav", configDir, exeDir);
            Assert.Equal(Path.GetFullPath(file), resolved);
        }
        finally
        {
            Directory.Delete(configDir, recursive: true);
            Directory.Delete(exeDir, recursive: true);
        }
    }
}

public class SlotRampTrackerTests
{
    private static SoundSlot RampSlot(bool resetOnStop = true) => new()
    {
        Id = "ramp",
        EnableSpeedRamp = true,
        BaseSpeed = 1f,
        SpeedStep = 0.2f,
        MaxSpeed = 3f,
        ResetOnStop = resetOnStop
    };

    [Fact]
    public void Restart_WhilePlaying_StepsSpeedWithoutReset()
    {
        var tracker = new SlotRampTracker();
        var slot = RampSlot();
        SpeedRamp.Normalize(slot);

        var first = tracker.ApplyHotkeyPress(slot, isPlaying: false, HotkeyBehavior.Restart);
        Assert.Equal(1.0f, first!.Value, precision: 3);

        var second = tracker.ApplyHotkeyPress(slot, isPlaying: true, HotkeyBehavior.Restart);
        Assert.Equal(1.2f, second!.Value, precision: 3);

        var third = tracker.ApplyHotkeyPress(slot, isPlaying: true, HotkeyBehavior.Restart);
        Assert.Equal(1.4f, third!.Value, precision: 3);

        Assert.False(SlotRampTracker.ShouldReset(PlaybackStopKind.Restart, resetOnStop: true));
    }

    [Fact]
    public void ToggleStop_ResetsRampWhenResetOnStop()
    {
        var tracker = new SlotRampTracker();
        var slot = RampSlot(resetOnStop: true);
        tracker.ApplyHotkeyPress(slot, isPlaying: false, HotkeyBehavior.Restart);
        tracker.ApplyHotkeyPress(slot, isPlaying: true, HotkeyBehavior.Restart);

        var stopped = tracker.ApplyHotkeyPress(slot, isPlaying: true, HotkeyBehavior.ToggleStop);
        Assert.Null(stopped);

        var next = tracker.ApplyHotkeyPress(slot, isPlaying: false, HotkeyBehavior.Restart);
        Assert.Equal(1.0f, next!.Value, precision: 3);
    }

    [Fact]
    public void NaturalEnd_ResetsWhenResetOnStop()
    {
        var tracker = new SlotRampTracker();
        var slot = RampSlot(resetOnStop: true);
        tracker.Consume(slot);
        tracker.Consume(slot);
        Assert.True(SlotRampTracker.ShouldReset(PlaybackStopKind.NaturalEnd, slot.ResetOnStop));
        tracker.Reset(slot.Id);
        Assert.Equal(1.0f, tracker.Consume(slot), precision: 3);
    }

    [Fact]
    public void ExplicitStop_DoesNotResetWhenResetOnStopFalse()
    {
        var tracker = new SlotRampTracker();
        var slot = RampSlot(resetOnStop: false);
        Assert.Equal(1.0f, tracker.Consume(slot), precision: 3);
        Assert.False(SlotRampTracker.ShouldReset(PlaybackStopKind.ExplicitStop, slot.ResetOnStop));
        Assert.False(SlotRampTracker.ShouldReset(PlaybackStopKind.NaturalEnd, slot.ResetOnStop));
        Assert.Equal(1.2f, tracker.Consume(slot), precision: 3);
    }

    [Fact]
    public void DisabledRamp_AlwaysOne()
    {
        var tracker = new SlotRampTracker();
        var slot = new SoundSlot { Id = "x", EnableSpeedRamp = false, BaseSpeed = 2f };
        Assert.Equal(1f, tracker.Consume(slot));
        Assert.Equal(1f, tracker.ApplyHotkeyPress(slot, true, HotkeyBehavior.Restart)!.Value);
    }

    [Fact]
    public void Restart_NegativeStep_SlowsWithoutReset()
    {
        var tracker = new SlotRampTracker();
        var slot = RampSlot();
        slot.SpeedStep = -0.2f;
        SpeedRamp.Normalize(slot);

        var first = tracker.ApplyHotkeyPress(slot, isPlaying: false, HotkeyBehavior.Restart);
        Assert.Equal(1.0f, first!.Value, precision: 3);

        var second = tracker.ApplyHotkeyPress(slot, isPlaying: true, HotkeyBehavior.Restart);
        Assert.Equal(0.8f, second!.Value, precision: 3);

        var third = tracker.ApplyHotkeyPress(slot, isPlaying: true, HotkeyBehavior.Restart);
        Assert.Equal(0.6f, third!.Value, precision: 3);
    }
}

public class MediaLibraryTests
{
    [Fact]
    public void Import_CopiesIntoLibraryWithSlotIdAndRelativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "soundboard-lib-" + Guid.NewGuid().ToString("N"));
        var srcDir = Path.Combine(root, "src");
        Directory.CreateDirectory(srcDir);
        var source = Path.Combine(srcDir, "cheer.mp3");
        File.WriteAllText(source, "audio");
        try
        {
            var stored = MediaLibrary.Import(source, root, "slotA");
            Assert.StartsWith("library/", stored.Replace('\\', '/'));
            Assert.Contains("slotA", stored);
            Assert.Contains("cheer", stored);
            var resolved = PathResolver.Resolve(stored, root);
            Assert.True(File.Exists(resolved));
            Assert.Equal("audio", File.ReadAllText(resolved));
            Assert.True(MediaLibrary.IsLibraryRelative(stored));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Import_ReplacesSameSlotAndOriginalName()
    {
        var root = Path.Combine(Path.GetTempPath(), "soundboard-lib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var a = Path.Combine(root, "a.wav");
        File.WriteAllText(a, "one");
        try
        {
            var first = MediaLibrary.Import(a, root, "id1");
            var again = MediaLibrary.Import(a, root, "id1");
            Assert.Equal(first, again);
            Assert.Equal("one", File.ReadAllText(PathResolver.Resolve(again, root)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Migrate_CopiesExeConfigAndReferencedFilesIntoAppData()
    {
        var exe = Path.Combine(Path.GetTempPath(), "soundboard-exe-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(Path.GetTempPath(), "soundboard-app-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(exe, "samples"));
        File.WriteAllText(Path.Combine(exe, "samples", "beep.wav"), "wav");
        var cfg = AppConfig.CreateDefault();
        cfg.Slots.Add(new SoundSlot
        {
            Id = "legacy",
            Name = "旧槽",
            FilePath = "samples/beep.wav"
        });
        File.WriteAllText(Path.Combine(exe, ConfigStore.FileName), ConfigStore.Serialize(cfg));
        try
        {
            var result = ConfigStore.TryMigrateFromExeDirectory(exe, app);
            Assert.True(result.Migrated);
            Assert.Equal(1, result.FilesCopied);
            var store = new ConfigStore(exe, Path.Combine(app, ConfigStore.FileName));
            var loaded = store.Load();
            Assert.True(MediaLibrary.IsLibraryRelative(loaded.Slots[0].FilePath));
            Assert.True(File.Exists(PathResolver.Resolve(loaded.Slots[0].FilePath, app)));
        }
        finally
        {
            Directory.Delete(exe, recursive: true);
            if (Directory.Exists(app))
            {
                Directory.Delete(app, recursive: true);
            }
        }
    }

    [Fact]
    public void Migrate_SkippedWhenAppDataConfigAlreadyExists()
    {
        var exe = Path.Combine(Path.GetTempPath(), "soundboard-exe-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(Path.GetTempPath(), "soundboard-app-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exe);
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(exe, ConfigStore.FileName), ConfigStore.Serialize(AppConfig.CreateDefault()));
        File.WriteAllText(Path.Combine(app, ConfigStore.FileName), ConfigStore.Serialize(AppConfig.CreateDefault()));
        try
        {
            var result = ConfigStore.TryMigrateFromExeDirectory(exe, app);
            Assert.False(result.Migrated);
        }
        finally
        {
            Directory.Delete(exe, recursive: true);
            Directory.Delete(app, recursive: true);
        }
    }

    [Fact]
    public void Resolve_AcceptsBackslashLibraryPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "soundboard-slash-" + Guid.NewGuid().ToString("N"));
        var lib = Path.Combine(dir, "library");
        Directory.CreateDirectory(lib);
        var file = Path.Combine(lib, "x.wav");
        File.WriteAllText(file, "x");
        try
        {
            var resolved = PathResolver.Resolve(@"library\x.wav", dir);
            Assert.Equal(Path.GetFullPath(file), resolved);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class SpeedRampTests
{
    [Fact]
    public void Disabled_AlwaysPlaysAtOne()
    {
        var slot = new SoundSlot { EnableSpeedRamp = false, BaseSpeed = 2f };
        Assert.Equal(1f, SpeedRamp.InitialSpeed(slot));
        Assert.Equal(1f, SpeedRamp.NextSpeed(slot, 2f));
    }

    [Fact]
    public void Enabled_StepsThenClampsAtMax()
    {
        var slot = new SoundSlot
        {
            EnableSpeedRamp = true,
            BaseSpeed = 1f,
            SpeedStep = 0.2f,
            MaxSpeed = 1.5f
        };
        SpeedRamp.Normalize(slot);

        var s1 = SpeedRamp.InitialSpeed(slot);
        var s2 = SpeedRamp.NextSpeed(slot, s1);
        var s3 = SpeedRamp.NextSpeed(slot, s2);
        var s4 = SpeedRamp.NextSpeed(slot, s3);

        Assert.Equal(1.0f, s1, precision: 3);
        Assert.Equal(1.2f, s2, precision: 3);
        Assert.Equal(1.4f, s3, precision: 3);
        Assert.Equal(1.5f, s4, precision: 3);
        Assert.Equal(1.5f, SpeedRamp.NextSpeed(slot, s4), precision: 3);
    }

    [Fact]
    public void Normalize_FixesNonFiniteAndOrdersMaxAboveBase()
    {
        var slot = new SoundSlot
        {
            EnableSpeedRamp = true,
            BaseSpeed = float.NaN,
            SpeedStep = float.NaN,
            MaxSpeed = 0.5f
        };
        SpeedRamp.Normalize(slot);
        Assert.Equal(1f, slot.BaseSpeed);
        Assert.Equal(0.2f, slot.SpeedStep);
        Assert.True(slot.MaxSpeed >= slot.BaseSpeed);
    }

    [Fact]
    public void Normalize_KeepsNegativeStep()
    {
        var slot = new SoundSlot
        {
            EnableSpeedRamp = true,
            BaseSpeed = 1f,
            SpeedStep = -0.2f,
            MaxSpeed = 3f
        };
        SpeedRamp.Normalize(slot);
        Assert.Equal(-0.2f, slot.SpeedStep, precision: 3);
    }

    [Fact]
    public void NextSpeed_NegativeStep_DecreasesThenClampsAtMin()
    {
        var slot = new SoundSlot
        {
            EnableSpeedRamp = true,
            BaseSpeed = 1f,
            SpeedStep = -0.3f,
            MaxSpeed = 3f
        };
        SpeedRamp.Normalize(slot);

        var s1 = SpeedRamp.InitialSpeed(slot);
        var s2 = SpeedRamp.NextSpeed(slot, s1);
        var s3 = SpeedRamp.NextSpeed(slot, s2);
        var s4 = SpeedRamp.NextSpeed(slot, 0.3f);

        Assert.Equal(1.0f, s1, precision: 3);
        Assert.Equal(0.7f, s2, precision: 3);
        Assert.Equal(0.4f, s3, precision: 3);
        Assert.Equal(SpeedRamp.MinSpeed, s4, precision: 3);
        Assert.Equal(SpeedRamp.MinSpeed, SpeedRamp.NextSpeed(slot, SpeedRamp.MinSpeed), precision: 3);
    }

    [Fact]
    public void FormatStep_ShowsSignedValue()
    {
        Assert.Equal("+0.2", SpeedRamp.FormatStep(0.2f));
        Assert.Equal("-0.2", SpeedRamp.FormatStep(-0.2f));
        Assert.Equal("0", SpeedRamp.FormatStep(0f));
    }

    [Fact]
    public void Format_UsesMultiplicationSign()
    {
        Assert.Equal("1.2×", SpeedRamp.Format(1.2f));
        Assert.Equal("3×", SpeedRamp.Format(3f));
    }

    [Fact]
    public void RoundTrip_MouseBindingAndRampFields()
    {
        var dir = Path.Combine(Path.GetTempPath(), "soundboard-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new ConfigStore(dir, Path.Combine(dir, "config.json"));
            var original = AppConfig.CreateDefault();
            original.Slots.Add(new SoundSlot
            {
                Name = "侧键加速",
                FilePath = "samples/beep.wav",
                Hotkey = new HotkeyBinding { Shift = true, Key = HotkeyBinding.KeyXButton1 },
                EnableSpeedRamp = true,
                BaseSpeed = 1.0f,
                SpeedStep = 0.25f,
                MaxSpeed = 2.5f,
                ResetOnStop = false
            });
            store.Save(original);

            var loaded = store.Load();
            var slot = Assert.Single(loaded.Slots);
            Assert.Equal(HotkeyBinding.KeyXButton1, slot.Hotkey?.Key);
            Assert.True(slot.Hotkey?.Shift);
            Assert.True(slot.EnableSpeedRamp);
            Assert.Equal(0.25f, slot.SpeedStep);
            Assert.Equal(2.5f, slot.MaxSpeed);
            Assert.False(slot.ResetOnStop);
            Assert.Equal("Shift + 鼠标侧键1", slot.Hotkey?.ToDisplayString());
            original.ToggleResetOnStopHotkey = new HotkeyBinding { Control = true, Shift = true, Key = "F10" };
            original.ResetSpeedHotkey = new HotkeyBinding { Control = true, Shift = true, Key = "F11" };
            original.ToggleSpeedDirectionHotkey = new HotkeyBinding { Control = true, Shift = true, Key = "F12" };
            original.Slots[0].SpeedStep = -0.25f;
            store.Save(original);
            var again = store.Load();
            Assert.Equal("F10", again.ToggleResetOnStopHotkey?.Key);
            Assert.Equal("F11", again.ResetSpeedHotkey?.Key);
            Assert.Equal("F12", again.ToggleSpeedDirectionHotkey?.Key);
            Assert.True(again.ToggleResetOnStopHotkey?.Control);
            Assert.Equal(-0.25f, again.Slots[0].SpeedStep, precision: 3);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class ResetOnStopToggleTests
{
    private static SoundSlot Slot(string id, bool enabled = true, bool reset = true) => new()
    {
        Id = id,
        Name = id,
        Enabled = enabled,
        ResetOnStop = reset
    };

    [Fact]
    public void PrefersLastTriggeredEnabledSlot()
    {
        var config = new AppConfig
        {
            Slots = [Slot("a"), Slot("b"), Slot("c", enabled: false)]
        };
        var targets = ResetOnStopToggle.ResolveTargets(config, "b", "a", settingsWindowOpen: true);
        Assert.Equal("b", Assert.Single(targets).Id);
    }

    [Fact]
    public void FallsBackToSelectedWhenSettingsOpen()
    {
        var config = new AppConfig { Slots = [Slot("a"), Slot("b")] };
        var targets = ResetOnStopToggle.ResolveTargets(config, lastTriggeredSlotId: null, "a", settingsWindowOpen: true);
        Assert.Equal("a", Assert.Single(targets).Id);
    }

    [Fact]
    public void IgnoresSelectedWhenSettingsClosed_TogglesAllEnabled()
    {
        var config = new AppConfig { Slots = [Slot("a"), Slot("b"), Slot("c", enabled: false)] };
        var targets = ResetOnStopToggle.ResolveTargets(config, null, "a", settingsWindowOpen: false);
        Assert.Equal(2, targets.Count);
        Assert.DoesNotContain(targets, s => s.Id == "c");
    }

    [Fact]
    public void Apply_TogglesSingleAndUnifiesMany()
    {
        var one = Slot("a", reset: true);
        Assert.False(ResetOnStopToggle.Apply([one]));
        Assert.False(one.ResetOnStop);

        var mixed = new[] { Slot("a", reset: true), Slot("b", reset: false) };
        Assert.True(ResetOnStopToggle.Apply(mixed));
        Assert.True(mixed.All(s => s.ResetOnStop));

        Assert.Null(ResetOnStopToggle.Apply([]));
    }

    [Fact]
    public void FindHotkeyConflicts_IncludesNewGlobalHotkeys()
    {
        var shared = new HotkeyBinding { Control = true, Key = "F10" };
        var config = new AppConfig
        {
            ToggleResetOnStopHotkey = shared.Clone(),
            ResetSpeedHotkey = shared.Clone()
        };
        var conflicts = config.FindHotkeyConflicts();
        Assert.NotEmpty(conflicts);
        Assert.Contains("重置倍速", conflicts[0]);
    }

    [Fact]
    public void FindHotkeyConflicts_IncludesToggleSpeedDirection()
    {
        var shared = new HotkeyBinding { Control = true, Key = "F12" };
        var config = new AppConfig
        {
            ResetSpeedHotkey = shared.Clone(),
            ToggleSpeedDirectionHotkey = shared.Clone()
        };
        var conflicts = config.FindHotkeyConflicts();
        Assert.NotEmpty(conflicts);
        Assert.Contains("切换加速/减速", conflicts[0]);
    }
}

public class SpeedDirectionToggleTests
{
    private static SoundSlot Slot(string id, bool enabled = true, bool ramp = true, float step = 0.2f) => new()
    {
        Id = id,
        Name = id,
        Enabled = enabled,
        EnableSpeedRamp = ramp,
        SpeedStep = step
    };

    [Fact]
    public void PrefersLastTriggeredEnabledSlot()
    {
        var config = new AppConfig
        {
            Slots = [Slot("a"), Slot("b"), Slot("c", enabled: false)]
        };
        var targets = SpeedDirectionToggle.ResolveTargets(config, "b", "a", settingsWindowOpen: true);
        Assert.Equal("b", Assert.Single(targets).Id);
    }

    [Fact]
    public void FallsBackToSelectedWhenSettingsOpen()
    {
        var config = new AppConfig { Slots = [Slot("a"), Slot("b")] };
        var targets = SpeedDirectionToggle.ResolveTargets(config, lastTriggeredSlotId: null, "a", settingsWindowOpen: true);
        Assert.Equal("a", Assert.Single(targets).Id);
    }

    [Fact]
    public void AllEnabledRampSlotsWhenClosed()
    {
        var config = new AppConfig
        {
            Slots = [Slot("a"), Slot("b", ramp: false), Slot("d"), Slot("c", enabled: false)]
        };
        var targets = SpeedDirectionToggle.ResolveTargets(config, null, "a", settingsWindowOpen: false);
        Assert.Equal(2, targets.Count);
        Assert.Contains(targets, s => s.Id == "a");
        Assert.Contains(targets, s => s.Id == "d");
        Assert.DoesNotContain(targets, s => s.Id == "b");
    }

    [Fact]
    public void Apply_FlipsSign_AndZeroIsNoOp()
    {
        var one = Slot("a", step: 0.2f);
        var result = SpeedDirectionToggle.Apply([one]);
        Assert.Equal(1, result.Flipped);
        Assert.Equal(-0.2f, one.SpeedStep, precision: 3);
        Assert.Equal("已切换为减速 -0.2", SpeedDirectionToggle.StatusMessage(result, 1));

        var again = SpeedDirectionToggle.Apply([one]);
        Assert.Equal(0.2f, one.SpeedStep, precision: 3);
        Assert.Equal("已切换为加速 +0.2", SpeedDirectionToggle.StatusMessage(again, 1));

        var zero = Slot("z", step: 0f);
        var skipped = SpeedDirectionToggle.Apply([zero]);
        Assert.Equal(0, skipped.Flipped);
        Assert.Equal(1, skipped.SkippedZero);
        Assert.Equal(0f, zero.SpeedStep);
        Assert.Equal("步进为 0，未切换方向。", SpeedDirectionToggle.StatusMessage(skipped, 1));
        Assert.Equal("没有可切换加速/减速的槽位。", SpeedDirectionToggle.StatusMessage(skipped, 0));
    }
}

public class HotkeyModeTests
{
    [Fact]
    public void CreateDefault_IsGame()
    {
        Assert.Equal(HotkeyMode.Game, AppConfig.CreateDefault().HotkeyMode);
        Assert.Equal(HotkeyMode.Game, new AppConfig().HotkeyMode);
    }

    [Fact]
    public void MissingField_DeserializesAsGame()
    {
        const string json = """{ "version": 1, "slots": [] }""";
        var config = ConfigStore.Deserialize(json);
        Assert.Equal(HotkeyMode.Game, config.HotkeyMode);
    }

    [Theory]
    [InlineData("not-a-mode")]
    [InlineData("hooks")]
    [InlineData("")]
    [InlineData("GAME_MODE")]
    [InlineData("2")]
    [InlineData("99")]
    public void InvalidValue_NormalizesToGame(string raw)
    {
        Assert.Equal(HotkeyMode.Game, HotkeyModeParser.Parse(raw));
        var json = "{ \"hotkeyMode\": " + JsonString(raw) + " }";
        var config = ConfigStore.Deserialize(json);
        Assert.Equal(HotkeyMode.Game, config.HotkeyMode);
    }

    [Theory]
    [InlineData("game", HotkeyMode.Game)]
    [InlineData("GAME", HotkeyMode.Game)]
    [InlineData("Game", HotkeyMode.Game)]
    [InlineData("desktop", HotkeyMode.Desktop)]
    [InlineData("DESKTOP", HotkeyMode.Desktop)]
    [InlineData("Desktop", HotkeyMode.Desktop)]
    [InlineData("0", HotkeyMode.Game)]
    [InlineData("1", HotkeyMode.Desktop)]
    public void Parse_AcceptsKnownValues(string raw, HotkeyMode expected)
    {
        Assert.Equal(expected, HotkeyModeParser.Parse(raw));
    }

    [Fact]
    public void InvalidEnumCast_Normalize_BecomesGame()
    {
        var config = new AppConfig { HotkeyMode = (HotkeyMode)99 };
        config.Normalize();
        Assert.Equal(HotkeyMode.Game, config.HotkeyMode);
    }

    [Fact]
    public void InvalidJsonNumber_BecomesGame()
    {
        var config = ConfigStore.Deserialize("""{ "hotkeyMode": 7 }""");
        Assert.Equal(HotkeyMode.Game, config.HotkeyMode);
    }

    [Fact]
    public void RoundTrip_DesktopPersistsAsString()
    {
        var dir = Path.Combine(Path.GetTempPath(), "soundboard-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new ConfigStore(dir, Path.Combine(dir, "config.json"));
            var original = AppConfig.CreateDefault();
            original.HotkeyMode = HotkeyMode.Desktop;
            store.Save(original);

            var json = File.ReadAllText(store.ConfigPath);
            Assert.Contains("\"hotkeyMode\": \"desktop\"", json);

            var loaded = store.Load();
            Assert.Equal(HotkeyMode.Desktop, loaded.HotkeyMode);

            loaded.HotkeyMode = HotkeyMode.Game;
            store.Save(loaded);
            var again = store.Load();
            Assert.Equal(HotkeyMode.Game, again.HotkeyMode);
            Assert.Contains("\"hotkeyMode\": \"game\"", File.ReadAllText(store.ConfigPath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Clone_CopiesMode()
    {
        var config = AppConfig.CreateDefault();
        config.HotkeyMode = HotkeyMode.Desktop;
        Assert.Equal(HotkeyMode.Desktop, config.Clone().HotkeyMode);
    }

    private static string JsonString(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}

public class DesktopHotkeyHelperTests
{
    [Fact]
    public void Modifiers_AlwaysIncludeNoRepeat()
    {
        var none = DesktopHotkeyModifiers.Build(false, false, false, false);
        Assert.Equal(DesktopHotkeyModifiers.ModNoRepeat, none);
        Assert.Equal(0x4000u, none);

        var ctrlAlt = DesktopHotkeyModifiers.Build(control: true, alt: true, shift: false, win: false);
        Assert.Equal(DesktopHotkeyModifiers.ModNoRepeat | DesktopHotkeyModifiers.ModControl | DesktopHotkeyModifiers.ModAlt, ctrlAlt);
        Assert.Equal(0x4003u, ctrlAlt);
    }

    [Fact]
    public void Modifiers_FromBinding_MatchesFlags()
    {
        var binding = new HotkeyBinding { Control = true, Shift = true, Key = "F8" };
        var mods = DesktopHotkeyModifiers.FromBinding(binding);
        Assert.True((mods & DesktopHotkeyModifiers.ModNoRepeat) != 0);
        Assert.True((mods & DesktopHotkeyModifiers.ModControl) != 0);
        Assert.True((mods & DesktopHotkeyModifiers.ModShift) != 0);
        Assert.True((mods & DesktopHotkeyModifiers.ModAlt) == 0);
        Assert.True((mods & DesktopHotkeyModifiers.ModWin) == 0);
    }

    [Fact]
    public void RegisterHotKeyError_ConflictAndInvalid()
    {
        Assert.Equal("组合键已被占用", RegisterHotKeyError.ToChinese(RegisterHotKeyError.AlreadyRegistered));
        Assert.Equal("组合键已被占用", RegisterHotKeyError.ToChinese(1409));
        Assert.Equal("按键或修饰键无效", RegisterHotKeyError.ToChinese(RegisterHotKeyError.InvalidParameter));
        Assert.Equal("没有可用于注册的窗口", RegisterHotKeyError.ToChinese(RegisterHotKeyError.NoWindow));
        Assert.Equal("注册窗口句柄无效", RegisterHotKeyError.ToChinese(1400));
        Assert.Contains("Win32 123", RegisterHotKeyError.ToChinese(123));
    }

    [Fact]
    public void Report_AllSucceeded_UsesBriefChineseStatus()
    {
        var text = DesktopHotkeyReport.StatusBar(3, 0, 1, @"C:\AppData\Soundboard");
        Assert.StartsWith("桌面模式：已注册 3 个热键", text);
        Assert.DoesNotContain("失败", text);
        Assert.Contains("Raw Input 观察 1 个鼠标侧键", text);
        Assert.Contains("Soundboard", text);
        Assert.Equal("桌面模式：已注册 3 个热键（会抢走该组合键）", DesktopHotkeyReport.SuccessBalloon(3));
    }

    [Fact]
    public void Report_Failures_IncludeCountsAndSummary()
    {
        var text = DesktopHotkeyReport.StatusBar(registeredKeyboard: 1, failedKeyboard: 2, mouseObserved: 0);
        Assert.Contains("已注册 1 个热键", text);
        Assert.Contains("失败 2 个", text);

        var summary = DesktopHotkeyReport.FailureSummary(
        [
            ("停止全部", "Ctrl + Alt + F8", "组合键已被占用"),
            ("欢呼", "Ctrl + Alt + F1", "按键或修饰键无效")
        ]);
        Assert.Contains("下列热键无法注册（桌面模式）", summary);
        Assert.Contains("组合键已被占用", summary);
        Assert.Contains("欢呼", summary);
    }

    [Fact]
    public void Report_Empty_HasNotConfiguredCopy()
    {
        var text = DesktopHotkeyReport.StatusBar(0, 0, 0);
        Assert.Contains("尚未设置热键", text);
        Assert.Contains("桌面模式", text);
    }

    [Fact]
    public void ConfigDefaults_UnchangedAfterDesktopHarden()
    {
        Assert.Equal(HotkeyMode.Game, AppConfig.CreateDefault().HotkeyMode);
        var loaded = ConfigStore.Deserialize("""{ "version": 1 }""");
        Assert.Equal(HotkeyMode.Game, loaded.HotkeyMode);
    }
}
