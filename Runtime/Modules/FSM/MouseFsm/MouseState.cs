using UnityEngine;

public class MouseState : FsmState
{
    public virtual void OnLeftButtonDown() { }
    /// <summary>
    /// 左键保持按下状态
    /// </summary>
    public virtual void OnLeftButtonHold() { }
    /// <summary>
    /// 左键抬起
    /// </summary>
    public virtual void OnLeftButtonUp() { }

    /// <summary>
    /// 右键按下
    /// </summary>
    public virtual void OnRightButtonDown() { }
    /// <summary>
    /// 右键保持按下状态
    /// </summary>
    public virtual void OnRightButtonHold() { }
    /// <summary>
    /// 右键抬起
    /// </summary>
    public virtual void OnRightButtonUp() { }

    /// <summary>
    /// 右键按下
    /// </summary>
    public virtual void OnCenterButtonDown() { }
    /// <summary>
    /// 右键保持按下状态
    /// </summary>
    public virtual void OnCenterButtonHold() { }
    /// <summary>
    /// 右键抬起
    /// </summary>
    public virtual void OnCenterButtonUp() { }
}