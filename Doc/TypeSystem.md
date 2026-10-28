# 类型别名与实例构造（XType）

`XType` 是 XFramework 类型系统的唯一对外入口：普通 C# 类通过 `[XTypeAlias("别名")]` 声明别名，运行时按字符串查询类型并创建实例，避免在业务代码里散落 `Type.GetType`、程序集扫描和 `Activator.CreateInstance`。

## 1. 基本用法

```csharp
using XFramework.TypeSystem;

[XTypeAlias("task.condition.hasItem")]
public sealed class HasItemCondition : ITaskCondition
{
    public bool Evaluate(TaskContext context)
    {
        return context.Bag.HasItem("apple");
    }
}

ITaskCondition condition = XType.Create<ITaskCondition>("task.condition.hasItem");
```

带构造参数：

```csharp
[XTypeAlias("battle.reward.gold")]
public sealed class GoldReward : IReward
{
    public GoldReward(int amount)
    {
        Amount = amount;
    }

    public int Amount { get; }
}

IReward reward = XType.Create<IReward>("battle.reward.gold", 100);
```

## 2. API

| 成员 | 说明 |
| --- | --- |
| `XType.TryGet(string alias, out Type type)` | 尝试查询别名；别名不存在或为空时返回 `false`。 |
| `XType.Get(string alias)` | 查询别名；不存在时抛 `XFrameworkException`。 |
| `XType.Create(string alias, params object[] args)` | 按别名创建实例。 |
| `XType.Create<T>(string alias, params object[] args)` | 按别名创建实例，并校验结果可转换为 `T`。 |
| `XType.GetAliases()` | 返回按名称排序的别名快照。 |
| `XType.Refresh()` | 重新扫描所有已加载程序集并重建索引。 |

## 3. 注册规则

- 只注册标记了 `[XTypeAlias("别名")]` 的类。
- 别名是全局的，使用 `StringComparer.Ordinal` 精确匹配，区分大小写和空白。
- 建议使用带业务前缀的别名，例如 `tavern.condition.hasItem`，避免不同业务域冲突。
- 以下情况在构建注册表时直接抛 `XFrameworkException`：
  - 别名为空；
  - 同一别名被多个类型声明；
  - 类型是抽象类或静态类；
  - 类型是泛型类型定义。
- 构造实例复用 `Utility.Reflection.CreateInstance`，使用公共构造函数匹配规则；创建失败会带上别名与类型名重新抛出。

## 4. 扫描时机与范围

- 首次调用任意查询/构造 API 时惰性扫描 `AppDomain.CurrentDomain.GetAssemblies()`。
- 只扫描已加载的程序集，跳过动态程序集；无法加载类型的程序集会被跳过。
- 之后如果动态加载了程序集（热更、插件），调用 `XType.Refresh()` 重建索引。
- 扫描会遍历所有程序集的类型，首次调用有一次性开销；不要在每帧首次使用时才触发。

## 5. 边界

- 只支持普通 C# 类，不处理 `ScriptableObject`、`MonoBehaviour` 或 `GameObject`。
- Editor-only 程序集中的别名只在 Editor 下可用，打包后不存在。
- IL2CPP 下仅通过字符串引用的类型可能被裁剪；给类型加 `[UnityEngine.Scripting.Preserve]`，或在 `link.xml` 中保留。
