using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Resource;

namespace XFramework.NodeKit.Editor
{
    public partial class XNodeGraphEditorWindow : EditorWindow
    {
        private static readonly List<XNodeGraphEditorWindow> s_OpenWindows = new();

        public static XNodeGraphEditorWindow OpenTextAsset(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                return null;
            }

            XNodeGraphAsset graphAsset = textAsset.ToXTextAsset<XNodeGraphAsset>();
            if (graphAsset == null)
            {
                return null;
            }

            return OpenGraphAsset(graphAsset, AssetDatabase.GetAssetPath(textAsset));
        }

        public static XNodeGraphEditorWindow OpenGraphAsset(XNodeGraphAsset asset, string assetPath = null)
        {
            if (asset == null)
            {
                return null;
            }

            string normalizedPath = NormalizeAssetPath(assetPath);
            XNodeGraphEditorWindow existingWindow = FindWindow(normalizedPath);
            if (existingWindow != null)
            {
                existingWindow.Focus();
                return existingWindow;
            }

            XNodeGraphEditorWindow window = CreateDockedWindow(CreateWindowTitle(normalizedPath));
            window.titleContent = new GUIContent(CreateWindowTitle(normalizedPath));
            window.minSize = new Vector2(980f, 560f);
            window.SetAsset(asset, normalizedPath);
            window.Show();
            window.Focus();
            return window;
        }

        [SerializeField] private TextAsset m_SourceAsset;
        [SerializeField] private string m_SourceAssetGuid;
        [SerializeField] private string currentAssetPath;
        private XNodeGraphAsset currentAsset;
        public XNodeGraphView graphView { get; private set; }
        
        private ObjectField m_AssetField;
        private Label m_GraphSummaryLabel;
        private Button m_SaveButton;
        private Button m_ReloadButton;
        private VisualElement m_WorkspaceRoot;
        private Label m_PlaceholderLabel;
        
        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.height = Length.Percent(100);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

            graphView = new XNodeGraphView(this)
            {
                style =
                {
                    flexGrow = 1f,
                    width = Length.Percent(100)
                }
            };
            var root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1f,
                    height = Length.Percent(100)
                }
            };

            root.Add(BuildToolbar());
            
            m_WorkspaceRoot = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1f
                }
            };

            m_WorkspaceRoot.Add(graphView);
            root.Add(m_WorkspaceRoot);
            rootVisualElement.Add(root);

            // 启动游戏的时候，会重新调用CreateGUI
            if (currentAsset != null)
            {
                RebuildGraph();
            }

            RefreshWindowStatus();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexWrap = Wrap.Wrap,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 5f,
                    paddingBottom = 5f,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f),
                    borderBottomWidth = 1f,
                    borderBottomColor = new Color(0.08f, 0.08f, 0.08f, 1f)
                }
            };

            m_SaveButton = new Button(SaveData)
            {
                text = "保存",
                tooltip = "保存当前 Graph 图"
            };
            m_SaveButton.style.height = 24f;
            m_SaveButton.style.marginRight = 8f;
            toolbar.Add(m_SaveButton);

            m_ReloadButton = new Button(ReloadFromDisk)
            {
                text = "重载",
                tooltip = "从磁盘重新加载当前 Graph 图"
            };
            m_ReloadButton.style.height = 24f;
            m_ReloadButton.style.marginRight = 10f;
            toolbar.Add(m_ReloadButton);

            var assetFieldContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1f,
                    flexShrink = 1f,
                    minWidth = 240f,
                    marginRight = 12f
                }
            };
            var assetLabel = new Label("XAsset")
            {
                style =
                {
                    marginRight = 3f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexShrink = 0f
                }
            };
            assetFieldContainer.Add(assetLabel);

            m_AssetField = CreatePingObjectField(string.Empty, null, typeof(TextAsset));
            m_AssetField.style.flexGrow = 1f;
            m_AssetField.style.flexShrink = 1f;
            m_AssetField.style.minWidth = 0f;
            m_AssetField.labelElement.style.display = DisplayStyle.None;
            assetFieldContainer.Add(m_AssetField);
            toolbar.Add(assetFieldContainer);

            var separator = new VisualElement
            {
                style =
                {
                    width = 1f,
                    height = 14f,
                    marginRight = 12f,
                    backgroundColor = new Color(0.42f, 0.42f, 0.42f, 0.7f)
                }
            };
            toolbar.Add(separator);

            m_GraphSummaryLabel = new Label
            {
                style =
                {
                    color = new Color(0.78f, 0.78f, 0.78f, 1f),
                    flexGrow = 1f,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            toolbar.Add(m_GraphSummaryLabel);
            return toolbar;
        }

        private void OnEnable()
        {
            if (!s_OpenWindows.Contains(this))
            {
                s_OpenWindows.Add(this);
            }

            EnsureCurrentAssetLoaded();
        }
        
                
        private void OnKeyDown(KeyDownEvent evt)
        {
            // 检查是否按下了 Ctrl 键和 S 键
            if (evt.keyCode == KeyCode.S && evt.ctrlKey)
            {
                SaveData();
                evt.StopPropagation (); // 阻止事件传递，避免触发其他事件
            }
        }
        
        private void OnDisable()
        {
            SaveData();
            XNodeGraphInspectorWindow inspectorWindow = XNodeGraphInspectorWindow.GetOpenWindow();
            if (inspectorWindow != null && inspectorWindow.IsOwnedBy(this))
            {
                inspectorWindow.Close();
            }

            s_OpenWindows.Remove(this);
        }
        
        public void SetAsset(XNodeGraphAsset asset)
        {
            SetAsset(asset, currentAssetPath);
        }

        public void SetAsset(XNodeGraphAsset asset, string assetPath)
        {
            currentAsset = asset;
            currentAssetPath = NormalizeAssetPath(assetPath);
            m_SourceAsset = ResolveSourceAsset(currentAssetPath);
            m_SourceAssetGuid = ResolveAssetGuid(m_SourceAsset);
            if (!string.IsNullOrEmpty(currentAssetPath))
            {
                currentAsset.SetAssetPath(currentAssetPath);
            }

            titleContent = new GUIContent(CreateWindowTitle(currentAssetPath));
            if (graphView == null)
            {
                RefreshWindowStatus();
                return;
            }

            RebuildGraph();
        }

        private void RebuildGraph()
        {
            if (graphView == null)
            {
                return;
            }

            ClearInspectorTarget();
            
            var nodesToDelete = graphView.graphElements;
            graphView.DeleteElements(nodesToDelete);

            if (currentAsset == null)
            {
                RefreshWindowStatus();
                return;
            }
            
            
            var runtimeNode2EditorNode = XNodeKitEditor.GetRuntimeNode2EditorNode();
            if (currentAsset.nodes != null)
            {
                var positionDict = currentAsset.GetNodePositionDict();
                foreach (var runtimeNode in currentAsset.nodes)
                {
                    var runtimeType = runtimeNode.GetType();

                    if (runtimeNode2EditorNode.TryGetValue(runtimeType, out var editorNodeType))
                    {
                        var editorNode = Utility.Reflection.CreateInstance<XEditorNodeBase>(editorNodeType);
                        var a = runtimeNode.GetId();
                        if (positionDict.TryGetValue(runtimeNode.GetId(), out var _position))
                        {
                            editorNode.SetPosition(new Rect(_position, editorNode.GetPosition().size));
                        }
                        editorNode.SetRuntimeNode(runtimeNode);
                        graphView.AddXNode(editorNode);
                    }
                    else
                    {
                        Debug.LogError("无法找到对应的编辑器节点类型：" + runtimeType.FullName);
                    }
                }

                foreach (var node in graphView.GetAllXNodes())
                {
                    node.ApplyFromRuntimeNode();
                }
            }
            
            // 恢复视图信息
            var pos = UUtility.EditorPrefs.GetVector3($"{GetViewStateKey()}_Position", graphView.contentViewContainer.resolvedStyle.translate);
            var scale = UUtility.EditorPrefs.GetVector3($"{GetViewStateKey()}_Scale", graphView.contentViewContainer.resolvedStyle.scale.value);
            graphView.contentViewContainer.style.translate = pos;
            graphView.contentViewContainer.style.scale = scale;
            RefreshWindowStatus();
        }

        public void BindInspectorTarget(IXNode runtimeNode)
        {
            if (runtimeNode == null)
            {
                ClearInspectorTarget();
                return;
            }
            
            XNodeGraphInspectorWindow.ShowWindow(this, runtimeNode);
        }

        public void ClearInspectorTarget()
        {
            XNodeGraphInspectorWindow inspectorWindow = XNodeGraphInspectorWindow.GetOpenWindow();
            if (inspectorWindow != null && inspectorWindow.IsOwnedBy(this))
            {
                inspectorWindow.ClearSelection();
            }
        }
        
        private void SaveData()
        {
            if (Application.isPlaying)
            {
                return;
            }
            if (currentAsset == null)
            {
                return;
            }
            var nodeDict = new Dictionary<string, IXNode>();
            var nodes = new List<IXNode>();
            var nodePositions = new List<NodePositon>();
            if (graphView == null)
            {
                return;
            }
            graphView.nodes.ForEach((element) =>
            {
                if (element is XEditorNodeBase node)
                {
                    if (nodeDict.ContainsKey(node.GetId()))
                    {
                        throw new System.Exception("存在重复的节点Id：" + node.GetId());
                    }
                    
                    node.ApplyToRuntimeNode();
                    var runtimeNode = node.GetRuntimeNode();
                    
                    nodeDict.Add(node.GetId(), runtimeNode);
                    nodes.Add(runtimeNode);
                    nodePositions.Add(new NodePositon
                    {
                        nodeId = node.GetId(),
                        position = node.GetPosition().position
                    });
                }
                else
                {
                    Debug.Log("element is not XEditorNodeBase: " + element.GetType().Name);
                }
            });
            
            currentAsset.nodes = nodes.ToArray();
            currentAsset.nodePositions = nodePositions.ToArray();
            
            // 保存视图信息
            UUtility.EditorPrefs.SetVector3($"{GetViewStateKey()}_Position", graphView.contentViewContainer.resolvedStyle.translate);
            UUtility.EditorPrefs.SetVector3($"{GetViewStateKey()}_Scale", graphView.contentViewContainer.resolvedStyle.scale.value);
            
            Debug.Log("保存成功，节点数量：" + nodes.Count);
            currentAsset.SaveAsset();
            RefreshWindowStatus();
        }

        private void ReloadFromDisk()
        {
            EnsureCurrentAssetLoaded();
            if (string.IsNullOrEmpty(currentAssetPath))
            {
                ShowNotification(new GUIContent("当前窗口没有绑定 Graph 图资源。"));
                return;
            }

            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(currentAssetPath);
            if (textAsset == null)
            {
                EditorUtility.DisplayDialog("重载失败", $"资源不存在: {currentAssetPath}", "OK");
                RefreshWindowStatus();
                return;
            }

            try
            {
                XNodeGraphAsset reloadedAsset = textAsset.ToXTextAsset<XNodeGraphAsset>();
                if (reloadedAsset == null)
                {
                    EditorUtility.DisplayDialog("重载失败", $"解析结果为空: {currentAssetPath}", "OK");
                    return;
                }

                SetAsset(reloadedAsset, currentAssetPath);
                ShowNotification(new GUIContent("Graph 图已重载"));
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                EditorUtility.DisplayDialog("重载失败", exception.Message, "OK");
            }

            RefreshWindowStatus();
        }

        private void EnsureCurrentAssetLoaded()
        {
            if (currentAsset != null)
            {
                return;
            }

            RestoreSourceAssetReference();
            if (m_SourceAsset == null && !string.IsNullOrEmpty(currentAssetPath))
            {
                m_SourceAsset = ResolveSourceAsset(currentAssetPath);
                m_SourceAssetGuid = ResolveAssetGuid(m_SourceAsset);
            }

            if (m_SourceAsset == null)
            {
                return;
            }

            try
            {
                XNodeGraphAsset graphAsset = m_SourceAsset.ToXTextAsset<XNodeGraphAsset>();
                if (graphAsset == null)
                {
                    return;
                }

                string assetPath = AssetDatabase.GetAssetPath(m_SourceAsset);
                SetAsset(graphAsset, assetPath);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        private void RestoreSourceAssetReference()
        {
            if (m_SourceAsset != null || string.IsNullOrEmpty(m_SourceAssetGuid))
            {
                return;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(m_SourceAssetGuid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            m_SourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (string.IsNullOrEmpty(currentAssetPath))
            {
                currentAssetPath = NormalizeAssetPath(assetPath);
            }
        }

        private static TextAsset ResolveSourceAsset(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }

        private static string ResolveAssetGuid(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                return string.Empty;
            }

            string assetPath = AssetDatabase.GetAssetPath(textAsset);
            return string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
        }

        internal void RefreshWindowStatus()
        {
            if (m_SaveButton != null)
            {
                m_SaveButton.SetEnabled(currentAsset != null && graphView != null && !Application.isPlaying);
            }

            if (m_ReloadButton != null)
            {
                m_ReloadButton.SetEnabled(!string.IsNullOrEmpty(currentAssetPath));
            }

            if (m_AssetField != null)
            {
                TextAsset textAsset = string.IsNullOrEmpty(currentAssetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<TextAsset>(currentAssetPath);
                m_AssetField.SetValueWithoutNotify(textAsset);
                m_AssetField.tooltip = string.IsNullOrEmpty(currentAssetPath) ? "当前窗口尚未绑定 Graph 图资源。" : currentAssetPath;
            }

            if (m_GraphSummaryLabel != null)
            {
                int runtimeNodeCount = currentAsset?.nodes?.Length ?? 0;
                int editorNodeCount = GetEditorNodeCount();
                m_GraphSummaryLabel.text = currentAsset == null
                    ? "双击 Graph 图资源以打开编辑器。"
                    : $"节点: {editorNodeCount} / 数据: {runtimeNodeCount}";
            }

            RefreshPlaceholder();
        }

        private ObjectField CreatePingObjectField(string label, UnityEngine.Object targetObject, Type objectType)
        {
            ObjectField field = new(label)
            {
                objectType = objectType ?? typeof(UnityEngine.Object),
                allowSceneObjects = false,
                value = targetObject
            };
            field.RegisterValueChangedCallback(_ => RefreshWindowStatus());
            field.RegisterCallback<MouseDownEvent>(evt =>
            {
                UnityEngine.Object currentObject = field.value;
                if (evt.button != 0 || currentObject == null)
                {
                    return;
                }

                Selection.activeObject = currentObject;
                EditorGUIUtility.PingObject(currentObject);
                if (evt.clickCount >= 2)
                {
                    AssetDatabase.OpenAsset(currentObject);
                }

                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            return field;
        }

        private void RefreshPlaceholder()
        {
            if (m_WorkspaceRoot == null || graphView == null)
            {
                return;
            }

            bool shouldShowPlaceholder = currentAsset == null;
            graphView.style.display = shouldShowPlaceholder ? DisplayStyle.None : DisplayStyle.Flex;

            if (!shouldShowPlaceholder)
            {
                m_PlaceholderLabel?.RemoveFromHierarchy();
                m_PlaceholderLabel = null;
                return;
            }

            if (m_PlaceholderLabel != null)
            {
                return;
            }

            m_PlaceholderLabel = new Label("双击 Graph 图资源以打开编辑器。")
            {
                style =
                {
                    flexGrow = 1f,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.78f, 0.78f, 0.78f, 1f)
                }
            };
            m_WorkspaceRoot.Insert(0, m_PlaceholderLabel);
        }

        private int GetEditorNodeCount()
        {
            if (graphView == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var _ in graphView.GetAllXNodes())
            {
                count++;
            }

            return count;
        }

        private string GetViewStateKey()
        {
            return string.IsNullOrEmpty(currentAssetPath) ? $"XNode_{currentAsset.GetHashCode()}" : $"XNode_{currentAssetPath}";
        }

        private static XNodeGraphEditorWindow FindWindow(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            s_OpenWindows.RemoveAll(window => window == null);
            return s_OpenWindows.FirstOrDefault(window =>
                string.Equals(window.currentAssetPath, assetPath, StringComparison.OrdinalIgnoreCase));
        }

        private static XNodeGraphEditorWindow CreateDockedWindow(string title)
        {
            try
            {
                Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                return gameViewType != null
                    ? CreateWindow<XNodeGraphEditorWindow>(title, typeof(SceneView), gameViewType)
                    : CreateWindow<XNodeGraphEditorWindow>(title, typeof(SceneView));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Graph 窗口停靠到 Scene 同级失败，改用普通窗口打开。{exception.Message}");
                var window = CreateInstance<XNodeGraphEditorWindow>();
                window.titleContent = new GUIContent(title);
                return window;
            }
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath.Replace('\\', '/');
        }

        private static string CreateWindowTitle(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return "Graph";
            }

            return $"Graph - {Path.GetFileNameWithoutExtension(assetPath)}";
        }

        private static string ShortenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= 64)
            {
                return path;
            }

            int index = path.LastIndexOf('/');
            return index >= 0 && index < path.Length - 1 ? $".../{path[(index + 1)..]}" : path;
        }
        
    }
}
