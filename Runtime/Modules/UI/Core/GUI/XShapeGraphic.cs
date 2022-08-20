using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 仅用于实现 Tree 的查找
    /// </summary>
    [RequireComponent(typeof(ShapeGraphic))]
    public class XShapeGraphic : GUIBase
    {
        /// <summary>
        /// Tree 实体
        /// </summary>
        public ShapeGraphic shapeGraphic;
        private void Reset()
        {
            shapeGraphic = transform.GetComponent<ShapeGraphic>();
        }
    }
}