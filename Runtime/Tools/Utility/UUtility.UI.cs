using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    public partial class UUtility
    {
        /// <summary>
        /// UI相关工具
        /// </summary>
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

            public static void ShowTip(string text, float time=5)
            {
                Canvas canvas = GameObject.Find("Canvas").GetComponent<Canvas>();

                var img = new GameObject("TempTip").AddComponent<Image>();
                var rectTransform = img.rectTransform;
                var canvasSize = canvas.GetComponent<RectTransform>().sizeDelta;
                rectTransform.position = canvasSize * 0.5f;
                rectTransform.sizeDelta = new Vector2(2000, 100);
                rectTransform.SetParent(canvas.transform);
                img.color = new Color(0, 0, 0, 0.7f);
                

                var tmp = new GameObject("text").AddComponent<TextMeshProUGUI>();
                rectTransform = tmp.rectTransform;
                rectTransform.SetParent(img.transform);
                rectTransform.anchorMin = Vector2.zero; 
                rectTransform.anchorMax = Vector2.one;  
                rectTransform.offsetMin = Vector2.zero; 
                rectTransform.offsetMax = Vector2.zero;
                tmp.text = text;
                tmp.fontSize = 60;
                tmp.font = XApplication.Setting.font;
                tmp.alignment = TextAlignmentOptions.Center;

                Timer.Register(time, () =>
                {
                    Object.Destroy(img.gameObject);
                });
            }
        }
    }
}