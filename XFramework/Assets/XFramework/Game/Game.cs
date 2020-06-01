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
    public static EntityManager EntityModule { get { return EntityManager.Instance; } }
    public static FsmManager FsmModule { get { return FsmManager.Instance; } }
    public static GraphicsManager GraphicsModule { get { return GraphicsManager.Instance; } }
    public static ResourceManager ResModule { get { return ResourceManager.Instance; } }
    public static DataSubjectManager ObserverModule { get { return DataSubjectManager.Instance; } }
    public static MessageManager MessageModule { get { return MessageManager.Instance; } }
    public static ProcedureManager ProcedureModule { get { return ProcedureManager.Instance; } }
    public static ObjectPoolManager ObjectPool { get { return ObjectPoolManager.Instance; } }
    // End0

    // 框架扩展模块
    // Start1
    public static UIHelper UIModule { get { return UIHelper.Instance; } }
    public static MeshManager MeshModule { get { return MeshManager.Instance; } }
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
        GameEntry.AddModule<EntityManager>();
        GameEntry.AddModule<FsmManager>();
        GameEntry.AddModule<GraphicsManager>();
        GameEntry.AddModule<DataSubjectManager>();
#if UNITY_EDITOR
        GameEntry.AddModule<ResourceManager>(new AssetDataBaseLoadHelper());
#else
        GameEntry.AddModule<ResourceManager>(new AssetBundleLoadHelper());
#endif
        GameEntry.AddModule<UIHelper>();
        GameEntry.AddModule<MeshManager>();
        // End2
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
