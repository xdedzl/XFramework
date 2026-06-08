using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace XFramework.NodeKit.Editor
{
    public static class XNodeKitEditor
    {
        public static IEnumerable<Type> GetAllXNodeEditorType()
        {
            var types = Utility.Reflection.GetAssignableTypes(typeof(XEditorNodeBase), "XFrameworkEditor", "Assembly-CSharp-Editor");
            return types;
        }
        
        public static IDictionary<Type, Type> GetEditorNode2RuntimeNode()
        {
            var map = new Dictionary<Type, Type>();
            var types = GetAllXNodeEditorType();
            foreach (var editorType in types)
            {
                var targetRuntimeNodeAttr = editorType.GetCustomAttribute<TargetRuntimeNodeAttribute>(false);
                if (targetRuntimeNodeAttr?.targetType != null)
                {
                    var runtimeNodeType = targetRuntimeNodeAttr.targetType;
                    if (!map.ContainsValue(runtimeNodeType))
                    {
                        map[editorType] = runtimeNodeType;
                    }
                    else
                    {
                        var existEditorType = map.First(kvp => kvp.Value == runtimeNodeType).Key;
                        Debug.LogError($"XNodeBase type {runtimeNodeType.FullName} has repeated EditorNode types, {existEditorType.FullName}, {editorType.FullName}");
                    }
                }
                else
                {
                    Debug.LogError($"XEditorNodeBase type {editorType.FullName} missing TargetRuntimeNodeAttribute");
                }
            }

            return map;
        }
        
        public static IDictionary<Type, Type> GetRuntimeNode2EditorNode()
        {
            var map = new Dictionary<Type, Type>();
            var editorToRuntimeMap = GetEditorNode2RuntimeNode();
            foreach (var kvp in editorToRuntimeMap)
            {
                map[kvp.Value] = kvp.Key;
            }
            return map;
        }
    }
}
