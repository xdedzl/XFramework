using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XFramework
{
    public partial class UUtility
    {
        /// <summary>
        /// UI相关工具
        /// </summary>
        public static class Physics2D
        {
            public static bool TryUpdateBoxColliderBySprites(GameObject gameObject, bool autoAdd)
            {
                var boxCollider = gameObject.GetComponent<BoxCollider2D>();
                if (boxCollider == null && autoAdd)
                {
                    boxCollider = gameObject.AddComponent<BoxCollider2D>();
                }
                if( boxCollider == null)
                {
                    return false;
                }
                SpriteRenderer[] spriteRenderers = boxCollider.GetComponentsInChildren<SpriteRenderer>(true);
                if (spriteRenderers.Length == 0)
                {
                    return false;
                }

                Bounds totalBounds = new Bounds();
                bool hasValidBounds = false;
                Transform parentTransform = boxCollider.transform;

                foreach (SpriteRenderer sr in spriteRenderers)
                {
                    if (sr.transform == parentTransform)
                        continue;

                    // 手动计算缩放的倒数（修复Vector3.Inverse()错误）
                    Vector3 scale = parentTransform.lossyScale;
                    Vector3 inverseScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

                    // 手动将世界空间边界转换为本地空间
                    Bounds worldBounds = sr.bounds;
                    Bounds localBounds = new Bounds(
                        parentTransform.InverseTransformPoint(worldBounds.center),
                        Vector3.Scale(worldBounds.size, inverseScale)
                    );

                    if (!hasValidBounds)
                    {
                        totalBounds = localBounds;
                        hasValidBounds = true;
                    }
                    else
                    {
                        totalBounds.Encapsulate(localBounds);
                    }
                }

                if (!hasValidBounds)
                {
                    return false;
                }

                boxCollider.size = totalBounds.size;
                boxCollider.offset = totalBounds.center;
                return true;
            }
        }
    }
}