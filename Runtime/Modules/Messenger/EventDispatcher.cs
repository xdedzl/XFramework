using System;

/// <summary>
/// 事件分发处理类
/// 这是个基类
/// </summary>
public class EventDispatcher
{    
    private event Action<object, EventArgs> EventListener;

    /// <summary>
    /// 分发消息
    /// </summary>    
    /// <param name="eventType"></param>
    /// <param name="data"></param>
    public void DispatchEvent(EventDispatchType eventType, object data = null)
    {
        EventListener?.Invoke(this, new EventArgs(eventType, data));
    }

    /// <summary>
    /// 注册监听
    /// </summary>
    public void RegistEvent(Action<object, EventArgs> fuc)
    {
        // 防止重复添加事件
        EventListener -= fuc;
        EventListener += fuc;
    }

    /// <summary>
    /// 注销监听
    /// </summary>
    public void UnRegistEvent(Action<object, EventArgs> fuc)
    {
        EventListener -= fuc;
    }
}

/// <summary>
/// 事件分发类型
/// </summary>
public enum EventDispatchType
{
    TIMER,                // 计时器运行帧
    TIME_RUNCHANGE,       // 计时器运行状态改变
}
