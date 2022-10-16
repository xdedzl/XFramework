using UnityEngine;
using UnityEngine.UI;
using XFramework;
using XFramework.Resource;

public class UIDemo : ProcedureBase
{
    private Button startBtn;

    public override void Init()
    {
        base.Init();

        GameObject obj = ResourceManager.Instance.Load<GameObject>("Assets/XFramework/Demo/UI/PanelPrefab/RootBtn.prefab");
        Transform trans = Object.Instantiate(obj, GameObject.Find("Canvas").transform).transform;
        startBtn = trans.GetComponent<Button>();
        Screen.SetResolution(1920, 1080, true);
        startBtn.onClick.AddListener(() =>
        {
            UIHelper.Instance.Open(UIName.Main, true);
        });
        trans.GetComponent<Image>().alphaHitTestMinimumThreshold = 0.1f;
    }

    public override void OnUpdate()
    {
        // 打开/关闭设置界面
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UIHelper.Instance.Open(UIName.Setting);
        }
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
        GUI.Label(new Rect(500, 0, 200, 80),
            "UI面板示例\n" +
            "Esc 弹出设置面板", style);
    }
}
