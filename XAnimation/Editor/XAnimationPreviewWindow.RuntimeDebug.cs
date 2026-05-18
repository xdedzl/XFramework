#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UEvent = UnityEngine.Event;
using XFramework.Animation;
using static XFramework.Editor.XAnimationEditorParameterUtility;
using static XFramework.Editor.XAnimationEditorUi;

namespace XFramework.Editor
{
    public sealed partial class XAnimationPreviewWindow
    {
        private void RegisterPreviewEvents()
        {
            m_PreviewImage.focusable = true;

            // Right-click drag to rotate camera
            m_PreviewImage.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 1 || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_IsPreviewDragging = true;
                m_LastPreviewMousePosition = evt.localMousePosition;
                m_PreviewImage.CaptureMouse();
                m_PreviewImage.Focus();
                evt.StopPropagation();
            });

            m_PreviewImage.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!m_IsPreviewDragging || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                Vector2 delta = evt.localMousePosition - m_LastPreviewMousePosition;
                m_LastPreviewMousePosition = evt.localMousePosition;
                m_Session.Orbit(delta);
                RenderPreview();
                evt.StopPropagation();
            });

            m_PreviewImage.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 1)
                {
                    return;
                }

                m_IsPreviewDragging = false;
                if (m_PreviewImage.HasMouseCapture())
                {
                    m_PreviewImage.ReleaseMouse();
                }
                evt.StopPropagation();
            });

            // Scroll to zoom
            m_PreviewImage.RegisterCallback<WheelEvent>(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_Session.Zoom(evt.delta.y);
                RenderPreview();
                evt.StopPropagation();
            });

            // Keyboard: WASD + QE for camera movement
            m_PreviewImage.RegisterCallback<KeyDownEvent>(evt =>
            {
                KeyCode key = EventToKeyCode(evt);
                if (key != KeyCode.None)
                {
                    m_PressedKeys.Add(key);
                    evt.StopPropagation();
                }
            });

            m_PreviewImage.RegisterCallback<KeyUpEvent>(evt =>
            {
                KeyCode key = EventToKeyCode(evt);
                if (key != KeyCode.None)
                {
                    m_PressedKeys.Remove(key);
                    evt.StopPropagation();
                }
            });

            m_PreviewImage.RegisterCallback<FocusOutEvent>(_ => m_PressedKeys.Clear());

            m_PreviewImage.RegisterCallback<GeometryChangedEvent>(_ => RenderPreview());
        }

        private static KeyCode EventToKeyCode(KeyboardEventBase<KeyDownEvent> evt)
        {
            return evt.keyCode switch
            {
                UnityEngine.KeyCode.W => KeyCode.W,
                UnityEngine.KeyCode.A => KeyCode.A,
                UnityEngine.KeyCode.S => KeyCode.S,
                UnityEngine.KeyCode.D => KeyCode.D,
                UnityEngine.KeyCode.Q => KeyCode.Q,
                UnityEngine.KeyCode.E => KeyCode.E,
                UnityEngine.KeyCode.LeftShift => KeyCode.LeftShift,
                UnityEngine.KeyCode.RightShift => KeyCode.RightShift,
                _ => KeyCode.None
            };
        }

        private static KeyCode EventToKeyCode(KeyboardEventBase<KeyUpEvent> evt)
        {
            return evt.keyCode switch
            {
                UnityEngine.KeyCode.W => KeyCode.W,
                UnityEngine.KeyCode.A => KeyCode.A,
                UnityEngine.KeyCode.S => KeyCode.S,
                UnityEngine.KeyCode.D => KeyCode.D,
                UnityEngine.KeyCode.Q => KeyCode.Q,
                UnityEngine.KeyCode.E => KeyCode.E,
                UnityEngine.KeyCode.LeftShift => KeyCode.LeftShift,
                UnityEngine.KeyCode.RightShift => KeyCode.RightShift,
                _ => KeyCode.None
            };
        }

        private bool ProcessCameraMovement(float deltaTime)
        {
            if (m_PressedKeys.Count == 0 || m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            bool shift = m_PressedKeys.Contains(KeyCode.LeftShift) || m_PressedKeys.Contains(KeyCode.RightShift);
            float speed = (shift ? 9f : 3f) * deltaTime;
            Vector3 move = Vector3.zero;

            if (m_PressedKeys.Contains(KeyCode.W)) move.z += speed;
            if (m_PressedKeys.Contains(KeyCode.S)) move.z -= speed;
            if (m_PressedKeys.Contains(KeyCode.A)) move.x -= speed;
            if (m_PressedKeys.Contains(KeyCode.D)) move.x += speed;
            if (m_PressedKeys.Contains(KeyCode.E)) move.y += speed;
            if (m_PressedKeys.Contains(KeyCode.Q)) move.y -= speed;

            if (move.sqrMagnitude > 0f)
            {
                m_Session.MoveCamera(move);
                return true;
            }

            return false;
        }

        private void HandleEditorUpdate()
        {
            if (!m_UpdateCoordinator.TryBuildUpdateResult(this, EditorApplication.timeSinceStartup, out XAnimationPreviewUpdateResult result))
            {
                return;
            }

            if (result.ShouldRenderPreview)
            {
                RenderPreview();
            }

            if (result.ShouldRefreshChannels)
            {
                RefreshChannelStates();
            }

            if (result.ShouldRefreshPlaybackScrubber)
            {
                RefreshPlaybackScrubber();
            }

            if (result.ShouldRefreshStatePlayback)
            {
                RefreshStatePlayingStates();
                m_PlaybackUiState.ClearStatePlaybackDirty();
            }

            if (result.ShouldRefreshClipPlayback)
            {
                RefreshClipPlayingStates();
                m_PlaybackUiState.ClearClipPlaybackDirty();
            }

            if (result.ShouldRefreshCueLog)
            {
                RefreshCueLogView(force: result.ForceCueLogRefresh);
                m_PlaybackUiState.ClearCueLogDirty();
            }

            if (result.ShouldRepaint)
            {
                Repaint();
            }
        }

        private void MarkStatePlaybackUiDirty()
        {
            m_PlaybackUiState.MarkStatePlaybackDirty();
        }

        private void MarkClipPlaybackUiDirty()
        {
            m_PlaybackUiState.MarkClipPlaybackDirty();
        }

        private void MarkCueLogUiDirty()
        {
            m_PlaybackUiState.MarkCueLogDirty();
        }

        private void MarkEventUiDirty()
        {
            m_PlaybackUiState.MarkAllDirty();
        }

        private void OpenGraphDebuggerForPreview()
        {
            XAnimationGraphDebuggerWindow.ShowPreview(GetPreviewDebugGraphSnapshot, BuildGraphDebuggerSourceName());
        }

        private string BuildGraphDebuggerSourceName()
        {
            string assetName = m_SelectedAsset != null ? m_SelectedAsset.name : "Preview";
            string prefabName = m_SelectedPrefab != null ? m_SelectedPrefab.name : "No Prefab";
            return $"{assetName} / {prefabName}";
        }

        private XAnimationDebugGraphSnapshot GetPreviewDebugGraphSnapshot()
        {
            return m_Session != null && m_Session.IsLoaded
                ? m_Session.GetDebugGraphSnapshot()
                : XAnimationDebugGraphSnapshot.Invalid("XAnimation Preview 尚未加载。请选择 asset 和 prefab 后点击 Load。");
        }

        private bool IsPreviewTabVisible()
        {
            if (this == null || focusedWindow != this || rootVisualElement == null || m_PreviewImage == null)
            {
                return false;
            }

            Rect windowRect = position;
            if (windowRect.width <= 0f || windowRect.height <= 0f)
            {
                return false;
            }

            Rect previewRect = m_PreviewImage.worldBound;
            return previewRect.width > 0f && previewRect.height > 0f;
        }

        private void ApplyDefaultSelections()
        {
            RestoreLastPreviewAssetsIfNeeded();

            if (m_SelectedPrefab != null)
            {
                m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            }

            if (m_SelectedAsset != null)
            {
                m_AssetField.SetValueWithoutNotify(m_SelectedAsset);
            }

            RefreshAssetsToolbarButtons();
        }

        private void SetPendingOpenRequest(TextAsset animationAsset, GameObject prefab, bool autoLoad)
        {
            m_PendingAsset = animationAsset;
            m_PendingPrefab = prefab;
            m_PendingAutoLoad = autoLoad;

            if (m_AssetField == null || m_PrefabField == null)
            {
                return;
            }

            ApplyPendingOpenRequest();
        }

        private void SetPendingPlaybackRequest(PendingPlaybackRequest request)
        {
            m_PendingPlaybackRequest = request;
            if (m_Session != null && m_Session.IsLoaded)
            {
                ApplyPendingPlaybackRequest();
            }
        }

        private void SetPendingFocusState(string stateKey)
        {
            m_PendingFocusStateKey = string.IsNullOrWhiteSpace(stateKey) ? null : stateKey;
            ApplyPendingFocusState();
        }

        private void ApplyPendingFocusState()
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(m_PendingFocusStateKey))
            {
                return;
            }

            string stateKey = m_PendingFocusStateKey;
            m_PendingFocusStateKey = null;
            FocusStateInInspector(stateKey);
        }

        private void ApplyPendingOpenRequest()
        {
            if (m_AssetField == null || m_PrefabField == null)
            {
                return;
            }

            bool hasPendingRequest = m_PendingAsset != null || m_PendingPrefab != null || m_PendingAutoLoad;
            if (!hasPendingRequest)
            {
                return;
            }

            if (m_PendingAsset != null)
            {
                m_SelectedAsset = m_PendingAsset;
                m_AssetField.SetValueWithoutNotify(m_SelectedAsset);
            }

            if (m_PendingPrefab != null)
            {
                m_SelectedPrefab = m_PendingPrefab;
                m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            }
            else if (m_PendingAsset != null)
            {
                GameObject defaultPrefab = m_SelectedAsset != null
                    ? LoadDefaultPrefabForAsset(m_SelectedAsset)
                    : null;
                if (defaultPrefab != null)
                {
                    m_SelectedPrefab = defaultPrefab;
                    m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
                }
            }
            else if (m_PrefabField.value == null && m_SelectedAsset != null)
            {
                m_SelectedPrefab = LoadDefaultPrefabForAsset(m_SelectedAsset);
                m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            }

            bool shouldAutoLoad = m_PendingAutoLoad && m_AssetField.value != null && m_PrefabField.value != null;

            m_PendingAsset = null;
            m_PendingPrefab = null;
            m_PendingAutoLoad = false;

            if (shouldAutoLoad)
            {
                LoadPreview();
            }

            RefreshAssetsToolbarButtons();
        }

        private void ScheduleAutoReloadPreview()
        {
            bool hasPendingRequest = m_PendingAsset != null || m_PendingPrefab != null || m_PendingAutoLoad;
            if (hasPendingRequest || m_Session != null || m_PendingPlaybackRequest.HasValue || !string.IsNullOrWhiteSpace(m_PendingFocusStateKey))
            {
                return;
            }

            RestoreLastPreviewAssetsIfNeeded();
            if (m_SelectedAsset == null || m_SelectedPrefab == null)
            {
                return;
            }

            m_ShouldAutoReloadPreview = true;
            EditorApplication.delayCall += AutoReloadPreview;
        }

        private void AutoReloadPreview()
        {
            if (this == null || m_AssetField == null || m_PrefabField == null)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoReloadPreview;
                return;
            }

            RestoreLastPreviewAssetsIfNeeded();
            if (m_SelectedAsset == null || m_SelectedPrefab == null)
            {
                m_ShouldAutoReloadPreview = false;
                return;
            }

            m_ShouldAutoReloadPreview = true;
            m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            m_AssetField.SetValueWithoutNotify(m_SelectedAsset);
            RefreshAssetsToolbarButtons();
            LoadPreview();
        }

        private void RestoreLastPreviewAssetsIfNeeded()
        {
            if (m_SelectedAsset == null)
            {
                string assetPath = EditorPrefs.GetString(LastAssetPathPrefsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    m_SelectedAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                }
            }

            if (m_SelectedPrefab == null)
            {
                string prefabPath = EditorPrefs.GetString(LastPrefabPathPrefsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(prefabPath))
                {
                    m_SelectedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                }
            }
        }

        private static void SaveLastPreviewAssetPaths(TextAsset assetText, GameObject prefab)
        {
            string assetPath = assetText == null ? string.Empty : AssetDatabase.GetAssetPath(assetText);
            string prefabPath = prefab == null ? string.Empty : AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                EditorPrefs.SetString(LastAssetPathPrefsKey, assetPath);
            }

            if (!string.IsNullOrWhiteSpace(prefabPath))
            {
                EditorPrefs.SetString(LastPrefabPathPrefsKey, prefabPath);
            }
        }

        private GameObject LoadDefaultPrefabForAsset(TextAsset assetText)
        {
            if (!TryGetDefaultPrefabPath(assetText, out string defaultPrefabPath) || string.IsNullOrWhiteSpace(defaultPrefabPath))
            {
                return null;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(defaultPrefabPath);
            if (prefab == null)
            {
                SetStatus($"默认 prefab 不存在：{defaultPrefabPath}", true);
                return null;
            }

            return prefab;
        }

        private bool TryGetDefaultPrefabPath(TextAsset assetText, out string defaultPrefabPath)
        {
            defaultPrefabPath = string.Empty;
            if (assetText == null)
            {
                return false;
            }

            XAnimationOverrideAsset overrideAsset = assetText.ToXAnimationAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                if (!string.IsNullOrWhiteSpace(overrideAsset.DefaultPrefabPath))
                {
                    defaultPrefabPath = overrideAsset.DefaultPrefabPath;
                    return true;
                }

                TextAsset baseTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(overrideAsset.baseAssetPath);
                if (baseTextAsset == null)
                {
                    SetStatus($"Override base asset 不存在：{overrideAsset.baseAssetPath}", true);
                    return false;
                }

                XAnimationAsset baseAsset = baseTextAsset.ToXAnimationAsset<XAnimationAsset>();
                defaultPrefabPath = baseAsset?.DefaultPrefabPath ?? string.Empty;
                return true;
            }

            XAnimationAsset asset = assetText.ToXAnimationAsset<XAnimationAsset>();
            defaultPrefabPath = asset?.DefaultPrefabPath ?? string.Empty;
            return true;
        }

        private bool TryGetAssetPreload(TextAsset assetText, out bool preload, out bool editable)
        {
            preload = false;
            editable = false;
            if (assetText == null)
            {
                return false;
            }

            XAnimationOverrideAsset overrideAsset = assetText.ToXAnimationAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                TextAsset baseTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(overrideAsset.baseAssetPath);
                if (baseTextAsset == null)
                {
                    SetStatus($"Override base asset 不存在：{overrideAsset.baseAssetPath}", true);
                    return false;
                }

                XAnimationAsset baseAsset = baseTextAsset.ToXAnimationAsset<XAnimationAsset>();
                preload = baseAsset != null && baseAsset.preload;
                editable = false;
                return baseAsset != null;
            }

            XAnimationAsset asset = assetText.ToXAnimationAsset<XAnimationAsset>();
            if (asset == null)
            {
                return false;
            }

            preload = asset.preload;
            editable = true;
            return true;
        }

        private void SetSelectedAssetPreload(bool preload)
        {
            TextAsset assetText = m_AssetField?.value as TextAsset;
            if (assetText == null)
            {
                RefreshPreloadToggle();
                SetStatus("请选择一个 XAnimationAsset 后再修改 preload。", true);
                return;
            }

            if (!TryGetAssetPreload(assetText, out _, out bool editable) || !editable)
            {
                RefreshPreloadToggle();
                SetStatus("XAnimationOverride 继承 base asset 的 preload 设置，请在 base .xanimation 中修改。", true);
                return;
            }

            if (m_Session != null && m_Session.IsLoaded)
            {
                m_Session.SetAssetPreload(preload);
            }
            else
            {
                XAnimationAsset asset = assetText.ToXAnimationAsset<XAnimationAsset>();
                if (asset == null)
                {
                    RefreshPreloadToggle();
                    SetStatus("当前资源不是有效的 XAnimationAsset。", true);
                    return;
                }

                asset.preload = preload;
                asset.SaveAsset();
            }

            RefreshPreloadToggle();
            SetStatus(preload ? "已开启 XAnimation 初始化预加载。" : "已关闭 XAnimation 初始化预加载，保持按需懒加载。");
        }

        private void RefreshPreloadToggle()
        {
            if (m_PreloadToggle == null)
            {
                return;
            }

            TextAsset assetText = m_AssetField?.value as TextAsset;
            bool hasPreload = TryGetAssetPreload(assetText, out bool preload, out bool editable);
            m_PreloadToggle.SetValueWithoutNotify(hasPreload && preload);
            m_PreloadToggle.SetEnabled(hasPreload && editable);
            m_PreloadToggle.tooltip = editable
                ? "开启后，XAnimationDriver 初始化当前资源时会同步 PreloadAll，适合小型或确定常驻的动作集。"
                : "XAnimationOverride 继承 base asset 的 preload 设置，请打开 base .xanimation 修改。";
        }

        private void SaveCurrentPrefabAsDefault()
        {
            GameObject prefab = m_PrefabField?.value as GameObject;
            if (prefab == null)
            {
                SetStatus("请选择一个 prefab 后再设为默认。", true);
                return;
            }

            TextAsset assetText = m_AssetField?.value as TextAsset;
            if (assetText == null)
            {
                SetStatus("请选择一个 XAnimationAsset 后再设为默认。", true);
                return;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                SetStatus("无法获取当前 prefab 的资源路径。", true);
                return;
            }

            XAnimationOverrideAsset overrideAsset = assetText.ToXAnimationAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                overrideAsset.DefaultPrefabPath = prefabPath;
                overrideAsset.SaveAsset();
                RefreshAssetsToolbarButtons();
                SetStatus($"已写入 Override 默认 prefab：{prefabPath}");
                return;
            }

            XAnimationAsset asset = assetText.ToXAnimationAsset<XAnimationAsset>();
            if (asset == null)
            {
                SetStatus("当前资源不是有效的 XAnimationAsset。", true);
                return;
            }

            asset.DefaultPrefabPath = prefabPath;
            asset.SaveAsset();
            RefreshAssetsToolbarButtons();
            SetStatus($"已写入默认 prefab：{prefabPath}");
        }

        private void ResetPrefabToDefault()
        {
            TextAsset assetText = m_AssetField?.value as TextAsset;
            if (assetText == null)
            {
                SetStatus("请选择一个 XAnimationAsset 后再恢复默认模型。", true);
                return;
            }

            GameObject defaultPrefab = LoadDefaultPrefabForAsset(assetText);
            if (defaultPrefab == null)
            {
                SetStatus("当前资源没有可用的默认 prefab。", true);
                RefreshAssetsToolbarButtons();
                return;
            }

            m_SelectedPrefab = defaultPrefab;
            m_PrefabField?.SetValueWithoutNotify(defaultPrefab);
            RefreshAssetsToolbarButtons();

            if (m_AssetField?.value != null && m_PrefabField?.value != null)
            {
                LoadPreview();
            }

            SetStatus($"已恢复默认 prefab：{defaultPrefab.name}。");
        }

        private void RefreshAssetsToolbarButtons()
        {
            if (m_SaveCurrentPrefabAsDefaultButton == null || m_ResetPrefabToDefaultButton == null)
            {
                return;
            }

            TextAsset assetText = m_AssetField?.value as TextAsset;
            GameObject currentPrefab = m_PrefabField?.value as GameObject;
            string currentPrefabPath = currentPrefab == null ? string.Empty : AssetDatabase.GetAssetPath(currentPrefab);
            bool hasDefaultPrefab = TryGetDefaultPrefabPath(assetText, out string defaultPrefabPath) &&
                                    !string.IsNullOrWhiteSpace(defaultPrefabPath);
            bool matchesDefaultPrefab = hasDefaultPrefab &&
                                        string.Equals(currentPrefabPath, defaultPrefabPath, StringComparison.Ordinal);
            bool showDefaultActions = currentPrefab != null && !matchesDefaultPrefab;

            m_SaveCurrentPrefabAsDefaultButton.style.display = showDefaultActions ? DisplayStyle.Flex : DisplayStyle.None;
            m_ResetPrefabToDefaultButton.style.display = showDefaultActions && hasDefaultPrefab ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshPreloadToggle();
        }

    }
}
#endif
