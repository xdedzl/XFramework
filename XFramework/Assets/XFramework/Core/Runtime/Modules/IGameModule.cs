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
        /// <param name="elapseSeconds">逻辑运行时间，以秒为单位</param>
        /// <param name="realElapseSeconds">真实运行时间，以秒为单位</param>
        void Update(float elapseSeconds, float realElapseSeconds);
        /// <summary>
        /// 关闭模块
        /// </summary> 
        void Shutdown();
    }
}