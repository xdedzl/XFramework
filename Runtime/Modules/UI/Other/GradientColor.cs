using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    /// <summary>
    /// 继承自Graphic的组件都可以添加此效果
    /// </summary>
    [AddComponentMenu("UI/Effects/GradientColor")]
    public class GradientColor : BaseMeshEffect
    {
        public enum DIRECTION
        {
            Vertical,
            Horizontal,
            Both,
        }
        public DIRECTION direction = DIRECTION.Vertical;
        public Color colorTop = Color.white;
        public Color colorBottom = Color.white;
        public Color colorLeft = Color.white;
        public Color colorRight = Color.white;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }
            float topX = 0f, topY = 0f, bottomX = 0f, bottomY = 0f;
            UIVertex tempVertex0 = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref tempVertex0, i);
                topX = Mathf.Max(topX, tempVertex0.position.x);
                topY = Mathf.Max(topY, tempVertex0.position.y);
                bottomX = Mathf.Min(bottomX, tempVertex0.position.x);
                bottomY = Mathf.Min(bottomY, tempVertex0.position.y);
            }

            float width = topX - bottomX;
            float height = topY - bottomY;

            UIVertex tempVertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref tempVertex, i);
                float realAlpha = tempVertex.color.a;
                byte orgAlpha = 0;
                Color colorOrg = tempVertex.color;
                Color colorV = Color.Lerp(colorBottom, colorTop, (tempVertex.position.y - bottomY) / height);
                Color colorH = Color.Lerp(colorLeft, colorRight, (tempVertex.position.x - bottomX) / width);
                switch (direction)
                {
                    case DIRECTION.Both:
                        orgAlpha = (byte)(255 * ((colorV.a + colorV.a) / 2) * (realAlpha / 255));
                        tempVertex.color = colorOrg * colorV * colorH;
                        break;
                    case DIRECTION.Vertical:
                        orgAlpha = (byte)(255 * colorV.a * (realAlpha / 255));
                        tempVertex.color = colorOrg * colorV;
                        break;
                    case DIRECTION.Horizontal:
                        orgAlpha = (byte)(255 * colorH.a * (realAlpha / 255));
                        tempVertex.color = colorOrg * colorH;
                        break;
                }
                tempVertex.color.a = (byte)orgAlpha;
                vh.SetUIVertex(tempVertex, i);
            }
        }
    }
}