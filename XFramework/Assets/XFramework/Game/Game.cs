using UnityEngine;
using XFramework;
using XFramework.Draw;
using XFramework.Entity;
using XFramework.Event;
using XFramework.Fsm;
using XFramework.Pool;
using XFramework.Resource;

/// <summary>
/// 这个类挂在初始场景中,是整个游戏的入口
/// StartX和EndX为刷新代码时的标志位
/// </summary>
public class Game : MonoBehaviour
{
    // 框架模块
    // Start0
    public static EntityManager EntityModule { get; private set; }
    public static FsmManager FsmModule { get; private set; }
    public static GraphicsManager GraphicsModule { get; private set; }
    public static ResourceManager ResModule { get; private set; }
    public static DataSubjectManager ObserverModule { get; private set; }
    public static MessageManager MessageModule { get { return MessageManager.Instance; } }
    public static ProcedureManager ProcedureModule { get { return ProcedureManager.Instance; } }
    public static ObjectPoolManager ObjectPool { get { return ObjectPoolManager.Instance; } }
    // End0

    // 框架扩展模块
    // Start1
    public static UIHelper UIModule { get; private set; }
    public static MeshManager MeshModule { get; private set; }
    // End1

    // 初始流程
    public string TypeName;

    public static Game activeGame { get; private set; }

    void Awake()
    {
        if (activeGame != null)
        {
            DestroyImmediate(this);
        }
        else
        {
            activeGame = this;
        }

        InitAllModel();
        Refresh();

        // 设置运行形后第一个进入的流程
        System.Type type = System.Type.GetType(TypeName);
        if (type != null)
        {
            ProcedureModule.ChangeProcedure(type);

            ProcedureBase procedure = ProcedureModule.CurrentProcedure;
            DeSerialize(procedure);
        }
        else
            Debug.LogError("当前工程还没有任何流程");

        DontDestroyOnLoad(this);
    }

    void Update()
    {
        GameEntry.ModuleUpdate(Time.deltaTime, Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        if (activeGame == this)
        {
            GameEntry.CleraAllModule();
        }
    }

    /// <summary>
    /// 初始化模块，这个应该放再各个流程中，暂时默认开始时初始化所有模块
    /// </summary>
    public void InitAllModel()
    {
        // Start2
        EntityModule = GameEntry.AddModule<EntityManager>();
        FsmModule = GameEntry.AddModule<FsmManager>();
        GraphicsModule = GameEntry.AddModule<GraphicsManager>();
        ObserverModule = GameEntry.AddModule<DataSubjectManager>();
#if UNITY_EDITOR
        ResModule = GameEntry.AddModule<ResourceManager>(new AssetDataBaseLoadHelper());
#else
        ResModule = GameEntry.AddModule<ResourceManager>(new AssetBundleLoadHelper());
#endif
        UIModule = GameEntry.AddModule<UIHelper>();
        MeshModule = GameEntry.AddModule<MeshManager>();
        // End2
    }

    /// <summary>
    /// 刷新静态引用
    /// </summary>
    public void Refresh()
    {
        // Start3
        EntityModule = GameEntry.GetModule<EntityManager>();
        FsmModule = GameEntry.GetModule<FsmManager>();
        GraphicsModule = GameEntry.GetModule<GraphicsManager>();
        ObserverModule = GameEntry.GetModule<DataSubjectManager>();
        UIModule = GameEntry.GetModule<UIHelper>();
        MeshModule = GameEntry.GetModule<MeshManager>();
        ResModule = GameEntry.GetModule<ResourceManager>();
        // End3
    }

    public static void ShutdownModule<T>() where T : IGameModule
    {
        GameEntry.ShutdownModule<T>();
    }

    public static void StartModule<T>() where T : IGameModule
    {
        GameEntry.AddModule<T>();
    }

    /// <summary>
    /// 根据存储的byte数值给流程赋值
    /// </summary>
    /// <param name="procedure"></param>
    public void DeSerialize(ProcedureBase procedure)
    {
        System.Type type = procedure.GetType();
        string path = Application.persistentDataPath + "/" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "Procedure/" + type.Name;
        if (!System.IO.File.Exists(path))
            return;

        ProtocolBytes p = new ProtocolBytes(System.IO.File.ReadAllBytes(path));

        if (p.GetString() != type.Name)
        {
            Debug.LogError("类型不匹配");
            return;
        }

        p.DeSerialize(procedure);
    }
}
