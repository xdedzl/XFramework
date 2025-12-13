namespace XFramework
{
    /// <summary>
    /// 模块接口
    /// </summary>
    public interface IGameModule
    {
        /// <summary>
        /// 关闭模块
        /// </summary> 
        void Shutdown();
        /// <summary>
        /// 模块是否为持久化模块
        /// </summary>
        bool IsPersistent{ get; }
    }
    
    /// <summary>
    /// 有Mono Update的模块
    /// </summary>
    public interface IMonoGameModule : IGameModule
    {
        /// <summary>
        /// 模块优先级
        /// </summary>
        /// <remarks>优先级较高的模块会优先轮询</remarks>
        int Priority{ get; }
        /// <summary>
        /// 游戏框架模块轮询
        /// </summary>
        void Update();
    }
}