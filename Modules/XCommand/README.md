# XFramework 运行时控制台 (`XCommand`)

> `XCommand` 是 XFramework 的运行时 / 编辑器命令行基础设施。同一套命令注册与执行链路，向上服务 **游戏内 UGUI 控制台、编辑器窗口、C# API、外部 CLI、XHunter 网页控制台** 五种入口，向下提供命令发现、参数解析、执行记录、日志查询、UGUI / GameObject 自动化操作与截图能力。
>
> 本文档聚焦命令注册、执行链路、CLI 协议与自动化管道，适合作为编写调试命令和接入外部自动化时的专题说明。

- 核心源码目录：[`Modules/XCommand/`](./)
- CLI 客户端脚本：[`Modules/XCommand/Tools/cli/xcli.ps1`](./Tools/cli/xcli.ps1)
- 框架总览：[`XFramework README`](../../README.md)

---

## 1. 能力总览

- **声明式命令注册**：静态方法标记 `[XCommandEntry]`，所在类继承 `XCommandGroup`；Editor 下由 `TypeCache` 自动发现，Player 下必须在 `AfterAssembliesLoaded` 时显式 `RegisterGroup`。
- **统一执行链路**：所有入口最终都走 `XCommandHub.Execute(...)`，产出统一的 `XCommandExecutionResult`。
- **执行记录与历史**：环形记录（上限 2000 条）+ 命令历史（上限 50 条），编辑器下可跨脚本重载保留；`history` / `history-text` / `history-input` 支持分页读取。
- **来源可追溯**：`Api` / `Editor` / `Ugui` / `Cli` / `Web` 五种来源会被写入记录；`Hunter` 枚举值仅为历史记录兼容保留。
- **异步操作管道**：UI 操作、条件等待、输入注入、截图和 Editor 重新编译 / Play Mode 切换以 operation 形式排队执行，返回 operation ID，可查询 / 取消 / 超时。
- **运行时日志缓冲**：50MB 环形缓冲，按游标增量查询，支持级别、关键字与堆栈筛选。
- **外部进程自动化**：Editor 与 Development Player 自动启动 TCP Server，并通过本机注册文件与 UDP 48733 局域网广播暴露实例；配合 `xcli.ps1` 或 XHunter 让脚本 / CI / AI 直接驱动 Unity。

---

## 2. 模块结构

| 类 / 文件 | 职责 |
| :-------- | :--- |
| `XCommand`（`Runtime/Core/Command/XCommand.cs`） | 终端门面；`IConsole` 接口、`Message` 消息与着色、执行器（Command Key）管理 |
| `XCommandHub`（`Runtime/Core/Command/XCommandHub.cs`） | 执行记录、命令历史与版本号、来源标记、显示清空 / 历史请求、运行时重置事件 |
| `XCommandRegistry`（`Runtime/Core/Command/XCommandRegistry.cs`） | 命令发现与注册（`RegisterGroup`、Editor `TypeCache`、动态注册）、可用性谓词、执行内核、诊断收集 |
| `XCommandCoreCommands`（`Runtime/Core/Command/XCommandCoreCommands.cs`） | `operation-list` / `operation-status` / `operation-cancel` 核心命令组 |
| `XCommandPipeline`（`Runtime/Core/AsyncOperation/XCommandPipeline.cs`） | 协程 / `EditorApplication.update` 驱动的异步操作队列；`RunOperation(IXCommandAsyncOperation)` 统一入口，支持 `CanCancel`、operation 恢复与 EditMode 执行 |
| `Runtime/Core/Wait/*` | `IXCommandWaitRequest` 契约、`XCommandWaitRequest` 与 `WaitOperation` |
| `Runtime/Commands/Wait/*` | `wait-for-ui` / `wait-for-go` / `wait-for-log` 的请求实现 |
| `Runtime/Commands/Screenshot/*`、`Runtime/Commands/UI/*`、`Runtime/Commands/Recording/*` | 截图、UGUI 操作 / 输入注入、离屏录制的 operation 实现 |
| `XCommandArguments`（`Runtime/Core/Command/XCommandArguments.cs`） | 命令行参数解析（值与标记选项、引号、位置参数） |
| `XCommandUI` / `XCommandUtility.GameObjects` | UGUI 与 GameObject 的查询、操作与稳定判定 |
| `XCommandUtility.*`（`Runtime/Utility/`） | 无状态工具集合（`PrefabPreview` / `FontBake` / `ScreenRecord` / `GameViewCapture` / `GameViewSize` / `OffscreenRecord` 为编辑器专用，用 `#if UNITY_EDITOR` 包裹） |
| `UGUIConsole`（`Runtime/UGUIConsole.cs`） | 游戏内 UGUI 控制台实现 |
| `XFrameworkCliServer` / `XFrameworkLanDiscovery` / `XFrameworkCliOperation` / `XCommandCliOperationProvider`（`Runtime/Core/CLI/`） | 对外 CLI Server、局域网发现、operation 数据契约与桥接（含内部 `wait-operation`、历史分页） |
| `XCommandEditorWindow`（`Editor/`） | 编辑器终端窗口 + 命令手册 + CLI 状态菜单 |
| `XCommandEditorHistory`（`Editor/`） | 编辑器侧记录 / 历史持久化 |
| `Editor/XCommandEditorCommands.cs` | 编辑器与 PlayMode 命令**入口**（薄入口） |
| `Editor/XCommandCompileOperation.cs` | `recompile` 异步 operation；跨 Domain Reload 恢复并收集编译诊断 |
| `Editor/XCommandPlayModeOperation.cs` | `play-start` / `play-stop` 异步 operation |
| `Editor/XCommandEditorWindowCapture.cs` | `editor-window-list` / `editor-window-screenshot` 的窗口枚举与离屏截图实现 |
| `Runtime/Utility/XCommandUtility.Performance.cs` / `Runtime/XCommandPerformanceSampler.cs` | 帧性能采样（`perf-*`）的参数解析与逐帧采样宿主 |
| `Editor/XCommandProfiler.cs` | Profiler 采样控制、帧标记与最差帧报告（`prof-*`） |
| `Tools/cli/xcli.ps1` | 外部命令行客户端（含 `wait-server`、Domain Reload 重连） |
| `C:\Projects\XDEDZL\XHunter` | 仓库外的独立网页网关与网页控制台，通过 CLI 协议接入，不在本模块内 |

---

## 3. 命令注册与执行

### 3.1 声明一个命令（推荐方式）

```csharp
using System;
using XFramework.Command;

public sealed class MyGameCommands : XCommandGroup
{
    [XCommandEntry("hero-set-level", name = "设置英雄等级", order = 10,
        mode = XCommandMode.Runtime, category = "Game",
        description = "把指定英雄的等级设置为指定值。",
        usage = "hero-set-level <name> <level>")]
    private static object SetHeroLevel(string argument)
    {
        XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), Array.Empty<string>());
        args.RequireValueCount(2, "usage: hero-set-level <name> <level>");

        string heroName = args.Values[0];
        int level = int.Parse(args.Values[1]);
        // ... 业务逻辑 ...
        return $"hero {heroName} -> level {level}";
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void RegisterCommandGroup()
    {
        XCommandRegistry.RegisterGroup(typeof(MyGameCommands));
    }
}
```

约定：

- 类必须继承 `XCommandGroup`（`public abstract class XCommandGroup`）。Editor 下由 `TypeCache.GetTypesDerivedFrom<XCommandGroup>()` 自动发现；Player 下只加载显式 `RegisterGroup` 的类型，未注册的命令组在 Player 中静默消失。
- 方法必须是 **非泛型静态方法**，参数只能是 **0 个或 1 个 `string`**（整行参数原文）。签名不合法时不会注册，而是产生一条诊断。
- 返回值即执行输出：`null` 会记录为 `OK`，其他对象会 `ToString()` 后写入记录 / 输出。
- 允许 `private static`，便于把调试命令封装在模块内部。

### 3.2 `[XCommandEntry]` 字段说明

| 字段 | 类型 | 默认值 | 说明 |
| :--- | :--- | :----- | :--- |
| `cmd` | `string` | 方法名 | 命令名，即命令行第一个空格之前的部分 |
| `name` | `string` | 同 `cmd` | 面向人的显示名 |
| `order` | `int` | `-1` | 同分类内排序，负数排在最后 |
| `mode` | `XCommandMode` | `Runtime` | 决定命令在哪个环境可用 |
| `category` | `string` | 声明类名 | 命令分组，编辑器窗口与 CLI 按此排序 |
| `description` | `string` | 空 | 说明文本，会通过 CLI `commands` 返回 |
| `usage` | `string` | 自动生成 | 用法串，默认 `cmd <args>`（有参数时） |
| `requireConfirmation` | `bool` | `false` | 为 `true` 时 CLI 执行必须带 `--confirm` |
| `recordExecution` | `bool` | `true` | 是否写入 Hub 记录与命令历史 |
| `displayType` | `XCommandDisplayType` | `Visible` | 设为 `Hidden` 后默认不出现在 CLI `commands` 结果中；请求 `includeHidden=true` 时返回全量（仍可执行） |

### 3.3 可用性判定（`XCommandMode`）

| Mode | 可用条件 |
| :--- | :------- |
| `Runtime` | `Application.isPlaying` |
| `Editor` | `!Application.isPlaying && Application.isEditor` |
| `Both` | 始终可用 |

不可用时执行结果为 `Unavailable`，并附带当前环境描述。

`XCommandRegistry.SetAvailabilityPredicate(predicate)` 可在不重新扫描命令方法的情况下按谓词过滤命令，配合 `RefreshAvailability()` 生效；谓词返回 `false` 的命令不会出现在 `commands` 中，也无法执行。

### 3.4 动态注册

```csharp
XCommandRegistry.AddCommand("my-dynamic", argument => $"received: {argument}", XCommandMode.Runtime);
```

动态命令与特性命令重名时，**保留特性命令** 并产生诊断（`Debug.LogWarning`）。

### 3.5 诊断信息

`XCommandRegistry.Diagnostics` 会列出所有注册期问题（命令重复、签名不合法、动态命令冲突等）。可通过 CLI `diagnostics` 或编辑器窗口的「诊断」折叠区查看，`AddDiagnostic` 同时会输出 `Debug.LogWarning`。

### 3.6 参数解析约定

`XCommandArguments.Parse(commandLine, valueOptions, flagOptions)` 的规则：

- 支持 `--name value` 与 `--name=value` 两种写法。
- `flagOptions` 中的选项不接受值，写成 `--flag=value` 会抛异常。
- 不以 `--` 开头的 token 会进入 `XCommandArguments.Values`（位置参数）。
- 支持单引号 / 双引号包裹，引号内可用 `\` 转义引号与反斜杠；未闭合的引号会报错。
- **未知选项直接抛 `ArgumentException`**，不会静默忽略。
- 读取值：`GetString` / `GetInt` / `GetLong` / `GetFloat` / `HasFlag`；`RequireValueCount(n, error)` 用于校验位置参数个数。

---

## 4. 命令清单

> 命令入口按程序集集中：Runtime 侧位于 `Runtime/Commands/XCommandCommands.cs`（`XCommandCommands`）与 `Runtime/Core/Command/XCommandCoreCommands.cs`（operation 核心命令），Editor 侧全部位于 `Editor/XCommandEditorCommands.cs`（`XCommandEditorCommands`）；各命令通过类内 `#region` 按功能分类。
>
> 复杂命令只保留「入口」：`[XCommandEntry]` 方法是一行委托，参数解析与实现位于 `Runtime/Utility/XCommandUtility.{FontBake,ScreenRecord,GameViewCapture}.cs`（`XCommandUtility` 的嵌套 `internal static` 类，`#if UNITY_EDITOR` 包裹）。

### 4.1 内置命令（`XCommandCommands`，category = `XCommand`）

| 命令 | 显示名 | Mode | 说明 |
| :--- | :----- | :--- | :--- |
| `clear` | 清空 XCommand | Both | 清空当前终端显示内容，不删除 Hub 历史（不记录执行） |
| `load_history` | 加载 XCommand 历史 | Both | 在当前终端重新加载 Hub 历史（不记录执行） |
| `help` | 查看 XCommand 帮助 | Both | 查看全部命令分类，或查看指定分类下的全部命令（不记录执行） |
| `log` | 输出 Unity 日志 | Both | 向 Unity Console 输出一条日志 |

> 以上内置命令均为 `displayType = Hidden`：默认不出现在 CLI `commands` 结果中，但 `help` 仍会展示，`exec` 仍可直接执行。

### 4.2 运行时与自动化命令（`XCommandCommands`）

| 命令 | Mode | 用途 |
| :--- | :--- | :--- |
| `play-stop` | Runtime | Editor 中异步退出 Play Mode 并等待 `EnteredEditMode`；Development Player 中返回退出请求回执 |
| `go-list` | Both | 列出 GameObject，支持 `name` / `path` / `component` / `tag` / `layer` / `active` 筛选 |
| `go-state` | Both | 按 `instance-id` / `name` / `path` / `indexed-path` / `component` 查询状态，默认要求唯一匹配 |
| `ui-list` | Runtime | 列出射线可达且存在事件处理器的 UGUI 元素，支持采样与增量快照 |
| `ui-act` | Runtime | 重新定位唯一稳定元素并注入 `click` / `down` / `up` / `scroll`，返回 operation ID |
| `wait-for-ui` | Runtime | 等待 UGUI 出现、消失或内容稳定，返回 operation ID |
| `wait-for-go` | Runtime | 等待 GameObject 出现、消失、激活、失活或变换稳定，返回 operation ID |
| `wait-for-log` | Runtime | 等待日志条数满足条件，返回 operation ID |
| `wait` | Runtime | 按真实时间 / 游戏时间 / 帧数等待，返回 operation ID |
| `logs` | Both | 查询 50MB 环形日志缓冲，支持级别、正文、条数与堆栈筛选 |
| `input-state` / `input-reset` | Both / Runtime | 查询或重置自动化键盘、鼠标、触摸保持状态 |
| `ui-input` | Runtime | 通过 Input System 注入键鼠 / 多点触控 JSON 序列；**注入前建议先执行 `window-focus`**（见 4.3），把 Unity / GameView 提到前台 |
| `screenshot` | Runtime | 帧末截图，支持输出尺寸、屏幕区域与仅 UI 模式 |
| `scene-state` | Both | 当前已加载场景、活动场景、Dirty 状态与根物体摘要 |
| `app-state` | Both | 应用、时间、屏幕、设备与常用路径状态 |

> `operation-list` / `operation-status` / `operation-cancel` 已移到 `XCommandCoreCommands`（`Runtime/Core/Command/XCommandCoreCommands.cs`），行为不变；它们仍为 `displayType = Hidden`。

> **模拟输入前先前置窗口**：`ui-input` 注入的是 Input System 层的设备事件，但真实点击能否被 GameView 接收还取决于窗口是否在前台。建议在任何 `ui-input` 之前先执行一次 `window-focus`（见 4.3），并用 `app-state.isFocused` 复核；否则可能出现“命令返回 OK 但点击被吞、对象无变化”的现象。

### 4.3 编辑器命令（`XCommandEditorCommands`）

| 命令 | Mode | 说明 |
| :--- | :--- | :--- |
| `editor_selection` | Editor | 返回当前选择对象的名称、类型与资源路径 |
| `editor-menu` | Both | 按完整路径执行 Unity Editor 菜单命令，例如 `XFramework/Tools/XCommand`；菜单不存在、被禁用或执行失败时返回错误 |
| `recompile` | Editor | 异步刷新 AssetDatabase 并重新编译脚本；CLI 默认等待编译成功或失败，operation 不可取消 |
| `play-start` | Editor | 异步进入 Play Mode，等待 `EnteredPlayMode` 和 CLI Server 恢复；operation 不可取消 |
| `preview-prefab` | Editor | 在临时场景离屏渲染 UI / 模型 Prefab 并导出 PNG，完成后恢复活动场景 |
| `gameview-screenshot` | Editor | 非 Play 模式下把当前场景视图相机离屏渲染成 PNG；仅含相机渲染内容，Screen Space Overlay 的 UI 不含在内 |
| `gameview-size` | Editor | 调整 GameView 渲染尺寸：`--maximize` / `--restore`，或 `--width/--height` 切换固定分辨率 |
| `font_bake` | Editor | 扫描 TMP 文本用字并补齐字体图集缺字，默认 dry-run，加 `--apply` 才写入 |
| `record` | Both | 调用 ffmpeg 录制屏幕指定秒数后自动停止并输出 MP4，默认录 GameView 区域 |
| `record-start` / `record-stop` | Both | 开始 / 停止录屏，用于录不确定时长的过程（可在 Play 模式中录制游戏画面） |
| `record-offscreen` | Both | 离屏逐帧录制 GameView 并输出 MP4，用 Unity 内置 `MediaEncoder`；需要 Play 模式，返回 operation ID |
| `record-offscreen-stop` | Both | 请求提前结束当前离屏录制；编码器在下一帧收尾并写出可播放的分片 MP4 |
| `record-offscreen-status` | Both | 返回最近一次 / 当前离屏录制的状态、已录帧数、编码器与最终文件信息 |
| `editor-window-list` | Editor | 列出当前已打开的 EditorWindow 标题、类型、聚焦和停靠状态，支持 `--query` 过滤 |
| `editor-window-screenshot` | Editor | 从指定 EditorWindow 的 GUIView 渲染面截取内容区并保存为 PNG；不依赖桌面可见性，不含 Dock 标签栏与标题栏 |
| `window-focus` | Both | 把 Unity Editor 主窗口恢复到前台并聚焦 GameView（Windows）；`ui-input` 注入前建议先执行。返回 `broughtToFront` / `gameViewFocused` / `isFocused` 供判定 |

> 上表中 `editor_selection` / `recompile` / `preview-prefab` / `record*` / `gameview-screenshot` / `window-focus` / `font_bake` / `gameview-size` 为 `displayType = Hidden`，默认不出现在 CLI `commands` 结果中；`editor-menu` / `editor-window-list` / `editor-window-screenshot` / `play-start` 保持可见，`play-stop` 也已恢复可见。
>
> `recompile` / `play-start` / `play-stop` 的实现分别位于 `Editor/XCommandCompileOperation.cs` 与 `Editor/XCommandPlayModeOperation.cs`；`editor-window-*` 的实现位于 `Editor/XCommandEditorWindowCapture.cs`；`font_bake` / `record*` / `gameview-*` 的参数解析与实现仍位于 `Runtime/Utility/XCommandUtility.*.cs`。

> `window-focus` 定义在编辑器程序集内，因此 Edit 模式与 Play 模式都可用（`Mode = Both`），但生成的 Player 里不会注册；Windows 前台锁可能拒绝抢占，失败时以返回值为准，必要时人工点一下 Game 窗口。

> 字体烘焙的无头批处理入口 `-executeMethod XFramework.Editor.XCommandEditorCommands.BakeFontAtlasInBatchMode` 保持不变。

> [!IMPORTANT]
> `record` / `record-start` 依赖外部 **ffmpeg**。使用前请先安装，未安装时命令会返回安装提示：
>
> ```powershell
> winget install --id Gyan.FFmpeg -e
> ```
>
> 安装后若命令仍提示找不到 ffmpeg，重启一次 Unity 让新的 PATH 生效（命令也会自动到 WinGet 包目录里兜底查找）。
> 另外 gdigrab 采集的是屏幕像素，录制期间需保持 Unity 窗口可见、不要最小化或被遮挡。桌面锁定（锁屏）时 gdigrab 只能抓到锁屏，这种场景请改用下面的 `record-offscreen`。

> [!TIP]
> `record-offscreen` 录的是 Unity 内部渲染帧，不走屏幕抓取，所以桌面锁屏也能录，且帧率稳定（实测 RTX 5080 上 2054x1156@30fps 实时录制约 29.8fps）。用法：
>
> ```text
> record-offscreen PATH [--fps N] [--duration S] [--frames N] [--width W --height H] [--frame-step] [--no-fallback]
> ```
>
> - 必须在 Play 模式下运行，且 GameView 正在渲染；输出路径必须以 `.mp4` 结尾。
> - 默认实时录制（30fps / 10 秒，按目标帧率节流），输入时序与真实时间一致。
> - 加 `--frame-step` 改用确定性帧步进（每帧固定推进 1/fps 游戏时间），视频恒定帧率且录制通常快于实时，但输入时序会与墙上时间脱钩。
> - 默认优先用 Unity 内置 `MediaEncoder`（平台硬件编码，不含音频）；创建失败时自动回退到 ffmpeg rawvideo 管道，可用 `--no-fallback` 禁止回退。
> - 返回 operation ID，用 `operation-status ID` 查结果，`operation-cancel ID` 可提前结束录制（已写入的分片仍可播放）。

### 4.4 性能分析命令（`Perf`）

| 命令 | Mode | 说明 |
| :--- | :--- | :--- |
| `perf-start` | Runtime | 开始逐帧采样帧间隔、GC、FrameTiming 与 Profiler 标记；支持 `--threshold-ms` / `--label` / `--timeline` / `--duration-seconds` / `--markers NAME1,NAME2` / `--track-tag TAG` |
| `perf-status` | Runtime | 查询采样会话的样本数、已采样时长与超阈值帧数 |
| `perf-stop` | Runtime | 结束采样并返回单行 JSON 报告（帧耗时分位、直方图、尖峰、GC、FrameTiming、标记与最差帧） |
| `prof-on` / `prof-off` | Both | 开启 / 停止 `ProfilerDriver` 采集；`prof-on` 支持 `--deep` / `--editor` / `--reset` |
| `prof-mark` | Both | 记录当前 Profiler 帧号，执行两次后 `prof-report` 默认只分析两点之间的帧 |
| `prof-report` | Both | 按自身耗时或 GC 分配列出最差帧与热点函数，支持调用栈、合并同名样本与隐藏编辑器样本 |

> 以上命令均为 `displayType = Hidden`。运行时采样默认只采集引擎级 Profiler 标记，项目自定义标记用 `--markers` 传入；位置追踪需显式指定 `--track-tag`。

> [!TIP]
> 典型流程：`prof-on --reset` 后运行游戏，`prof-mark` 圈定区间，`prof-off` 后 `prof-report` 看最差帧与热点函数；需要逐帧曲线与 GC / FrameTiming 统计时用 `perf-start` / `perf-stop`。

### 4.5 其他模块贡献的命令

各功能模块可以自带命令组；这些命令组必须在自身类型内声明 `RegisterCommandGroup`（`[RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)]` + `XCommandRegistry.RegisterGroup`），否则 Player 下不会注册：

| 命令 | 来源 | 说明 |
| :--- | :--- | :--- |
| `fsm_list` | `Runtime/Modules/FSM/FsmConsoleCommands.cs` | 输出当前所有活动 FSM 的文本快照 |
| `procedure-switch` | `Runtime/Base/Procedure/ProcedureConsoleCommands.cs` | 按类型名或完整类型名切换唯一的 `MainProcedure` |
| `ui-open` / `ui-close` | `Runtime/Modules/UI/Core/UIConsoleCommands.cs` | 按 `PanelInfo` 名称打开 / 关闭面板 |

---

## 5. 执行入口

### 5.1 游戏内 UGUI 控制台

- **打开方式**：`XGame.OnGUI` 左下角的「调试」按钮切换 `XCommand.IsOpen`（首次打开时才初始化控制台）。
- **界面组成**：日志区（富文本着色，前缀与颜色由 `Message` 决定）、命令输入框、执行器下拉（`XCommand.CommandKeys`）、「历史」按钮（加载 Hub 历史）、「异常:开 / 关」按钮（异常详情）。
- **历史导航**：`XCommand.JumpToPreviousCmd()` / `JumpToNextCmd()`。
- **消息着色**：`Message` 结构定义 `NORMAL` / `WARNING` / `ERROR` / `SYSTEM` / `INPUT` / `OUTPUT` / `UNITY` 七类，通过 `XCommand.Log*` 系列方法写入。

### 5.2 编辑器窗口

菜单：**`XFramework/Tools/XCommand`**

- **终端面板**：`Enter` 执行、`Tab` 预测补全、`↑↓` 候选 / 历史、`Esc` 关闭 / 清空；支持「自动滚动」「复制全部」「清空输出」「刷新命令」「显示隐藏命令」。
- **命令手册（Command Reference）**：搜索、模式筛选（全部 / 当前可用 / Runtime / Editor / Both）、分类筛选、仅收藏、双击填入命令、查看用法与声明信息；底部为诊断折叠区。
- **CLI 菜单按钮**：查看 Server 运行状态与端口，启动 / 停止 / 重启 Server，复制连接参数。
- 窗口默认只显示新命令记录，点「加载历史」才展示共享记录。

### 5.3 C# API

```csharp
using XFramework.Command;

// 简化调用：内部使用当前执行器（默认 Auto），返回是否成功
bool ok = XCommand.Execute("app-state");
XCommand.Execute("ui-list --compact", out object value);

// 带来源与详细结果
XCommandExecutionResult result = XCommandHub.Execute("screenshot temp:/shot.png", XCommandSource.Api);
// 等价写法：XCommandRegistry.ExecuteDetailed("screenshot temp:/shot.png");
```

`XCommandExecutionResult` 字段：`Status`（`Succeeded` / `NotFound` / `Unavailable` / `Failed`）、`CommandLine`、`Command`（`XCommandDescriptor`）、`Value`、`Exception`、`DurationMilliseconds`、`Message`。

### 5.4 执行器（Command Key，历史机制）

除新注册表外，`XCommand` 仍保留旧的执行器表：`AddCommand(key, CommandDelegate, registerAuto)` 注册、`ChangeCommand(key)` 切换、`CurrentCommandKey` 查询。内置两个 key：

- `Command`：只走新注册表。
- `Auto`：先查新注册表，未命中再按注册顺序尝试标记为 auto 的旧执行器。

自定义执行器主要用于兼容与兜底，**新功能请直接使用 `[XCommandEntry]` 声明**。

### 5.5 XHunter 网页控制台

旧的 UDP Hunter 客户端已删除，远程调试统一走独立工具 **XHunter**：

- 目录：`C:\Projects\XDEDZL\XHunter`（仓库外，不随 XFramework 包分发）。
- 启动：双击 `run.bat`，网关监听 `127.0.0.1:5182`，浏览器打开 XHunter 页面；`stop.bat` 关闭网关与专属浏览器窗口。
- 依赖：系统 Python 3.9+（PATH 中的 `py.exe -3` 或 `python.exe`），只使用标准库。
- 接入协议：与 `xcli.ps1` 相同的 CLI TCP 协议（`protocolVersion = 1`），通过本机注册文件、UDP 48733 广播和手动 IPv4:端口发现 Unity 目标。
- 详细说明见 `C:\Projects\XDEDZL\XHunter\README.md`。

---

## 6. 对外 CLI（`xcli.ps1`）

### 6.1 构成与生命周期

- **启动时机**：Editor 侧由 `XFrameworkCliServerBootstrap`（`[InitializeOnLoad]`）自动启动，进程类型为 `Editor`；Development Player 侧由 `XFrameworkCliPlayerBootstrap` 在 `DEVELOPMENT_BUILD` 下自动启动，进程类型为 `DevelopmentPlayer`。
- **监听方式**：`TcpListener` 绑定 `IPAddress.Any` 的随机端口，允许同一网段的主机连接；**只应在可信内网使用，命令没有鉴权**。
- **实例发现**：启动后生成 `instanceId`（Editor 用 `SessionState` 跨 Domain Reload 保留），Editor / Standalone 把自身信息写入 `%LocalAppData%/XFrameworkCLI/servers/<pid>-<port>.json`；同时每 2 秒向 UDP `48733` 广播 `{"type":"XFrameworkDiscovery","discoveryVersion":1,"instanceId","pid","port"}`。客户端以「进程存在」判定注册文件是否有效，不再校验进程启动时间。
- **请求处理**：Editor / Player 每帧调用 `XFrameworkCliServer.Pump()`，在 **Unity 主线程** 处理请求；`ping` 在 TCP 工作线程直接回包，读取缓存的 Server 快照，不占用主线程。
- **报文格式**：请求与响应都是单行 JSON，必须以换行结尾。请求体上限 4MB。

### 6.2 客户端命令

```
xcli.ps1 help
xcli.ps1 servers [--pretty]
xcli.ps1 ping        [--pid PID|--port PORT] [--target editor|player] [--pretty]
xcli.ps1 wait-server [--pid PID|--port PORT] [--target editor|player] [--timeout SECONDS] [--pretty]
xcli.ps1 commands    [--available] [--pid PID|--port PORT] [--target editor|player] [--pretty]
xcli.ps1 diagnostics [--pid PID|--port PORT] [--target editor|player] [--pretty]
xcli.ps1 exec '<完整命令行>' [--no-wait] [--timeout SECONDS] [--confirm] [--pid PID|--port PORT] [--target editor|player] [--pretty]
```

选项：

| 选项 | 说明 |
| :--- | :--- |
| `--pid N` | 指定目标进程 ID |
| `--port N` | 指定端口，跳过注册文件发现流程 |
| `--target editor\|player` | 按进程类型筛选 Server |
| `--timeout S` | 等待异步 operation 的秒数，默认 60；`recompile`、`play-start`、`play-stop` 默认 300 |
| `--no-wait` | 命令返回 operation 后立即返回，不等待完成 |
| `--confirm` | 对 `requireConfirmation` 命令确认执行 |
| `--available` | `commands` 只列出当前环境可执行的命令 |
| `--pretty` | 美化 JSON 输出 |

`wait-server` 会持续等待目标 Server 恢复：`--pid` 指定进程且进程退出时返回 `ProcessExited`，超时返回 `124`；适合在启动 Unity 后阻塞等待 CLI 就绪。

Server 选择规则：指定 `--port` 直接使用；否则先按 `--target` 过滤，再按 `--pid` 精确匹配；剩下的候选中优先选「当前工作目录位于其项目路径内」的那个，唯一命中即使用，多个候选或零候选会直接报错提示。

### 6.3 退出码

| 退出码 | 含义 |
| :----- | :--- |
| `0` | 成功 |
| `1` | 命令执行失败、需要确认，或 operation 失败 / 取消 |
| `2` | 参数错误（未知命令、缺少参数、值不合法） |
| `3` | 找不到匹配的 Server、匹配到多个，或 `wait-server` 的进程不存在 / 提前退出 |
| `4` | 传输 / 协议错误（连接超时、报文非法、Server 已停止） |
| `124` | 等待 operation 或 `wait-server` 等待 Server 恢复超时（**操作本身不会被取消**） |

### 6.4 典型示例

```powershell
$cli = '.\UnityClient\Packages\com.xdedzl.xframework\Modules\XCommand\Tools\cli\xcli.ps1'

& $cli exec 'recompile' --target editor --timeout 300
& $cli exec 'play-start'
& $cli exec 'editor-window-list --query Console'
& $cli exec 'editor-window-screenshot temp:/window.png --window Console'
& $cli exec 'editor-menu XFramework/Tools/XCommand'
& $cli exec 'ui-list --compact'
& $cli exec 'ui-act --name StartButton click'
& $cli exec 'screenshot temp:/xcommand.png'
& $cli exec 'record temp:/fight.mp4 --duration 10'
& $cli exec 'logs --level error --stack --limit 20 --compact'
& $cli exec 'font_bake --apply' --timeout 900
& $cli commands --available --pretty
```

### 6.5 operation 等待语义

`exec` 默认 `waitForOperation = true`：如果命令返回值是可解析为带整型 `id` 的 JSON 字符串（即 operation 序列化结果），Server 会直接等待 operation 到终态再返回。`recompile`、`play-start`、`play-stop` 会触发 Domain Reload，CLI 改用内部协议 `wait-operation`，不再反复执行 `operation-status`。

- 使用 `--no-wait` 时只返回 operation 的初始状态（`Queued` / `Running`），响应中仍会带 `operation` 字段，需要自行用 `operation-status` 轮询。
- 超时返回 `124` 与 `Timeout` 状态，**不会**取消该操作；如需中止请显式执行 `exec 'operation-cancel <id>'`。
- 命令返回非 operation 结果时，`waitForOperation` 不生效，直接返回执行结果。
- `recompile`、`play-start`、`play-stop` 会触发 Domain Reload。CLI 先取得 operation，再按 PID 重新发现 Server 并提交内部 `wait-operation` 请求；成功和失败都以最终 operation 状态为准。重连不会重置总超时，等待期间也不会产生 `operation-status` 命令记录。
- `play-start` 在 `EnteredPlayMode` 且 CLI Server 恢复后成功；Editor 内的 `play-stop` 在 `EnteredEditMode` 且 CLI Server 恢复后成功。Development Player 的 `play-stop` 仍是同步退出请求回执。
- `recompile`、`play-start`、`play-stop` 均实现 `IXCommandAsyncOperation` 且 `CanCancel=false`，开始后不能用 `operation-cancel` 中止。新 Server 最多等待 5 秒恢复 operation，超过后返回失败。`operation-status` 仍可用于手工查询，并会正常产生命令记录。

### 6.6 CLI、Agent 与模型的数据流

XCommand 不直接调用大模型，也不向大模型发送回调。数据流为：

```text
模型生成工具调用
    |
    v
Agent 工具运行时启动 PowerShell
    |
    v
xcli.ps1 通过 TCP 向 XFramework CLI Server 发请求
    |
    v
Server 在 Unity 主线程执行命令，并通过 TCP 返回 JSON
    |
    v
xcli.ps1 将最终 JSON 写入 stdout，并以 exitCode 退出
    |
    v
Agent 工具运行时捕获 stdout / stderr / exitCode，并作为 tool result 放回模型上下文
```

职责边界：

| 组件 | 职责 |
| :--- | :--- |
| XFramework CLI Server | 执行命令、维护 operation、收集结果，通过 TCP 返回 JSON |
| `xcli.ps1` | 发现 Server、发送请求、等待 operation、处理 Domain Reload 后重连，把最终 JSON 写到 stdout |
| Agent 工具运行时 | 启动和监控 CLI 进程，捕获 stdout / stderr / exitCode，并把结果作为 tool result 交给模型 |
| 模型 | 发起工具调用；收到 tool result 后读取 JSON 并决定后续动作 |

需要区分三个标识：

| 标识 | 所属层 | 含义 | 生命周期 |
| --- | --- | --- | --- |
| `session_id` | Agent 工具运行时 | 跟踪仍在运行的 PowerShell / CLI 进程 | 临时工具会话，不落盘 |
| operation ID | XCommand | 跟踪 Unity 中的异步命令状态 | 当前 Editor 会话；`recompile` 可跨 Domain Reload 恢复 |
| PID | 操作系统 | 标识 Unity Editor、PowerShell 等进程 | 进程存活期间 |

- **默认等待**：`xcli.ps1` 持续等待 operation，直到成功、失败或超时后输出最终 JSON 并退出。
- **`--no-wait`**：取得 operation 后立即输出初始 JSON 并退出，后续必须主动执行 `operation-status`。

---

## 7. 自动化管道（`XCommandPipeline`）

- **状态机**：`Queued → Running → Succeeded` / `Failed` / `Cancelled`。
- **驱动方式**：Play Mode 由隐藏的 `[XFramework XCommand Pipeline]` GameObject 上的协程串行驱动；Edit Mode 下的 Editor operation 由 `EditorApplication.update` 驱动；队列 FIFO，保留最近 100 条已完成操作。
- **操作字段**：`Id` / `Name` / `State` / `Output` / `Error` / `DurationMilliseconds` / `CreatedTimeUtc` / `StartedTimeUtc` / `CompletedTimeUtc`。
- **主要接口**：`RunOperation(IXCommandAsyncOperation)`、`TryGetOperation`、`CancelOperation`、`CancelAllOperations`、`Operations`；`IXCommandAsyncOperation.CanCancel` 决定是否允许取消；`XCommandPipeline.DelegateOperation` 供业务程序集快速接入。
- **Editor operation**：`recompile` / `play-start` / `play-stop` 通过 `SessionState` 持久化，Domain Reload 后由 `XCommandPipeline.RestoreOperation(...)` 恢复；`CanCancel=false`。
- **取消语义**：取消 `ui-act` / `ui-input` 时，由 operation 自身的 `finally` 调用 `ResetInterruptedInput()`，释放可能残留的按键 / 指针状态；`XCommandPipeline` 不再按操作名做特殊处理。
- **运行时重置**：`XCommandPipeline.ResetOperations()` 只触发 `XCommandHub.CommandRuntimeResetRequested`；`XCommandUI` 与 `XCommandUtility.Input` 订阅该事件并各自清理快照 / 输入状态。

### 7.1 选择器（UGUI 与 GameObject）

`XCommandUISelector` 与 `XCommandGameObjectQuery` 支持的定位字段：

| 字段 | 说明 |
| :--- | :--- |
| `name` | 对象名 |
| `path` | 层级路径 |
| `indexed-path` | 带序号的层级路径，用于同名兄弟节点消歧 |
| `text` | UGUI 文本内容 |
| `component` | 组件类型名（`go-*` 使用） |
| `action` | 事件动作名（`ui-list` 使用） |

`ui-*` 系列要求唯一且稳定的匹配；`go-state` 默认要求唯一匹配，使用 `--all` 时才返回多条。

### 7.2 截图路径与前缀

路径必须是绝对路径，或使用以下前缀：

| 前缀 | 对应目录 |
| :--- | :------- |
| `temp:/` | `Application.temporaryCachePath` |
| `data:/` | `Application.dataPath` |
| `persistent:/` | `Application.persistentDataPath` |

截图在 `WaitForEndOfFrame` 后执行，支持 `--width` / `--height`（必须同时指定）、`--region X,Y,W,H` 与 `--ui-only`。

> [!NOTE]
> `--ui-only` 会临时把所有非 UI 相机的 `cullingMask` 置 0，并在结束时（含异常路径）于 `finally` 中还原；若场景中没有可用相机，框架会自建一个清屏相机。

### 7.3 输入注入（`ui-input`）的正确用法

`ui-input {json}` 注入一段输入序列，返回 operation ID（`exec` 默认等待其完成）。JSON 结构：

```json
{ "steps": [ ... ], "resetAfter": true }
```

- `steps`：按顺序执行的输入步骤。
- `resetAfter`：序列结束后是否重置所有按键 / 指针状态，建议置 `true`，避免残留按住状态。

支持的 step（`type`）：

| `type` | 关键字段 | 说明 |
| :--- | :--- | :--- |
| `key` / `keyboard` | `action` = `down` / `up` / `press` / `text`，`key`，`frames` | 键盘；`key` 用 `Key` 枚举名（如 `W`、`Space`、`LeftShift`） |
| `mouse` | `action` = `move` / `down` / `up` / `click` / `scroll`，`button`，`x` / `y` | 鼠标 |
| `touch` | `action` 为触控 phase，`touchId`，`x` / `y`，或 `touches` 数组 | 触控，支持多点 |
| `wait` | `frames` | 等待 N 帧 |
| `reset` | — | 立即重置输入状态 |

> [!IMPORTANT]
> **连续按键（走路、蓄力、长按等）必须放在同一个 `ui-input` 操作内，用 `down → wait → up` 表达；不要用 `press`，也不要把按键拆到多个操作、中间夹独立的 `wait` 命令。**
>
> 原因有两个：
> 1. `ui-input` 的 `Run` 协程只在**整个操作期间**临时把 `InputSettings.editorInputBehaviorInPlayMode` 设为 `AllDeviceInputAlwaysGoesToGameView`、把 `backgroundBehavior` 设为 `IgnoreFocus`，操作一结束就在 `finally` 里还原。若把按键拆到多个操作、中间用 `wait` 命令等待，等待期间这两个开关已失效；加上 CLI 驱动时 GameView 通常没有焦点，按键不会送进游戏（表现为「录了视频但角色原地不动」）。
> 2. `press` 只按住 1 帧就松开，无法产生持续位移，必须用 `down` + `wait` + `up`。
>
> 正确示例（按住 W 连续移动 N 帧）：
>
> ```json
> {"steps":[
>   {"type":"key","action":"down","key":"W","frames":2},
>   {"type":"wait","frames":300},
>   {"type":"key","action":"up","key":"W","frames":2}
> ],"resetAfter":true}
> ```

---

## 8. 日志与记录

### 8.1 运行时日志缓冲（`logs` / `wait-for-log`）

- 通过 `Application.logMessageReceivedThreaded` 采集，**环形缓冲上限 50MB**，超出后逐条淘汰最旧条目。
- 每一条包含 `sequence` / `timestampUtc` / `threadId` / `level` / `message` / `stackTrace`。
- 查询结果包含 `cursor`（下次增量查询用）、`latestSequence`、`oldestSequence`、`dropped`、`hasMore`、`bufferBytes` / `capacityBytes`。
- 级别语义：`all` / `log` / `info` → `warn` / `warning` → `error` / `exception` / `assert`，按最小严重度过滤。
- 可用 `--message-limit` / `--stack-limit` 截断长文本；需要堆栈时加 `--stack`。
- `wait-for-log` 的 `--since` 默认 `-1`，含义是「等待命令开始之后产生的新日志」，因此不会被历史日志立即满足。

### 8.2 执行记录与历史（`XCommandHub`）

- 记录上限 **2000** 条，命令历史上限 **50** 条（新命令插到最前）。
- 记录状态为 `Running → Completed`；`recordExecution = false` 的命令（如 `clear`、`load_history`）既不写记录也不写历史。
- 编辑器侧由 `XCommandEditorHistory` 持久化：记录存 `SessionState`（跨脚本重载保留，退出编辑器清除），历史存 `EditorPrefs`（key 由项目 `dataPath` 的 `Hash128` 派生）。
- 若脚本重载时记录仍处于 `Running`，恢复后会被标记为 `Failed`，消息为「命令执行因脚本重载中断」。

### 8.3 历史分页协议（`history` / `history-text` / `history-input`）

外部网关通过三个 action 分页读取 Hub 历史：

| action | 关键请求字段 | 响应 |
| :--- | :--- | :--- |
| `history` | `historyVersion`、`afterRecordId`、`pageSize` | `historyVersion`、`unchanged`、`recordCount`、`recordLimit`、`records`、`hasMore`、`nextRecordId`、`commandHistoryCount`、`commandHistory` |
| `history-text` | `historyVersion`、`recordId`、`recordField`（`commandLine` / `message` / `output` / `exception`）、`textOffset` | `historyVersion`、`recordId`、`recordText`、`textLength` |
| `history-input` | `historyVersion`、`historyIndex`、`textOffset` | `historyVersion`、`historyIndex`、`recordText`、`textLength` |

约束：

- `pageSize` 上限 `100`，单页响应上限 `4MB`，单次文本块上限 `65536` 个 UTF-16 代码单元；切分时不会拆开代理对。
- 首次请求（`afterRecordId == 0`）在体积允许时附带完整 `commandHistory`；超过半页预算时省略，由客户端改用 `history-input` 逐条读取。
- 版本不一致且请求后续页时返回 `HistoryChanged`（exitCode 4）；记录已被环形缓冲淘汰时返回 `RecordNotFound`（exitCode 4）。
- `textLength` 与 `textOffset` 都按 UTF-16 代码单元计算。

---

## 9. 注意事项

> [!IMPORTANT]
> 所有命令都在 **Unity 主线程** 执行。耗时逻辑（等待、批量操作、渲染）应拆成 operation 异步执行，否则会阻塞 Editor 或玩家进程。

- `ui-*` 系列依赖 Unity Input System 注入事件，被取消时由对应 operation 在 `finally` 中重置输入状态。
- `requireConfirmation = true` 的命令在 CLI 下必须带 `--confirm`，否则返回 `ConfirmationRequired` 且不执行。
- 命令 `displayType = Hidden` 只影响 CLI `commands` 的枚举结果，不影响执行。
- `XCommand` 静态类在编辑器下通过 `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` 与 `Application.exitCancellationToken` 重置静态状态，用于兼容「关闭 Reload Domain」。如果你也在 `XCommand` 相关类型上添加静态缓存，请在同样的时机重置。
- `XCommandPipeline` 会在同一时机清空操作队列、销毁 runner GameObject，并通过 `CommandRuntimeResetRequested` 通知 `XCommandUI` / `XCommandUtility.Input` 清理自身状态；`recompile` / `play-start` / `play-stop` 的 operation 由 `XCommandCompileOperation` / `XCommandPlayModeOperation` 通过 `SessionState` 自行恢复。
- CLI Server 绑定 `IPAddress.Any` 且命令无鉴权，只应在可信内网开发环境使用；`instanceId` 只用于防止串实例，不是访问令牌。
- XHunter 依赖系统 Python 3.9+，固定使用 `127.0.0.1:5182`，Web 资源与启停脚本位于 `C:\Projects\XDEDZL\XHunter`。

---

## 10. Workflow 测试约定

> 本节是本仓库的测试产物约定。Agent 测试流程放在 `.agents/workflows/<workflow-name>.md`，通过 `xcli.ps1` 驱动 Unity；测试产物统一写入 `.agents/workflows/runs/`。

### 10.1 目录结构

每次执行 workflow 都新建一个独立运行目录，不覆盖历史结果：

```text
.agents/workflows/runs/
  <yyyyMMdd-HHmmss>_<workflow-name>/
    report.md          # 必填：测试报告
    logs/              # 可选：xcli 输出、错误日志、perf JSON
    screenshots/       # 可选：截图
    recordings/        # 可选：录屏 / GIF
    artifacts/         # 可选：Profiler 导出、构建日志等
```

- `report.md` 必填；其余目录只在确实有内容时创建，不建空目录。
- 截图、录屏不是每次都有，没有就省略，并在报告中写「未录制」。
- 大文件（长录屏、Profiler 数据）默认只保留在本地，不提交仓库。
- 完整约定与模板见 `.agents/workflows/runs/README.md`。

### 10.2 测试报告格式

`report.md` 至少包含以下部分：

1. **结论**：`PASS` / `FAIL` / `BLOCKED`，以及一句话摘要。
2. **环境**：时间、项目/场景、Git 分支与 commit、未提交改动、Unity 版本与运行方式（Editor Play Mode / Development Build）。
3. **测试范围**：本次覆盖的 workflow 步骤。
4. **步骤与结果**：表格列出操作/命令、期望、实际、结果；未执行的步骤标 `SKIP` 并说明原因。
5. **指标**：编译 error/warning、关键 Profiler 标记耗时、主线程同步读盘、运行时 error 数等。
6. **问题与失败**：失败项必须给出复现步骤或日志位置。
7. **产物**：实际保存的日志、截图、录屏、Profiler 文件路径。
8. **备注**：限制、未覆盖项、后续待办。

### 10.3 编写建议

- 命令统一使用 `xcli.ps1 exec '<command>' --target editor`，并在报告中保留关键命令原文。
- 先 `recompile`，再 `play-start`，结束时 `play-stop`；采样期间不要插入其它 xcli 命令。
- 需要堆栈时使用 `logs --level error --stack --compact`；按关键字筛选用 `--contains`。
- 失败结论必须能仅凭 `report.md` + `logs/` 复现或定位，不依赖口头描述。
