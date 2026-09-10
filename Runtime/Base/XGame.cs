using System;
using System.Reflection;
using UnityEngine;
using XAnimationEngine;
using XFramework.Command;
using XFramework.Json;

namespace XFramework
{
    /// <summary>
    /// 这个类挂在初始场景中,是整个游戏的入口
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(XFrameworkConst.GameExecutionOrder)]
    public class XGame : MonoBehaviour
    {
        // 初始流程
        [HideInInspector] public string startTypeName;
        public MainProcedure startProcedure;

        private static GameObject m_MainPlayer;

        public static XGame activeGame { get; private set; }

        /// <summary>
        /// 玩家当前在世界中的主要对象。未显式设置时返回主相机对象。
        /// </summary>
        public static GameObject MainPlayer => m_MainPlayer != null ? m_MainPlayer : Camera.main?.gameObject;

        /// <summary>
        /// 设置玩家当前在世界中的主要对象。传入 null 时恢复使用主相机对象。
        /// </summary>
        public static void SetMainPlayer(GameObject mainPlayer)
        {
            m_MainPlayer = mainPlayer;
        }

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

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(this);
            }

            OnInit();

            EnterFirstProcedure();
        }

        private void OnInit()
        {
            XJson.SetUnityDefaultSetting();
            GameEntry.InitializeModules(ModuleLifecycle.Persistent, ModuleLifecycle.RuntimePersistent);
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
                XCommand.IsOpen = !XCommand.IsOpen;
            }

            string buttonName = XCommand.IsHunterEnable ? "关闭Hunter" : "打开Hunter";
            if (GUI.Button(new Rect(120, Screen.height - 60, 100, 50), buttonName))
            {
                if (XCommand.IsHunterEnable)
                {
                    XCommand.Execute("disable_hunter");
                }
                else
                {
                    XCommand.Execute("enable_hunter");
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (ReferenceEquals(activeGame, this))
            {
                ProcedureManager.Instance.ChangeProcedure(null);
                GameEntry.ClearAllModule(true);
                activeGame = null;
            }
        }

        private void OnValidate()
        {
            if (ProcedureManager.IsValid && startProcedure != null)
            {
                ProcedureManager.Instance.UpdateProcedure(startProcedure);
            }
        }
    }
}
