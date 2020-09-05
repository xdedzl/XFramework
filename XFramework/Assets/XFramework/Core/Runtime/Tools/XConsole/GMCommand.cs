using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class GMCommandBase
{
    public abstract int Order { get; }
    public abstract string TabName { get; }
}

public class XGMCommand : GMCommandBase
{
    public override int Order => -1;

    public override string TabName => "";

    [GMCommand]
    public static void Test1()
    {
        Debug.Log("hahaha");
    }

    [GMCommand]
    public static Vector3 Test2(string parm)
    {
        Debug.Log(parm);
        return Vector3.zero;
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class GMCommandAttribute : Attribute
{
    public string name;
    public string cmd;
    public int order;
    public GMCommandAttribute(string name = null, string cmd = null, int order = -1)
    {
        this.name = name;
        this.cmd = cmd;
        this.order = order;
    }
}