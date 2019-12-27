using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFramework;
using XFramework.Draw;

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
        obj.AddComponent<RuntimeHandle>();
    }

    public override void OnEnter(params object[] parms)
    {
        MonoEvent.Instance.ONGUI += OnGUI;
    }

    public override void OnExit()
    {
        MonoEvent.Instance.ONGUI -= OnGUI;
    }

    public void OnGUI()
    {
        GUIStyle style = new GUIStyle
        {
            padding = new RectOffset(10, 10, 10, 10),
            fontSize = 15,
            fontStyle = FontStyle.Normal,
        };
        GUI.Label(new Rect(0, 0, 200, 80),
            "运行时手柄,操作和编辑器手柄类似", style);
    }
}
