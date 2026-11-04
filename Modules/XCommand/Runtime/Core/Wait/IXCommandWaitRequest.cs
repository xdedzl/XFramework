namespace XFramework.Command
{
    /// <summary>跨帧等待请求契约：请求自身实现校验、逐帧评估与回执生成，统一入口负责超时、取消与输出。</summary>
    public interface IXCommandWaitRequest
    {
        /// <summary>等待的实时秒数预算；小于等于 0 视为参数错误。</summary>
        float TimeoutSeconds { get; }

        /// <summary>是否使用紧凑 JSON 输出。</summary>
        bool Compact { get; }

        /// <summary>超时异常与失败回执使用的错误文案。</summary>
        string TimeoutMessage { get; }

        /// <summary>执行请求级校验，并重置本次等待的运行时状态。</summary>
        void Validate();

        /// <summary>在当前帧评估条件是否满足。</summary>
        bool Evaluate();

        /// <summary>生成回执 JSON，成功与超时都必须带 success 与 error 字段。</summary>
        string CreateOutput(bool success, float elapsedSeconds);
    }
}
