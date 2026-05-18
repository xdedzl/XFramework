#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XFramework.Animation;

namespace XFramework.Editor
{
    public sealed class XAnimationClipBatchSettingsWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/XAnimation Clip Batch Settings";
        private const int ObjectPickerControlId = 731245;
        private const float LeftPaneWidth = 420f;

        private enum SourceRootKind
        {
            StandaloneAnimation,
            SingleClipFbx,
            MultiClipFbxGroup,
        }

        private enum NodeCheckState
        {
            Unchecked,
            Checked,
            Mixed,
        }

        private enum RotationBasedUpon
        {
            Original,
            BodyOrientation,
        }

        private enum PositionYBasedUpon
        {
            Original,
            CenterOfMass,
            Feet,
        }

        private enum PositionXZBasedUpon
        {
            Original,
            CenterOfMass,
        }

        [Serializable]
        private sealed class FieldOverride<T>
        {
            public bool enabled;
            public T value;
        }

        [Serializable]
        private sealed class BatchSettings
        {
            public FieldOverride<bool> loopTime = new FieldOverride<bool> { value = true };
            public FieldOverride<bool> loopPose = new FieldOverride<bool>();
            public FieldOverride<float> cycleOffset = new FieldOverride<float>();

            public FieldOverride<bool> rotationBakeIntoPose = new FieldOverride<bool> { value = true };
            public FieldOverride<RotationBasedUpon> rotationBasedUpon = new FieldOverride<RotationBasedUpon> { value = RotationBasedUpon.Original };
            public FieldOverride<float> rotationOffset = new FieldOverride<float>();

            public FieldOverride<bool> positionYBakeIntoPose = new FieldOverride<bool> { value = true };
            public FieldOverride<PositionYBasedUpon> positionYBasedUpon = new FieldOverride<PositionYBasedUpon> { value = PositionYBasedUpon.Original };
            public FieldOverride<float> positionYOffset = new FieldOverride<float>();

            public FieldOverride<bool> positionXZBakeIntoPose = new FieldOverride<bool> { value = true };
            public FieldOverride<PositionXZBasedUpon> positionXZBasedUpon = new FieldOverride<PositionXZBasedUpon> { value = PositionXZBasedUpon.Original };

            public FieldOverride<bool> mirror = new FieldOverride<bool>();
            public FieldOverride<bool> additiveReferencePose = new FieldOverride<bool>();
            public FieldOverride<float> poseFrame = new FieldOverride<float>();
        }

        private sealed class ClipLeaf
        {
            public string key;
            public string clipName;
            public string displayName;
            public string assetPath;
            public string hostFbxPath;
            public AnimationClip clip;
            public bool selected = true;

            public bool IsStandaloneAnimation => string.IsNullOrWhiteSpace(hostFbxPath);
        }

        private sealed class SourceRoot
        {
            public string key;
            public string displayName;
            public string assetPath;
            public SourceRootKind kind;
            public bool expanded = true;
            public readonly List<ClipLeaf> leaves = new List<ClipLeaf>();
        }

        private sealed class ApplySummary
        {
            public int modifiedLeafCount;
            public int modifiedStandaloneAnimationCount;
            public int reimportedFbxCount;
            public readonly List<string> warnings = new List<string>();
        }

        private readonly BatchSettings m_Settings = new BatchSettings();
        private readonly List<SourceRoot> m_SourceRoots = new List<SourceRoot>();

        private Vector2 m_SourceScroll;
        private Vector2 m_SettingsScroll;
        private string m_FilterText = string.Empty;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XAnimationClipBatchSettingsWindow window = GetWindow<XAnimationClipBatchSettingsWindow>();
            window.titleContent = new GUIContent("XAnim Batch");
            window.minSize = new Vector2(1180f, 680f);
            window.Show();
        }

        public static void ShowWindowWithClips(IEnumerable<AnimationClip> clips)
        {
            XAnimationClipBatchSettingsWindow window = GetWindow<XAnimationClipBatchSettingsWindow>();
            window.titleContent = new GUIContent("XAnim Batch");
            window.minSize = new Vector2(1180f, 680f);
            window.Show();
            window.Focus();
            window.ReplaceSourcesWithClips(clips);
        }

        private void OnGUI()
        {
            HandleObjectPickerEvents();
            HandleEscapeToClose();

            EditorGUILayout.Space(4f);
            DrawToolbar();
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSourcePane();
                DrawSettingsPane();
            }
        }

        private void HandleEscapeToClose()
        {
            UnityEngine.Event currentEvent = UnityEngine.Event.current;
            if (currentEvent == null ||
                currentEvent.type != EventType.KeyDown ||
                currentEvent.keyCode != KeyCode.Escape ||
                focusedWindow != this)
            {
                return;
            }

            currentEvent.Use();
            Close();
            GUIUtility.ExitGUI();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("+", "Open object picker"), EditorStyles.toolbarButton, GUILayout.Width(28f)))
                {
                    EditorGUIUtility.ShowObjectPicker<UnityEngine.Object>(null, true, string.Empty, ObjectPickerControlId);
                }

                if (GUILayout.Button("Add Selection", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                {
                    AddObjects(Selection.objects);
                }

                if (GUILayout.Button("Apply To Selected", EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    ApplyToSelected();
                }

                if (GUILayout.Button("Select All Visible", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                {
                    SetVisibleSelection(true);
                }

                if (GUILayout.Button("Deselect All Visible", EditorStyles.toolbarButton, GUILayout.Width(128f)))
                {
                    SetVisibleSelection(false);
                }

                if (GUILayout.Button("Remove Selected Sources", EditorStyles.toolbarButton, GUILayout.Width(148f)))
                {
                    RemoveSelectedSources();
                }

                if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    m_SourceRoots.Clear();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSourcePane()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPaneWidth)))
            {
                DrawDropArea();
                EditorGUILayout.Space(6f);

                m_FilterText = EditorGUILayout.TextField("Filter", m_FilterText ?? string.Empty);
                EditorGUILayout.Space(4f);

                EditorGUILayout.LabelField($"Sources: {m_SourceRoots.Count}", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(2f);

                m_SourceScroll = EditorGUILayout.BeginScrollView(m_SourceScroll, GUI.skin.box);
                if (m_SourceRoots.Count == 0)
                {
                    EditorGUILayout.HelpBox("Drag .fbx or .anim assets here, or use + / Add Selection.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < m_SourceRoots.Count; i++)
                    {
                        DrawRootEntry(m_SourceRoots[i]);
                    }
                }

                GUILayout.Space(6f);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSettingsPane()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                m_SettingsScroll = EditorGUILayout.BeginScrollView(m_SettingsScroll, GUI.skin.box);

                EditorGUILayout.LabelField("Batch Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Enable Override on any field you want to write. Disabled fields keep their current values.", MessageType.None);
                EditorGUILayout.Space(4f);

                DrawOverrideToggle("Loop Time", m_Settings.loopTime);
                DrawOverrideToggle("Loop Pose", m_Settings.loopPose);
                DrawOverrideFloatField("Cycle Offset", m_Settings.cycleOffset);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Root Transform Rotation", EditorStyles.boldLabel);
                DrawOverrideToggle("Bake Into Pose", m_Settings.rotationBakeIntoPose);
                DrawOverrideEnumPopup("Based Upon", m_Settings.rotationBasedUpon);
                DrawOverrideFloatField("Offset", m_Settings.rotationOffset);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Root Transform Position (Y)", EditorStyles.boldLabel);
                DrawOverrideToggle("Bake Into Pose", m_Settings.positionYBakeIntoPose);
                DrawOverrideEnumPopup("Based Upon (at Start)", m_Settings.positionYBasedUpon);
                DrawOverrideFloatField("Offset", m_Settings.positionYOffset);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Root Transform Position (XZ)", EditorStyles.boldLabel);
                DrawOverrideToggle("Bake Into Pose", m_Settings.positionXZBakeIntoPose);
                DrawOverrideEnumPopup("Based Upon (at Start)", m_Settings.positionXZBasedUpon);

                EditorGUILayout.Space(8f);
                DrawOverrideToggle("Mirror", m_Settings.mirror);
                DrawOverrideToggle("Additive Reference Pose", m_Settings.additiveReferencePose);
                DrawOverrideFloatField("Pose Frame", m_Settings.poseFrame);

                GUILayout.Space(6f);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, 62f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop .fbx / .anim assets here", EditorStyles.helpBox);

            UnityEngine.Event evt = UnityEngine.Event.current;
            if (!dropRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = HasSupportedObjects(DragAndDrop.objectReferences)
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddObjects(DragAndDrop.objectReferences);
                evt.Use();
            }
        }

        private void DrawRootEntry(SourceRoot root)
        {
            List<ClipLeaf> visibleLeaves = GetVisibleLeaves(root);
            if (visibleLeaves.Count == 0)
            {
                return;
            }

            if (root.kind == SourceRootKind.MultiClipFbxGroup)
            {
                DrawGroupRoot(root, visibleLeaves);
                if (!root.expanded)
                {
                    return;
                }

                for (int i = 0; i < visibleLeaves.Count; i++)
                {
                    DrawLeafEntry(visibleLeaves[i], 18f);
                }

                EditorGUILayout.Space(2f);
                return;
            }

            DrawLeafEntry(root.leaves[0], 0f);
            EditorGUILayout.Space(2f);
        }

        private void DrawGroupRoot(SourceRoot root, List<ClipLeaf> visibleLeaves)
        {
            NodeCheckState state = GetRootCheckState(root);
            Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect toggleRect = new Rect(rowRect.x, rowRect.y, 18f, rowRect.height);
            Rect foldoutRect = new Rect(rowRect.x + 18f, rowRect.y, rowRect.width - 18f, rowRect.height);

            bool oldMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = state == NodeCheckState.Mixed;
            bool newSelected = EditorGUI.Toggle(toggleRect, state == NodeCheckState.Checked);
            EditorGUI.showMixedValue = oldMixed;
            if (newSelected != (state == NodeCheckState.Checked))
            {
                SetRootSelected(root, newSelected);
            }

            string label = visibleLeaves.Count == root.leaves.Count
                ? $"{root.displayName} ({root.leaves.Count})"
                : $"{root.displayName} ({visibleLeaves.Count}/{root.leaves.Count})";
            root.expanded = EditorGUI.Foldout(foldoutRect, root.expanded, label, true);
        }

        private void DrawLeafEntry(ClipLeaf leaf, float indent)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            rowRect.x += indent;
            rowRect.width -= indent;

            Rect toggleRect = new Rect(rowRect.x, rowRect.y, 18f, rowRect.height);
            Rect fieldRect = new Rect(rowRect.x + 20f, rowRect.y, rowRect.width - 20f, rowRect.height);

            leaf.selected = EditorGUI.Toggle(toggleRect, leaf.selected);
            using (new EditorGUI.DisabledScope(true))
            {
                GUIContent label = new GUIContent(leaf.displayName, leaf.displayName);
                EditorGUI.ObjectField(fieldRect, label, leaf.clip, typeof(AnimationClip), false);
            }
        }

        private void DrawOverrideToggle(string label, FieldOverride<bool> field)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                field.enabled = GUILayout.Toggle(field.enabled, GUIContent.none, GUILayout.Width(18f));
                using (new EditorGUI.DisabledScope(!field.enabled))
                {
                    field.value = EditorGUILayout.ToggleLeft(label, field.value);
                }
            }
        }

        private void DrawOverrideFloatField(string label, FieldOverride<float> field)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                field.enabled = GUILayout.Toggle(field.enabled, GUIContent.none, GUILayout.Width(18f));
                using (new EditorGUI.DisabledScope(!field.enabled))
                {
                    field.value = EditorGUILayout.FloatField(label, field.value);
                }
            }
        }

        private void DrawOverrideEnumPopup<TEnum>(string label, FieldOverride<TEnum> field) where TEnum : Enum
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                field.enabled = GUILayout.Toggle(field.enabled, GUIContent.none, GUILayout.Width(18f));
                using (new EditorGUI.DisabledScope(!field.enabled))
                {
                    field.value = (TEnum)EditorGUILayout.EnumPopup(label, field.value);
                }
            }
        }

        private void ApplyToSelected()
        {
            List<ClipLeaf> selectedLeaves = m_SourceRoots
                .SelectMany(root => root.leaves)
                .Where(leaf => leaf.selected)
                .ToList();

            if (selectedLeaves.Count == 0)
            {
                EditorUtility.DisplayDialog("XAnimation Clip Batch Settings", "No animation clips are selected.", "OK");
                return;
            }

            ApplySummary summary = new ApplySummary();

            List<ClipLeaf> standaloneLeaves = selectedLeaves.Where(leaf => leaf.IsStandaloneAnimation).ToList();
            for (int i = 0; i < standaloneLeaves.Count; i++)
            {
                if (ApplyStandaloneAnimationSettings(standaloneLeaves[i], summary))
                {
                    summary.modifiedLeafCount++;
                    summary.modifiedStandaloneAnimationCount++;
                }
            }

            IGrouping<string, ClipLeaf>[] groupedFbxLeaves = selectedLeaves
                .Where(leaf => !leaf.IsStandaloneAnimation)
                .GroupBy(leaf => leaf.hostFbxPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int i = 0; i < groupedFbxLeaves.Length; i++)
            {
                if (ApplyFbxAnimationSettings(groupedFbxLeaves[i], summary, out int modifiedCount))
                {
                    summary.reimportedFbxCount++;
                    summary.modifiedLeafCount += modifiedCount;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (summary.warnings.Count > 0)
            {
                Debug.LogWarning(string.Join(Environment.NewLine, summary.warnings));
            }

            EditorUtility.DisplayDialog(
                "XAnimation Clip Batch Settings",
                $"Modified clips: {summary.modifiedLeafCount}\nModified standalone .anim: {summary.modifiedStandaloneAnimationCount}\nReimported FBX: {summary.reimportedFbxCount}\nWarnings: {summary.warnings.Count}",
                "OK");
        }

        private bool ApplyStandaloneAnimationSettings(ClipLeaf leaf, ApplySummary summary)
        {
            if (leaf.clip == null)
            {
                summary.warnings.Add($"[{leaf.key}] Standalone animation reference is missing.");
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(leaf.clip);
            SerializedProperty clipSettings = serializedObject.FindProperty("m_AnimationClipSettings");
            if (clipSettings == null)
            {
                summary.warnings.Add($"[{leaf.key}] Could not find m_AnimationClipSettings.");
                return false;
            }

            ApplyStandaloneFieldOverrides(leaf.clip, clipSettings);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(leaf.clip);
            return true;
        }

        private bool ApplyFbxAnimationSettings(IGrouping<string, ClipLeaf> leavesByFbx, ApplySummary summary, out int modifiedCount)
        {
            modifiedCount = 0;
            string fbxPath = leavesByFbx.Key;
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                summary.warnings.Add($"[{fbxPath}] Could not get ModelImporter.");
                return false;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                summary.warnings.Add($"[{fbxPath}] No clip animations found.");
                return false;
            }

            bool changed = false;
            List<ClipLeaf> leaves = leavesByFbx.ToList();
            for (int i = 0; i < leaves.Count; i++)
            {
                List<int> matchingIndices = new List<int>();
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    if (string.Equals(clips[clipIndex].name, leaves[i].clipName, StringComparison.Ordinal))
                    {
                        matchingIndices.Add(clipIndex);
                    }
                }

                if (matchingIndices.Count != 1)
                {
                    summary.warnings.Add($"[{leaves[i].key}] Expected exactly one clip match inside FBX, found {matchingIndices.Count}.");
                    continue;
                }

                int targetIndex = matchingIndices[0];
                object boxedClip = clips[targetIndex];
                ApplyImporterFieldOverrides(boxedClip);
                clips[targetIndex] = (ModelImporterClipAnimation)boxedClip;
                changed = true;
                modifiedCount++;
            }

            if (!changed)
            {
                return false;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            return true;
        }

        private void ApplyStandaloneFieldOverrides(AnimationClip clip, SerializedProperty clipSettings)
        {
            if (m_Settings.loopTime.enabled)
            {
                SetSerializedBool(clipSettings.FindPropertyRelative("m_LoopTime"), m_Settings.loopTime.value);
            }

            if (m_Settings.loopPose.enabled)
            {
                SetSerializedBool(clipSettings.FindPropertyRelative("m_LoopBlend"), m_Settings.loopPose.value);
            }

            if (m_Settings.cycleOffset.enabled)
            {
                SetSerializedFloat(clipSettings.FindPropertyRelative("m_CycleOffset"), m_Settings.cycleOffset.value);
            }

            if (m_Settings.rotationBakeIntoPose.enabled)
            {
                SetSerializedBool(clipSettings.FindPropertyRelative("m_LoopBlendOrientation"), m_Settings.rotationBakeIntoPose.value);
            }

            if (m_Settings.rotationBasedUpon.enabled)
            {
                SetSerializedBool(
                    clipSettings.FindPropertyRelative("m_KeepOriginalOrientation"),
                    m_Settings.rotationBasedUpon.value == RotationBasedUpon.Original);
            }

            if (m_Settings.rotationOffset.enabled)
            {
                SetSerializedFloat(clipSettings.FindPropertyRelative("m_OrientationOffsetY"), m_Settings.rotationOffset.value);
            }

            if (m_Settings.positionYBakeIntoPose.enabled)
            {
                SetSerializedBool(clipSettings.FindPropertyRelative("m_LoopBlendPositionY"), m_Settings.positionYBakeIntoPose.value);
            }

            if (m_Settings.positionYBasedUpon.enabled)
            {
                bool keepOriginal = m_Settings.positionYBasedUpon.value == PositionYBasedUpon.Original;
                bool heightFromFeet = m_Settings.positionYBasedUpon.value == PositionYBasedUpon.Feet;
                SetSerializedBool(clipSettings.FindPropertyRelative("m_KeepOriginalPositionY"), keepOriginal);
                SetSerializedBool(clipSettings.FindPropertyRelative("m_HeightFromFeet"), heightFromFeet);
            }

            if (m_Settings.positionYOffset.enabled)
            {
                SetSerializedFloat(clipSettings.FindPropertyRelative("m_Level"), m_Settings.positionYOffset.value);
            }

            if (m_Settings.positionXZBakeIntoPose.enabled)
            {
                SetSerializedBool(clipSettings.FindPropertyRelative("m_LoopBlendPositionXZ"), m_Settings.positionXZBakeIntoPose.value);
            }

            if (m_Settings.positionXZBasedUpon.enabled)
            {
                SetSerializedBool(
                    clipSettings.FindPropertyRelative("m_KeepOriginalPositionXZ"),
                    m_Settings.positionXZBasedUpon.value == PositionXZBasedUpon.Original);
            }

            if (m_Settings.mirror.enabled)
            {
                SetSerializedBool(clipSettings.FindPropertyRelative("m_Mirror"), m_Settings.mirror.value);
            }

            if (m_Settings.additiveReferencePose.enabled)
            {
                SetSerializedBool(clipSettings.FindPropertyRelative("m_HasAdditiveReferencePose"), m_Settings.additiveReferencePose.value);
            }

            if (m_Settings.poseFrame.enabled)
            {
                float timeValue = m_Settings.poseFrame.value / Mathf.Max(clip.frameRate, 1f);
                SetSerializedFloat(clipSettings.FindPropertyRelative("m_AdditiveReferencePoseTime"), timeValue);
            }
        }

        private void ApplyImporterFieldOverrides(object boxedClip)
        {
            if (m_Settings.loopTime.enabled)
            {
                TrySetMember(boxedClip, m_Settings.loopTime.value, "loopTime");
            }

            if (m_Settings.loopPose.enabled)
            {
                TrySetMember(boxedClip, m_Settings.loopPose.value, "loopPose", "loopBlend");
            }

            if (m_Settings.cycleOffset.enabled)
            {
                TrySetMember(boxedClip, m_Settings.cycleOffset.value, "cycleOffset");
            }

            if (m_Settings.rotationBakeIntoPose.enabled)
            {
                TrySetMember(boxedClip, m_Settings.rotationBakeIntoPose.value, "lockRootRotation", "loopBlendOrientation");
            }

            if (m_Settings.rotationBasedUpon.enabled)
            {
                TrySetMember(boxedClip, m_Settings.rotationBasedUpon.value == RotationBasedUpon.Original, "keepOriginalOrientation");
            }

            if (m_Settings.rotationOffset.enabled)
            {
                TrySetMember(boxedClip, m_Settings.rotationOffset.value, "rotationOffset", "orientationOffsetY");
            }

            if (m_Settings.positionYBakeIntoPose.enabled)
            {
                TrySetMember(boxedClip, m_Settings.positionYBakeIntoPose.value, "lockRootHeightY", "loopBlendPositionY");
            }

            if (m_Settings.positionYBasedUpon.enabled)
            {
                bool keepOriginal = m_Settings.positionYBasedUpon.value == PositionYBasedUpon.Original;
                bool heightFromFeet = m_Settings.positionYBasedUpon.value == PositionYBasedUpon.Feet;
                TrySetMember(boxedClip, keepOriginal, "keepOriginalPositionY");
                TrySetMember(boxedClip, heightFromFeet, "heightFromFeet");
            }

            if (m_Settings.positionYOffset.enabled)
            {
                TrySetMember(boxedClip, m_Settings.positionYOffset.value, "heightOffset", "level");
            }

            if (m_Settings.positionXZBakeIntoPose.enabled)
            {
                TrySetMember(boxedClip, m_Settings.positionXZBakeIntoPose.value, "lockRootPositionXZ", "loopBlendPositionXZ");
            }

            if (m_Settings.positionXZBasedUpon.enabled)
            {
                TrySetMember(boxedClip, m_Settings.positionXZBasedUpon.value == PositionXZBasedUpon.Original, "keepOriginalPositionXZ");
            }

            if (m_Settings.mirror.enabled)
            {
                TrySetMember(boxedClip, m_Settings.mirror.value, "mirror");
            }

            if (m_Settings.additiveReferencePose.enabled)
            {
                TrySetMember(boxedClip, m_Settings.additiveReferencePose.value, "hasAdditiveReferencePose");
            }

            if (m_Settings.poseFrame.enabled)
            {
                TrySetMember(boxedClip, m_Settings.poseFrame.value, "additiveReferencePoseFrame");
            }
        }

        private void SetVisibleSelection(bool value)
        {
            for (int i = 0; i < m_SourceRoots.Count; i++)
            {
                List<ClipLeaf> visibleLeaves = GetVisibleLeaves(m_SourceRoots[i]);
                for (int leafIndex = 0; leafIndex < visibleLeaves.Count; leafIndex++)
                {
                    visibleLeaves[leafIndex].selected = value;
                }
            }
        }

        private void RemoveSelectedSources()
        {
            m_SourceRoots.RemoveAll(root =>
            {
                if (root.kind == SourceRootKind.MultiClipFbxGroup)
                {
                    return GetRootCheckState(root) == NodeCheckState.Checked;
                }

                return root.leaves.Count > 0 && root.leaves[0].selected;
            });
        }

        private void ReplaceSourcesWithClips(IEnumerable<AnimationClip> clips)
        {
            m_SourceRoots.Clear();
            m_FilterText = string.Empty;
            List<AnimationClip> clipList = clips?.Where(clip => clip != null).Distinct().ToList() ?? new List<AnimationClip>();
            AddObjects(clipList.Cast<UnityEngine.Object>());

            HashSet<string> selectedClipKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < clipList.Count; i++)
            {
                AnimationClip clip = clipList[i];
                string clipKey = XAnimationEditorAssetResolver.BuildClipPath(clip);
                if (!string.IsNullOrWhiteSpace(clipKey))
                {
                    selectedClipKeys.Add(clipKey);
                }

                string assetPath = AssetDatabase.GetAssetPath(clip);
                if (!string.IsNullOrWhiteSpace(assetPath) &&
                    string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    selectedClipKeys.Add(XAnimationClipPathUtility.Compose(assetPath, clip.name));
                }
            }

            for (int rootIndex = 0; rootIndex < m_SourceRoots.Count; rootIndex++)
            {
                SourceRoot root = m_SourceRoots[rootIndex];
                for (int leafIndex = 0; leafIndex < root.leaves.Count; leafIndex++)
                {
                    ClipLeaf leaf = root.leaves[leafIndex];
                    leaf.selected = selectedClipKeys.Contains(leaf.key);
                }
            }
        }

        private void AddObjects(IEnumerable<UnityEngine.Object> objects)
        {
            if (objects == null)
            {
                return;
            }

            List<string> warnings = new List<string>();
            foreach (UnityEngine.Object obj in objects)
            {
                AddObject(obj, warnings);
            }

            if (warnings.Count > 0)
            {
                Debug.LogWarning(string.Join(Environment.NewLine, warnings));
            }
        }

        private void AddObject(UnityEngine.Object obj, List<string> warnings)
        {
            if (obj == null)
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                warnings.Add($"Skipped object without asset path: {obj.name}");
                return;
            }

            string extension = Path.GetExtension(assetPath);
            if (string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                AddFbxSource(assetPath, warnings);
                return;
            }

            if (string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase))
            {
                AnimationClip clip = obj as AnimationClip ?? AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null)
                {
                    warnings.Add($"Skipped .anim without AnimationClip: {assetPath}");
                    return;
                }

                AddStandaloneAnimationSource(assetPath, clip);
                return;
            }

            warnings.Add($"Skipped unsupported asset: {assetPath}");
        }

        private void AddFbxSource(string assetPath, List<string> warnings)
        {
            if (m_SourceRoots.Any(root => string.Equals(root.key, assetPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            List<AnimationClip> clips = new List<AnimationClip>();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.Contains("__preview__", StringComparison.Ordinal))
                {
                    clips.Add(clip);
                }
            }

            if (clips.Count == 0)
            {
                warnings.Add($"Skipped FBX without animation clips: {assetPath}");
                return;
            }

            string fbxName = Path.GetFileNameWithoutExtension(assetPath);
            SourceRoot root = new SourceRoot
            {
                key = assetPath,
                assetPath = assetPath,
                kind = clips.Count == 1 ? SourceRootKind.SingleClipFbx : SourceRootKind.MultiClipFbxGroup,
                displayName = clips.Count == 1 ? $"{fbxName}/{clips[0].name}" : fbxName,
            };

            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                root.leaves.Add(new ClipLeaf
                {
                    key = XAnimationClipPathUtility.Compose(assetPath, clip.name),
                    clipName = clip.name,
                    displayName = clips.Count == 1 ? root.displayName : clip.name,
                    assetPath = assetPath,
                    hostFbxPath = assetPath,
                    clip = clip,
                    selected = true,
                });
            }

            m_SourceRoots.Add(root);
        }

        private void AddStandaloneAnimationSource(string assetPath, AnimationClip clip)
        {
            if (m_SourceRoots.Any(root => string.Equals(root.key, assetPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            SourceRoot root = new SourceRoot
            {
                key = assetPath,
                assetPath = assetPath,
                kind = SourceRootKind.StandaloneAnimation,
                displayName = clip.name,
            };

            root.leaves.Add(new ClipLeaf
            {
                key = assetPath,
                clipName = clip.name,
                displayName = clip.name,
                assetPath = assetPath,
                hostFbxPath = null,
                clip = clip,
                selected = true,
            });

            m_SourceRoots.Add(root);
        }

        private List<ClipLeaf> GetVisibleLeaves(SourceRoot root)
        {
            List<ClipLeaf> visibleLeaves = new List<ClipLeaf>();
            string filter = m_FilterText ?? string.Empty;
            for (int i = 0; i < root.leaves.Count; i++)
            {
                ClipLeaf leaf = root.leaves[i];
                if (string.IsNullOrWhiteSpace(filter) ||
                    leaf.displayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    leaf.clipName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    visibleLeaves.Add(leaf);
                }
            }

            if (root.kind == SourceRootKind.MultiClipFbxGroup &&
                visibleLeaves.Count > 0 &&
                !string.IsNullOrWhiteSpace(filter))
            {
                root.expanded = true;
            }

            return visibleLeaves;
        }

        private NodeCheckState GetRootCheckState(SourceRoot root)
        {
            int selectedCount = root.leaves.Count(leaf => leaf.selected);
            if (selectedCount <= 0)
            {
                return NodeCheckState.Unchecked;
            }

            if (selectedCount >= root.leaves.Count)
            {
                return NodeCheckState.Checked;
            }

            return NodeCheckState.Mixed;
        }

        private void SetRootSelected(SourceRoot root, bool value)
        {
            for (int i = 0; i < root.leaves.Count; i++)
            {
                root.leaves[i].selected = value;
            }
        }

        private void HandleObjectPickerEvents()
        {
            UnityEngine.Event current = UnityEngine.Event.current;
            if (current == null)
            {
                return;
            }

            if (!string.Equals(current.commandName, "ObjectSelectorClosed", StringComparison.Ordinal))
            {
                return;
            }

            if (EditorGUIUtility.GetObjectPickerControlID() != ObjectPickerControlId)
            {
                return;
            }

            UnityEngine.Object selectedObject = EditorGUIUtility.GetObjectPickerObject();
            if (selectedObject != null)
            {
                AddObjects(new[] { selectedObject });
            }
        }

        private static bool HasSupportedObjects(IEnumerable<UnityEngine.Object> objects)
        {
            if (objects == null)
            {
                return false;
            }

            foreach (UnityEngine.Object obj in objects)
            {
                if (obj == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                string extension = Path.GetExtension(assetPath);
                if (string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetSerializedBool(SerializedProperty property, bool value)
        {
            if (property == null)
            {
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = value;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = value ? 1 : 0;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = value ? 1f : 0f;
                    break;
            }
        }

        private static void SetSerializedFloat(SerializedProperty property, float value)
        {
            if (property == null)
            {
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = value;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = Mathf.RoundToInt(value);
                    break;
            }
        }

        private static bool TrySetMember(object target, object value, params string[] memberNames)
        {
            if (target == null || memberNames == null)
            {
                return false;
            }

            Type targetType = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int i = 0; i < memberNames.Length; i++)
            {
                string memberName = memberNames[i];
                if (string.IsNullOrWhiteSpace(memberName))
                {
                    continue;
                }

                PropertyInfo property = targetType.GetProperty(memberName, flags);
                if (property != null && property.CanWrite && TryConvertValue(value, property.PropertyType, out object convertedPropertyValue))
                {
                    property.SetValue(target, convertedPropertyValue);
                    return true;
                }

                FieldInfo field = targetType.GetField(memberName, flags);
                if (field != null && TryConvertValue(value, field.FieldType, out object convertedFieldValue))
                {
                    field.SetValue(target, convertedFieldValue);
                    return true;
                }
            }

            return false;
        }

        private static bool TryConvertValue(object value, Type destinationType, out object convertedValue)
        {
            convertedValue = null;
            if (destinationType == null)
            {
                return false;
            }

            if (value == null)
            {
                if (!destinationType.IsValueType || Nullable.GetUnderlyingType(destinationType) != null)
                {
                    convertedValue = null;
                    return true;
                }

                return false;
            }

            Type sourceType = value.GetType();
            if (destinationType.IsAssignableFrom(sourceType))
            {
                convertedValue = value;
                return true;
            }

            if (destinationType.IsEnum && value is string enumString)
            {
                convertedValue = Enum.Parse(destinationType, enumString);
                return true;
            }

            if (destinationType == typeof(bool))
            {
                if (value is int intValue)
                {
                    convertedValue = intValue != 0;
                    return true;
                }

                if (value is float floatValue)
                {
                    convertedValue = !Mathf.Approximately(floatValue, 0f);
                    return true;
                }
            }

            if (destinationType == typeof(int) && value is bool boolValue)
            {
                convertedValue = boolValue ? 1 : 0;
                return true;
            }

            if (destinationType == typeof(float) && value is bool floatBoolValue)
            {
                convertedValue = floatBoolValue ? 1f : 0f;
                return true;
            }

            try
            {
                convertedValue = Convert.ChangeType(value, destinationType);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
