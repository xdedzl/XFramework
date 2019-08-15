# XFramework  --xdedzl
我是薛定谔的蟑螂

## 1. 启动流程

在场景新建一个空物体命名为GameManager，将Game脚本附加到物体上。新建一个类MyFirstProcedure继承ProcedureBase，Game脚本的属性面板可以选择游戏开始时第一个进入的流程。

## 2. 模块说明

**`Entity`**：实体的管理，提供了实体的加载，回收以及不同实体之间父子关系的处理。

**`FSM`**：状态机，内置提供了鼠标状态机。

**`Graphics`**：图像处理器，方便的使用Unity底层绘图API在相机上绘制图形，并提供了一系列构造Mesh的工具方法。

**`Messenger`**：全局消息处理。

**`Observer`**：观察者模式实现的事件处理机制。

**`Pool`**：基础对象池，实体的对象池的管理集成到了Entity中。

**`Procedure`**：流程，实际上是一个贯穿整个游戏过程的状态机。

**`Resource`**：资源管理机制，统一使用Game.ResourceModule.Load<T>("Assets/Path/xxx.png")的方式加载，开发过程中使用AssetDataBase,输出版本后使用assetbundle。提供了ab包编辑器

**`Task`**：任务处理。

**`Terrain`**：运行时Terrain编辑。

**`UI`**：基于UGUI的UI管理模块，GUCompotent是对UI组件的扩展并提供了一种UI组件的查找方式以替代transform.Find，在开发过程中修改了UI面板的层级之后无需更改代码。



![在这里插入图片描述](https://img-blog.csdnimg.cn/20190509223528770.png)