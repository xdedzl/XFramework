using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Resource;
using XFramework.UI;

namespace XFramework.NodeKit.Editor
{
    public abstract class GraphRefEditorNodeBase<T> : ProcessEditorNode<T> where T : GraphRefNodeBase, new()
    {
        private readonly Label m_TargetGraphLabel;
        private readonly Button m_OpenGraphButton;
        private readonly Button m_LocateGraphButton;
        private readonly XNodeGraphThumbnailElement m_Thumbnail;
        private TextAsset m_TargetTextAsset;
        private XNodeGraphAsset m_TargetGraphAsset;

        protected GraphRefEditorNodeBase()
        {
            style.minWidth = 220;
            var previewRoot = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginTop = 4,
                    marginBottom = 4,
                    marginLeft = 4,
                    marginRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 6,
                    paddingRight = 6,
                    backgroundColor = new Color(0.14f, 0.14f, 0.14f, 0.95f),
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new Color(0.28f, 0.28f, 0.28f, 1f),
                    borderBottomColor = new Color(0.08f, 0.08f, 0.08f, 1f),
                    borderLeftColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderRightColor = new Color(0.22f, 0.22f, 0.22f, 1f)
                }
            };

            m_TargetGraphLabel = new Label("目标图: 未绑定")
            {
                style =
                {
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginBottom = 3
                }
            };

            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            m_OpenGraphButton = new Button(OpenTargetGraph)
            {
                text = "打开",
                tooltip = "打开引用的 Graph 图"
            };
            m_OpenGraphButton.style.flexGrow = 1f;
            m_OpenGraphButton.style.marginRight = 3f;

            m_LocateGraphButton = new Button(LocateTargetGraph)
            {
                text = "定位",
                tooltip = "在 Project 中定位引用的图资源"
            };
            m_LocateGraphButton.style.flexGrow = 1f;
            m_LocateGraphButton.style.marginLeft = 3f;

            buttonRow.Add(m_OpenGraphButton);
            buttonRow.Add(m_LocateGraphButton);

            m_Thumbnail = new XNodeGraphThumbnailElement();

            previewRoot.Add(m_TargetGraphLabel);
            previewRoot.Add(buttonRow);
            previewRoot.Add(m_Thumbnail);
            contentContainer.Add(previewRoot);

            RefreshTargetGraphPreview();
            RefreshExpandedState();
            RefreshPorts();
        }

        public override void OnRuntimeNodeChange()
        {
            base.OnRuntimeNodeChange();
            RefreshTargetGraphPreview();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            RefreshTargetGraphPreview();
        }

        private void OpenTargetGraph()
        {
            if (!RefreshTargetGraphPreview() || m_TargetGraphAsset == null)
            {
                return;
            }

            XNodeGraphEditorWindow.OpenGraphAsset(m_TargetGraphAsset, AssetDatabase.GetAssetPath(m_TargetTextAsset));
        }

        private void LocateTargetGraph()
        {
            RefreshTargetGraphPreview();
            if (m_TargetTextAsset == null)
            {
                return;
            }

            Selection.activeObject = m_TargetTextAsset;
            EditorGUIUtility.PingObject(m_TargetTextAsset);
        }

        private bool RefreshTargetGraphPreview()
        {
            m_TargetTextAsset = null;
            m_TargetGraphAsset = null;
            m_Thumbnail.SetGraph(null);

            if (runtimeNode == null)
            {
                SetPreviewState("目标图: 未绑定", "当前节点尚未绑定运行时数据。", false, false, WarningColor);
                return false;
            }

            if (!TryGetGraphAssetPath(out string graphAssetPath, out string pathError))
            {
                SetPreviewState($"目标图: {pathError}", pathError, false, false, ErrorColor);
                return false;
            }

            if (string.IsNullOrWhiteSpace(graphAssetPath))
            {
                SetPreviewState("目标图: 未配置", "当前引用节点没有配置目标图资源。", false, false, WarningColor);
                return false;
            }

            m_TargetTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(graphAssetPath);
            if (m_TargetTextAsset == null)
            {
                string missingMessage = $"资源不存在: {graphAssetPath}";
                SetPreviewState($"目标图: {ShortenPath(graphAssetPath)} 缺失", missingMessage, false, false, ErrorColor);
                return false;
            }

            try
            {
                m_TargetGraphAsset = m_TargetTextAsset.ToXTextAsset<XNodeGraphAsset>();
            }
            catch (Exception exception)
            {
                string parseMessage = $"解析失败: {graphAssetPath}\n{exception.Message}";
                SetPreviewState($"目标图: {ShortenPath(graphAssetPath)} 解析失败", parseMessage, false, true, ErrorColor);
                return false;
            }

            if (m_TargetGraphAsset == null)
            {
                string nullMessage = $"解析结果为空: {graphAssetPath}";
                SetPreviewState($"目标图: {ShortenPath(graphAssetPath)} 解析失败", nullMessage, false, true, ErrorColor);
                return false;
            }

            SetPreviewState($"目标图: {ShortenPath(graphAssetPath)}", graphAssetPath, true, true, NormalColor);
            m_Thumbnail.SetGraph(m_TargetGraphAsset);
            return true;
        }

        private void SetPreviewState(string labelText, string tooltip, bool canOpen, bool canLocate, Color color)
        {
            m_TargetGraphLabel.text = labelText;
            m_TargetGraphLabel.tooltip = tooltip;
            m_TargetGraphLabel.style.color = color;
            m_OpenGraphButton.SetEnabled(canOpen);
            m_LocateGraphButton.SetEnabled(canLocate);
        }

        private bool TryGetGraphAssetPath(out string graphAssetPath, out string error)
        {
            graphAssetPath = null;
            error = null;

            if (TryGetAssetPathFieldValue(out graphAssetPath))
            {
                return true;
            }

            if (TryGetDataTableRefAssetPath(out graphAssetPath, out error))
            {
                return string.IsNullOrEmpty(error);
            }

            try
            {
                graphAssetPath = runtimeNode.GetGraphAssetPath();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private bool TryGetAssetPathFieldValue(out string graphAssetPath)
        {
            graphAssetPath = null;
            foreach (FieldInfo field in GetRuntimeNodeFields())
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                AssetPathAttribute assetPathAttribute = field.GetCustomAttribute<AssetPathAttribute>(true);
                if (assetPathAttribute?.targetType == null || !typeof(TextAsset).IsAssignableFrom(assetPathAttribute.targetType))
                {
                    continue;
                }

                graphAssetPath = field.GetValue(runtimeNode) as string;
                return true;
            }

            return false;
        }

        private bool TryGetDataTableRefAssetPath(out string graphAssetPath, out string error)
        {
            graphAssetPath = null;
            error = null;
            foreach (FieldInfo field in GetRuntimeNodeFields())
            {
                DataTableRefAttribute dataTableRefAttribute = field.GetCustomAttribute<DataTableRefAttribute>(true);
                if (dataTableRefAttribute == null)
                {
                    continue;
                }

                object keyValue = field.GetValue(runtimeNode);
                if (IsEmptyReferenceValue(keyValue))
                {
                    graphAssetPath = string.Empty;
                    return true;
                }

                if (TryGetRowAssetPath(dataTableRefAttribute.tableType, keyValue, out graphAssetPath, out error))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetRowAssetPath(Type tableType, object keyValue, out string graphAssetPath, out string error)
        {
            graphAssetPath = null;
            error = null;
            if (tableType == null)
            {
                error = "DataTableRef 缺少表类型";
                return false;
            }

            MethodInfo getDataMethod = tableType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(method =>
                    string.Equals(method.Name, "GetData", StringComparison.Ordinal)
                    && !method.IsGenericMethod
                    && method.GetParameters().Length == 1);
            if (getDataMethod == null)
            {
                return false;
            }

            object row;
            try
            {
                row = getDataMethod.Invoke(null, new[] { keyValue });
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException?.Message ?? exception.Message;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return true;
            }

            if (row == null)
            {
                error = $"数据表引用无效: {keyValue}";
                return true;
            }

            foreach (FieldInfo rowField in row.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (rowField.FieldType != typeof(string))
                {
                    continue;
                }

                AssetPathAttribute assetPathAttribute = rowField.GetCustomAttribute<AssetPathAttribute>(true);
                if (assetPathAttribute?.targetType == null || !typeof(TextAsset).IsAssignableFrom(assetPathAttribute.targetType))
                {
                    continue;
                }

                graphAssetPath = rowField.GetValue(row) as string;
                return true;
            }

            return false;
        }

        private FieldInfo[] GetRuntimeNodeFields()
        {
            return runtimeNode.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static bool IsEmptyReferenceValue(object value)
        {
            return value == null
                   || value is int intValue && intValue == 0
                   || value is uint uintValue && uintValue == 0u
                   || value is long longValue && longValue == 0L
                   || value is ulong ulongValue && ulongValue == 0UL;
        }

        private static string ShortenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= 42)
            {
                return path;
            }

            int index = path.LastIndexOf('/');
            return index >= 0 && index < path.Length - 1 ? $".../{path[(index + 1)..]}" : path;
        }

        private static Color NormalColor => new Color(0.78f, 0.9f, 0.78f, 1f);
        private static Color WarningColor => new Color(1f, 0.78f, 0.35f, 1f);
        private static Color ErrorColor => new Color(1f, 0.45f, 0.42f, 1f);
    }

    [MenuPath("流程节点/Graph Ref Node")]
    [TargetRuntimeNode(typeof(GraphRefNode))]
    public class GraphRefEditorNode : GraphRefEditorNodeBase<GraphRefNode>
    {
    }
}
