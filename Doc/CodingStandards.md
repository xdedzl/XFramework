# XFramework 编码规范

本规范适用于所有使用 XFramework 的代码。框架整体说明见 [XFramework 开发手册](../README.md)。

## 1. 编码标准

- **不使用长类型名**：严禁在代码中直接使用包含完整命名空间的长类型名（如 `System.Collections.Generic.List<T>` 或 `XFramework.Event.MessageManager`），必须在文件顶部通过 `using` 引入对应的命名空间。
- **命名空间与 Using**：使用框架组件时，必须在文件顶部 `using XFramework` 或 `using XFramework.ModuleName`。
- **单例管理**：避免散乱地创建临时的单例；应优先实现为模块（如 `IGameModule`）并交由框架统一管理，通过 `XXXManager.Instance` 进行访问。
- **重复造轮子审查**：在编写代码时，如果需要通用的功能逻辑，优先检查框架提供的 `Utility`（纯 C#）和 `UUtility`（Unity 定制）系列中是否已有实现，**严禁重复造轮子**；如果没有，也可以在 XFramework 的合适位置添加。
- **日志输出**：在适用的情况下，使用 `XFramework.Console.XConsole` 或框架提供的方式进行游戏内调试和日志记录。
- **判空**：不要无条件地使用 `if (x != null) x.DoSomething()` 这种写法，很多时候 x 可以被包装为非空，或者 x 为空时就应该产生问题。

## 2. 命名规范

- **单例/管理器**：应以 `Manager` 结尾（例如：`UIManager`、`SoundManager`）。
- **流程类**：应以 `Procedure` 结尾（例如：`LoadingProcedure`、`BattleProcedure` 等）。
- **UI 面板**：应以 `Panel` 结尾。
