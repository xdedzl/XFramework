using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Farm
{
    public class PolygonArea : MonoBehaviour
    {
        public List<Vector3> points;
        
        // 选中点的索引（Scene 编辑用），-1 表示未选中
        [HideInInspector]
        public int selectedIndex = -1;

        // 确保存在一个默认矩形（本地空间下，以Transform为中心，1x1）
        private void Reset()
        {
            EnsureDefaultRectangle();
        }

        private void OnValidate()
        {
            // 保证列表非空
            if (points == null || points.Count < 3)
            {
                EnsureDefaultRectangle();
            }
        }

        private void EnsureDefaultRectangle()
        {
            if (points == null)
                points = new List<Vector3>();
            points.Clear();
            // 本地坐标下的单位矩形（顺时针）
            points.Add(new Vector3(-0.5f, 0f, -0.5f));
            points.Add(new Vector3(0.5f, 0f, -0.5f));
            points.Add(new Vector3(0.5f, 0f, 0.5f));
            points.Add(new Vector3(-0.5f, 0f, 0.5f));
            selectedIndex = -1;
        }
    }
}