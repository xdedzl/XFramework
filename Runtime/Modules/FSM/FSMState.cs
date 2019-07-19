﻿/// <summary>
/// 状态基类，考虑要不要改为接口
/// </summary>
public abstract class FsmState
{
    public virtual void Init() { }

    public virtual void OnEnter() { }

    public virtual void OnUpdate() { }

    public virtual void OnExit() { }
}