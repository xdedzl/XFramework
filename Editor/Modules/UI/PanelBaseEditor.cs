using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework;
using XFramework.UI;
using UEditor = UnityEditor.Editor;

namespace XFramework.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PanelBase), true)]
    public sealed class PanelBaseEditor : UEditor
    {
        private const string ScriptPropertyName = "m_Script";
        private const float KeyLabelWidth = 160f;
        private const float ListenerOpenButtonWidth = 22f;
        private const float ListenerKeyLabelWidth = 100f;
        private const float ListenerEventLabelWidth = 70f;
        private const float ListenerMethodLabelWidth = 125f;
        private const float ListenerSignatureLabelWidth = 80f;
        private const float ListenerTargetMinWidth = 120f;

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

            CreateDefaultInspector(root);
            CreatePanelXUIList(root);
            CreatePanelUIListenerList(root);

            return root;
        }

        private void CreateDefaultInspector(VisualElement root)
        {
            using SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                SerializedProperty property = iterator.Copy();
                PropertyField field = new PropertyField(property);
                field.Bind(serializedObject);
                if (property.name == ScriptPropertyName)
                {
                    field.SetEnabled(false);
                }

                root.Add(field);
                enterChildren = false;
            }
        }

        private void CreatePanelXUIList(VisualElement root)
        {
            PanelBase panel = target as PanelBase;
            if (panel == null)
            {
                return;
            }

            List<ComponentFindHelper<XUIBase>.ComponentInfo> components = ComponentFindHelper<XUIBase>.CollectComponents(panel.gameObject);
            Dictionary<string, int> keyCounts = CountKeys(components);
            VisualElement section = CreateSection();
            section.Add(CreateTitle($"XUIBase：{components.Count}"));

            for (int i = 0; i < components.Count; i++)
            {
                ComponentFindHelper<XUIBase>.ComponentInfo componentInfo = components[i];
                section.Add(CreateItem(
                    componentInfo.Component,
                    componentInfo.Key,
                    keyCounts[componentInfo.Key] > 1));
            }

            root.Add(section);
        }

        private void CreatePanelUIListenerList(VisualElement root)
        {
            PanelBase panel = target as PanelBase;
            if (panel == null)
            {
                return;
            }

            List<UIListenerInfo> listeners = CollectUIListeners(panel.GetType());
            VisualElement section = CreateSection();
            section.Add(CreateTitle($"UIListener 绑定：{listeners.Count}"));
            if (listeners.Count == 0)
            {
                section.Add(new HelpBox("当前面板没有声明 [UIListener]。", HelpBoxMessageType.Info));
                root.Add(section);
                return;
            }

            List<ComponentFindHelper<XUIBase>.ComponentInfo> components = ComponentFindHelper<XUIBase>.CollectComponents(panel.gameObject);
            Dictionary<string, int> keyCounts = CountKeys(components);
            Dictionary<string, XUIBase> componentByKey = CreateComponentMap(components);
            for (int i = 0; i < listeners.Count; i++)
            {
                UIListenerInfo listener = listeners[i];
                componentByKey.TryGetValue(listener.Key, out XUIBase component);
                keyCounts.TryGetValue(listener.Key, out int keyCount);
                section.Add(CreateListenerItem(listener, component, keyCount));
            }

            root.Add(section);
        }

        private static VisualElement CreateSection()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginTop = 8f,
                    marginLeft = 3f,
                    marginRight = 3f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    borderTopWidth = 1f,
                    borderTopColor = new Color(0.16f, 0.16f, 0.16f, 1f)
                }
            };
        }

        private static Label CreateTitle(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 4f
                }
            };
        }

        private static VisualElement CreateItem(XUIBase component, string key, bool isDuplicatedKey)
        {
            VisualElement row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 1f,
                    marginBottom = 1f
                }
            };

            Image errorIcon = CreateDuplicateKeyIcon(key, isDuplicatedKey);

            Label keyLabel = new Label(key)
            {
                tooltip = "运行时查找 Key",
                style =
                {
                    width = KeyLabelWidth,
                    minWidth = KeyLabelWidth,
                    maxWidth = KeyLabelWidth,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    overflow = Overflow.Hidden
                }
            };

            ObjectField objectField = new ObjectField
            {
                objectType = typeof(XUIBase),
                value = component,
                allowSceneObjects = true,
                style =
                {
                    flexGrow = 1f
                }
            };
            objectField.SetEnabled(false);

            row.Add(errorIcon);
            row.Add(keyLabel);
            row.Add(objectField);
            return row;
        }

        private static VisualElement CreateListenerItem(UIListenerInfo listener, XUIBase component, int keyCount)
        {
            UIListenerStatus status = GetListenerStatus(listener, component, keyCount);
            VisualElement row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 1f,
                    marginBottom = 1f
                }
            };

            Image statusIcon = CreateStatusIcon(status);
            Button openButton = CreateOpenMethodButton(listener.Method);
            Label keyLabel = CreateFixedLabel(listener.Key, ListenerKeyLabelWidth, "UIListener 查找 Key");
            Label eventLabel = CreateFixedLabel(GetUIListenerEventDisplayName(listener.EventName), ListenerEventLabelWidth, "UIListener 事件名");
            Label methodLabel = CreateFixedLabel(listener.Method.Name, ListenerMethodLabelWidth, "绑定方法");
            Label signatureLabel = CreateFixedLabel(GetMethodSignature(listener.Method), ListenerSignatureLabelWidth, "方法签名");
            ObjectField objectField = new ObjectField
            {
                objectType = typeof(XUIBase),
                value = component,
                allowSceneObjects = true,
                tooltip = "目标 UI 组件",
                style =
                {
                    flexGrow = 1f,
                    minWidth = ListenerTargetMinWidth
                }
            };
            objectField.SetEnabled(false);

            row.Add(statusIcon);
            row.Add(openButton);
            row.Add(keyLabel);
            row.Add(eventLabel);
            row.Add(methodLabel);
            row.Add(signatureLabel);
            row.Add(objectField);
            return row;
        }

        private static Button CreateOpenMethodButton(MethodInfo method)
        {
            Button button = new Button(() => OpenMethod(method))
            {
                text = ">",
                tooltip = $"跳转到脚本方法：{method.DeclaringType?.Name}.{method.Name}",
                style =
                {
                    width = ListenerOpenButtonWidth,
                    minWidth = ListenerOpenButtonWidth,
                    maxWidth = ListenerOpenButtonWidth,
                    height = 18f,
                    marginRight = 4f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            return button;
        }

        private static Image CreateDuplicateKeyIcon(string key, bool isDuplicatedKey)
        {
            Image icon = new Image
            {
                image = isDuplicatedKey ? EditorGUIUtility.IconContent("console.erroricon").image : null,
                tooltip = isDuplicatedKey ? $"重复的 UI 查找 Key：{key}" : string.Empty,
                style =
                {
                    width = 16f,
                    minWidth = 16f,
                    maxWidth = 16f,
                    height = 16f,
                    marginRight = 4f,
                    unityBackgroundImageTintColor = Color.red
                }
            };

            return icon;
        }

        private static Image CreateStatusIcon(UIListenerStatus status)
        {
            bool hasError = !string.IsNullOrEmpty(status.Error);
            return new Image
            {
                image = hasError ? EditorGUIUtility.IconContent("console.erroricon").image : EditorGUIUtility.IconContent("TestPassed").image,
                tooltip = hasError ? status.Error : status.Message,
                style =
                {
                    width = 16f,
                    minWidth = 16f,
                    maxWidth = 16f,
                    height = 16f,
                    marginRight = 4f,
                    unityBackgroundImageTintColor = hasError ? Color.red : Color.green
                }
            };
        }

        private static Label CreateFixedLabel(string text, float width, string tooltip)
        {
            return new Label(text)
            {
                tooltip = tooltip,
                style =
                {
                    width = width,
                    minWidth = width,
                    maxWidth = width,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    overflow = Overflow.Hidden
                }
            };
        }

        private static Dictionary<string, int> CountKeys(List<ComponentFindHelper<XUIBase>.ComponentInfo> components)
        {
            Dictionary<string, int> keyCounts = new Dictionary<string, int>();
            for (int i = 0; i < components.Count; i++)
            {
                string key = components[i].Key;
                if (keyCounts.ContainsKey(key))
                {
                    keyCounts[key]++;
                }
                else
                {
                    keyCounts.Add(key, 1);
                }
            }

            return keyCounts;
        }

        private static Dictionary<string, XUIBase> CreateComponentMap(List<ComponentFindHelper<XUIBase>.ComponentInfo> components)
        {
            Dictionary<string, XUIBase> componentByKey = new Dictionary<string, XUIBase>();
            for (int i = 0; i < components.Count; i++)
            {
                ComponentFindHelper<XUIBase>.ComponentInfo componentInfo = components[i];
                if (!componentByKey.ContainsKey(componentInfo.Key))
                {
                    componentByKey.Add(componentInfo.Key, componentInfo.Component);
                }
            }

            return componentByKey;
        }

        private static void OpenMethod(MethodInfo method)
        {
            Type declaringType = method.DeclaringType;
            if (declaringType == null)
            {
                return;
            }

            MonoScript script = FindMonoScript(declaringType);
            if (script == null)
            {
                Debug.LogWarning($"Failed to find script asset for {declaringType.FullName}.");
                return;
            }

            int line = FindMethodLine(script, method);
            if (line > 0)
            {
                AssetDatabase.OpenAsset(script, line);
            }
            else
            {
                AssetDatabase.OpenAsset(script);
            }

            EditorGUIUtility.PingObject(script);
        }

        private static MonoScript FindMonoScript(Type type)
        {
            string[] guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    return script;
                }
            }

            return null;
        }

        private static int FindMethodLine(MonoScript script, MethodInfo method)
        {
            string path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return -1;
            }

            string methodToken = method.Name + "(";
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();
                if (IsMethodDeclarationLine(trimmedLine, methodToken))
                {
                    return i + 1;
                }
            }

            return -1;
        }

        private static bool IsMethodDeclarationLine(string line, string methodToken)
        {
            int methodIndex = line.IndexOf(methodToken, StringComparison.Ordinal);
            if (methodIndex <= 0)
            {
                return false;
            }

            string beforeMethodName = line.Substring(0, methodIndex).TrimEnd();
            if (beforeMethodName.EndsWith(".", StringComparison.Ordinal))
            {
                return false;
            }

            return beforeMethodName.Contains(" ");
        }

        private static List<UIListenerInfo> CollectUIListeners(Type panelType)
        {
            List<UIListenerInfo> listeners = new List<UIListenerInfo>();
            for (Type type = panelType; type != null && type != typeof(PanelBase); type = type.BaseType)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    UIListenerAttribute attribute = method.GetCustomAttribute<UIListenerAttribute>(true);
                    if (attribute == null)
                    {
                        continue;
                    }

                    listeners.Add(new UIListenerInfo(method, GetUIListenerKey(method, attribute), attribute.EventName));
                }
            }

            return listeners;
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

            return string.IsNullOrEmpty(key) ? method.Name : char.ToUpperInvariant(key[0]) + key.Substring(1);
        }

        private static UIListenerStatus GetListenerStatus(UIListenerInfo listener, XUIBase component, int keyCount)
        {
            if (keyCount <= 0)
            {
                return UIListenerStatus.CreateError($"未找到 Key：{listener.Key}");
            }

            if (keyCount > 1)
            {
                return UIListenerStatus.CreateError($"重复的 UI 查找 Key：{listener.Key}");
            }

            if (component is not IUIEventSource eventSource)
            {
                return UIListenerStatus.CreateError($"目标组件不支持 UIListener：{component.GetType().Name}");
            }

            Type listenerType = GetListenerType(listener, component, eventSource, out string listenerTypeError);
            if (!string.IsNullOrEmpty(listenerTypeError))
            {
                return UIListenerStatus.CreateError(listenerTypeError);
            }

            string error = GetSignatureError(listener.Method, listenerType);
            if (!string.IsNullOrEmpty(error))
            {
                return UIListenerStatus.CreateError(error);
            }

            return UIListenerStatus.Valid($"{listener.Key}.{GetUIListenerEventDisplayName(listener.EventName)} -> {listener.Method.Name}");
        }

        private static Type GetListenerType(UIListenerInfo listener, XUIBase component, IUIEventSource eventSource, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(listener.EventName))
            {
                return eventSource.ListenerType;
            }

            if (component is not IUIMultiEventSource multiEventSource)
            {
                error = $"目标组件不支持多事件 UIListener：{component.GetType().Name}";
                return null;
            }

            Type listenerType = multiEventSource.GetListenerType(listener.EventName);
            if (listenerType == null)
            {
                error = $"目标组件不支持事件：{listener.EventName}";
                return null;
            }

            return listenerType;
        }

        private static string GetUIListenerEventDisplayName(string eventName)
        {
            return string.IsNullOrEmpty(eventName) ? "Default" : eventName;
        }

        private static string GetSignatureError(MethodInfo method, Type listenerType)
        {
            if (listenerType == null || !typeof(Delegate).IsAssignableFrom(listenerType))
            {
                return $"事件源 ListenerType 无效：{listenerType?.Name ?? "null"}";
            }

            MethodInfo invokeMethod = listenerType.GetMethod("Invoke");
            if (invokeMethod == null)
            {
                return $"事件源 ListenerType 缺少 Invoke：{listenerType.Name}";
            }

            if (method.ReturnType != invokeMethod.ReturnType)
            {
                return $"返回类型不匹配，期望 {invokeMethod.ReturnType.Name}，实际 {method.ReturnType.Name}";
            }

            ParameterInfo[] methodParameters = method.GetParameters();
            ParameterInfo[] listenerParameters = invokeMethod.GetParameters();
            if (methodParameters.Length != listenerParameters.Length)
            {
                return $"参数数量不匹配，期望 {listenerParameters.Length}，实际 {methodParameters.Length}";
            }

            for (int i = 0; i < methodParameters.Length; i++)
            {
                Type expectedType = listenerParameters[i].ParameterType;
                Type actualType = methodParameters[i].ParameterType;
                if (actualType != expectedType)
                {
                    return $"第 {i} 个参数类型不匹配，期望 {expectedType.Name}，实际 {actualType.Name}";
                }
            }

            return string.Empty;
        }

        private static string GetMethodSignature(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                return "void()";
            }

            string[] parameterTypeNames = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypeNames[i] = parameters[i].ParameterType.Name;
            }

            return $"{method.ReturnType.Name}({string.Join(", ", parameterTypeNames)})";
        }

        private readonly struct UIListenerInfo
        {
            public readonly MethodInfo Method;
            public readonly string Key;
            public readonly string EventName;

            public UIListenerInfo(MethodInfo method, string key, string eventName)
            {
                Method = method;
                Key = key;
                EventName = eventName;
            }
        }

        private readonly struct UIListenerStatus
        {
            public readonly string Message;
            public readonly string Error;

            private UIListenerStatus(string message, string error)
            {
                Message = message;
                Error = error;
            }

            public static UIListenerStatus Valid(string message)
            {
                return new UIListenerStatus(message, string.Empty);
            }

            public static UIListenerStatus CreateError(string error)
            {
                return new UIListenerStatus(string.Empty, error);
            }
        }
    }
}
