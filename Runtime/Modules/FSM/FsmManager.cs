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
            public FsmScope Scope;
            public UnityEngine.Object Owner;
            public IManagedFsm Fsm;
        }

        private readonly Dictionary<string, FsmRecord> m_Records = new ();
        private readonly List<string> m_UpdateKeys = new ();
        private readonly List<FsmDebugEntry> m_DebugEntries = new ();

        public int Count => m_Records.Count;

        public override int Priority => (int)GameModulePriority.Highest;

        public override void Initialize()
        {
            base.Initialize();
            FsmClock.Configure(() => Time.frameCount, () => Time.realtimeSinceStartup);
        }

        public Fsm<TContext> CreateGlobalFsm<TContext>(string key, TContext context, bool autoStart = false)
        {
            return CreateManagedFsm(key, context, FsmScope.Global, null, autoStart);
        }

        public Fsm<TContext> CreateInstanceFsm<TContext>(string key, TContext context, UnityEngine.Object owner = null, bool autoStart = false)
        {
            return CreateManagedFsm(key, context, FsmScope.Instance, owner, autoStart);
        }
        
        private Fsm<TContext> CreateManagedFsm<TContext>(string key, TContext context, FsmScope scope, UnityEngine.Object owner, bool autoStart)
        {
            ValidateKey(key);

            if (m_Records.ContainsKey(key))
            {
                throw new XFrameworkException($"[FSM] duplicate registration key: {key}");
            }

            var managedFsm = new Fsm<TContext>(context, key, autoStart);
            managedFsm.BindManager(this, key);
            Register(key, managedFsm, scope, owner);
            return (Fsm<TContext>)managedFsm;
        }

        
        private void Register(string key, IManagedFsm fsm, FsmScope scope, UnityEngine.Object owner)
        {
            var record = new FsmRecord
            {
                Scope = scope,
                Owner = owner,
                Fsm = fsm
            };

            m_Records.Add(key, record);
        }
        
        public void Unregister(string key)
        {
            if (!m_Records.ContainsKey(key))
            {
                return;
            }

            m_Records.Remove(key);
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
                fsm = record.Fsm;
                return true;
            }

            fsm = null;
            return false;
        }

        
        public IReadOnlyList<FsmDebugEntry> GetDebugEntries()
        {
            m_DebugEntries.Clear();
            foreach (KeyValuePair<string, FsmRecord> pair in m_Records.OrderBy(pair => pair.Key))
            {
                m_DebugEntries.Add(new FsmDebugEntry(pair.Key, pair.Value.Scope, pair.Value.Owner, pair.Value.Fsm));
            }

            return m_DebugEntries;
        }

        public override void Update()
        {
            m_UpdateKeys.Clear();
            foreach (KeyValuePair<string, FsmRecord> pair in m_Records)
            {
                m_UpdateKeys.Add(pair.Key);
            }

            for (int i = 0; i < m_UpdateKeys.Count; i++)
            {
                if (m_Records.TryGetValue(m_UpdateKeys[i], out FsmRecord record))
                {
                    record.Fsm.Update();
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
                    record.Fsm.Dispose();
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
        

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new XFrameworkException("[FSM] registration key can not be empty");
            }
        }
    }
}
