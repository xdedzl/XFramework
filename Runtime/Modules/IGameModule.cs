namespace XFramework
{
    /// <summary>
    /// 模块接口
    /// </summary>
    public interface IGameModule
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
        /// <summary>
        /// 关闭模块
        /// </summary> 
        void Shutdown();
    }
}