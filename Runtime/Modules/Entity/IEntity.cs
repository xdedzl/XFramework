namespace XFramework.Entity
{
    public interface IEntity
    {
        /// <summary>
        /// 初始化
        /// </summary>
        void OnInit();
        /// <summary>
        /// 每帧运行
        /// </summary>
        /// <param name="elapseSeconds">逻辑运行时间</param>
        /// <param name="realElapseSeconds">真实运行时间</param>
        void OnUpdate(float elapseSeconds, float realElapseSeconds);
    }
}