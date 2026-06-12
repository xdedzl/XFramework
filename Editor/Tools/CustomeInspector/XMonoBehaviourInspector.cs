using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UEditor = UnityEditor.Editor;

namespace XFramework.Editor
{
    /// <summary>
    /// 对象检视器
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(XMonoBehaviour), true)]
    public sealed class XMonoBehaviourInspector : UEditor
    {
        private const float GroupHeaderHeight = 26f;
        private const float GroupBorderWidth = 1f;
        private const float GroupBodyPadding = 4f;
        private const float GroupChildSpacing = 2f;
        private const string ScriptPropertyName = "m_Script";

        private readonly List<SceneField> m_sceneFields = new ();
        private readonly List<MethodInspector> m_methods = new ();

        private void OnEnable()
        {
            try
            {
                m_sceneFields.Clear();
                m_methods.Clear();

                using (SerializedProperty iterator = serializedObject.GetIterator())
                {
                    bool enterChildren = true;
                    while (iterator.NextVisible(enterChildren))
                    {
                        SerializedProperty property = serializedObject.FindProperty(iterator.name);
                        if (property != null)
                        {
                            AddSceneField(property);
                        }

                        enterChildren = false;
                    }
                }


                List<MethodInfo> methods = target.GetType().GetMethods((method) =>
                {
                    return method.IsDefined(typeof(ButtonAttribute), true) && method.GetParameters().Length == 0;
                });
                for (int i = 0; i < methods.Count; i++)
                {
                    m_methods.Add(new MethodInspector(methods[i]));
                }
                m_methods.Sort((a, b) => { return a.Attribute.Order - b.Attribute.Order; });
            }
            catch { }
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1f
                }
            };

            CreatePropertiesGUI(root);
            CreateMethodGUI(root);

            return root;
        }

        private void CreatePropertiesGUI(VisualElement root)
        {
            List<SerializedProperty> properties = GetVisibleRootProperties();
            Dictionary<string, List<SerializedProperty>> groupProperties = CollectGroupProperties(properties);
            HashSet<string> drawnGroups = new HashSet<string>();

            for (int i = 0; i < properties.Count; i++)
            {
                SerializedProperty property = properties[i];
                if (TryGetPrettyGroupTitle(property, out string groupTitle))
                {
                    if (drawnGroups.Add(groupTitle))
                    {
                        root.Add(new PrettyGroupElement(
                            serializedObject,
                            target.GetType(),
                            groupTitle,
                            groupProperties[groupTitle]));
                    }

                    continue;
                }

                root.Add(CreatePropertyField(property, property.name == ScriptPropertyName));
            }
        }

        private List<SerializedProperty> GetVisibleRootProperties()
        {
            List<SerializedProperty> properties = new List<SerializedProperty>();
            using (SerializedProperty iterator = serializedObject.GetIterator())
            {
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    properties.Add(iterator.Copy());
                    enterChildren = false;
                }
            }

            return properties;
        }

        private Dictionary<string, List<SerializedProperty>> CollectGroupProperties(List<SerializedProperty> properties)
        {
            Dictionary<string, List<SerializedProperty>> groupProperties = new Dictionary<string, List<SerializedProperty>>();
            for (int i = 0; i < properties.Count; i++)
            {
                SerializedProperty property = properties[i];
                if (!TryGetPrettyGroupTitle(property, out string groupTitle))
                {
                    continue;
                }

                if (!groupProperties.TryGetValue(groupTitle, out List<SerializedProperty> list))
                {
                    list = new List<SerializedProperty>();
                    groupProperties[groupTitle] = list;
                }

                list.Add(property.Copy());
            }

            return groupProperties;
        }

        private VisualElement CreatePropertyField(SerializedProperty property, bool disabled)
        {
            PropertyField field = new PropertyField(property.Copy());
            field.Bind(serializedObject);
            if (disabled)
            {
                field.SetEnabled(false);
            }

            return field;
        }

        private bool TryGetPrettyGroupTitle(SerializedProperty property, out string title)
        {
            title = null;
            FieldInfo field = GetField(property);
            PrettyGroupAttribute attribute = field?.GetCustomAttribute<PrettyGroupAttribute>(true);
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Title))
            {
                return false;
            }

            title = attribute.Title;
            return true;
        }

        private void OnSceneGUI()
        {
            FieldSceneHandle();
        }
        

        /// <summary>
        /// 绘制方法
        /// </summary>
        private void CreateMethodGUI(VisualElement root)
        {
            if (m_methods.Count == 0)
            {
                return;
            }

            VisualElement methodContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginTop = 4f,
                    marginLeft = 3f,
                    marginRight = 3f
                }
            };

            for (int i = 0; i < m_methods.Count; i++)
            {
                methodContainer.Add(m_methods[i].CreateElement(this));
            }

            root.Add(methodContainer);
        }

        /// <summary>
        /// 场景中处理字段
        /// </summary>
        private void FieldSceneHandle()
        {
            for (int i = 0; i < m_sceneFields.Count; i++)
            {
                m_sceneFields[i].SceneHandle(this);
            }
        }

        private void AddSceneField(SerializedProperty property)
        {
            FieldInfo field = GetField(property);
            if (field == null)
            {
                return;
            }

            List<FieldSceneHandler> sceneHandlers = SceneHandlerDrawerFactory.Create(field);
            if (sceneHandlers.Count > 0)
            {
                m_sceneFields.Add(new SceneField(field, sceneHandlers));
            }
        }

        /// <summary>
        /// 标记目标已改变
        /// </summary>
        public void HasChanged()
        {
            HasChanged(target);
        }

        /// <summary>
        /// 标记指定目标已改变
        /// </summary>
        public void HasChanged(UnityEngine.Object changedTarget)
        {
            if (changedTarget == null)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                EditorUtility.SetDirty(changedTarget);
                Component component = changedTarget as Component;
                if (component != null && component.gameObject.scene != null)
                {
                    EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                }
            }
        }

        private FieldInfo GetField(SerializedProperty property)
        {
            if (property == null || property.serializedObject.targetObject == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = property.serializedObject.targetObject.GetType();
            while (type != null && type != typeof(object))
            {
                FieldInfo field = type.GetField(property.name, flags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Color GetGroupBorderColor()
        {
            return new Color(0.07f, 0.07f, 0.07f, 1f);
        }

        private sealed class PrettyGroupElement : VisualElement
        {
            private readonly SerializedObject m_SerializedObject;
            private readonly Type m_TargetType;
            private readonly string m_Title;
            private readonly List<SerializedProperty> m_Properties;
            private Label m_Foldout;

            public PrettyGroupElement(
                SerializedObject serializedObject,
                Type targetType,
                string title,
                List<SerializedProperty> properties)
            {
                m_SerializedObject = serializedObject;
                m_TargetType = targetType;
                m_Title = title;
                m_Properties = properties;

                style.flexDirection = FlexDirection.Column;
                style.borderLeftWidth = GroupBorderWidth;
                style.borderRightWidth = GroupBorderWidth;
                style.borderTopWidth = GroupBorderWidth;
                style.borderBottomWidth = GroupBorderWidth;
                style.borderLeftColor = GetGroupBorderColor();
                style.borderRightColor = GetGroupBorderColor();
                style.borderTopColor = GetGroupBorderColor();
                style.borderBottomColor = GetGroupBorderColor();
                style.marginTop = -GroupBorderWidth;
                style.marginBottom = 2f;

                Rebuild();
            }

            private void Rebuild()
            {
                Clear();
                bool expanded = IsExpanded();
                Add(CreateHeader(expanded));

                if (!expanded)
                {
                    return;
                }

                VisualElement children = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        paddingLeft = GroupBodyPadding,
                        paddingRight = GroupBodyPadding,
                        paddingTop = GroupBodyPadding,
                        paddingBottom = GroupBodyPadding
                    }
                };

                for (int i = 0; i < m_Properties.Count; i++)
                {
                    PropertyField field = new PropertyField(m_Properties[i].Copy())
                    {
                        style =
                        {
                            marginBottom = GroupChildSpacing
                        }
                    };
                    field.Bind(m_SerializedObject);
                    children.Add(field);
                }

                Add(children);
            }

            private VisualElement CreateHeader(bool expanded)
            {
                VisualElement header = new VisualElement
                {
                    style =
                    {
                        height = GroupHeaderHeight,
                        minHeight = GroupHeaderHeight,
                        maxHeight = GroupHeaderHeight,
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        backgroundColor = new Color(0.20f, 0.20f, 0.20f, 1f),
                        borderBottomWidth = expanded ? GroupBorderWidth : 0f,
                        borderBottomColor = GetGroupBorderColor()
                    }
                };

                m_Foldout = new Label(expanded ? "▾" : "▸")
                {
                    style =
                    {
                        width = 18,
                        minWidth = 18,
                        height = GroupHeaderHeight,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 17,
                        color = new Color(0.62f, 0.62f, 0.62f, 1f)
                    }
                };

                Label title = new Label(m_Title)
                {
                    style =
                    {
                        flexGrow = 1,
                        height = GroupHeaderHeight - 2,
                        minHeight = GroupHeaderHeight - 2,
                        marginTop = 1,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        color = new Color(0.72f, 0.72f, 0.72f, 1f)
                    }
                };

                header.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    ToggleExpanded();
                    evt.StopPropagation();
                });

                header.Add(m_Foldout);
                header.Add(title);
                return header;
            }

            private bool IsExpanded()
            {
                return SessionState.GetBool(GetSessionKey(), true);
            }

            private void ToggleExpanded()
            {
                SessionState.SetBool(GetSessionKey(), !IsExpanded());
                if (m_Foldout != null)
                {
                    m_Foldout.text = IsExpanded() ? "▾" : "▸";
                }

                Rebuild();
            }

            private string GetSessionKey()
            {
                return $"{nameof(PrettyGroupElement)}.{m_TargetType.FullName}.{m_Title}";
            }
        }

        private sealed class SceneField
        {
            private readonly FieldInfo m_Field;
            private readonly List<FieldSceneHandler> m_SceneHandlers;

            public SceneField(FieldInfo field, List<FieldSceneHandler> sceneHandlers)
            {
                m_Field = field;
                m_SceneHandlers = sceneHandlers;
            }

            public void SceneHandle(XMonoBehaviourInspector inspector)
            {
                for (int i = 0; i < m_SceneHandlers.Count; i++)
                {
                    m_SceneHandlers[i].SceneHandle(inspector, m_Field);
                }
            }
        }

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

            public VisualElement CreateElement(XMonoBehaviourInspector inspector)
            {
                Button button = new Button(() => Invoke(inspector))
                {
                    text = Name,
                    style =
                    {
                        height = 22f,
                        marginTop = 2f,
                        marginBottom = 2f
                    }
                };

                button.SetEnabled(IsEnabled());
                void RefreshState(PlayModeStateChange _)
                {
                    button.SetEnabled(IsEnabled());
                }

                button.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    EditorApplication.playModeStateChanged += RefreshState;
                    button.SetEnabled(IsEnabled());
                });
                button.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    EditorApplication.playModeStateChanged -= RefreshState;
                });

                return button;
            }

            private bool IsEnabled()
            {
                return Attribute.Mode == ButtonAttribute.EnableMode.Always
                    || (Attribute.Mode == ButtonAttribute.EnableMode.Editor && !EditorApplication.isPlaying)
                    || (Attribute.Mode == ButtonAttribute.EnableMode.Playmode && EditorApplication.isPlaying);
            }

            private void Invoke(XMonoBehaviourInspector inspector)
            {
                if (Method.IsStatic)
                {
                    Method.Invoke(null, null);
                    return;
                }

                UnityEngine.Object[] targets = inspector.targets;
                for (int i = 0; i < targets.Length; i++)
                {
                    UnityEngine.Object invokeTarget = targets[i];
                    MethodInfo method = ResolveMethod(invokeTarget);
                    if (method == null)
                    {
                        continue;
                    }

                    Undo.RecordObject(invokeTarget, Name);
                    method.Invoke(invokeTarget, null);
                    inspector.HasChanged(invokeTarget);
                }
            }

            private MethodInfo ResolveMethod(UnityEngine.Object invokeTarget)
            {
                if (invokeTarget == null)
                {
                    return null;
                }

                Type targetType = invokeTarget.GetType();
                if (Method.DeclaringType != null && Method.DeclaringType.IsAssignableFrom(targetType))
                {
                    return Method;
                }

                const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo method = targetType.GetMethod(Method.Name, flags);
                if (method == null || !method.IsDefined(typeof(ButtonAttribute), true) || method.GetParameters().Length > 0)
                {
                    return null;
                }

                return method;
            }
        }
        #endregion
    }
}
