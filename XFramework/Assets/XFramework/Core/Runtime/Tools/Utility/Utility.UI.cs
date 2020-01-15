using UnityEngine;

namespace XFramework
{
    public partial class Utility
    {
        public class UI
        {
            /// <summary>
            /// texture转sprite
            /// </summary>
            /// <param name="tex"></param>
            /// <returns></returns>
            public static Sprite TexToSprite(Texture2D tex)
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            /// <summary>
            /// 判断是否source在target区域内
            /// </summary>
            /// <param name="source"></param>
            /// <param name="target"></param>
            /// <returns></returns>
            public static bool CheckUICoincide(RectTransform source, RectTransform target)
            {
                Vector2 sourcePosition = Camera.main.WorldToScreenPoint(source.position);
                Vector2 targetPosition = Camera.main.WorldToScreenPoint(target.position);

                float halfWidth = target.rect.width / 2; 
                float halfHeight = target.rect.height / 2;

                if (sourcePosition.x <= targetPosition.x + halfWidth
                    && sourcePosition.x >= targetPosition.x - halfWidth
                    && sourcePosition.y <= targetPosition.y + halfHeight
                    && sourcePosition.y >= targetPosition.y - halfHeight)
                {
                    return true;
                }

                return false;
            }
        }
    }
}