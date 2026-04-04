using System;

namespace XFramework
{
    /// <summary>
    /// 模块生命周期类型
    /// </summary>
    public enum ModuleLifecycle
    {
        /// <summary>
        /// 普通模块，按需创建和销毁
        /// </summary>
        Normal,
        /// <summary>
        /// 持久化模块，游戏启动或编译后创建，始终存在
        /// </summary>
        Persistent,
        /// <summary>
        /// 运行时持久化模块，游戏启动后创建，始终存在
        /// </summary>
        RuntimePersistent,
        /// <summary>
        /// 编辑器持久化模块，编译后创建，始终存在
        /// </summary>
        EditorPersistent
    }

    /// <summary>
    /// 标记模块生命周期
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ModuleLifecycleAttribute : Attribute
    {
        public ModuleLifecycle Lifecycle { get; }

        public ModuleLifecycleAttribute(ModuleLifecycle lifecycle)
        {
            Lifecycle = lifecycle;
        }
    }
}
