using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 这个类管理一系列的拓展函数
    /// 以后把System和UnityEngine的扩展分开
    /// </summary>
    public static partial class Extend
    {
        #region Vector相关
        public static Vector3 WithX(this Vector3 v, float x)
        {
            return new Vector3(x, v.y, v.z);
        }

        public static Vector3 WithY(this Vector3 v, float y)
        {
            return new Vector3(v.x, y, v.z);
        }

        public static Vector3 WithZ(this Vector3 v, float z)
        {
            return new Vector3(v.x, v.y, z);
        }

        public static Vector2 WithX(this Vector2 v, float x)
        {
            return new Vector2(x, v.y);
        }

        public static Vector2 WithY(this Vector2 v, float y)
        {
            return new Vector2(v.x, y);
        }

        public static Vector2 YZ(this Vector3 vec)
        {
            return new Vector2(vec.y, vec.z);
        }

        public static Vector2 XZ(this Vector3 vec)
        {
            return new Vector2(vec.x, vec.z);
        }

        public static Vector2 XY(this Vector3 vec)
        {
            return new Vector2(vec.x, vec.y);
        }

        #region 通过Vector2获取Vector3

        public static Vector3 AddX(this Vector2 vec, float value = 0)
        {
            return new Vector3(value, vec.x, vec.y);
        }

        public static Vector3 AddY(this Vector2 vec, float value = 0)
        {
            return new Vector3(vec.x, value, vec.y);
        }

        public static Vector3 AddZ(this Vector2 vec, float value = 0)
        {
            return new Vector3(vec.x, vec.y, value);
        }

        #endregion

        #endregion

        #region Transform

        /// <summary>
        /// 找寻名字为name的子物体
        /// </summary>
        public static Transform FindRecursive(this Transform transform, string name)
        {
            if (transform.name.Equals(name))
            {
                return transform;
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform transform2 = transform.GetChild(i).FindRecursive(name);
                if (transform2 != null)
                {
                    return transform2;
                }
            }
            return null;
        }
        #endregion

        #region Rigibogy

        /// <summary>
        /// 重置刚体
        /// </summary>
        public static void ResetDynamics(this Rigidbody body)
        {
            Vector3 zero = Vector3.zero;
            body.angularVelocity = zero;
            body.velocity = zero;
        }

        #endregion

        #region Quaternion 加减貌似只在其他两个轴为0的时候起作用

        /// <summary>
        /// 将q加上rotation并返回
        /// </summary>
        public static Quaternion AddRotation(this Quaternion q, Quaternion rotation)
        {
            return q * rotation;
        }
        public static Quaternion AddRotation(this Quaternion q, Vector3 angle)
        {
            return q * Quaternion.Euler(angle);
        }

        /// <summary>
        /// 将减去rotation并返回
        /// </summary>
        public static Quaternion SubtractRotation(this Quaternion q, Quaternion rotation)
        {
            return q * Quaternion.Inverse(rotation);
        }
        public static Quaternion SubtractRotation(this Quaternion q, Vector3 angle)
        {
            return q * Quaternion.Inverse(Quaternion.Euler(angle));
        }

        #endregion

        #region Color

        public static Color Alpha(this Color color, float a)
        {
            return new Color(color.r, color.g, color.b, a);
        }

        #endregion

        #region UI

        /// <summary>
        /// 给Toggle添加受控制的物体
        /// </summary>
        /// <param name="toggle"></param>
        /// <param name="panel"></param>
        public static void AddCotroledPanel(this Toggle toggle, GameObject panel)
        {
            toggle.onValueChanged.AddListener((a) => panel.SetActive(a));
        }

        #endregion

        #region Terrain

        /// <summary>
        /// 右边的地形块
        /// </summary>
        /// <param name="terrain"></param>
        /// <returns></returns>
        public static Terrain Right(this Terrain terrain)
        {
#if UNITY_2018_3_OR_NEWER
            return terrain.rightNeighbor;
#else
        Vector3 rayStart = terrain.GetPosition() + new Vector3(terrain.terrainData.size.x * 1.5f, 10000, terrain.terrainData.size.z * 0.5f);
        RaycastHit hitInfo;
        Physics.Raycast(rayStart, Vector3.down, out hitInfo, float.MaxValue, LayerMask.GetMask("Terrain"));
        return hitInfo.collider?.GetComponent<Terrain>();
#endif
        }

        /// <summary>
        /// 上边的地形块
        /// </summary>
        /// <param name="terrain"></param>
        /// <returns></returns>
        public static Terrain Top(this Terrain terrain)
        {
#if UNITY_2018_3_OR_NEWER
            return terrain.topNeighbor;
#else
        Vector3 rayStart = terrain.GetPosition() + new Vector3(terrain.terrainData.size.x * 0.5f, 10000, terrain.terrainData.size.z * 1.5f);
        RaycastHit hitInfo;
        Physics.Raycast(rayStart, Vector3.down, out hitInfo, float.MaxValue, LayerMask.GetMask("Terrain"));
        return hitInfo.collider?.GetComponent<Terrain>();
#endif
        }

        /// <summary>
        /// 左边的地形块
        /// </summary>
        /// <param name="terrain"></param>
        /// <returns></returns>
        public static Terrain Left(this Terrain terrain)
        {
#if UNITY_2018_3_OR_NEWER
            return terrain.leftNeighbor;
#else
        Vector3 rayStart = terrain.GetPosition() + new Vector3(-terrain.terrainData.size.x * 0.5f, 10000, terrain.terrainData.size.z * 0.5f);
        RaycastHit hitInfo;
        Physics.Raycast(rayStart, Vector3.down, out hitInfo, float.MaxValue, LayerMask.GetMask("Terrain"));
        return hitInfo.collider?.GetComponent<Terrain>();
#endif
        }

        /// <summary>
        /// 下边的地形块
        /// </summary>
        /// <param name="terrain"></param>
        /// <returns></returns>
        public static Terrain Bottom(this Terrain terrain)
        {
#if UNITY_2018_3_OR_NEWER
            return terrain.bottomNeighbor;
#else
        Vector3 rayStart = terrain.GetPosition() + new Vector3(terrain.terrainData.size.x * 0.5f, 10000, -terrain.terrainData.size.z * 0.5f);
        RaycastHit hitInfo;
        Physics.Raycast(rayStart, Vector3.down, out hitInfo, float.MaxValue, LayerMask.GetMask("Terrain"));
        return hitInfo.collider?.GetComponent<Terrain>();
#endif
        }

        #endregion

        #region Bounds

        /// <summary>
        /// 绘制一个包围盒
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="color"></param>
        public static void Draw(this Bounds bounds, Color color)
        {
            var e = bounds.extents;
            Debug.DrawLine(bounds.center + new Vector3(+e.x, +e.y, +e.z), bounds.center + new Vector3(-e.x, +e.y, +e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(+e.x, -e.y, +e.z), bounds.center + new Vector3(-e.x, -e.y, +e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(+e.x, -e.y, -e.z), bounds.center + new Vector3(-e.x, -e.y, -e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(+e.x, +e.y, -e.z), bounds.center + new Vector3(-e.x, +e.y, -e.z), color);

            Debug.DrawLine(bounds.center + new Vector3(+e.x, +e.y, +e.z), bounds.center + new Vector3(+e.x, -e.y, +e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(-e.x, +e.y, +e.z), bounds.center + new Vector3(-e.x, -e.y, +e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(-e.x, +e.y, -e.z), bounds.center + new Vector3(-e.x, -e.y, -e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(+e.x, +e.y, -e.z), bounds.center + new Vector3(+e.x, -e.y, -e.z), color);

            Debug.DrawLine(bounds.center + new Vector3(+e.x, +e.y, +e.z), bounds.center + new Vector3(+e.x, +e.y, -e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(+e.x, -e.y, +e.z), bounds.center + new Vector3(+e.x, -e.y, -e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(-e.x, +e.y, +e.z), bounds.center + new Vector3(-e.x, +e.y, -e.z), color);
            Debug.DrawLine(bounds.center + new Vector3(-e.x, -e.y, +e.z), bounds.center + new Vector3(-e.x, -e.y, -e.z), color);
        }

        #endregion
    }
}