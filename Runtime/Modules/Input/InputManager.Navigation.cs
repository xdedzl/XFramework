using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    public sealed partial class InputManager
    {
        private readonly Vector3[] m_SelectionCorners = new Vector3[4];

        // 仅在焦点改变时调用，避免用户滚轮浏览时被持续拉回选中项。
        private void EnsureSelectionVisible(GameObject selected)
        {
            if (selected == null || m_CurrentPanel == null ||
                !selected.transform.IsChildOf(m_CurrentPanel.Owner.transform) ||
                selected.transform is not RectTransform selectedRect)
            {
                return;
            }

            bool layoutUpdated = false;
            Transform panelRoot = m_CurrentPanel.Owner.transform;
            for (Transform parent = selectedRect.parent; parent != null; parent = parent.parent)
            {
                if (parent.TryGetComponent(out ScrollRect scroll) && scroll.isActiveAndEnabled &&
                    scroll.content != null && selectedRect.IsChildOf(scroll.content))
                {
                    if (!layoutUpdated)
                    {
                        Canvas.ForceUpdateCanvases();
                        layoutUpdated = true;
                    }

                    ScrollSelectionIntoView(scroll, selectedRect);
                }

                if (parent == panelRoot) break;
            }
        }

        private void ScrollSelectionIntoView(ScrollRect scroll, RectTransform selected)
        {
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            Rect view = viewport.rect;
            Bounds selection = GetBoundsInViewport(selected, viewport);
            Bounds content = GetBoundsInViewport(scroll.content, viewport);
            Vector2 position = scroll.normalizedPosition;
            Vector2 next = position;
            float hiddenWidth = content.size.x - view.width;
            float hiddenHeight = content.size.y - view.height;

            if (scroll.horizontal && hiddenWidth > 0.01f)
            {
                float offset = GetVisibilityOffset(selection.min.x, selection.max.x, view.xMin, view.xMax);
                next.x = Mathf.Clamp01(position.x - offset / hiddenWidth);
            }

            if (scroll.vertical && hiddenHeight > 0.01f)
            {
                float offset = GetVisibilityOffset(selection.min.y, selection.max.y, view.yMin, view.yMax);
                next.y = Mathf.Clamp01(position.y - offset / hiddenHeight);
            }

            if (Mathf.Approximately(next.x, position.x) && Mathf.Approximately(next.y, position.y)) return;
            scroll.StopMovement();
            scroll.normalizedPosition = next;
        }

        private Bounds GetBoundsInViewport(RectTransform target, RectTransform viewport)
        {
            target.GetWorldCorners(m_SelectionCorners);
            var bounds = new Bounds(viewport.InverseTransformPoint(m_SelectionCorners[0]), Vector3.zero);
            for (int i = 1; i < m_SelectionCorners.Length; i++)
            {
                bounds.Encapsulate(viewport.InverseTransformPoint(m_SelectionCorners[i]));
            }
            return bounds;
        }

        private static float GetVisibilityOffset(float min, float max, float viewMin, float viewMax)
        {
            // 控件比视口大时居中，否则只移动被裁切的部分。
            if (max - min > viewMax - viewMin) return (viewMin + viewMax - min - max) * 0.5f;
            if (min < viewMin) return viewMin - min;
            if (max > viewMax) return viewMax - max;
            return 0f;
        }
    }
}
