using UnityEngine;

using System;

namespace XFramework.UI
{
    public interface IUIEventSource
    {
        Type ListenerType { get; }
        void AddListener(Delegate listener);
    }

    public interface IUIMultiEventSource : IUIEventSource
    {
        Type GetListenerType(string eventName);
        void AddListener(string eventName, Delegate listener);
    }

    public abstract class XUIBase : MonoBehaviour, IComponentKeyProvider
    {
        [SerializeField] private string searchKey = "";
        public string Key => searchKey;
    }
}
