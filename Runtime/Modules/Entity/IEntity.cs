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
        void OnUpdate();
    }
}