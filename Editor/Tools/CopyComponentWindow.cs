using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;

namespace XFramework.Editor
{
    public class CopyComponentWindow : EditorWindow
    {
        private GameObject sourceObj;
        private List<GameObject> targetObjs = new List<GameObject>();
        
        private Component[] sourceComponents;
        private bool[] selectedComponents;

        private bool copyPosition = false;
        private bool copyRotation = false;
        private bool copyScale = false;

        private Vector2 scrollPos;

        [MenuItem("XFramework/Tools/Copy Components Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<CopyComponentWindow>("Copy Components");
            window.minSize = new Vector2(350, 400);
            window.Show();
        }

        private void OnEnable()
        {
            UpdateSelection();
        }

        private void OnSelectionChange()
        {
            UpdateSelection();
            Repaint();
        }

        private void UpdateSelection()
        {
            // 如果需要可以在这里自动拾取当前选中的作为Target等，这里选择让用户手动拖拽或分配
            if (Selection.activeGameObject != null && sourceObj == null)
            {
                sourceObj = Selection.activeGameObject;
                RefreshComponents();
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("组件批量复制工具", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            sourceObj = (GameObject)EditorGUILayout.ObjectField("来源 GameObject (Source)", sourceObj, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshComponents();
            }

            EditorGUILayout.Space();
            GUILayout.Label("目标 GameObject (Targets):", EditorStyles.boldLabel);
            
            // Drop area for targets
            UnityEngine.Event evt = UnityEngine.Event.current;
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "将目标 GameObjects 拖拽到此区域", EditorStyles.helpBox);

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (Object draggedObj in DragAndDrop.objectReferences)
                        {
                            if (draggedObj is GameObject go && !targetObjs.Contains(go) && go != sourceObj)
                            {
                                targetObjs.Add(go);
                            }
                        }
                    }
                    UnityEngine.Event.current.Use();
                    break;
            }

            for (int i = 0; i < targetObjs.Count; i++)
            {
                GUILayout.BeginHorizontal();
                targetObjs[i] = (GameObject)EditorGUILayout.ObjectField(targetObjs[i], typeof(GameObject), true);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    targetObjs.RemoveAt(i);
                    i--;
                }
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("清空目标列表 (Clear Targets)"))
            {
                targetObjs.Clear();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            if (sourceObj != null && sourceComponents != null && sourceComponents.Length > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Transform选项 : ", GUILayout.Width(120));
                if (GUILayout.Button("全选", GUILayout.Width(60))) copyPosition = copyRotation = copyScale = true;
                if (GUILayout.Button("全不选", GUILayout.Width(60))) copyPosition = copyRotation = copyScale = false;
                GUILayout.EndHorizontal();

                copyPosition = EditorGUILayout.ToggleLeft(new GUIContent(" Local Position", EditorGUIUtility.IconContent("MoveTool").image), copyPosition);
                copyRotation = EditorGUILayout.ToggleLeft(new GUIContent(" Local Rotation", EditorGUIUtility.IconContent("RotateTool").image), copyRotation);
                copyScale    = EditorGUILayout.ToggleLeft(new GUIContent(" Local Scale", EditorGUIUtility.IconContent("ScaleTool").image), copyScale);
                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();
                GUILayout.Label("其他组件选项 : ", GUILayout.Width(120));
                if (GUILayout.Button("全选", GUILayout.Width(60)))
                {
                    for (int i = 0; i < selectedComponents.Length; i++) 
                        if (sourceComponents[i] != null && !(sourceComponents[i] is Transform)) selectedComponents[i] = true;
                }
                if (GUILayout.Button("全不选", GUILayout.Width(60)))
                {
                    for (int i = 0; i < selectedComponents.Length; i++) selectedComponents[i] = false;
                }
                GUILayout.EndHorizontal();

                scrollPos = GUILayout.BeginScrollView(scrollPos);

                for (int i = 0; i < sourceComponents.Length; i++)
                {
                    var comp = sourceComponents[i];
                    if (comp == null) continue; // 忽略Missing的脚本
                    if (comp is Transform) continue; // Transform通常不需要复制

                    GUILayout.BeginHorizontal();
                    selectedComponents[i] = EditorGUILayout.Toggle(selectedComponents[i], GUILayout.Width(20));
                    EditorGUILayout.ObjectField(comp, typeof(Component), true);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();

                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("复制选中的组件 (Copy)", GUILayout.Height(40)))
                {
                    CopySelectedComponents(false);
                }
                if (GUILayout.Button("剪切选中的组件 (Cut)", GUILayout.Height(40)))
                {
                    CopySelectedComponents(true);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void RefreshComponents()
        {
            if (sourceObj != null)
            {
                sourceComponents = sourceObj.GetComponents<Component>();
                selectedComponents = new bool[sourceComponents.Length];
                // 默认选中除了Transform以外的所有组件
                for (int i = 0; i < sourceComponents.Length; i++)
                {
                    if (sourceComponents[i] != null && !(sourceComponents[i] is Transform))
                    {
                        selectedComponents[i] = true;
                    }
                }
            }
            else
            {
                sourceComponents = null;
                selectedComponents = null;
            }
        }

        private void CopySelectedComponents(bool isCut)
        {
            if (sourceObj == null || targetObjs.Count == 0 || sourceComponents == null)
            {
                EditorUtility.DisplayDialog("错误", "请确保设置了来源(Source)并且至少有一个目标(Target)。", "OK");
                return;
            }

            List<Component> compsToCopy = new List<Component>();
            for (int i = 0; i < sourceComponents.Length; i++)
            {
                if (selectedComponents[i] && sourceComponents[i] != null)
                {
                    compsToCopy.Add(sourceComponents[i]);
                }
            }

            ExecuteCopyPaste(sourceObj, targetObjs.ToArray(), compsToCopy.ToArray(), isCut, copyPosition, copyRotation, copyScale);
            
            if (isCut)
            {
                RefreshComponents();
            }
        }

        public static void ExecuteCopyPaste(GameObject source, GameObject[] targets, Component[] componentsToCopy, bool isCut, bool copyPos = false, bool copyRot = false, bool copyScale = false)
        {
            if (source == null || targets == null || targets.Length == 0) return;

            Undo.SetCurrentGroupName(isCut ? "批量剪切组件" : "批量复制组件");
            int group = Undo.GetCurrentGroup();

            // 拷贝 Transform 属性
            if (copyPos || copyRot || copyScale)
            {
                Transform sourceTrans = source.transform;
                foreach (var target in targets)
                {
                    if (target == null) continue;
                    Undo.RecordObject(target.transform, "Paste Transform");
                    if (copyPos) target.transform.localPosition = sourceTrans.localPosition;
                    if (copyRot) target.transform.localRotation = sourceTrans.localRotation;
                    if (copyScale) target.transform.localScale = sourceTrans.localScale;
                }
            }

            if (componentsToCopy != null)
            {
                for (int i = 0; i < componentsToCopy.Length; i++)
                {
                    var sourceComp = componentsToCopy[i];
                    if (sourceComp == null || sourceComp is Transform) continue;

                    UnityEditorInternal.ComponentUtility.CopyComponent(sourceComp);

                    foreach (var target in targets)
                    {
                        if (target == null) continue;

                        Undo.RecordObject(target, "Paste Component");
                        Component existingComp = target.GetComponent(sourceComp.GetType());
                        if (existingComp != null)
                        {
                            UnityEditorInternal.ComponentUtility.PasteComponentValues(existingComp);
                        }
                        else
                        {
                            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(target);
                        }
                    }
                }

                if (isCut)
                {
                    // 倒序删除更安全
                    for (int i = componentsToCopy.Length - 1; i >= 0; i--)
                    {
                        var comp = componentsToCopy[i];
                        if (comp != null && !(comp is Transform))
                        {
                            Undo.DestroyObjectImmediate(comp);
                        }
                    }
                }
            }

            Undo.CollapseUndoOperations(group);
            string actionName = isCut ? "剪切" : "复制";
            
            // 只有多目标明确通过面板操作时才弹窗，1对1右键只需要Log提示，通过 targets 数量和 context 可以隐式判断。但可以在此统一。
            if (targets.Length > 1) 
            {
                EditorUtility.DisplayDialog("完成", $"成功将所选组件从 {source.name} {actionName}到了 {targets.Length} 个对象上！", "OK");
            }
            Debug.Log($"[批量{actionName}组件] 成功从 {source.name} {actionName}到 {targets.Length} 个目标对象。");
        }
    }

    public class ClipboardSelectWindow : EditorWindow
    {
        private GameObject sourceObj;
        private Component[] allComponents;
        private bool[] selectedFlags;

        private bool copyPosition = false;
        private bool copyRotation = false;
        private bool copyScale = false;

        private Vector2 scrollPos;

        public static void ShowForm(GameObject source)
        {
            if (source == null) return;
            var window = GetWindow<ClipboardSelectWindow>(true, "提取组件选择", true);
            window.Init(source);
            window.minSize = new Vector2(300, 350);
            window.maxSize = new Vector2(300, 500);
            window.ShowUtility();
        }

        private void Init(GameObject source)
        {
            sourceObj = source;
            allComponents = sourceObj.GetComponents<Component>();
            selectedFlags = new bool[allComponents.Length];

            // 默认全选，除 Transform 外
            for (int i = 0; i < allComponents.Length; i++)
            {
                if (allComponents[i] != null && !(allComponents[i] is Transform))
                {
                    selectedFlags[i] = true;
                }
            }
        }

        private void OnGUI()
        {
            if (sourceObj == null || allComponents == null)
            {
                Close();
                return;
            }

            GUILayout.Space(10);
            GUILayout.Label($"来源: {sourceObj.name}", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Transform选项 : ", GUILayout.Width(110));
            if (GUILayout.Button("全选", GUILayout.Width(50))) copyPosition = copyRotation = copyScale = true;
            if (GUILayout.Button("全不选", GUILayout.Width(50))) copyPosition = copyRotation = copyScale = false;
            GUILayout.EndHorizontal();

            copyPosition = EditorGUILayout.ToggleLeft(new GUIContent(" Local Position", EditorGUIUtility.IconContent("MoveTool").image), copyPosition);
            copyRotation = EditorGUILayout.ToggleLeft(new GUIContent(" Local Rotation", EditorGUIUtility.IconContent("RotateTool").image), copyRotation);
            copyScale    = EditorGUILayout.ToggleLeft(new GUIContent(" Local Scale", EditorGUIUtility.IconContent("ScaleTool").image), copyScale);
            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.Label("其他组件选项 : ", GUILayout.Width(110));
            if (GUILayout.Button("全选", GUILayout.Width(50)))
            {
                for (int i = 0; i < selectedFlags.Length; i++) 
                    if (allComponents[i] != null && !(allComponents[i] is Transform)) selectedFlags[i] = true;
            }
            if (GUILayout.Button("全不选", GUILayout.Width(50)))
            {
                for (int i = 0; i < selectedFlags.Length; i++) selectedFlags[i] = false;
            }
            GUILayout.EndHorizontal();

            scrollPos = GUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < allComponents.Length; i++)
            {
                var comp = allComponents[i];
                if (comp == null || comp is Transform) continue;

                GUILayout.BeginHorizontal();
                selectedFlags[i] = EditorGUILayout.Toggle(selectedFlags[i], GUILayout.Width(20));
                EditorGUILayout.ObjectField(comp, typeof(Component), true);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("确认复制 (Copy)", GUILayout.Height(35)))
            {
                ConfirmSelection(false);
            }
            if (GUILayout.Button("确认剪切 (Cut)", GUILayout.Height(35)))
            {
                ConfirmSelection(true);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private void ConfirmSelection(bool isCutMode)
        {
            List<Component> selectedComps = new List<Component>();
            for (int i = 0; i < allComponents.Length; i++)
            {
                if (selectedFlags[i] && allComponents[i] != null)
                {
                    selectedComps.Add(allComponents[i]);
                }
            }

            if (selectedComps.Count > 0 || copyPosition || copyRotation || copyScale)
            {
                CopyComponentMenuTool.SetClipboard(sourceObj, selectedComps.ToArray(), isCutMode, copyPosition, copyRotation, copyScale);
                Debug.Log($"[组件选择] 已存入剪贴板。普通组件: {selectedComps.Count} 个，包含 Transform 属性。");
            }
            else
            {
                Debug.LogWarning("[组件选择] 未选择任何组件及属性，剪贴板未更新。");
            }
            
            Close();
        }
    }

    /// <summary>
    /// 提供在 Hierarchy 或 GameObject 上的快捷右键菜单，实现精准筛选的复制/剪切/粘贴。
    /// </summary>
    public static class CopyComponentMenuTool
    {
        private static Component[] copiedComponents;
        private static bool isCutMode;
        private static GameObject sourceObj;
        
        private static bool clipboardPos;
        private static bool clipboardRot;
        private static bool clipboardScale;

        public static void SetClipboard(GameObject source, Component[] comps, bool cutMode, bool pos, bool rot, bool scale)
        {
            sourceObj = source;
            copiedComponents = comps;
            isCutMode = cutMode;
            clipboardPos = pos;
            clipboardRot = rot;
            clipboardScale = scale;
        }

        [MenuItem("GameObject/XFramework 快捷组件/选择提取组件 (Copy & Cut...)", false, -10)]
        public static void ExtractSelected(MenuCommand menuCommand)
        {
            GameObject go = menuCommand.context as GameObject ?? Selection.activeGameObject;
            if (go == null) return;
            ClipboardSelectWindow.ShowForm(go);
        }

        [MenuItem("GameObject/XFramework 快捷组件/粘贴选中组件 (Paste)", false, -8)]
        public static void PasteComponents(MenuCommand menuCommand)
        {
            GameObject target = menuCommand.context as GameObject ?? Selection.activeGameObject;
            if (target == null) return;
            if (copiedComponents == null && !clipboardPos && !clipboardRot && !clipboardScale) return;

            CopyComponentWindow.ExecuteCopyPaste(sourceObj, new GameObject[] { target }, copiedComponents, isCutMode, clipboardPos, clipboardRot, clipboardScale);

            if (isCutMode)
            {
                // 剪切生效只有一次，粘贴完清空
                copiedComponents = null;
                sourceObj = null;
                isCutMode = false;
            }
        }

        [MenuItem("GameObject/XFramework 快捷组件/粘贴选中组件 (Paste)", true)]
        public static bool ValidatePasteComponents()
        {
            return sourceObj != null && ((copiedComponents != null && copiedComponents.Length > 0) || clipboardPos || clipboardRot || clipboardScale);
        }
    }
}
