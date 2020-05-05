using UnityEngine;
using XFramework.Fsm;

public class State1 : MouseState
{
    public override void Init()
    {
        Debug.Log("State1Init");
    }

    public override void OnEnter(params object[] parms)
    {
        Debug.Log("State1Enter");
    }

    public override void OnUpdate()
    {
        Debug.Log("State1Update");
    }

    public override void OnExit()
    {
        Debug.Log("State1Exit");
    }

    public override void OnLeftButtonDown()
    {
        Debug.Log("1_Left");
    }

    public override void OnRightButtonDown()
    {
        Debug.Log("1_Right");
    }
}

public class State2 : MouseState
{
    public override void Init()
    {
        Debug.Log("State2Init");
    }
    public override void OnEnter(params object[] parms)
    {
        Debug.Log("State2Enter");
    }

    public override void OnUpdate()
    {
        Debug.Log("State2Update");
    }

    public override void OnExit()
    {
        Debug.Log("State2Exit");
    }

    public override void OnLeftButtonDown()
    {
        Debug.Log("2_Left");
    }

    public override void OnRightButtonDown()
    {
        Debug.Log("2_Right");
    }
}



public class QQQFsm : Fsm<QQQState>
{

}
public class QQQState : FsmState
{

}

public class QQQ1 : QQQState
{
    public override void Init()
    {
        Debug.Log("QQQ1Init");
    }
    public override void OnEnter(params object[] parms)
    {
        Debug.Log("QQQ1Enter");
    }

    public override void OnUpdate()
    {
        Debug.Log("QQQ1Update");
    }

    public override void OnExit()
    {
        Debug.Log("QQQ1Exit");
    }
}

public class QQQ2 : QQQState
{
    public override void Init()
    {
        Debug.Log("QQQ2Init");
    }
    public override void OnEnter(params object[] parms)
    {
        Debug.Log("QQQ2Enter");
    }

    public override void OnUpdate()
    {
        Debug.Log("QQQ2Update");
    }

    public override void OnExit()
    {
        Debug.Log("QQQ2Exit");
    }
}