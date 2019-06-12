using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class UIDemo : ProcedureBase
{
    private Button startBtn;

    public override void Init()
    {
        base.Init();

        GameObject obj = Resources.Load<GameObject>("UIPanelPrefabs/RootBtn");
        Transform trans = Object.Instantiate(obj, GameObject.Find("Canvas").transform).transform;
        startBtn = trans.GetComponent<Button>();
        Screen.SetResolution(1920, 1080, true);
        startBtn.onClick.AddListener(()=> 
        {
            Game.UIModule.Open(UIName.Main);
        });
        trans.GetComponent<Image>().alphaHitTestMinimumThreshold = 0.1f;
    }

    public override void OnUpdate()
    {
        // 打开/关闭设置界面
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Game.UIModule.Open(UIName.Setting);
        }
    }
}
