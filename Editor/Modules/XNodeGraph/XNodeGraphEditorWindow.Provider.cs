using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Reflection;

namespace XFramework.NodeKit.Editor
{
    public class XNodeSelectWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        private XNodeGraphView graphView;
        private XNodeGraphEditorWindow editorWindow;

        public static XNodeSelectWindowProvider Create(XNodeGraphView graphView, XNodeGraphEditorWindow editorWindow)
        {
            var provider = CreateInstance<XNodeSelectWindowProvider>();
            provider.Init(graphView, editorWindow);
            return provider;
        }

        private void Init(XNodeGraphView targetGraphView, XNodeGraphEditorWindow targetWindow)
        {
            graphView = targetGraphView;
            editorWindow = targetWindow;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Node"))
            };

            var types = XNodeKitEditor.GetAllXNodeEditorType().ToList();
            types.Sort((a, b) =>
            {
                var a_mp = a.GetCustomAttribute<MenuPathAttribute>();
                var b_mp = b.GetCustomAttribute<MenuPathAttribute>();
                var aKey = a_mp is null ? a.Name : a_mp.path;
                var bKey = b_mp is null ? b.Name : b_mp.path;
                var aPaths = aKey.Split("/");
                var bPaths = bKey.Split("/");
                var count = Math.Min(aPaths.Length, bPaths.Length);
                for (int i = 0; i < count; i++)
                {
                    if (i == count - 1 && aPaths.Length != bPaths.Length)
                    {
                        return aPaths.Length - bPaths.Length;
                    }
                    else
                    {
                        var compare = string.Compare(aPaths[i], bPaths[i], StringComparison.Ordinal);
                        if (compare != 0)
                        {
                            return compare;
                        }
                    }
                }

                return 0;
            });
            var groupPaths = new HashSet<string>();
            foreach (var type in types)
            {
                var menuPath = type.GetCustomAttribute<MenuPathAttribute>();
                if (menuPath is not null)
                {
                    var spiltPaths = menuPath.path.Split("/");
                    for (int i = 0; i < spiltPaths.Length; i++)
                    {
                        var path = string.Join("/", spiltPaths.Take(i + 1));
                        if (i == spiltPaths.Length - 1)
                        {
                            entries.Add(new SearchTreeEntry(new GUIContent("     " + spiltPaths[i])) { level = i + 1, userData = type });
                        }
                        else
                        {
                            if (!groupPaths.Contains(path))
                            {
                                entries.Add(new SearchTreeGroupEntry(new GUIContent(spiltPaths[i])) { level = i + 1 });
                                groupPaths.Add(path);
                            }
                        }
                    }
                }
                else
                {
                    entries.Add(new SearchTreeEntry(new GUIContent(type.Name)) { level = 1, userData = type });
                }
            }
            
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (graphView == null || editorWindow == null)
            {
                Debug.LogError("XNodeSelectWindowProvider missing graph context.");
                return false;
            }

            if (searchTreeEntry.userData is not Type type)
            {
                Debug.LogError("userData is not Type");
                return false;
            }
            
            var mousePosition = editorWindow.rootVisualElement.ChangeCoordinatesTo(editorWindow.rootVisualElement.parent, context.screenMousePosition - editorWindow.position.position);
            var graphMousePosition = graphView.contentViewContainer.WorldToLocal(mousePosition);

            var targetNodeType = type.GetCustomAttribute<TargetRuntimeNodeAttribute>();
            
            if (targetNodeType is null)
            {
                Debug.LogError($"Type {type.Name} is missing TargetRuntimeNodeAttribute");
                return false;
            }
            
            var runtimeNodeType = targetNodeType.targetType;
            var runtimeNode = Utility.Reflection.CreateInstance<IXNode>(runtimeNodeType);
            if (runtimeNode is null)
            {
                Debug.LogError($"Cannot create instance of runtime node type {runtimeNodeType.FullName}");
                return false;
            }
            
            var node = Utility.Reflection.CreateInstance<XEditorNodeBase>(type);
            node.SetRuntimeNode(runtimeNode);
            graphView.AddXNode(node);
            node.SetPosition(new Rect(graphMousePosition, node.GetPosition().size));
            return true;
        }
    }
}
