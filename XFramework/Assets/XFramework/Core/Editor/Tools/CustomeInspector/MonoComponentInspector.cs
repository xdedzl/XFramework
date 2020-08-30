using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UEditor = UnityEditor.Editor;

namespace XFramework.Editor
{
    /// <summary>
    /// 对象检视器
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public sealed class MonoComponentInspector : UEditor
    {
        private List<FieldInspector> m_fields = new List<FieldInspector>();
        private List<EventInspector> m_events = new List<EventInspector>();
        private List<MethodInspector> m_methods = new List<MethodInspector>();

        private static Dictionary<Type, Type> s_Attribute2Drawer;

        public MonoComponentInspector()
        {
            if (s_Attribute2Drawer == null)
            {
                s_Attribute2Drawer = new Dictionary<Type, Type>();
                var drawerType = typeof(FieldDrawer);
                Utility.Reflection.GetTypesInAllAssemblies((type) =>
                {
                    if (type.IsSubclassOf(drawerType))
                    {
                        var attr = type.GetCustomAttribute<CustomDrawerAttribute>();
                        if (attr != null)
                        {
                            s_Attribute2Drawer.Add(attr.type, type);
                            return true;
                        }
                    }
                    return false;
                });
            }
        }

        public static Type GetDrawerType(Type type)
        {
            if (s_Attribute2Drawer.TryGetValue(type, out Type drawerType))
            {
                return drawerType;
            }

            throw new XFrameworkException($"{type.Name}没有设置对应的drawer，请补充");
        }

        private void OnEnable()
        {
            try
            {
                using (SerializedProperty iterator = serializedObject.GetIterator())
                {
                    while (iterator.NextVisible(true))
                    {
                        SerializedProperty property = serializedObject.FindProperty(iterator.name);
                        if (property != null)
                        {
                            m_fields.Add(new FieldInspector(property));
                        }
                    }
                }

                List<FieldInfo> events = target.GetType().GetFields((field) =>
                {
                    return field.FieldType.IsSubclassOf(typeof(MulticastDelegate)) && field.IsDefined(typeof(EventAttribute), true);
                });
                for (int i = 0; i < events.Count; i++)
                {
                    m_events.Add(new EventInspector(events[i]));
                }

                List<MethodInfo> methods = target.GetType().GetMethods((method) =>
                {
                    return method.IsDefined(typeof(ButtonAttribute), true);
                });
                for (int i = 0; i < methods.Count; i++)
                {
                    m_methods.Add(new MethodInspector(methods[i]));
                }
                m_methods.Sort((a, b) => { return a.Attribute.Order - b.Attribute.Order; });
            }
            catch { }
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FieldGUI();
            EventGUI();
            MethodGUI();

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            FieldSceneHandle();
        }

        /// <summary>
        /// 绘制字段
        /// </summary>
        private void FieldGUI()
        {
            for (int i = 0; i < m_fields.Count; i++)
            {
                m_fields[i].Draw(this);
            }
        }

        /// <summary>
        /// 绘制事件
        /// </summary>
        private void EventGUI()
        {
            for (int i = 0; i < m_events.Count; i++)
            {
                m_events[i].Draw(this);
            }
        }

        /// <summary>
        /// 绘制方法
        /// </summary>
        private void MethodGUI()
        {
            for (int i = 0; i < m_methods.Count; i++)
            {
                m_methods[i].Draw(this);
            }
        }

        /// <summary>
        /// 场景中处理字段
        /// </summary>
        private void FieldSceneHandle()
        {
            for (int i = 0; i < m_fields.Count; i++)
            {
                m_fields[i].SceneHandle(this);
            }
        }

        /// <summary>
        /// 标记目标已改变
        /// </summary>
        public void HasChanged()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.SetDirty(target);
                Component component = target as Component;
                if (component != null && component.gameObject.scene != null)
                {
                    EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                }
            }
        }

        #region Event
        /// <summary>
        /// 事件检视器
        /// </summary>
        private sealed class EventInspector
        {
            public FieldInfo Field;
            public EventAttribute Attribute;
            public string Name;
            public bool IsFoldout;

            public EventInspector(FieldInfo field)
            {
                Debug.Log("Generate");

                Field = field;
                Attribute = field.GetCustomAttribute<EventAttribute>(true);
                Name = string.IsNullOrEmpty(Attribute.Text) ? field.Name : Attribute.Text;
                IsFoldout = true;
            }

            public void Draw(MonoComponentInspector inspector)
            {
                Delegate[] delegates = Field.GetValue(inspector.target) is MulticastDelegate multicast ? multicast.GetInvocationList() : null;

                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                IsFoldout = EditorGUILayout.Foldout(IsFoldout, string.Format("{0} [{1}]", Name, delegates != null ? delegates.Length : 0));
                GUILayout.EndHorizontal();

                if (IsFoldout && delegates != null)
                {
                    for (int i = 0; i < delegates.Length; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUILayout.Label(string.Format("{0}->{1}", delegates[i].Target, delegates[i].Method), "Textfield");
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }
        #endregion

        #region Method
        /// <summary>
        /// 函数检视器
        /// </summary>
        private sealed class MethodInspector
        {
            public MethodInfo Method;
            public ButtonAttribute Attribute;
            public string Name;

            public MethodInspector(MethodInfo method)
            {
                Method = method;
                Attribute = method.GetCustomAttribute<ButtonAttribute>(true);
                Name = string.IsNullOrEmpty(Attribute.Text) ? Method.Name : Attribute.Text;
            }

            public void Draw(MonoComponentInspector inspector)
            {
                GUI.enabled = Attribute.Mode == ButtonAttribute.EnableMode.Always
                || (Attribute.Mode == ButtonAttribute.EnableMode.Editor && !EditorApplication.isPlaying)
                || (Attribute.Mode == ButtonAttribute.EnableMode.Playmode && EditorApplication.isPlaying);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Name, Attribute.Style))
                {
                    inspector.HasChanged();

                    if (Method.ReturnType.Name != "Void")
                    {
                        object result = null;
                        if (Method.IsStatic) result = Method.Invoke(null, null);
                        else result = Method.Invoke(inspector.target, null);
                        //Log.Info("点击按钮 " + Name + " 后，存在返回值：" + result);
                    }
                    else
                    {
                        if (Method.IsStatic) Method.Invoke(null, null);
                        else Method.Invoke(inspector.target, null);
                    }
                }
                GUILayout.EndHorizontal();

                GUI.enabled = true;
            }
        }
        #endregion
    }
}