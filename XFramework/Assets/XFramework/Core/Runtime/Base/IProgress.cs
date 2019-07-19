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
}