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

                throw new XFrameworkException($"must load model before use it. ModuleName: {typeof(T).Name} use --> GameEntry.AddModule ");
            }
        }
        /// <summary>
        /// 关闭模块
        /// </summary> 
        public virtual void Shutdown() { }
        /// <summary>
        /// 游戏框架模块轮询
        /// </summary>
        /// <param name="elapseSeconds">逻辑运行时间，以秒为单位</param>
        /// <param name="realElapseSeconds">真实运行时间，以秒为单位</param>
        public virtual void Update(float elapseSeconds, float realElapseSeconds) { }
    }
}