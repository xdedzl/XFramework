using System.Collections.Generic;
using XFramework.Mathematics;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 贝塞尔曲线绘制
    /// </summary>
    public class BezierGraphic : LineGraphic
    {
        public BezierGraphic(Vector3[] originPoints, uint[] segTypes)
        {
            List<Vector3> points = new List<Vector3>();
            int index = 0;
            for (int i = 0; i < segTypes.Length; i++)
            {
                switch (segTypes[i])
                {
                    case 0:     // 直线,只取2个点
                        AddifNotContain(points, new List<Vector3>() { originPoints[index++], originPoints[index] });
                        break;
                    case 1:     // 二次贝塞尔, 取3个点
                        Vector3[] bezier2 = new Vector3[3];
                        bezier2[0] = originPoints[index++];
                        bezier2[1] = originPoints[index++];
                        bezier2[2] = originPoints[index];
                        AddifNotContain(points, Math3d.GetBezierList(bezier2));
                        break;
                    case 2:     // 三次贝塞尔, 取4个点
                        Vector3[] bezier3 = new Vector3[4];
                        bezier3[0] = originPoints[index++];
                        bezier3[1] = originPoints[index++];
                        bezier3[2] = originPoints[index++];
                        bezier3[3] = originPoints[index];
                        AddifNotContain(points, Math3d.GetBezierList(bezier3));
                        break;
                    default:
                        break;
                }
            }

            m_Poses = points;

            void AddifNotContain(List<Vector3> _drawPath, List<Vector3> _pointsAdd)
            {
                if (_drawPath.Count > 0 && _drawPath.End() == _pointsAdd[0])
                {
                    _pointsAdd.RemoveAt(0);
                }
                _drawPath.AddRange(_pointsAdd);
            }       // 内部函数, 去重添加
        }
    }
}