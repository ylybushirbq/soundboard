# 音板（Soundboard）

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue)](#系统要求)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

独立运行的 **Windows 托盘音板**：为自定义音频绑定全局热键或鼠标侧键，打游戏、办公、直播时一键触发。界面为简体中文。

> 普通用户态桌面程序：**不注入游戏**、不读写其他进程内存、不安装驱动、不使用 `WH_*` 低级钩子。清单为 `asInvoker`。

仓库目前没有 Release 附件。要拿到 `Soundboard.exe`，按下面的步骤从源码发布。

## 功能一览

- 系统托盘常驻。关闭设置窗口只是藏进托盘；退出用托盘菜单。可选「启动后最小化到托盘」
- 单实例：再次启动会唤起已经在跑的窗口
- 多音效槽位：名称、热键、音量、启用开关，以及导入到本机资料库的音频
- 配置和音频在 `%AppData%\Soundboard\`，与 exe 分开存放
- **游戏模式 / 桌面模式** 可切换。鼠标侧键（`XButton1` / `XButton2`）两种模式都用 Raw Input
- 每槽播放速度变化：步进可为正（加速）或负（减速）；可用热键翻转方向
- 全局热键：停止全部、全部静音、切换「停止后重置」、重置倍速、切换加速/减速
- 可选：当前用户开机启动（只写 `HKCU\...\Run`）
- 推荐产物：自包含 **单文件** `Soundboard.exe`

## 快速开始

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。`global.json` 指定 8.0，并允许同一主版本向前滚动。

```bash
dotnet restore Soundboard.sln
dotnet build Soundboard.sln -c Release
dotnet test tests/Soundboard.Core.Tests/Soundboard.Core.Tests.csproj -c Release
```

`Soundboard.App` 同时面向 `net8.0-windows`（托盘程序）和 `net8.0`（只编译 `Domain/`，给无界面测试用）。上面的 `dotnet test` 走 `net8.0`，不需要 Windows 桌面运行时。发布时保留 `-f net8.0-windows`。

### 发布单文件 exe

Windows x64、自包含、单文件（对方不必单独安装 .NET）：

```bash
dotnet publish src/Soundboard.App/Soundboard.App.csproj -c Release -f net8.0-windows -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish
```

产物：`publish/Soundboard.exe`。工程已开启 `EnableWindowsTargeting`。在 Linux 上可以尝试同一条命令；若交叉发布无法生成真正的 Windows 单文件 apphost，请在 Windows 上执行。

已安装 .NET 8 桌面运行时、想要更小体积时：

```bash
dotnet publish src/Soundboard.App/Soundboard.App.csproj -c Release -f net8.0-windows -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish-framework
```

### 第一次运行

1. 双击 `Soundboard.exe`
2. 数据目录：`%AppData%\Soundboard\`（`Win + R` 后粘贴该路径）
3. 「添加」→「导入到音板」→「捕获」热键或鼠标侧键
4. 打游戏用 **游戏模式**；日常切歌可改 **桌面模式**
5. 关掉设置窗口后程序仍在托盘。双击图标打开设置；右键有「打开设置」「停止全部声音」「退出」

本机还没有 `config.json` 时：

| 条件 | 写入的内容 |
| --- | --- |
| exe 旁边有 `config.example.json`（`dotnet publish` 会把它拷到输出目录） | 用该示例做种子，并把还能找到的音频导入 `library\` |
| exe 旁边没有示例 | 内置默认：游戏模式、再次按下从头播放、启动后最小化、停止全部 `Ctrl+Shift+F8`、全部静音 `Ctrl+Shift+F9`、槽位为空，其余全局热键未设置 |

[`config.example.json`](config.example.json) 里两个槽位的 `filePath` 指向 `samples/beep.wav`。仓库里没有这份音频，导入自己的文件之前这两个槽播不出来。

若 AppData 里还没有配置、exe 目录里却有旧的 `config.json`（以前把配置放在程序旁边的版本），启动时会复制过去，并尽量把仍存在的音频导入资料库。

命令行 `--minimized` 或 `-minimized` 强制最小化启动。开机启动项写的就是 `"exe路径" --minimized`。

## 界面

| 位置 | 内容 |
| --- | --- |
| 工具栏 | 添加、编辑、删除、试听、停止全部、重置倍速、保存、重新加载 |
| 列表 | 名称、热键、音量、状态、文件。回车编辑，Delete 删除，空格试听 |
| 设置 | 全局音量、全部静音、五组全局热键、热键获取方式、开机启动、启动后最小化、槽位热键行为 |
| 编辑槽位 | 名称、导入到音板、捕获热键、音量、启用、速度变化（起始速度 / 每次变化 / 最高速度 / 停止后重置） |

取消「启用该音效」后，该槽不再观察或注册热键。重复热键会提示冲突，并重新载入磁盘上的配置。

## 热键模式

| 模式 | 配置值 | 键盘 | 鼠标侧键 | 是否抢键 |
| --- | --- | --- | --- | --- |
| **游戏模式**（默认） | `game` | `GetAsyncKeyState` 轮询（间隔 12 ms，按下沿触发，按住不连发） | Raw Input（`RIDEV_INPUTSINK`） | **不抢键**，前台仍能收到同一按键 |
| **桌面模式** | `desktop` | `RegisterHotKey` + `MOD_NOREPEAT`，挂在消息窗口上，托盘里也能收到 | 仍是 Raw Input（`RegisterHotKey` 绑不了侧键） | **键盘组合会被抢走**；侧键不抢 |

- 游戏模式不要绑 WASD、空格或建造键；优先鼠标侧键或不常用组合。
- 桌面模式建议 `Ctrl+Alt+…`。注册结果写在状态栏；失败时弹窗列出原因。
- 切换模式会先卸掉另一条路径，避免双重触发。打开「捕获」时会暂时放开已注册的热键。
- Raw Input 注册失败时，侧键改用 `GetAsyncKeyState` 观察。
- `hotkeyMode` 为空或无法识别时按 `game`。也接受 `desktop` / `game`（不区分大小写）和数字 `0` / `1`。

## 槽位热键行为

`hotkeyBehavior` 是整份配置一份，不是每个槽位各一份。

| 行为 | 配置值 | 说明 |
| --- | --- | --- |
| **再次按下从头播放**（默认） | `restart` | 正在播时再按会从头重播。该槽开了速度变化时，每次重播按步进加快或减速，重播本身不回到起始速度 |
| **播放 / 停止切换** | `toggleStop` | 没在播 → 播放；正在播 → 停止。这次停止不推进速度；下次从停止再播才用下一档（若「停止后重置」没把记忆清掉） |

## 播放速度变化

每个槽位单独开关，默认关闭。关闭时始终 `1.0×`。实现是 NAudio 重采样，**音调会随速率变化**。

| 字段 | 默认 | 含义 |
| --- | --- | --- |
| `enableSpeedRamp` | `false` | 是否启用 |
| `baseSpeed` | `1.0` | 一轮里第一次播放的速率，夹在 `0.25`–`8` |
| `speedStep` | `0.2` | 每次按下的变化量。**可填负数减速**，夹在 `-8`–`8`。编辑页「每次变化」直接填写。步进为 `0` 时不切换方向 |
| `maxSpeed` | `3.0` | 上限，不低于 `baseSpeed`，最高 `8` |
| `resetOnStop` | `true` | 停止、停止全部、播放/停止里的停止、或自然播完之后，下次从 `baseSpeed` 开始。`restart` 重播不会重置 |

实际播放速率不低于 `0.25×`。

「重置倍速」（工具栏或热键）只清空速度记忆，**不停掉正在播的声音**。下次播放从起始速度开始。

## 全局热键

| 动作 | 内置默认 | 效果 |
| --- | --- | --- |
| 停止全部 | `Ctrl+Shift+F8` | 立刻停掉所有正在播放的声音 |
| 全部静音 | `Ctrl+Shift+F9` | 切换静音，并写入配置 |
| 切换停止后重置 | 未设置 | 见下表 |
| 重置倍速 | 未设置 | 清空速度记忆，不停播 |
| 切换加速/减速 | 未设置 | 翻转目标槽位 `speedStep` 的正负号 |

「切换停止后重置」和「切换加速/减速」选槽规则相同，差别只在最后一档：

| 顺序 | 作用对象 |
| --- | --- |
| 1 | 最近一次触发过的启用槽位（不论它是否已开速度变化） |
| 2 | 还没触发过、且设置窗口正开着：当前选中的启用槽位 |
| 3 | 否则：停止后重置作用于全部启用槽位；加速/减速只作用于已开速度变化的启用槽位 |

多个槽位一起改「停止后重置」时：本来全开则一起关掉，否则一起打开。只有一个槽位时直接翻转。加速/减速在步进为 `0` 时跳过该槽。

热键 JSON：`control` / `alt` / `shift` / `win`，加上 `key`。`key` 使用 WinForms `Keys` 名（如 `F1`、`Space`、`D1`）；侧键为 `XButton1`、`XButton2`。侧键也可以带修饰键。

## 数据目录

| 路径 | 用途 |
| --- | --- |
| `%AppData%\Soundboard\config.json` | 槽位、热键、音量、模式 |
| `%AppData%\Soundboard\library\` | 「导入到音板」复制进来的音频。配置里存相对路径 `library/...` |

导入后的文件名是 `{槽位 id}_{原文件名}{扩展名}`。同一槽位再次导入会覆盖；路径变了会删掉资料库里的上一份。可选扩展名：`.mp3` `.wav` `.ogg` `.aiff` `.aif` `.wma` `.flac` `.m4a`。`.ogg` 用 Vorbis，其余走 NAudio `AudioFileReader`（依赖系统解码）。相对路径先在配置目录找，再在 exe 目录找。

## 堡垒之夜 / 反作弊说明

- **推荐无边框窗口**。独占全屏（尤其 EAC）经常让后台收不到键
- 打游戏用 **游戏模式**。桌面模式会抢走键盘组合；独占全屏下后台仍可能收不到键
- 先开音板再进游戏；优先鼠标侧键
- **不保证**任意游戏、任意全屏模式都能收到键
- 本项目不注入游戏、不挂钩游戏进程、不规避反作弊

## 技术说明

| 项目 | 实现 |
| --- | --- |
| 游戏模式键盘 | `GetAsyncKeyState`（观察，不注册系统热键） |
| 桌面模式键盘 | `user32!RegisterHotKey` → `WM_HOTKEY`（消息专用窗口，`MOD_NOREPEAT`） |
| 鼠标侧键 | `RegisterRawInputDevices`（`RIDEV_INPUTSINK`），不吞掉点击 |
| 明确不用 | `WH_KEYBOARD_LL`、`WH_MOUSE_LL`、`SetWindowsHookEx`；不读写其他进程内存，不安装驱动 |
| 音频 | NAudio 2.2.1、NAudio.Vorbis 1.5.0 |
| 开机启动 | 仅当前用户 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `Soundboard` |
| 权限 | 清单 `asInvoker`，不要求管理员 |

## 系统要求

- Windows 10 / 11 x64
- 构建：.NET 8 SDK
- 运行：自包含单文件无需额外安装；依赖框架的构建需要 .NET 8 桌面运行时

GUI 只在 Windows 上运行。工程开启 `EnableWindowsTargeting`，非 Windows 上可以 `dotnet build` / `dotnet test`（测试只编译 `Domain/`）。

## 项目结构

```
src/Soundboard.App/            唯一应用工程，程序集名 Soundboard
  Domain/                      配置、路径、热键模型、速度变化、资料库
                               命名空间仍是 Soundboard.Core
  Ui/  Hotkeys/  Audio/        托盘界面、热键、播放（仅 net8.0-windows 编译）
  Native/  Startup/  Program.cs
  Soundboard.App.csproj        TargetFrameworks: net8.0; net8.0-windows
tests/Soundboard.Core.Tests/   xUnit，引用 App 的 net8.0 输出
config.example.json            示例配置（仓库里没有对应的音频文件）
Soundboard.sln
```

领域类型在 `src/Soundboard.App/Domain/`，命名空间仍是 `Soundboard.Core`。测试工程名仍是 `Soundboard.Core.Tests`。解决方案里只有这两个工程：没有独立的 Core 工程，根目录也没有 `samples/` 或 `发布说明.txt`。`net8.0` 目标只编译 `Domain/`，配置 JSON 形状与这些类型一致。

## 许可与免责

本项目采用 [MIT License](LICENSE)。

请只加载你有权使用的音频。本软件按「现状」提供，与任何游戏或反作弊厂商均无合作或对抗关系。使用全局热键 / Raw Input 类工具的风险由你自行判断。
