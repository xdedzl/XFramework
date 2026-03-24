using System.Reflection;
using XFramework.Event;

namespace XFramework
{
    public abstract class GameModuleBase<T> : IGameModule where T : GameModuleBase<T>
    {
        public virtual bool IsPersistent
        {
            get
            {
                var attr = GetType().GetCustomAttribute<ModuleLifecycleAttribute>();
                if (attr == null)
                {
                    return false;
                }

                return attr.Lifecycle == ModuleLifecycle.Persistent || attr.Lifecycle == ModuleLifecycle.EditorPersistent;
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
        /// <summary>
        /// 关闭模块
        /// </summary> 
        public virtual void Shutdown() { }
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
        /// 模块优先级
        /// </summary>
        /// <remarks>优先级较高的模块会优先轮询</remarks>
        public abstract int Priority { get; }
        /// <summary>
        /// 游戏框架模块轮询
        /// </summary>
        public virtual void Update() { }
    }
    
    public abstract class MonoGameModuleWithEvent<T> : GameModuleBase<T> where T : MonoGameModuleWithEvent<T>
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