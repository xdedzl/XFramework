# XFramework 数据表驱动配置 (`XDataTable`)

> `XDataTable` 用于加载 `.xasset` 配表文本资源，并按数据类型缓存表数据。本文档聚焦配表基类、资源绑定、访问方式与运行时缓存约定，适合作为业务表接入时的专题说明。

---

## 1. 概述

业务表通常继承 `XDataTable<TData>`、`XDataTableHasKey<TKey, TData>` 或 `XDataTableHasAlias<TKey, TData>`，再通过 `[DataResourcePath]` 与 `[TargetDataType]` 绑定资源路径和数据结构。

- 核心源码目录：[`Runtime/Modules/Data/`](../Runtime/Modules/Data/)
- README 入口：[`README.md`](../README.md)

---

## 2. 核心能力

- 从 `.xasset` 文本资源加载结构化表数据。
- 按数据类型缓存整张表，避免重复反序列化。
- 支持按完整列表、主键或别名访问数据。
- 支持重写 `AfterLoad()`，在首次加载后构建额外索引或运行时缓存。
- 支持通过 `LoadTable<TTable>()` 获取具体派生表实例，访问自定义扩展字段。

---

## 3. 配置与访问方式

### 3.1 基础访问

```csharp
// 加载完整列表
IReadOnlyList<DrinkData> drinks = DrinkDataTable.LoadData();

// 按主键访问
DrinkData drink = DrinkDataTable.GetData(1001);

// 按别名访问
DrinkData tavernAle = DrinkDataTable.GetDataByAlias("tavern_ale");
```

### 3.2 资源路径与目标类型绑定

业务表通过特性声明资源路径与目标数据类型：

```csharp
[DataResourcePath("Assets/ABRes/Data/DrinkDataTable.xasset")]
[TargetDataType(typeof(DrinkData))]
public class DrinkDataTable : XDataTableHasAlias<uint, DrinkData>
{
}
```

- `[DataResourcePath]`：指定 `.xasset` 配表资源路径。
- `[TargetDataType]`：声明表内数据项的实际结构类型。
- `XDataTableHasKey<TKey, TData>`：适合按主键查询的表。
- `XDataTableHasAlias<TKey, TData>`：适合同时按主键和别名查询的表。

### 3.3 加载后处理 (`AfterLoad`)

当配表需要构建额外索引、分类缓存或预计算字段时，重写 `AfterLoad()`。该方法会在表资源首次反序列化后调用一次，适合把 `items` 转换为运行期高效查询结构。

```csharp
using System.Collections.Generic;
using XFramework.Data;

[DataResourcePath("Assets/ABRes/Data/DrinkDataTable.xasset")]
[TargetDataType(typeof(DrinkData))]
public class DrinkDataTable : XDataTableHasAlias<uint, DrinkData>
{
    public Dictionary<DrinkType, List<DrinkData>> DataByType { get; private set; }

    protected override void AfterLoad()
    {
        base.AfterLoad();

        DataByType = new Dictionary<DrinkType, List<DrinkData>>();
        foreach (var data in items)
        {
            if (!DataByType.TryGetValue(data.type, out var list))
            {
                list = new List<DrinkData>();
                DataByType.Add(data.type, list);
            }

            list.Add(data);
        }
    }
}
```

需要访问这些派生表字段时，使用 `LoadTable<TTable>()` 获取表实例：

```csharp
var drinkTable = XDataTable.LoadTable<DrinkDataTable>();
IReadOnlyList<DrinkData> wines = drinkTable.DataByType[DrinkType.Wine];
```

---

## 4. 典型使用场景

- 读取整张配表后做只读展示或配置驱动初始化。
- 通过主键快速查询单条配置。
- 通过别名访问更贴近业务语义的配置项。
- 在首次加载后建立二级索引、分组缓存或预处理结果，降低运行时重复计算成本。

---

## 5. 注意事项

> [!TIP]
> `LoadData<T>()`、`LoadDictData<TKey, TValue>()` 和 `LoadTable<TTable>()` 共享同一份表缓存；`AfterLoad()` 只会在首次加载该表时执行，不要在其中放依赖外部运行时状态的临时逻辑。

- `AfterLoad()` 适合做纯数据转换，不适合放临时业务状态。
- 若业务代码需要访问派生类上的扩展字段，应优先通过 `LoadTable<TTable>()` 获取表实例，而不是重复拼装同类缓存。
- 文本配表的结构和目标类型应保持一致，否则首次反序列化时就会暴露问题。
