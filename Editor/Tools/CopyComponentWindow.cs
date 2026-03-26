using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace XFramework.Editor
{
    public class CopyComponentWindow : EditorWindow
    {
        private GameObject sourceObj;
        private List<GameObject> targetObjs = new List<GameObject>();
        
        private Component[] sourceComponents;
        private bool[] selectedComponents;

        private VisualElement targetsContainer;
        private VisualElement componentsContainer;

        private Toggle posToggle;
        private Toggle rotToggle;
        private Toggle scaleToggle;
        private List<Toggle> compToggles = new List<Toggle>();

        [MenuItem("XFramework/Tools/Copy Components Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<CopyComponentWindow>("Copy Components");
            window.minSize = new Vector2(350, 400);
            window.Show();
        }

        private void OnEnable()
        {
            if (Selection.activeGameObject != null && sourceObj == null)
            {
                sourceObj = Selection.activeGameObject;
                RefreshComponentsData();
            }
        }

        private void OnSelectionChange()
        {
            // 对于UI Toolkit，不需要过于频繁地改变，特别是用户如果在操作
            // 如果必须同步拾取，可以保持，但体验上用户手动拖入更佳
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 5;
            root.style.paddingRight = 5;
            root.style.paddingTop = 5;
            root.style.paddingBottom = 5;

            var titleLbl = new Label("组件批量复制工具");
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.marginBottom = 5;
            root.Add(titleLbl);

            var sourceField = new ObjectField("来源 GameObject (Source)") { objectType = typeof(GameObject), value = sourceObj };
            sourceField.RegisterValueChangedCallback(evt => {
                sourceObj = evt.newValue as GameObject;
                RefreshComponentsData();
                BuildComponentsUI();
            });
            root.Add(sourceField);

            var targetTitle = new Label("目标 GameObject (Targets):");
            targetTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            targetTitle.style.marginTop = 10;
            targetTitle.style.marginBottom = 5;
            root.Add(targetTitle);

            var dropArea = new VisualElement();
            dropArea.style.height = 40;
            dropArea.style.backgroundColor = new Color(0, 0, 0, 0.1f);
            dropArea.style.borderTopWidth = dropArea.style.borderBottomWidth = dropArea.style.borderLeftWidth = dropArea.style.borderRightWidth = 1;
            dropArea.style.borderTopColor = dropArea.style.borderBottomColor = dropArea.style.borderLeftColor = dropArea.style.borderRightColor = Color.gray;
            dropArea.style.justifyContent = Justify.Center;
            dropArea.style.alignItems = Align.Center;
            dropArea.Add(new Label("将目标 GameObjects 拖拽到此区域"));
            
            dropArea.RegisterCallback<DragUpdatedEvent>(e => {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            dropArea.RegisterCallback<DragPerformEvent>(e => {
                DragAndDrop.AcceptDrag();
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go && go != sourceObj && !targetObjs.Contains(go))
                    {
                        targetObjs.Add(go);
                    }
                }
                BuildTargetsUI();
            });
            root.Add(dropArea);

            targetsContainer = new VisualElement();
            targetsContainer.style.marginTop = 5;
            root.Add(targetsContainer);

            var btnClear = new Button(() => { targetObjs.Clear(); BuildTargetsUI(); }) { text = "清空目标列表 (Clear Targets)" };
            btnClear.style.marginTop = 5;
            root.Add(btnClear);

            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = Color.gray;
            divider.style.marginTop = 10;
            divider.style.marginBottom = 10;
            root.Add(divider);

            componentsContainer = new VisualElement();
            componentsContainer.style.flexGrow = 1;
            root.Add(componentsContainer);

            BuildTargetsUI();
            BuildComponentsUI();
        }

        private void BuildTargetsUI()
        {
            targetsContainer.Clear();
            for (int i = 0; i < targetObjs.Count; i++)
            {
                int index = i;
                var row = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
                var objField = new ObjectField() { objectType = typeof(GameObject), value = targetObjs[i], style = { flexGrow = 1 } };
                objField.RegisterValueChangedCallback(evt => targetObjs[index] = evt.newValue as GameObject);
                
                var btnDel = new Button(() => { targetObjs.RemoveAt(index); BuildTargetsUI(); }) { text = "X", style = { width = 25 } };
                row.Add(objField);
                row.Add(btnDel);
                targetsContainer.Add(row);
            }
        }

        private VisualElement CreateIconToggle(string label, string iconName, out Toggle tOut, bool defaultVal)
        {
            var row = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 20, marginTop = 2 } };
            tOut = new Toggle() { value = defaultVal };
            var icon = new Image() { image = EditorGUIUtility.IconContent(iconName).image, style = { width = 16, height = 16, marginRight = 5 } };
            var l = new Label(label);
            row.Add(tOut);
            row.Add(icon);
            row.Add(l);
            return row;
        }

        private void BuildComponentsUI()
        {
            componentsContainer.Clear();
            compToggles.Clear();
            posToggle = rotToggle = scaleToggle = null;

            if (sourceObj == null || sourceComponents == null || sourceComponents.Length == 0) return;

            var rowTr = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 5, alignItems = Align.Center } };
            var lblTr = new Label("Transform选项 : ") { style = { width = 120, unityFontStyleAndWeight = FontStyle.Bold } };
            rowTr.Add(lblTr);
            rowTr.Add(new Button(() => { posToggle.value = rotToggle.value = scaleToggle.value = true; }) { text = "全选", style = { width = 60 } });
            rowTr.Add(new Button(() => { posToggle.value = rotToggle.value = scaleToggle.value = false; }) { text = "全不选", style = { width = 60 } });
            componentsContainer.Add(rowTr);

            componentsContainer.Add(CreateIconToggle("Local Position", "MoveTool", out posToggle, false));
            componentsContainer.Add(CreateIconToggle("Local Rotation", "RotateTool", out rotToggle, false));
            componentsContainer.Add(CreateIconToggle("Local Scale", "ScaleTool", out scaleToggle, false));

            var rowOther = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 15, alignItems = Align.Center } };
            var lblOther = new Label("其他组件选项 : ") { style = { width = 120, unityFontStyleAndWeight = FontStyle.Bold } };
            rowOther.Add(lblOther);
            rowOther.Add(new Button(() => { foreach (var t in compToggles) t.value = true; }) { text = "全选", style = { width = 60 } });
            rowOther.Add(new Button(() => { foreach (var t in compToggles) t.value = false; }) { text = "全不选", style = { width = 60 } });
            componentsContainer.Add(rowOther);

            var scrollView = new ScrollView();
            scrollView.style.marginTop = 5;
            scrollView.style.flexGrow = 1;

            for (int i = 0; i < sourceComponents.Length; i++)
            {
                var comp = sourceComponents[i];
                if (comp == null || comp is Transform) continue;

                int index = i;
                var rowComp = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 2, alignItems = Align.Center } };
                var t = new Toggle() { value = selectedComponents[i] };
                t.RegisterValueChangedCallback(e => selectedComponents[index] = e.newValue);
                compToggles.Add(t);
                
                var f = new ObjectField() { objectType = typeof(Component), value = comp, style = { flexGrow = 1 } };
                rowComp.Add(t);
                rowComp.Add(f);
                scrollView.Add(rowComp);
            }
            componentsContainer.Add(scrollView);

            var rowBtn = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };
            var btnCopy = new Button(() => CopySelectedComponents(false)) { text = "复制选中的组件 (Copy)", style = { height = 40, flexGrow = 1 } };
            var btnCut = new Button(() => CopySelectedComponents(true)) { text = "剪切选中的组件 (Cut)", style = { height = 40, flexGrow = 1 } };
            rowBtn.Add(btnCopy);
            rowBtn.Add(btnCut);
            componentsContainer.Add(rowBtn);
        }

        private void RefreshComponentsData()
        {
            if (sourceObj != null)
            {
                sourceComponents = sourceObj.GetComponents<Component>();
                selectedComponents = new bool[sourceComponents.Length];
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

            bool cp = posToggle?.value ?? false;
            bool cr = rotToggle?.value ?? false;
            bool cs = scaleToggle?.value ?? false;

            ExecuteCopyPaste(sourceObj, targetObjs.ToArray(), compsToCopy.ToArray(), isCut, cp, cr, cs);
            
            if (isCut)
            {
                RefreshComponentsData();
                BuildComponentsUI();
            }
        }

        public static void ExecuteCopyPaste(GameObject source, GameObject[] targets, Component[] componentsToCopy, bool isCut, bool copyPos = false, bool copyRot = false, bool copyScale = false)
        {
            if (source == null || targets == null || targets.Length == 0) return;

            Undo.SetCurrentGroupName(isCut ? "批量剪切组件" : "批量复制组件");
            int group = Undo.GetCurrentGroup();

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

        private Toggle posToggle;
        private Toggle rotToggle;
        private Toggle scaleToggle;
        private List<Toggle> compToggles = new List<Toggle>();

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

            for (int i = 0; i < allComponents.Length; i++)
            {
                if (allComponents[i] != null && !(allComponents[i] is Transform))
                {
                    selectedFlags[i] = true;
                }
            }
            BuildUI();
        }

        private VisualElement CreateIconToggle(string label, string iconName, out Toggle tOut, bool defaultVal)
        {
            var row = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 20, marginTop = 2 } };
            tOut = new Toggle() { value = defaultVal };
            var icon = new Image() { image = EditorGUIUtility.IconContent(iconName).image, style = { width = 16, height = 16, marginRight = 5 } };
            var l = new Label(label);
            row.Add(tOut);
            row.Add(icon);
            row.Add(l);
            return row;
        }

        public void CreateGUI()
        {
            if (sourceObj != null && allComponents != null)
            {
                BuildUI();
            }
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            if (sourceObj == null || allComponents == null) return;

            root.style.paddingLeft = 5;
            root.style.paddingRight = 5;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 5;

            var lblTitle = new Label($"来源: {sourceObj.name}") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } };
            root.Add(lblTitle);

            var rowTr = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 5, alignItems = Align.Center } };
            var lblTr = new Label("Transform选项 : ") { style = { width = 110, unityFontStyleAndWeight = FontStyle.Bold } };
            rowTr.Add(lblTr);
            rowTr.Add(new Button(() => { posToggle.value = rotToggle.value = scaleToggle.value = true; }) { text = "全选", style = { width = 50 } });
            rowTr.Add(new Button(() => { posToggle.value = rotToggle.value = scaleToggle.value = false; }) { text = "全不选", style = { width = 50 } });
            root.Add(rowTr);

            root.Add(CreateIconToggle("Local Position", "MoveTool", out posToggle, false));
            root.Add(CreateIconToggle("Local Rotation", "RotateTool", out rotToggle, false));
            root.Add(CreateIconToggle("Local Scale", "ScaleTool", out scaleToggle, false));

            var rowOther = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 15, alignItems = Align.Center } };
            var lblOther = new Label("其他组件选项 : ") { style = { width = 110, unityFontStyleAndWeight = FontStyle.Bold } };
            rowOther.Add(lblOther);
            rowOther.Add(new Button(() => { foreach (var t in compToggles) t.value = true; }) { text = "全选", style = { width = 50 } });
            rowOther.Add(new Button(() => { foreach (var t in compToggles) t.value = false; }) { text = "全不选", style = { width = 50 } });
            root.Add(rowOther);

            var scrollView = new ScrollView();
            scrollView.style.marginTop = 5;
            scrollView.style.flexGrow = 1;

            for (int i = 0; i < allComponents.Length; i++)
            {
                var comp = allComponents[i];
                if (comp == null || comp is Transform) continue;

                int index = i;
                var rowComp = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 2, alignItems = Align.Center } };
                var t = new Toggle() { value = selectedFlags[i] };
                t.RegisterValueChangedCallback(e => selectedFlags[index] = e.newValue);
                compToggles.Add(t);
                
                var f = new ObjectField() { objectType = typeof(Component), value = comp, style = { flexGrow = 1 } };
                rowComp.Add(t);
                rowComp.Add(f);
                scrollView.Add(rowComp);
            }
            root.Add(scrollView);

            var rowBtn = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };
            rowBtn.Add(new Button(() => ConfirmSelection(false)) { text = "确认复制 (Copy)", style = { height = 35, flexGrow = 1 } });
            rowBtn.Add(new Button(() => ConfirmSelection(true)) { text = "确认剪切 (Cut)", style = { height = 35, flexGrow = 1 } });
            root.Add(rowBtn);
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

            bool cp = posToggle?.value ?? false;
            bool cr = rotToggle?.value ?? false;
            bool cs = scaleToggle?.value ?? false;

            if (selectedComps.Count > 0 || cp || cr || cs)
            {
                CopyComponentMenuTool.SetClipboard(sourceObj, selectedComps.ToArray(), isCutMode, cp, cr, cs);
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
