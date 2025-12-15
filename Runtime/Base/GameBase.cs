using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using XFramework;
using XFramework.Console;
using XFramework.Entity;
using XFramework.Fsm;
using XFramework.Json;
using XFramework.Resource;

/// <summary>
/// 这个类挂在初始场景中,是整个游戏的入口
/// </summary>
[DisallowMultipleComponent]
public class GameBase : MonoBehaviour
{
    // 初始流程
    public string startTypeName;
    public ProcedureBase startProcedure;

    public static GameBase activeGame { get; private set; }

    private void Awake()
    {
        var a = XApplication.Setting;
        
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

    private void OnInit()
    {
        GameEntry.AddModule<ResourceManager>();
        GameEntry.AddModule<EntityManager>();
        GameEntry.AddModule<FsmManager>();
        GameEntry.AddModule<SoundManager>();
        
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            Converters = { new Vector2IntConverter(), new Vector2Converter() },
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new WritablePropertiesOnlyResolver()
        };
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
            GameEntry.ClearAllModule(true);
        }
    }

    /// <summary>
    /// 初始化模块，这个应该放再各个流程中，暂时默认开始时初始化所有模块
    /// </summary>
    protected virtual void InitAllModel()
    {

    }

    private void OnValidate()
    {
        if (ProcedureManager.IsValid)
        {
            ProcedureManager.Instance.UpdateProcedure(startProcedure);
        }
    }
}
