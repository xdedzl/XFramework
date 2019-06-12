using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

public class RuntimeHandleDemo : ProcedureBase
{
    public override void Init()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "target";
        obj.transform.position = Vector3.zero;

        GameObject.CreatePrimitive(PrimitiveType.Plane);


        GameObject camera = Camera.main.gameObject;
        camera.transform.position = new Vector3(-2, 6, -5);
        camera.transform.eulerAngles = new Vector3(56, 25, 0);
        camera.AddComponent<RuntimeHandle>();
        RuntimeHandle.SetTarget(obj.transform);
    }
}
