# 音板（Soundboard）

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue)](#要求)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

独立运行的 **Windows 托盘音板**：为自定义音频绑定全局热键或鼠标侧键，打游戏、办公、直播时一键触发。界面为简体中文。

> 普通用户态桌面程序：**不注入游戏**、不读写其他进程内存、不安装驱动、不使用 `WH_*` 低级钩子。

## 功能一览

- 系统托盘常驻，可最小化启动
- 多音效槽位：名称、热键、音量、导入到本机资料库的音频
- 配置与音频默认在 `%AppData%\Soundboard\`（与 exe 分离，换路径不丢）
- **游戏模式 / 桌面模式** 热键获取方式可切换
- 每槽播放速度变化：步进可为正（加速）或负（减速）；可热键切换方向
- 全局热键：停止全部、全部静音、切换「停止后重置」、重置倍速、切换加速/减速
- 可选：当前用户开机启动
- 推荐产物：自包含 **单文件** `Soundboard.exe`

## 快速开始

### 直接使用（推荐）

1. 在 [Releases](../../releases) 下载 `Soundboard.exe`（或自行按下方命令发布）
2. 双击运行；首次会在 `%AppData%\Soundboard\` 写入配置
3. 「添加」→「导入到音板」→「捕获」热键或鼠标侧键
4. 打游戏请用 **游戏模式**；日常切歌可改 **桌面模式**

数据目录快捷打开：`Win + R` → 输入 `%AppData%\Soundboard` → 回车。

### 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```bash
dotnet restore Soundboard.sln
dotnet build Soundboard.sln -c Release
dotnet test tests/Soundboard.Core.Tests/Soundboard.Core.Tests.csproj -c Release
```

### 发布单文件 exe

```bash
dotnet publish src/Soundboard.App/Soundboard.App.csproj ^
  -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o ./publish
```

产物：`publish/Soundboard.exe`。详见 [`发布说明.txt`](发布说明.txt)。

## 热键模式

| 模式 | 配置值 | 键盘 | 鼠标侧键 | 是否抢键 |
| --- | --- | --- | --- | --- |
| **游戏模式**（默认） | `game` | `GetAsyncKeyState` 轮询观察 | Raw Input | **不抢键**，游戏仍能收到同一按键 |
| **桌面模式** | `desktop` | `RegisterHotKey` + `MOD_NOREPEAT` | Raw Input | **会抢走该组合键**（类似 QQ 音乐全局热键） |

- 游戏模式：不要绑 WASD / 空格 / 建造键；推荐**鼠标侧键**或不常用组合。
- 桌面模式：建议 `Ctrl+Alt+…`；热键挂在消息窗口上，托盘里也能收到；注册成败会在状态栏/弹窗提示。
- 切换模式会立刻切换实现路径，避免双重触发。

## 槽位热键行为

| 行为 | 说明 |
| --- | --- |
| **再次按下从头播放**（默认） | 正在播时再按会从头重播；开启速度变化时按步进累加 |
| **播放 / 停止切换** | 没在播 → 播放；正在播 → 停止 |

## 播放速度变化

每个槽位可单独开启（默认关闭）：

| 字段 | 默认 | 含义 |
| --- | --- | --- |
| `enableSpeedRamp` | `false` | 是否启用 |
| `baseSpeed` | `1.0` | 一轮中首次播放速率 |
| `speedStep` | `0.2` | 每次按下变化量；**可填负数减速** |
| `maxSpeed` | `3.0` | 上限 |
| `resetOnStop` | `true` | 停止 / 停止全部 / 自然结束后回到 `baseSpeed`；再次重播不会重置 |

编辑页「每次变化」可直接填负数，无需另开页面。可用全局热键「切换加速/减速」翻转步进正负号。

实现为 NAudio 重采样，**音调会随速率变化**。

## 全局热键一览

| 动作 | 效果 |
| --- | --- |
| 停止全部 | 立刻停掉所有正在播放的声音 |
| 全部静音 | 切换静音 |
| 切换停止后重置 | 切换「停止后重置回起始速度」 |
| 重置倍速 | 清空速度变化记忆，不停播 |
| 切换加速/减速 | 翻转 `speedStep` 正负号 |

## 数据目录

| 路径 | 用途 |
| --- | --- |
| `%AppData%\Soundboard\config.json` | 槽位、热键、音量等 |
| `%AppData%\Soundboard\library\` | 「导入到音板」复制的音频 |

示例配置见 [`config.example.json`](config.example.json)。

## 堡垒之夜 / 反作弊说明

- **推荐无边框窗口**，独占全屏（尤其 EAC）经常导致后台收不到键
- 打游戏用 **游戏模式**；桌面模式不会「魔法修好」独占全屏，反而会抢键
- 先开音板再进游戏；优先鼠标侧键
- **不保证**任意游戏 / 任意全屏模式可用
- 请勿要求本项目去注入游戏、挂钩游戏进程或规避反作弊

更细的安全边界见下文「技术说明」。

## 技术说明

- 游戏模式键盘：`GetAsyncKeyState`（观察，不注册系统热键）
- 桌面模式键盘：`user32!RegisterHotKey` → `WM_HOTKEY`（消息专用窗口）
- 鼠标侧键：`RegisterRawInputDevices`（`RIDEV_INPUTSINK`）
- **不使用** `WH_KEYBOARD_LL` / `WH_MOUSE_LL` / `SetWindowsHookEx`
- 音频：NAudio + 系统音频 API
- 开机启动：仅当前用户 `HKCU\...\Run`，清单 `asInvoker`

## 系统要求

- Windows 10 / 11 x64
- 构建：.NET 8 SDK
- 运行：自包含单文件无需额外安装；或安装 .NET 8 桌面运行时后使用依赖框架的构建

无法在 Linux / macOS 上运行 GUI；工程开启 `EnableWindowsTargeting`，可在非 Windows 上 `dotnet build` / `dotnet test`。

## 项目结构

```
src/Soundboard.Core/          配置、路径、速度变化、资料库（net8.0）
src/Soundboard.App/           WinForms 托盘程序（net8.0-windows）
tests/Soundboard.Core.Tests/  单元测试
config.example.json           示例配置
发布说明.txt                  发布命令备忘
```

## 许可与免责

本项目采用 [MIT License](LICENSE)。

请只加载你有权使用的音频。本软件按「现状」提供，与任何游戏或反作弊厂商均无合作或对抗关系。使用全局热键 / Raw Input 类工具的风险由你自行判断。
