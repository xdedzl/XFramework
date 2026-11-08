using System;
using UnityEngine.InputSystem;

namespace XFramework
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class InputContextDefinitionAttribute : Attribute
    {
        public InputContextDefinitionAttribute(string actionMapName, int priority, bool defaultActive = false)
        {
            ActionMapName = actionMapName;
            Priority = priority;
            DefaultActive = defaultActive;
        }

        public string ActionMapName { get; }
        public int Priority { get; }
        public bool DefaultActive { get; }
    }

    public abstract class InputContext : IDisposable
    {
        private InputActionMap m_ActionMap;

        public string ActionMapName { get; private set; }
        public int Priority { get; private set; }
        public bool IsActive { get; internal set; }

        internal void Initialize(InputActionAsset actions, InputContextDefinitionAttribute definition)
        {
            ActionMapName = definition.ActionMapName;
            Priority = definition.Priority;
            IsActive = definition.DefaultActive;
            m_ActionMap = actions.FindActionMap(ActionMapName, true);
            OnInitialize(m_ActionMap);
        }

        internal void Enable()
        {
            m_ActionMap.Enable();
            OnEnabled();
        }

        internal void Disable()
        {
            m_ActionMap.Disable();
            OnDisabled();
        }

        public void Dispose()
        {
            m_ActionMap?.Disable();
            OnDispose();
            m_ActionMap = null;
        }

        protected virtual void OnInitialize(InputActionMap actionMap)
        {
        }

        protected virtual void OnEnabled()
        {
        }

        protected virtual void OnDisabled()
        {
        }

        protected virtual void OnDispose()
        {
        }
    }
}
