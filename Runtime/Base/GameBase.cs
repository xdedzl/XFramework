using System.Reflection;
using UnityEngine;
using XFramework;
using XFramework.Console;

/// <summary>
/// 这个类挂在初始场景中,是整个游戏的入口
/// </summary>
public class GameBase : MonoBehaviour
{
    // 初始流程
    public string startTypeName;
    public ProcedureBase startProcedure;
    [SerializeField]
    private XFrameworkSetting _setting;

    public static GameBase activeGame { get; private set; }

    public static XFrameworkSetting Setting => activeGame._setting;

    private void Awake()
    {
        if(_setting == null)
        {
            _setting = Resources.Load<XFrameworkSetting>("XFramework/XFrameworkSetting");
        }

        if (activeGame != null)
        {
            DestroyImmediate(this);
            return;
        }
        else
        {
            activeGame = this;
        }

        DontDestroyOnLoad(this);

        OnInit();

        InitAllModel();

        EnterFirstProcedure();
    }

    protected virtual void OnInit()
    {

    }

    private void EnterFirstProcedure()
    {
#if UNITY_EDITOR
        EnterFirstProcedure_Editor();
#else
        EnterFirstProcedure_Runtime();
#endif
    }

    protected virtual void EnterFirstProcedure_Editor()
    {
        // 设置运行后第一个进入的流程
        if (startProcedure != null)
        {
            if (startProcedure.GetType().Name == startTypeName)
            {
                ProcedureManager.Instance.UpdateProcedure(startProcedure);
                ProcedureManager.Instance.ChangeProcedure(startProcedure.GetType());
            }
            else
            {
                ProcedureManager.Instance.ChangeProcedure(GetAssembly().GetType(startTypeName));
            }
        }
        else
        {
            if(!string.IsNullOrEmpty(startTypeName))
            {
                var type = GetAssembly().GetType(startTypeName);
                if (type is not null)
                {
                    ProcedureManager.Instance.ChangeProcedure(type);
                }
                else
                {
                    Debug.LogError($"没有流程 {startTypeName}");
                }
            }
            else
            {
                Debug.LogError("Game还没有设置初始流程");
            }
        }
    }

    protected virtual void EnterFirstProcedure_Runtime()
    {
        // 设置运行后第一个进入的流程
        if (startProcedure != null)
        {
            if (startProcedure.GetType().Name == startTypeName)
            {
                ProcedureManager.Instance.UpdateProcedure(startProcedure);
                ProcedureManager.Instance.ChangeProcedure(startProcedure.GetType());
            }
            else
            {
                ProcedureManager.Instance.ChangeProcedure(GetAssembly().GetType(startTypeName));
            }
        }
        else
            Debug.LogError("当前工程还没有任何流程");
    }

    protected virtual Assembly GetAssembly()
    {
        return Assembly.Load("Assembly-CSharp"); ;
    }

    protected virtual void Update()
    {
        GameEntry.ModuleUpdate();
    }

    public void OnGUI()
    {
        if (GUI.Button(new Rect(10, Screen.height - 60, 100, 50), "调试"))
        {
            XConsole.IsOpen = !XConsole.IsOpen;
        }

        string buttonName = XConsole.IsHunterEnable ? "关闭Hunter" : "打开Hunter";
        if (GUI.Button(new Rect(120, Screen.height - 60, 100, 50), buttonName))
        {
            if (XConsole.IsHunterEnable)
            {
                XConsole.Execute("disable_hunter");
            }
            else
            {
                XConsole.Execute("enable_hunter");
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (activeGame == this)
        {
            ProcedureManager.Instance.ChangeProcedure(null);
            GameEntry.CleraAllModule();
        }
    }

    /// <summary>
    /// 初始化模块，这个应该放再各个流程中，暂时默认开始时初始化所有模块
    /// </summary>
    protected virtual void InitAllModel()
    {
        // Start2
        //GameEntry.AddModule<EntityManager>();
        //GameEntry.AddModule<FsmManager>();

//#if UNITY_EDITOR
//        string mapInfoPath = $"{Application.streamingAssetsPath}/pathMap.info";
//        ResourceManager.GeneratePathMap(mapInfoPath, "Assets/Res");
//        GameEntry.AddModule<ResourceManager>(new AssetDataBaseLoadHelper());
//#else
//        GameEntry.AddModule<ResourceManager>(new AssetBundleLoadHelper(), mapInfoPath);
//#endif
        //GameEntry.AddModule<UIHelper>();
        // End2
    }

    private void OnValidate()
    {
        if (ProcedureManager.IsValid)
        {
            ProcedureManager.Instance.UpdateProcedure(startProcedure);
        }
    }
}
