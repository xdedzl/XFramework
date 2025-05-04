using System.Collections.Generic;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 形状绘制基类
    /// </summary>
    public abstract class ShapeGraphicBase
    {
        public abstract void GetVertexs(ref List<UIVertex> vertexs);
        public abstract void GetTriangles(ref List<int> triangles, int offset);
    }
}