# XFramework UI 系统详细说明

本文档用于说明 XFramework 运行时 UI 系统的使用方式、面板写法、节点绑定规则和 Prefab 生成要求。Editor 工具窗口仍遵循 UI Toolkit 规范；游戏内界面使用本文档约定的写法。

## 1. 基本原则

- 面板脚本继承 `PanelBase`，并使用 `[PanelInfo]` 声明面板名、Prefab 路径和层级。
- 面板脚本中不要用 `[SerializeField]` 暴露 `Text`、`Button`、`Image` 等节点引用给 Inspector 手动拖拽。
- 需要被代码访问的 UI 节点必须挂载 XFramework 封装组件，例如 `XText`、`XTMPText`、`XButton`、`XImage`、`XLayoutGroup`、`XTMPInputField`。
- 在 `OnInit()` 中通过 `this["NodeName"] as XButton` 或 `Find<XButton>("NodeName")` 查找并缓存节点。
- 查找 key 默认使用 GameObject 名称；如需稳定别名，可在 `XUIBase` 的 `searchKey` 中填写自定义 key。
- 节点命名要稳定、语义清楚，避免依赖层级路径。调整父子层级时，只要节点名或 `searchKey` 不变，脚本不需要修改。
- 使用框架组件提供的方法注册事件，例如 `XButton.AddListener`、`XTMPInputField.AddOnValueChanged`。

## 2. 面板脚本写法

推荐写法：

```csharp
using UnityEngine;
using XFramework.UI;

[PanelInfo("Letter", "Assets/ABRes/Panels/LetterPanel.prefab", 20)]
public class LetterPanel : PanelBase
{
    private XText m_TitleText;
    private XText m_ContentText;
    private XButton m_CloseButton;

    protected override void OnInit()
    {
        m_TitleText = Find<XText>("TitleText");
        m_ContentText = Find<XText>("ContentText");
        m_CloseButton = Find<XButton>("CloseButton");
        m_CloseButton.AddListener(Close);
    }

    public override void OnOpen(params object[] args)
    {

    }
}
```

不推荐写法：

```csharp
using UnityEngine;
using UnityEngine.UI;
using XFramework.UI;

public class BadPanel : PanelBase
{
    [SerializeField] private Text m_TitleText;
    [SerializeField] private Button m_CloseButton;
}
```

## 3. 生命周期约定

- `OnInit()` 只在面板首次加载时调用一次，适合查找组件、绑定按钮事件、初始化绑定关系。
- `OnOpen(params object[] args)` 每次打开面板都会调用，适合接收参数、刷新显示、注册业务状态。
- `OnClose()` 适合解绑当前面板打开期间注册的外部事件；重写时要调用 `base.OnClose()`，以保证 `[EventListener]` 自动注销。
- `OnAfterClose()` 适合关闭动画之后或面板真正关闭后的业务回调，例如恢复输入锁、发送已读通知。
- `OnUpdate()` 只写必要的轻量逻辑；不要在每帧做 UI 节点查找。

## 4. 面板标签

面板可以通过 `[PanelTag]` 声明一个或多个标签。标签只描述面板打开期间需要维持的状态，具体行为由项目侧定义：

```csharp
using XFramework.UI;

[PanelInfo("Inventory", "Assets/UI/Inventory.prefab", 20)]
[PanelTag(GameUITag.BlockPlayerInput, GameUITag.PauseGameTime)]
public class InventoryPanel : PanelBase
{
}
```

项目为每个标签实现一个独立的 `IPanelTagHandler`。处理器通过 `Tag` 指定唯一负责的标签，并必须提供公开无参构造函数；`UIManager` 会从 `Assembly-CSharp` 自动发现并创建：

```csharp
using XFramework.UI;

public sealed class BlockPlayerInputUITagHandler : IPanelTagHandler
{
    public string Tag => GameUITag.BlockPlayerInput;

    public void OnTagStateChanged(bool active)
    {
        GameLock.SetLock(GameLock.PlayerInput, active);
    }
}
```

标签采用“首开末关”语义：

- 第一个持有该标签的面板打开时，处理器收到一次 `active = true`。
- 其他同标签面板继续打开时不会重复激活。
- 关闭其中一个面板不会失活；最后一个持有者关闭后，处理器收到一次 `active = false`。
- 一个标签必须且只能对应一个处理器；缺少处理器或重复处理器都会在 UI 系统初始化时抛出异常。
- 标签区分大小写；空标签会在 UI 系统初始化时抛出异常；同一声明中的重复标签会自动去重。
- 可通过 `UIManager.Instance.IsTagActive(tag)` 和 `GetTagActivePanelCount(tag)` 查询运行时状态。
- UI Debuger 的面板详情会显示标签及其当前持有面板数量，并支持按标签搜索。

## 5. Prefab 节点要求

每个需要被脚本访问的节点，都要同时具备 Unity 原生组件和 XFramework 封装组件：

| 需求 | 原生组件 | XFramework 组件 | 代码访问 |
| :--- | :--- | :--- | :--- |
| 普通文本 | `Text` | `XText` | `Find<XText>("TitleText")` |
| TMP 文本 | `TextMeshProUGUI` | `XTMPText` | `Find<XTMPText>("MoneyLabel")` |
| 按钮 | `Button` | `XButton` | `Find<XButton>("CloseButton")` |
| 图片 | `Image` | `XImage` | `Find<XImage>("Icon")` |
| 输入框 | `TMP_InputField` | `XTMPInputField` | `Find<XTMPInputField>("CountInput")` |
| 列表容器 | `LayoutGroup` | `XLayoutGroup` | `Find<XLayoutGroup>("ItemList")` |

`XText.text`、`XTMPText.text`、`XButton.button`、`XImage.image` 等字段需要引用同节点上的原生组件。手工制作 Prefab 时，可通过组件 `Reset` 自动填充；直接生成 Prefab 时必须显式赋值。

- 不要在同一个 GameObject 上同时挂 `Image` 和 `Text` / `TextMeshProUGUI`。图片节点和文字节点必须拆开。
- 如果文字需要背景，应让父节点挂 `Image` + `XImage` 作为背景，再把文字节点作为子物体，子物体单独挂 `Text` + `XText` 或 `TextMeshProUGUI` + `XTMPText`。

## 6. 列表与子节点

- 列表容器使用 `XLayoutGroup`，第一个子物体作为模板。
- 模板节点会在运行时被改名为 `"(Template)"` 并隐藏，后续数据项由框架复制生成。
- 列表项逻辑继承 `UINode`，在 `Awake()` 或绑定回调中使用 `this["Title"] as XTMPText`、`go.Find<XTMPText>("Title")` 查找子节点。
- `SetItemType<T>()` 用于指定列表项节点类型；`SetOnItemChange` 适合刷新数据对象，`SetOnItemBind` 适合配合 `BindableDataSet` 建立绑定。

## 7. 直接生成 Prefab 要求

如果通过工具直接生成 UI Prefab，最终产物必须一次性满足运行时要求，不依赖人工二次拖拽或补引用。生成结果至少要满足以下约束：

- 每个需要被脚本访问的节点，都要同时挂好 Unity 原生组件和对应的 XFramework 组件。
- `XText.text`、`XTMPText.text`、`XButton.button`、`XImage.image`、`XTMPInputField.inputField` 等字段都要在生成时完成引用绑定。
- 节点命名必须稳定且语义清晰，供 `Find<T>("NodeName")` 或 `this["NodeName"]` 直接查找。
- 不要把 `Image` 和 `Text` / `TextMeshProUGUI` 挂在同一个 GameObject 上。
- 如果文字需要背景，应生成“父节点背景图 + 子节点文字”的层级结构。
- 按钮节点应包含 `Image`、`Button`、`XButton`，并让 `Button.targetGraphic` 指向同节点 `Image`，`XButton.button` 指向同节点 `Button`。
- 普通文本节点应包含 `Text`、`XText`，并让 `XText.text` 指向同节点 `Text`。
- TMP 文本节点应包含 `TextMeshProUGUI`、`XTMPText`，并让 `XTMPText.text` 指向同节点 `TextMeshProUGUI`。
- 图片节点应包含 `Image`、`XImage`，并让 `XImage.image` 指向同节点 `Image`。
- 输入框节点应包含 `TMP_InputField`、`XTMPInputField`，并补齐文本区、占位文本、输入文本等运行所需引用。
- 列表容器应包含布局组件和 `XLayoutGroup`，第一个子物体作为模板节点，模板结构也要满足上述绑定规范。
- 生成结果要同时补齐布局、锚点、尺寸、字号、颜色、RaycastTarget、模板节点和 `ScrollRect` 引用等所有运行必需配置。

带背景文字的正确层级示例：

```
LabelRoot
├── Background (`Image` + `XImage`)
└── LabelText (`Text` + `XText`)
```

## 8. 常见问题

- `there is no ui component named 'xxx'`：目标节点未挂 XFramework 组件，或节点名、`searchKey` 与代码不一致。
- `already have a XUIBase component named xxx`：同一个面板查找范围内有重复 key。改名或填写唯一 `searchKey`。
- 按钮点击没有响应：确认节点挂了 `XButton`，`XButton.button` 已引用同节点 `Button`，并在 `OnInit()` 中调用了 `AddListener`。
- 文本不刷新：确认使用的是 `XText.text.text` 或 `XTMPText.text.text`，不是直接把字符串赋给 XFramework 组件字段。
- 子列表查找串到外层面板：列表项逻辑应继承 `UINode`，让 `ComponentFindHelper` 的忽略边界隔离子节点查找。
