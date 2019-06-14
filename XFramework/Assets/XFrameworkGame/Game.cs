using UnityEngine;
using XFramework;
using XFramework.Pool;
using XFramework.Tasks;
using XFramework.Net;

/// <summary>
/// 这个类挂在初始场景中,是整个游戏的入口
/// </summary>
public class Game : MonoBehaviour
{
    // 框架模块
    public static ProcedureManager ProcedureModule { get; private set; }
    public static FsmManager FsmModule { get; private set; }
    public static ObjectPoolManager PoolModule { get; private set; }
    public static GameObjectPoolManager GameObjPoolMoudle { get; private set; }
    public static ResourceManager ResModule { get; private set; }
    public static GraphicsManager GraphicsModule { get; private set; }
    public static DataSubjectManager ObserverModule { get; private set; }
    public static Messenger MessengerModule { get; private set; }
    public static TaskManager TaskModule { get; private set; }
    public static NetManager NetModule { get; private set; }


    // 业务模块
    public static UIHelper UIModule { get; private set; }

    // 初始流程
    public string TypeName;

    void Awake()
    {
        if (GameObject.FindObjectsOfType<Game>().Length > 1)
        {
            DestroyImmediate(this);
            return;
        }
        
        InitAllModel();
        Refresh();

        // 设置运行形后第一个进入的流程
        System.Type type = System.Type.GetType(TypeName);
        if (type != null)
            ProcedureModule.StartProcedure(type);
        else
            Debug.LogError("当前工程还没有任何流程");

        DontDestroyOnLoad(this);
    }

    void Update()
    {
        GameEntry.ModuleUpdate(Time.deltaTime, Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 初始化模块，这个应该放再各个流程中，暂时默认开始时初始化所有模块
    /// </summary>
    public void InitAllModel()
    {
        FsmModule = GameEntry.AddMoudle<FsmManager>();
        ProcedureModule = GameEntry.AddMoudle<ProcedureManager>();
        PoolModule = GameEntry.AddMoudle<ObjectPoolManager>();
        GameObjPoolMoudle = GameEntry.AddMoudle<GameObjectPoolManager>();
        ResModule = GameEntry.AddMoudle<ResourceManager>();
        GraphicsModule = GameEntry.AddMoudle<GraphicsManager>();
        ObserverModule = GameEntry.AddMoudle<DataSubjectManager>();
        MessengerModule = GameEntry.AddMoudle<Messenger>();
        TaskModule = GameEntry.AddMoudle<TaskManager>();
        NetModule = GameEntry.AddMoudle<NetManager>();

        UIModule = GameEntry.AddMoudle<UIHelper>();
    }

    /// <summary>
    /// 刷新静态引用
    /// </summary>
    public void Refresh()
    {
        FsmModule = GameEntry.GetModule<FsmManager>();
        ProcedureModule = GameEntry.GetModule<ProcedureManager>();
        PoolModule = GameEntry.GetModule<ObjectPoolManager>();
        GameObjPoolMoudle = GameEntry.GetModule<GameObjectPoolManager>();
        ResModule = GameEntry.GetModule<ResourceManager>();
        GraphicsModule = GameEntry.GetModule<GraphicsManager>();
        ObserverModule = GameEntry.GetModule<DataSubjectManager>();
        MessengerModule = GameEntry.GetModule<Messenger>();
        TaskModule = GameEntry.GetModule<TaskManager>();
        NetModule = GameEntry.GetModule<NetManager>();

        UIModule = GameEntry.GetModule<UIHelper>();
    }

    public static void ShutdownModule<T>() where T : IGameModule
    {
        GameEntry.ShutdownModule<T>();
    }

    public static void StartModule<T>() where T : IGameModule
    {
        GameEntry.AddMoudle<T>();
    }
}
