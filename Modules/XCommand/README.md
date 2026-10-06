# XFramework 运行时控制台 (`XCommand`)

> `XCommand` 是 XFramework 的运行时 / 编辑器命令行基础设施。同一套命令注册与执行链路，向上服务 **游戏内 UGUI 控制台、编辑器窗口、C# API、外部 CLI、远程 Hunter** 五种入口，向下提供命令发现、参数解析、执行记录、日志查询、UGUI / GameObject 自动化操作与截图能力。
>
> 本文档聚焦命令注册、执行链路、CLI 协议与自动化管道，适合作为编写调试命令和接入外部自动化时的专题说明。

- 核心源码目录：[`Modules/XCommand/`](./)
- CLI 客户端脚本：[`Modules/XCommand/Tools/cli/xframeworkcli.ps1`](./Tools/cli/xframeworkcli.ps1)
- 框架总览：[`XFramework README`](../../README.md)

---

## 1. 能力总览

- **声明式命令注册**：静态方法标记 `[XCommandEntry]`，所在类继承 `XCommandGroup` 即会被自动扫描，无需手动登记。
- **统一执行链路**：所有入口最终都走 `XCommandHub.Execute(...)`，产出统一的 `XCommandExecutionResult`。
- **执行记录与历史**：环形记录（上限 2000 条）+ 命令历史（上限 50 条），编辑器下可跨脚本重载保留。
- **来源可追溯**：`Api` / `Editor` / `Ugui` / `Cli` / `Hunter` 五种来源会被写入记录。
- **异步操作管道**：UI 操作、条件等待、输入注入、截图以 operation 形式排队执行，返回 operation ID，可查询 / 取消 / 超时。
- **运行时日志缓冲**：50MB 环形缓冲，按游标增量查询，支持级别、关键字与堆栈筛选。
- **外部进程自动化**：Editor 与 Development Player 自动启动 loopback TCP Server，配合 `xframeworkcli.ps1` 让脚本 / CI / AI 直接驱动 Unity。

---

## 2. 模块结构

| 类 / 文件 | 职责 |
| :-------- | :--- |
| `XCommand`（`Runtime/XCommand.cs`） | 终端门面；`IConsole` 接口、`Message` 消息与着色、执行器（Command Key）管理、Hunter 联动入口 |
| `XCommandHub`（`Runtime/XCommandHub.cs`） | 执行记录、命令历史、来源标记、显示清空 / 历史请求事件 |
| `XCommandRegistry`（`Runtime/XCommandRegistry.cs`） | 命令扫描与注册（`[XCommandEntry]` + 动态注册）、可用性判定、命令行拆分、执行内核、诊断收集 |
| `XCommandPipeline`（`Runtime/XCommandPipeline.cs`） | 协程驱动的异步操作队列，UI / GameObject / 日志 / 等待 / 输入 / 截图的统一入口 |
| `XCommandArguments` + `XCommandUtility.CommandLine` | 命令行参数解析（值与标记选项、引号、位置参数） |
| `XCommandUI` / `XCommandGameObjects` | UGUI 与 GameObject 的查询、操作与等待实现 |
| `XCommandUtility.*`（`State` / `Logs` / `Capture` / `Input` / `Wait` / `Path` / `Text` / `GameObject`） | 无状态工具集合 |
| `UGUIConsole`（`Runtime/UGUIConsole.cs`） | 游戏内 UGUI 控制台实现 |
| `XFrameworkCliServer` / `XFrameworkCliOperation` / `XCommandCliOperationProvider` | 对外 CLI Server、operation 数据契约与桥接 |
| `XCommandEditorWindow`（`Editor/`） | 编辑器终端窗口 + 命令手册 + CLI 状态菜单 |
| `XCommandEditorHistory`（`Editor/`） | 编辑器侧记录 / 历史持久化 |
| `Editor/*Commands.cs` | 编辑器与 PlayMode 命令集 |
| `Tools/cli/xframeworkcli.ps1` | 外部命令行客户端 |

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
}
```

约定：

- 类必须继承 `XCommandGroup`（`public abstract class XCommandGroup`）。注册表会遍历所有已加载程序集，扫描它的非抽象子类，因此命令可以散落在框架任意模块与业务程序集中。
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
| `displayType` | `XCommandDisplayType` | `Visible` | 设为 `Hidden` 后不出现在 CLI `commands` 结果中（仍可执行） |

### 3.3 可用性判定（`XCommandMode`）

| Mode | 可用条件 |
| :--- | :------- |
| `Runtime` | `Application.isPlaying` |
| `Editor` | `!Application.isPlaying && Application.isEditor` |
| `Both` | 始终可用 |

不可用时执行结果为 `Unavailable`，并附带当前环境描述。

### 3.4 动态注册

```csharp
XCommandRegistry.AddCommand("my-dynamic", argument => $"received: {argument}", XCommandMode.Runtime);
```

动态命令与特性命令重名时，**保留特性命令** 并产生诊断（`Debug.LogWarning`）。

### 3.5 诊断信息

`XCommandRegistry.Diagnostics` 会列出所有注册期问题（命令重复、签名不合法、动态命令冲突等）。可通过 CLI `diagnostics` 或编辑器窗口的「诊断」折叠区查看，`AddDiagnostic` 同时会输出 `Debug.LogWarning`。

### 3.6 参数解析约定

`XCommandUtility.CommandLine.Parse` / `XCommandArguments.Parse(commandLine, valueOptions, flagOptions)` 的规则：

- 支持 `--name value` 与 `--name=value` 两种写法。
- `flagOptions` 中的选项不接受值，写成 `--flag=value` 会抛异常。
- 不以 `--` 开头的 token 会进入 `XCommandArguments.Values`（位置参数）。
- 支持单引号 / 双引号包裹，引号内可用 `\` 转义引号与反斜杠；未闭合的引号会报错。
- **未知选项直接抛 `ArgumentException`**，不会静默忽略。
- 读取值：`GetString` / `GetInt` / `GetLong` / `GetFloat` / `HasFlag`；`RequireValueCount(n, error)` 用于校验位置参数个数。

---

## 4. 命令清单

### 4.1 内置命令（`XCommandBuiltInCommands`，category = `XCommand`）

| 命令 | 显示名 | Mode | 说明 |
| :--- | :----- | :--- | :--- |
| `clear` | 清空 XCommand | Both | 清空当前终端显示内容，不删除 Hub 历史（不记录执行） |
| `load_history` | 加载 XCommand 历史 | Both | 在当前终端重新加载 Hub 历史（不记录执行） |
| `log` | 输出 Unity 日志 | Both | 向 Unity Console 输出一条日志 |
| `enable_hunter` / `disable_hunter` | 连接 / 断开 Hunter | Runtime | 连接或断开远程 UDP 调试端 |

### 4.2 运行时与自动化命令（`XCommandRuntimeCommands`）

| 命令 | Mode | 用途 |
| :--- | :--- | :--- |
| `play-stop` | Runtime | Editor 中退出 Play Mode；Development Player 中请求退出 |
| `go-list` | Both | 列出 GameObject，支持 `name` / `path` / `component` / `tag` / `layer` / `active` 筛选 |
| `go-state` | Both | 按 `instance-id` / `name` / `path` / `indexed-path` / `component` 查询状态，默认要求唯一匹配 |
| `ui-list` | Runtime | 列出射线可达且存在事件处理器的 UGUI 元素，支持采样与增量快照 |
| `ui-act` | Runtime | 重新定位唯一稳定元素并注入 `click` / `down` / `up` / `scroll`，返回 operation ID |
| `wait-for` | Runtime | 等待 `ui` / `go` / `log` 条件满足，返回 operation ID |
| `wait` | Runtime | 按真实时间 / 游戏时间 / 帧数等待，返回 operation ID |
| `logs` | Both | 查询 50MB 环形日志缓冲，支持级别、正文、条数与堆栈筛选 |
| `input-state` / `input-reset` | Both / Runtime | 查询或重置自动化键盘、鼠标、触摸保持状态 |
| `ui-input` | Runtime | 通过 Input System 注入键鼠 / 多点触控 JSON 序列 |
| `screenshot` | Runtime | 帧末截图，支持输出尺寸、屏幕区域与仅 UI 模式 |
| `operation-list` / `operation-status` / `operation-cancel` | Both / Both / Runtime | 列出、查询、取消异步 operation（`all` 取消全部） |
| `scene-state` | Both | 当前已加载场景、活动场景、Dirty 状态与根物体摘要 |
| `app-state` | Both | 应用、时间、屏幕、设备与常用路径状态 |

### 4.3 编辑器命令

| 命令 | Mode | 说明 |
| :--- | :--- | :--- |
| `editor_selection` | Editor | 返回当前选择对象的名称、类型与资源路径 |
| `recompile` | Editor | 刷新 AssetDatabase 并请求重新编译脚本（延迟 0.5 秒执行） |
| `play-start` | Editor | 让 Unity 进入 Play Mode |
| `preview-prefab` | Editor | 在临时场景离屏渲染 UI / 模型 Prefab 并导出 PNG，完成后恢复活动场景 |
| `font_bake` | Editor | 扫描 TMP 文本用字并补齐字体图集缺字，默认 dry-run，加 `--apply` 才写入 |

### 4.4 其他模块贡献的命令

命令注册会扫描全部程序集，因此各功能模块可以自带命令：

| 命令 | 来源 | 说明 |
| :--- | :--- | :--- |
| `fsm_list` | `Runtime/Modules/FSM/FsmConsoleCommands.cs` | 输出当前所有活动 FSM 的文本快照 |
| `procedure-switch` | `Runtime/Base/Procedure/ProcedureConsoleCommands.cs` | 按类型名或完整类型名切换唯一的 `MainProcedure` |
| `ui-open` / `ui-close` | `Runtime/Modules/UI/Core/UIConsoleCommands.cs` | 按 `PanelInfo` 名称打开 / 关闭面板 |

---

## 5. 执行入口

### 5.1 游戏内 UGUI 控制台

- **打开方式**：`XGame.OnGUI` 左下角的「调试」按钮切换 `XCommand.IsOpen`（首次打开时才初始化控制台）；同一排的「打开 / 关闭 Hunter」按钮用于切换远程调试端。
- **界面组成**：日志区（富文本着色，前缀与颜色由 `Message` 决定）、命令输入框、执行器下拉（`XCommand.CommandKeys`）、「历史」按钮（加载 Hub 历史）、「异常:开 / 关」按钮（异常详情）。
- **历史导航**：`XCommand.JumpToPreviousCmd()` / `JumpToNextCmd()`。
- **消息着色**：`Message` 结构定义 `NORMAL` / `WARNING` / `ERROR` / `SYSTEM` / `INPUT` / `OUTPUT` / `UNITY` 七类，通过 `XCommand.Log*` 系列方法写入。

### 5.2 编辑器窗口

菜单：**`XFramework/Debug/XCommand`**

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

### 5.5 Hunter 远程调试（UDP）

- 接口：`XCommand.ConnetHunter()` / `DisConnetHunter()`，命令 `enable_hunter` / `disable_hunter`，状态查询 `XCommand.IsHunterEnable`。
- 上行：把 XCommand 消息与 Unity 日志打包（`int32` 消息类型 + `int32` 来源 + 长度前缀 UTF-8 字符串）发往 Hunter 端。
- 下行：接收 Hunter 下发的命令文本，在 Unity 主线程执行，来源标记为 `Hunter`。
- 对端地址目前是 `XCommand.Hunter.cs` 中的硬编码常量 `HUNTER_IP` / `HUNTER_PORT`，本机监听 `GAME_PORT`，更换调试端需改代码。

---

## 6. 对外 CLI（`xframeworkcli.ps1`）

### 6.1 构成与生命周期

- **启动时机**：Editor 侧由 `XFrameworkCliServerBootstrap`（`[InitializeOnLoad]`）自动启动，进程类型为 `Editor`；Development Player 侧由 `XFrameworkCliPlayerBootstrap` 在 `DEVELOPMENT_BUILD` 下自动启动，进程类型为 `DevelopmentPlayer`。
- **监听方式**：`TcpListener` 绑定 `IPAddress.Loopback` 的随机端口，只接受本机连接。
- **实例发现**：启动后把自身信息写入 `%LocalAppData%/XFrameworkCLI/servers/<pid>-<port>.json`；客户端脚本据此发现实例，并通过「进程存在 + 进程启动时间戳一致」剔除残留注册文件。
- **请求处理**：Editor / Player 每帧调用 `XFrameworkCliServer.Pump()`，在 **Unity 主线程** 处理请求，因此命令天然运行在主线程。
- **报文格式**：请求与响应都是单行 JSON，必须以换行结尾。请求体上限 4MB。

### 6.2 客户端命令

```
xframeworkcli.ps1 help
xframeworkcli.ps1 servers [--pretty]
xframeworkcli.ps1 ping        [--pid PID|--port PORT] [--target editor|player] [--pretty]
xframeworkcli.ps1 commands    [--available] [--pid PID|--port PORT] [--target editor|player] [--pretty]
xframeworkcli.ps1 diagnostics [--pid PID|--port PORT] [--target editor|player] [--pretty]
xframeworkcli.ps1 exec '<完整命令行>' [--no-wait] [--timeout SECONDS] [--confirm] [--pid PID|--port PORT] [--target editor|player] [--pretty]
```

选项：

| 选项 | 说明 |
| :--- | :--- |
| `--pid N` | 指定目标进程 ID |
| `--port N` | 指定端口，跳过注册文件发现流程 |
| `--target editor\|player` | 按进程类型筛选 Server |
| `--timeout S` | 等待异步 operation 的秒数，默认 60 |
| `--no-wait` | 命令返回 operation 后立即返回，不等待完成 |
| `--confirm` | 对 `requireConfirmation` 命令确认执行 |
| `--available` | `commands` 只列出当前环境可执行的命令 |
| `--pretty` | 美化 JSON 输出 |

Server 选择规则：指定 `--port` 直接使用；否则先按 `--target` 过滤，再按 `--pid` 精确匹配；剩下的候选中优先选「当前工作目录位于其项目路径内」的那个，唯一命中即使用，多个候选或零候选会直接报错提示。

### 6.3 退出码

| 退出码 | 含义 |
| :----- | :--- |
| `0` | 成功 |
| `1` | 命令执行失败、需要确认，或 operation 失败 / 取消 |
| `2` | 参数错误（未知命令、缺少参数、值不合法） |
| `3` | 找不到匹配的 Server，或匹配到多个 |
| `4` | 传输 / 协议错误（连接超时、报文非法、Server 已停止） |
| `124` | 等待 operation 超时（**操作本身不会被取消**） |

### 6.4 典型示例

```powershell
$cli = '.\UnityClient\Packages\com.xdedzl.xframework\Modules\XCommand\Tools\cli\xframeworkcli.ps1'

& $cli exec 'recompile' --target editor
& $cli exec 'play-start'
& $cli exec 'ui-list --compact'
& $cli exec 'ui-act --name StartButton click'
& $cli exec 'screenshot temp:/xcommand.png'
& $cli exec 'logs --level error --stack --limit 20 --compact'
& $cli exec 'font_bake --apply' --timeout 900
& $cli commands --available --pretty
```

### 6.5 operation 等待语义

`exec` 默认 `waitForOperation = true`：如果命令返回值是可解析为带整型 `id` 的 JSON 字符串（即 operation 序列化结果），Server 会把它转换为 operation 并轮询到结束再返回。

- 使用 `--no-wait` 时只返回 operation 的初始状态（`Queued` / `Running`），需要自行用 `operation-status` 轮询。
- 超时返回 `124` 与 `Timeout` 状态，**不会**取消该操作；如需中止请显式执行 `exec 'operation-cancel <id>'`。
- 命令返回非 operation 结果时，`waitForOperation` 不生效，直接返回执行结果。

---

## 7. 自动化管道（`XCommandPipeline`）

- **状态机**：`Queued → Running → Succeeded` / `Failed` / `Cancelled`。
- **驱动方式**：由隐藏的 `[XFramework XCommand Pipeline]` GameObject 上的协程串行驱动，队列 FIFO；保留最近 100 条已完成操作。
- **操作字段**：`Id` / `Name` / `State` / `Output` / `Error` / `DurationMilliseconds` / `CreatedTimeUtc` / `StartedTimeUtc` / `CompletedTimeUtc`。
- **主要接口**：`ActOnUI`、`WaitForUI`、`WaitForGameObject`、`WaitForLog`、`Wait`、`RunInput`、`CaptureScreenshot`、`TryGetOperation`、`CancelOperation`、`CancelAllOperations`。
- **取消语义**：取消 `ui-act` / `ui-input` 时会自动调用 `ResetInterruptedInput()`，释放可能残留的按键 / 指针状态。

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

---

## 8. 日志与记录

### 8.1 运行时日志缓冲（`logs` / `wait-for log`）

- 通过 `Application.logMessageReceivedThreaded` 采集，**环形缓冲上限 50MB**，超出后逐条淘汰最旧条目。
- 每一条包含 `sequence` / `timestampUtc` / `threadId` / `level` / `message` / `stackTrace`。
- 查询结果包含 `cursor`（下次增量查询用）、`latestSequence`、`oldestSequence`、`dropped`、`hasMore`、`bufferBytes` / `capacityBytes`。
- 级别语义：`all` / `log` / `info` → `warn` / `warning` → `error` / `exception` / `assert`，按最小严重度过滤。
- 可用 `--message-limit` / `--stack-limit` 截断长文本；需要堆栈时加 `--stack`。
- `wait-for log` 的 `--since` 默认 `-1`，含义是「等待命令开始之后产生的新日志」，因此不会被历史日志立即满足。

### 8.2 执行记录与历史（`XCommandHub`）

- 记录上限 **2000** 条，命令历史上限 **50** 条（新命令插到最前）。
- 记录状态为 `Running → Completed`；`recordExecution = false` 的命令（如 `clear`、`load_history`）既不写记录也不写历史。
- 编辑器侧由 `XCommandEditorHistory` 持久化：记录存 `SessionState`（跨脚本重载保留，退出编辑器清除），历史存 `EditorPrefs`（key 由项目 `dataPath` 的 `Hash128` 派生）。
- 若脚本重载时记录仍处于 `Running`，恢复后会被标记为 `Failed`，消息为「命令执行因脚本重载中断」。

---

## 9. 注意事项

> [!IMPORTANT]
> 所有命令都在 **Unity 主线程** 执行。耗时逻辑（等待、批量操作、渲染）应拆成 operation 异步执行，否则会阻塞 Editor 或玩家进程。

- `ui-*` 系列依赖 Unity Input System 注入事件，被取消时框架会自动重置输入状态。
- `requireConfirmation = true` 的命令在 CLI 下必须带 `--confirm`，否则返回 `ConfirmationRequired` 且不执行。
- 命令 `displayType = Hidden` 只影响 CLI `commands` 的枚举结果，不影响执行。
- `XCommand` 静态类在编辑器下通过 `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` 与 `Application.exitCancellationToken` 重置静态状态，用于兼容「关闭 Reload Domain」。如果你也在 `XCommand` 相关类型上添加静态缓存，请在同样的时机重置。
- `XCommandPipeline` 会在同一时机清空操作队列、销毁 runner GameObject，并重置输入与 UGUI 快照状态。
- Hunter 的远端地址是硬编码常量，更换调试端需要修改源码。
