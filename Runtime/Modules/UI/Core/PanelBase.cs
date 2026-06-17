using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Event;
using System.Reflection;
using JetBrains.Annotations;

namespace XFramework.UI
{
    public interface IPanelOpenRequest
    {
    }

    public interface PanelBaseWithRequest
    {
        Type RequestType { get; }
        void OpenRequestObject(object request);
    }

    /// <summary>
    /// Marks a PanelBase field to be filled from the panel's XUIBase lookup table.
    /// This is a UI object reference helper, not a data binding attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class UIRefAttribute : Attribute
    {
        public UIRefAttribute()
        {
        }

        public UIRefAttribute(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }

    /// <summary>
    /// Marks a PanelBase method to be bound to an XUIBase event source during panel initialization.
    /// </summary>
    [MeansImplicitUse(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.Itself)]
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class UIListenerAttribute : Attribute
    {
        public UIListenerAttribute()
        {
        }

        public UIListenerAttribute(string key)
        {
            Key = key;
        }

        public UIListenerAttribute(string key, string eventName)
        {
            Key = key;
            EventName = eventName;
        }

        public string Key { get; }
        public string EventName { get; }
    }

    /// <summary>
    /// 面板基类
    /// </summary>
    [DisallowMultipleComponent]
    public class PanelBase : MonoBehaviour, IComponentFindIgnore
    {
        /// <summary>
        /// UI层级,层级最低地显示在底层
        /// </summary>
        public int Level => GetType().GetCustomAttribute<PanelInfoAttribute>().level;

        /// <summary>
        /// 面板名(全局唯一)
        /// </summary>
        public string PanelName => GetType().GetCustomAttribute<PanelInfoAttribute>().name;

        /// <summary>
        /// 面板显示名
        /// </summary>
        public string PanelShowName => GetType().GetCustomAttribute<PanelInfoAttribute>().showName;
        
        /// <summary>
        /// 面板路径
        /// </summary>
        public string PanelPath => GetType().GetCustomAttribute<PanelInfoAttribute>().path;

        protected RectTransform rect;

        /// <summary>
        /// 控制面板显隐，子类可覆写以支持 UI Toolkit 等非 GameObject 方式
        /// </summary>
        public virtual void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// 面板是否可见
        /// </summary>
        public virtual bool IsVisible => gameObject.activeSelf;

        private List<SubPanelBase> m_SubPanels;

        private ComponentFindHelper<XUIBase> m_ComponentFindHelper;

        private EventRegisterHelper _registerHelper;

        /// <summary>
        /// 面板初始化，只会执行一次，在Awake后start前执行
        /// </summary>
        internal void Init()
        {
            m_ComponentFindHelper = ComponentFindHelper<XUIBase>.CreateHelper(this.gameObject);
            _registerHelper = EventRegisterHelper.Create(this);
            rect = transform.GetComponent<RectTransform>();
            Vector3 rectSize = rect.localScale;
            rect.localScale = rectSize;

            InjectUIRefs();
            BindUIListeners();
            OnInit();
        }

        private void InjectUIRefs()
        {
            Type panelType = GetType();
            for (Type type = panelType; type != null && type != typeof(PanelBase); type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    UIRefAttribute attribute = field.GetCustomAttribute<UIRefAttribute>();
                    if (attribute == null)
                    {
                        continue;
                    }

                    InjectUIRefField(panelType, field, attribute);
                }
            }
        }

        private void InjectUIRefField(Type panelType, FieldInfo field, UIRefAttribute attribute)
        {
            if (field.IsStatic || field.IsInitOnly)
            {
                throw new XFrameworkException(
                    $"[UIRef] {panelType.Name}.{field.Name} cannot be static or readonly.");
            }

            string key = GetUIRefKey(field, attribute);
            XUIBase uiRef = GetUIRefComponent(panelType, field, key);
            object value = GetUIRefValue(panelType, field, key, uiRef);
            field.SetValue(this, value);
        }

        private XUIBase GetUIRefComponent(Type panelType, FieldInfo field, string key)
        {
            try
            {
                return m_ComponentFindHelper[key];
            }
            catch (Exception exception)
            {
                throw new XFrameworkException(
                    $"[UIRef] Failed to find UI reference. Panel: {panelType.Name}, Field: {field.Name}, Key: {key}, Expected: {field.FieldType.Name}. {exception.Message}");
            }
        }

        private static object GetUIRefValue(Type panelType, FieldInfo field, string key, XUIBase uiRef)
        {
            Type fieldType = field.FieldType;
            if (fieldType.IsAssignableFrom(uiRef.GetType()))
            {
                return uiRef;
            }

            if (fieldType == typeof(GameObject))
            {
                return uiRef.gameObject;
            }

            if (typeof(Component).IsAssignableFrom(fieldType))
            {
                Component component = uiRef.GetComponent(fieldType);
                if (component != null)
                {
                    return component;
                }
            }

            throw new XFrameworkException(
                $"[UIRef] UI reference type mismatch. Panel: {panelType.Name}, Field: {field.Name}, Key: {key}, Expected: {fieldType.Name}, Actual: {uiRef.GetType().Name}.");
        }

        private static string GetUIRefKey(FieldInfo field, UIRefAttribute attribute)
        {
            if (!string.IsNullOrWhiteSpace(attribute.Key))
            {
                return attribute.Key;
            }

            string key = field.Name;
            if (key.StartsWith("m_", StringComparison.Ordinal))
            {
                key = key.Substring(2);
            }
            else if (key.StartsWith("_", StringComparison.Ordinal))
            {
                key = key.Substring(1);
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new XFrameworkException($"[UIRef] Cannot infer UI reference key from field {field.Name}.");
            }

            return char.ToUpperInvariant(key[0]) + key.Substring(1);
        }

        private void BindUIListeners()
        {
            Type panelType = GetType();
            for (Type type = panelType; type != null && type != typeof(PanelBase); type = type.BaseType)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (MethodInfo method in methods)
                {
                    UIListenerAttribute attribute = method.GetCustomAttribute<UIListenerAttribute>(true);
                    if (attribute == null)
                    {
                        continue;
                    }

                    BindUIListenerMethod(panelType, method, attribute);
                }
            }
        }

        private void BindUIListenerMethod(Type panelType, MethodInfo method, UIListenerAttribute attribute)
        {
            if (method.IsStatic)
            {
                throw new XFrameworkException(
                    $"[UIListener] {panelType.Name}.{method.Name} cannot be static.");
            }

            string key = GetUIListenerKey(method, attribute);
            XUIBase uiRef = GetUIListenerComponent(panelType, method, key);
            if (uiRef is not IUIEventSource eventSource)
            {
                throw new XFrameworkException(
                    $"[UIListener] UI reference does not support listener binding. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, Actual: {uiRef.GetType().Name}.");
            }

            string eventName = attribute.EventName;
            Type listenerType = GetUIListenerType(panelType, method, key, eventName, uiRef, eventSource);
            ValidateUIListenerMethod(panelType, method, key, eventName, listenerType);
            Delegate listener = Delegate.CreateDelegate(listenerType, this, method);
            AddUIListener(panelType, method, key, eventName, uiRef, eventSource, listener);
        }

        private static Type GetUIListenerType(
            Type panelType,
            MethodInfo method,
            string key,
            string eventName,
            XUIBase uiRef,
            IUIEventSource eventSource)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return eventSource.ListenerType;
            }

            if (uiRef is not IUIMultiEventSource multiEventSource)
            {
                throw new XFrameworkException(
                    $"[UIListener] UI reference does not support named listener binding. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, EventName: {eventName}, Actual: {uiRef.GetType().Name}.");
            }

            Type listenerType = multiEventSource.GetListenerType(eventName);
            if (listenerType == null)
            {
                throw new XFrameworkException(
                    $"[UIListener] Unsupported UI listener event. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, EventName: {eventName}, Actual: {uiRef.GetType().Name}.");
            }

            return listenerType;
        }

        private static void AddUIListener(
            Type panelType,
            MethodInfo method,
            string key,
            string eventName,
            XUIBase uiRef,
            IUIEventSource eventSource,
            Delegate listener)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                eventSource.AddListener(listener);
                return;
            }

            if (uiRef is not IUIMultiEventSource multiEventSource)
            {
                throw new XFrameworkException(
                    $"[UIListener] UI reference does not support named listener binding. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, EventName: {eventName}, Actual: {uiRef.GetType().Name}.");
            }

            multiEventSource.AddListener(eventName, listener);
        }

        private XUIBase GetUIListenerComponent(Type panelType, MethodInfo method, string key)
        {
            try
            {
                return m_ComponentFindHelper[key];
            }
            catch (Exception exception)
            {
                throw new XFrameworkException(
                    $"[UIListener] Failed to find UI listener target. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}. {exception.Message}");
            }
        }

        private static void ValidateUIListenerMethod(Type panelType, MethodInfo method, string key, string eventName, Type listenerType)
        {
            if (listenerType == null || !typeof(Delegate).IsAssignableFrom(listenerType))
            {
                throw new XFrameworkException(
                    $"[UIListener] Invalid listener type. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, ListenerType: {listenerType?.Name ?? "null"}.");
            }

            MethodInfo invokeMethod = listenerType.GetMethod("Invoke");
            if (invokeMethod == null)
            {
                throw new XFrameworkException(
                    $"[UIListener] Invalid listener type. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, ListenerType: {listenerType.Name}.");
            }

            if (method.ReturnType != invokeMethod.ReturnType)
            {
                throw new XFrameworkException(
                    $"[UIListener] Method return type mismatch. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, Expected: {invokeMethod.ReturnType.Name}, Actual: {method.ReturnType.Name}.");
            }

            ParameterInfo[] methodParameters = method.GetParameters();
            ParameterInfo[] listenerParameters = invokeMethod.GetParameters();
            if (methodParameters.Length != listenerParameters.Length)
            {
                throw new XFrameworkException(
                    $"[UIListener] Method parameter count mismatch. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, Expected: {listenerParameters.Length}, Actual: {methodParameters.Length}.");
            }

            for (int i = 0; i < methodParameters.Length; i++)
            {
                Type expectedType = listenerParameters[i].ParameterType;
                Type actualType = methodParameters[i].ParameterType;
                if (actualType != expectedType)
                {
                    throw new XFrameworkException(
                        $"[UIListener] Method parameter type mismatch. Panel: {panelType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, Parameter: {i}, Expected: {expectedType.Name}, Actual: {actualType.Name}.");
                }
            }
        }

        private static string FormatUIListenerEventName(string eventName)
        {
            return string.IsNullOrEmpty(eventName) ? "Default" : eventName;
        }

        private static string GetUIListenerKey(MethodInfo method, UIListenerAttribute attribute)
        {
            if (!string.IsNullOrWhiteSpace(attribute.Key))
            {
                return attribute.Key;
            }

            string key = method.Name;
            if (key.StartsWith("On", StringComparison.Ordinal) && key.Length > 2)
            {
                key = key.Substring(2);
            }

            if (key.EndsWith("Click", StringComparison.Ordinal) && key.Length > "Click".Length)
            {
                key = key.Substring(0, key.Length - "Click".Length);
            }
            else if (key.EndsWith("Changed", StringComparison.Ordinal) && key.Length > "Changed".Length)
            {
                key = key.Substring(0, key.Length - "Changed".Length);
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new XFrameworkException($"[UIListener] Cannot infer UI listener key from method {method.Name}.");
            }

            return char.ToUpperInvariant(key[0]) + key.Substring(1);
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        protected virtual void OnInit()
        {

        }

        /// <summary>
        /// 界面打开前
        /// </summary>
        public virtual void OnBeforeOpen()
        {
            _registerHelper?.Register();
        }

        internal void OnBeforeOpenSubPanels()
        {
            if (m_SubPanels != null)
            {
                foreach (var item in m_SubPanels)
                {
                    item.OnBeforeOpen();
                }
            }
        }

        /// <summary>
        /// 界面打开后
        /// </summary>
        public virtual void OnOpened()
        {
        }

        internal void OnOpenedSubPanels()
        {
            if (m_SubPanels != null)
            {
                foreach (var item in m_SubPanels)
                {
                    item.OnOpened();
                }
            }
        }

        /// <summary>
        /// 每帧运行
        /// </summary>
        public virtual void OnUpdate()
        {

        }

        /// <summary>
        /// 界面暂停,被遮挡
        /// </summary>
        public virtual void OnPause()
        {

        }

        /// <summary>
        /// 界面恢复
        /// </summary>
        public virtual void OnResume()
        {

        }

        /// <summary>
        /// 界面关闭前
        /// </summary>
        public virtual void OnBeforeClose()
        {
            _registerHelper?.UnRegister();
        }

        public virtual void OnClosed()
        {
            
        }

        internal void OnBeforeCloseSubPanels()
        {
            if (m_SubPanels != null)
            {
                foreach (var item in m_SubPanels)
                {
                    item.OnBeforeClose();
                }
            }
        }

        internal void OnClosedSubPanels()
        {
            if (m_SubPanels != null)
            {
                foreach (var item in m_SubPanels)
                {
                    item.OnClosed();
                }
            }
        }


        public void Open()
        {
            UIManager.Instance.OpenPanel(PanelName);
        }

        public void Open(Action onClose)
        {
            UIManager.Instance.OpenPanel(PanelName, onClose);
        }

        public void Close()
        {
            UIManager.Instance.ClosePanel(PanelName);
        } 
        
        
        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public XUIBase this[string key] => m_ComponentFindHelper[key];

        /// <summary>
        /// 创建子面板
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T CreateSubPanel<T>(GameObject obj) where T : SubPanelBase
        {
            m_SubPanels ??= new List<SubPanelBase>();

            T subPanel = obj.AddComponent<T>();
            subPanel.Config(this);
            subPanel.Reg();
            m_SubPanels.Add(subPanel);

            return subPanel;
        }

        public T Find<T>(string path) where T : XUIBase
        {
            return this[path] as T;
        }

        public T FindNode<T>(string path) where T : UINode, new()
        {
            return UINode.FindNode<T>(transform, path);
        }

        public UINode FindNode(string path)
        {
            return FindNode<UINode>(path);
        }
    }

    public abstract class PanelBase<TRequest> : PanelBase, PanelBaseWithRequest where TRequest : struct
    {
        Type PanelBaseWithRequest.RequestType => typeof(TRequest);

        public sealed override void OnBeforeOpen()
        {
            throw new XFrameworkException(
                $"[UI] Invalid open request for {GetType().Name}. Expected: {typeof(TRequest).Name}.");
        }

        internal void OpenRequest(in TRequest request)
        {
            base.OnBeforeOpen();
            OnBeforeOpen(in request);
        }

        void PanelBaseWithRequest.OpenRequestObject(object request)
        {
            if (request is not TRequest typedRequest)
            {
                throw new XFrameworkException(
                    $"[UI] Invalid open request for {GetType().Name}. Expected: {typeof(TRequest).Name}.");
            }

            OpenRequest(in typedRequest);
        }

        protected abstract void OnBeforeOpen(in TRequest request);
    }

}
