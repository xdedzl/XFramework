using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace XFramework.Interaction
{
    /// <summary>
    /// 可通过按键交互的对象
    /// </summary>
    public interface IKeyCodeInteractable : IInteractable
    {
        void Interact();
    }
    
    [TargetInteractable(typeof(IKeyCodeInteractable))]
    public class KeyCodeInteractionHandler : InteractionHandlerBase<IKeyCodeInteractable>
    {
        private const float HintVerticalOffset = 24f;
        private const int PromptTextureSize = 64;
        private static readonly Vector2 HintSize = new(56f, 56f);
        private static Sprite s_PromptSprite;

        private GameObject m_HintRoot;
        private RectTransform m_HintRect;
        private TextMeshProUGUI m_KeyLabel;
        private Renderer m_TargetRenderer;
        private Camera m_Camera;
        private bool m_IsVisible = true;
        
        public override void OnTargetChange()
        {
            if (targetObject != null)
            {
                m_TargetRenderer = targetObject.GetComponentInChildren<Renderer>();
                EnsureHintCreated();
                UpdateHintPosition();
            }
            else
            {
                m_TargetRenderer = null;
                SetHintVisible(false);
            }
        }
        
        private void EnsureHintCreated()
        {
            if (m_HintRoot)
            {
                return;
            }

            m_HintRoot = new GameObject("InteractHintCanvas", typeof(RectTransform));
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(m_HintRoot);
            }

            var canvas = m_HintRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            var scaler = m_HintRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var canvasRect = m_HintRoot.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var hintGo = new GameObject("InteractHint", typeof(RectTransform));
            hintGo.transform.SetParent(m_HintRoot.transform, false);

            m_HintRect = hintGo.GetComponent<RectTransform>();
            m_HintRect.anchorMin = Vector2.zero;
            m_HintRect.anchorMax = Vector2.zero;
            m_HintRect.pivot = new Vector2(0.5f, 0.5f);
            m_HintRect.sizeDelta = HintSize;

            var circle = hintGo.AddComponent<Image>();
            circle.sprite = GetPromptSprite();
            circle.raycastTarget = false;

            var textGo = new GameObject("KeyLabel", typeof(RectTransform));
            textGo.transform.SetParent(hintGo.transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            m_KeyLabel = text;
            m_KeyLabel.text = string.Empty;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 27f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color32(255, 242, 203, 255);
            text.outlineWidth = 0.12f;
            text.outlineColor = new Color32(45, 31, 21, 190);
            text.raycastTarget = false;

            SetHintVisible(false);
        }

        private void UpdateHintPosition()
        {
            if (targetObject == null)
            {
                SetHintVisible(false);
                return;
            }

            ResolveCamera();
            if (!m_Camera)
            {
                SetHintVisible(false);
                return;
            }

            var screenPosition = m_Camera.WorldToScreenPoint(GetHintWorldPosition());
            if (screenPosition.z <= 0f || float.IsNaN(screenPosition.x) || float.IsNaN(screenPosition.y) ||
                float.IsInfinity(screenPosition.x) || float.IsInfinity(screenPosition.y))
            {
                SetHintVisible(false);
                return;
            }

            m_HintRect.anchoredPosition = new Vector2(screenPosition.x, screenPosition.y + HintVerticalOffset);
            SetHintVisible(true);
        }

        private Vector3 GetHintWorldPosition()
        {
            if (m_TargetRenderer)
            {
                var bounds = m_TargetRenderer.bounds;
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }

            return targetObject.transform.position + Vector3.up * 1.8f;
        }

        private void ResolveCamera()
        {
            if (!m_Camera)
            {
                m_Camera = Camera.main;
            }
        }

        private void SetHintVisible(bool visible)
        {
            visible &= m_IsVisible;
            if (m_HintRoot && m_HintRoot.activeSelf != visible)
            {
                m_HintRoot.SetActive(visible);
            }
        }

        public override void SetVisible(bool visible)
        {
            m_IsVisible = visible;
            if (!visible)
            {
                SetHintVisible(false);
            }
        }

        public override void OnUpdate()
        {
            if (targetInteractable == null || !m_IsVisible ||
                !InteractionManager.IsInteractionInputEnabled ||
                !GameEntry.IsModuleLoaded<InputManager>() ||
                !InputManager.Instance.IsActionMapActive(XApplication.Setting.inputDefaultActionMap))
            {
                SetHintVisible(false);
                return;
            }

            EnsureHintCreated();
            UpdateHintKeyLabel();
            UpdateHintPosition();

            if (GetInteractAction().WasPressedThisFrame())
            {
                targetInteractable.Interact();
            }
        }

        private void UpdateHintKeyLabel()
        {
            if (m_KeyLabel && targetInteractable != null)
            {
                m_KeyLabel.text = InputManager.Instance.GetBindingDisplayString(
                    XApplication.Setting.inputDefaultActionMap, XApplication.Setting.inputInteractActionName);
            }
        }

        private static InputAction GetInteractAction()
        {
            return InputManager.Instance.GetAction(
                XApplication.Setting.inputDefaultActionMap, XApplication.Setting.inputInteractActionName);
        }

        private static Sprite GetPromptSprite()
        {
            if (s_PromptSprite)
            {
                return s_PromptSprite;
            }

            var texture = new Texture2D(PromptTextureSize, PromptTextureSize, TextureFormat.RGBA32, false)
            {
                name = "InteractionPromptCircle",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[PromptTextureSize * PromptTextureSize];
            var fillColor = new Color32(31, 35, 42, 232);
            var outlineColor = new Color32(232, 177, 82, 255);
            var transparent = new Color32(0, 0, 0, 0);
            float center = (PromptTextureSize - 1) * 0.5f;
            float outerRadius = PromptTextureSize * 0.5f - 1f;
            float innerRadius = outerRadius - 5f;
            float outerRadiusSqr = outerRadius * outerRadius;
            float innerRadiusSqr = innerRadius * innerRadius;

            for (int y = 0; y < PromptTextureSize; y++)
            {
                for (int x = 0; x < PromptTextureSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distanceSqr = dx * dx + dy * dy;
                    int index = y * PromptTextureSize + x;

                    if (distanceSqr > outerRadiusSqr)
                    {
                        pixels[index] = transparent;
                    }
                    else if (distanceSqr >= innerRadiusSqr)
                    {
                        pixels[index] = outlineColor;
                    }
                    else
                    {
                        pixels[index] = fillColor;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            s_PromptSprite = Sprite.Create(texture, new Rect(0f, 0f, PromptTextureSize, PromptTextureSize),
                new Vector2(0.5f, 0.5f), PromptTextureSize);
            s_PromptSprite.name = "InteractionPromptCircle";
            s_PromptSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_PromptSprite;
        }
    }
}
