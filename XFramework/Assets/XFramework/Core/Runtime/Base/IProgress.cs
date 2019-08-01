namespace XFramework
{
    /// <summary>
    /// 进度
    /// </summary>
    public interface IProgress
    {
        bool IsDone { get; }
        float Progress { get; }
    }

    public class DefaultProgress : IProgress
    {
        public bool IsDone => true;

        public float Progress => 1;
    }
}