using XFramework.Event;

namespace XFramework
{
    public abstract class GameModuleBase<T> : IGameModule where T : GameModuleBase<T>
    {
        /// <summary>
        /// 模块优先级
        /// </summary>
        /// <remarks>优先级较高的模块会优先轮询</remarks>
        public abstract int Priority { get; }

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
        /// <summary>
        /// 游戏框架模块轮询
        /// </summary>
        public virtual void Update() { }
    }

    public abstract class GameModuleWithEvent<T> : GameModuleBase<T> where T : GameModuleBase<T>
    {
        private readonly EventRegersterHelper regersterHelper;

        public GameModuleWithEvent()
        {
            regersterHelper = EventRegersterHelper.Create(this);
            regersterHelper.Register();
        }

        public override void Shutdown()
        {
            regersterHelper.UnRegister();
        }
    }

    public abstract class GameModuleBaseNoTick
    {

    }
}