using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Fsm
{
    /// <summary>
    /// 新一代状态机管理器：全局 FSM 驱动 + 调试注册中心。
    /// </summary>
    [ModuleLifecycle(ModuleLifecycle.Persistent)]
    public class FsmManager : MonoGameModuleBase<FsmManager>
    {
        private sealed class FsmRecord
        {
            public string Key;
            public FsmScope Scope;
            public UnityEngine.Object Owner;
            public IFsmInspectable Inspectable;
            public Action DisposeHandler;
            public Action UpdateHandler;
            public Action ManualDisposeHandler;
        }

        private readonly Dictionary<string, FsmRecord> m_Records = new Dictionary<string, FsmRecord>();
        private readonly List<string> m_UpdateKeys = new List<string>();
        private readonly List<FsmDebugEntry> m_DebugEntries = new List<FsmDebugEntry>();

        public int Count => m_Records.Count;

        public override int Priority => (int)GameModulePriority.Highest;

        public override void Initialize()
        {
            base.Initialize();
            FsmClock.Configure(() => Time.frameCount, () => Time.realtimeSinceStartup);
        }

        public Fsm<TContext> CreateGlobalFsm<TContext>(string key, TContext context, bool autoStart = false)
        {
            ValidateKey(key);

            if (m_Records.ContainsKey(key))
            {
                throw new XFrameworkException($"[FSM] duplicate registration key: {key}");
            }

            var fsm = new Fsm<TContext>(context, key, autoStart);
            Register(key, fsm, FsmScope.Global, null, fsm.Update, fsm.Dispose);
            return fsm;
        }

        public IFsmInspectable GetGlobalFsm(string key)
        {
            if (!TryGetGlobalFsm(key, out IFsmInspectable fsm))
            {
                throw new XFrameworkException($"[FSM] global fsm not found: {key}");
            }

            return fsm;
        }

        public bool TryGetGlobalFsm(string key, out IFsmInspectable fsm)
        {
            if (m_Records.TryGetValue(key, out FsmRecord record) && record.Scope == FsmScope.Global)
            {
                fsm = record.Inspectable;
                return true;
            }

            fsm = null;
            return false;
        }

        public void RegisterInstance(string key, IFsmInspectable fsm, UnityEngine.Object owner = null)
        {
            ValidateKey(key);

            if (fsm == null)
            {
                throw new XFrameworkException("[FSM] registered instance can not be null");
            }

            if (m_Records.ContainsKey(key))
            {
                throw new XFrameworkException($"[FSM] duplicate registration key: {key}");
            }

            if (fsm.IsDisposed)
            {
                throw new XFrameworkException($"[FSM] can not register disposed fsm: {key}");
            }

            Register(key, fsm, FsmScope.Instance, owner, CreateUpdateHandler(fsm), CreateDisposeHandler(fsm));
        }

        public void Unregister(string key)
        {
            if (!m_Records.TryGetValue(key, out FsmRecord record))
            {
                return;
            }

            if (record.Inspectable is IFsmDisposableNotifier disposableNotifier && record.DisposeHandler != null)
            {
                disposableNotifier.Disposed -= record.DisposeHandler;
            }

            m_Records.Remove(key);
        }

        public IReadOnlyList<FsmDebugEntry> GetDebugEntries()
        {
            m_DebugEntries.Clear();
            foreach (KeyValuePair<string, FsmRecord> pair in m_Records.OrderBy(pair => pair.Key))
            {
                m_DebugEntries.Add(new FsmDebugEntry(pair.Key, pair.Value.Scope, pair.Value.Owner, pair.Value.Inspectable));
            }

            return m_DebugEntries;
        }

        public override void Update()
        {
            m_UpdateKeys.Clear();
            foreach (KeyValuePair<string, FsmRecord> pair in m_Records)
            {
                if (pair.Value.UpdateHandler != null)
                {
                    m_UpdateKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < m_UpdateKeys.Count; i++)
            {
                if (m_Records.TryGetValue(m_UpdateKeys[i], out FsmRecord record))
                {
                    record.UpdateHandler?.Invoke();
                }
            }
        }

        public override void Shutdown()
        {
            string[] keys = m_Records.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                if (!m_Records.TryGetValue(keys[i], out FsmRecord record))
                {
                    continue;
                }

                if (record.Scope == FsmScope.Global)
                {
                    record.ManualDisposeHandler?.Invoke();
                }
                else
                {
                    Unregister(keys[i]);
                }
            }

            m_Records.Clear();
            m_UpdateKeys.Clear();
            m_DebugEntries.Clear();
            base.Shutdown();
        }

        private void Register(string key, IFsmInspectable fsm, FsmScope scope, UnityEngine.Object owner, Action updateHandler, Action disposeHandler)
        {
            var record = new FsmRecord
            {
                Key = key,
                Scope = scope,
                Owner = owner,
                Inspectable = fsm,
                UpdateHandler = updateHandler,
                ManualDisposeHandler = disposeHandler
            };

            if (fsm is IFsmDisposableNotifier notifier)
            {
                record.DisposeHandler = () => Unregister(key);
                notifier.Disposed += record.DisposeHandler;
            }

            m_Records.Add(key, record);
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new XFrameworkException("[FSM] registration key can not be empty");
            }
        }

        private static Action CreateUpdateHandler(IFsmInspectable fsm)
        {
            var method = fsm.GetType().GetMethod("Update", Type.EmptyTypes);
            if (method == null)
            {
                return null;
            }

            return () => method.Invoke(fsm, null);
        }

        private static Action CreateDisposeHandler(IFsmInspectable fsm)
        {
            if (fsm is IDisposable disposable)
            {
                return disposable.Dispose;
            }

            return null;
        }
    }
}
