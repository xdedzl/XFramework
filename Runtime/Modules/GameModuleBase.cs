using System.Reflection;
using XFramework.Event;

namespace XFramework
{
    public enum GameModulePriority
    {
        /// <summary>
        /// 最高优先级，框架核心模块建议使用该优先级
        /// </summary>
        Highest = 0,
        /// <summary>
        /// 存档系统
        /// </summary>
        Save = 100,
        /// <summary>
        /// 较高优先级，游戏核心模块建议使用该优先级
        /// </summary>
        Higher = 200,
        /// <summary>
        /// 默认优先级，普通模块建议使用该优先级
        /// </summary>
        Default = 500,
        /// <summary>
        /// 较低优先级，非核心模块建议使用该优先级
        /// </summary>
        Lower = 750,
        /// <summary>
        /// 最低优先级，框架辅助模块建议使用该优先级
        /// </summary>
        Lowest = 1000
    }

    public abstract class GameModuleBase<T> : IGameModule where T : GameModuleBase<T>
    {
        public virtual bool IsPersistent
        {
            get
            {
                var attr = GetType().GetCustomAttribute<ModuleLifecycleAttribute>();
                return attr?.Lifecycle is ModuleLifecycle.Persistent or ModuleLifecycle.EditorPersistent or ModuleLifecycle.RuntimePersistent;
            }
        }

        // 该变量名称和GameEntry有联系, 不要随意修改
        private static T m_instance;

        /// <summary>
        /// 获取由GameEntry管理的模块T
        /// </summary>
        public static T Instance
        {
            get
            {
                if (m_instance != null)
                {
                    return m_instance;
                }

                throw new XFrameworkException($"must load module before use it. ModuleName: {typeof(T).Name} use --> GameEntry.AddModule ");
            }
        }

        public virtual void Initialize() { }

        /// <summary>
        /// 关闭模块
        /// </summary> 
        public virtual void Shutdown() { }

        public virtual int Priority => (int)GameModulePriority.Default;
    }

    public abstract class GameModuleWithEvent<T> : GameModuleBase<T> where T : GameModuleWithEvent<T>
    {
        private readonly EventRegisterHelper registerHelper;

        protected GameModuleWithEvent()
        {
            registerHelper = EventRegisterHelper.Create(this);
            registerHelper.Register();
        }

        public override void Shutdown()
        {
            registerHelper.UnRegister();
        }
    }
    
    
    public abstract class MonoGameModuleBase<T> : GameModuleBase<T>, IMonoGameModule where T : MonoGameModuleBase<T>
    {
        /// <summary>
        /// 游戏框架模块轮询
        /// </summary>
        public virtual void Update() { }
    }
    
    public abstract class MonoGameModuleWithEvent<T> : MonoGameModuleBase<T> where T : MonoGameModuleWithEvent<T>
    {
        private readonly EventRegisterHelper registerHelper;

        protected MonoGameModuleWithEvent()
        {
            registerHelper = EventRegisterHelper.Create(this);
            registerHelper.Register();
        }

        public override void Shutdown()
        {
            registerHelper.UnRegister();
        }
    }
    
    
    
}