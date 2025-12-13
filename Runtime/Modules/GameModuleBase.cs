using XFramework.Event;

namespace XFramework
{
    public abstract class GameModuleBase<T> : IGameModule where T : GameModuleBase<T>
    {
        public virtual bool IsPersistent => false;

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
        private readonly EventRegersterHelper registerHelper;

        protected GameModuleWithEvent()
        {
            registerHelper = EventRegersterHelper.Create(this);
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
        private readonly EventRegersterHelper registerHelper;

        protected MonoGameModuleWithEvent()
        {
            registerHelper = EventRegersterHelper.Create(this);
            registerHelper.Register();
        }

        public override void Shutdown()
        {
            registerHelper.UnRegister();
        }
    }
    
    
    /// <summary>
    /// 持久化模块基类, 一但创建就不可以被销毁
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class PersistentGameModuleBase<T> : GameModuleBase<T> where T : GameModuleBase<T>
    {
        public override bool IsPersistent => true;
    }
    
    public abstract class PersistentMonoGameModuleBase<T> : PersistentGameModuleBase<T>, IMonoGameModule where T : PersistentGameModuleBase<T>
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
}