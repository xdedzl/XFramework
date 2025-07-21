using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public enum VectorType
    {
        LocalPos,
        Pos,
        LocalAngle,
        Angle,
        LocalScale
    }

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

        /// <summary>
        /// 分别对三个分离取觉得值
        /// </summary>
        public static Vector3 Abs(this Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        /// <summary>
        /// 是否趋近于0
        /// </summary>
        public static bool AlmostZero(this Vector3 v)
        {
            return v.sqrMagnitude < 9.999999E-09f;
        }

        public static Vector3 ToVector3(this Vector3Int v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static Vector2 ToVector2(this Vector2Int v)
        {
            return new Vector2(v.x, v.y);
        }

        public static Vector2Int Top(this Vector2Int v)
        {
            return new Vector2Int(v.x, v.y + 1);
        }

        public static Vector2Int Bottom(this Vector2Int v)
        {
            return new Vector2Int(v.x, v.y - 1);
        }

        public static Vector2Int Left(this Vector2Int v)
        {
            return new Vector2Int(v.x - 1, v.y);
        }

        public static Vector2Int Right(this Vector2Int v)
        {
            return new Vector2Int(v.x + 1, v.y);
        }

        public static Vector2Int Normalize(this Vector2Int v)
        {
            return new Vector2Int(Math.Sign(v.x), Math.Sign(v.y));
        }

        public static Vector2 Rotate90(this Vector2 v)
        {
            return new Vector2(v.y, -v.x);
        }

        public static Vector2 RotateNegative90(this Vector2 v)
        {
            return new Vector2(-v.y, v.x);
        }

        public static Vector2 Rotate(this Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }

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

        /// <summary>
        /// 获取RectTransform
        /// </summary>
        public static RectTransform RectTransform(this Component component)
        {
            return component.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 获取RectTransform
        /// </summary>
        public static RectTransform RectTransform(this GameObject obj)
        {
            return obj.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 获取Transform的所有子物体
        /// </summary>
        public static Transform[] GetChilds(this Transform transform)
        {
            int count = transform.childCount;
            Transform[] childs = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                childs[i] = transform.GetChild(i);
            }
            return childs;
        }

        /// <summary>
        /// 获取Transform的所有 名为 _name 的 子物体
        /// </summary>
        public static Transform[] GetChilds(this Transform transform, string name)
        {
            int count = transform.childCount;
            List<Transform> childs = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                Transform tmpChild = transform.GetChild(i);
                if (tmpChild.name == name)
                {
                    childs.Add(tmpChild);
                }
                childs.AddRange(tmpChild.GetChilds(name));
            }
            return childs.ToArray();
        }

        /// <summary>
        /// 获取Transform的所有子物体的GameObject
        /// </summary>
        public static GameObject[] GetChildsObj(this Transform transform)
        {
            int count = transform.childCount;
            GameObject[] childs = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                childs[i] = transform.GetChild(i).gameObject;
            }
            return childs;
        }

        /// <summary>
        /// 设置某个矢量x的值
        /// </summary>
        public static void SetX(this Transform transform, float x, VectorType type = VectorType.Pos)
        {
            switch (type)
            {
                case VectorType.LocalPos:
                    transform.localPosition = new Vector3(x, transform.localPosition.y, transform.localPosition.z);
                    break;
                case VectorType.Pos:
                    transform.position = new Vector3(x, transform.position.y, transform.position.z);
                    break;
                case VectorType.LocalAngle:
                    transform.localEulerAngles = new Vector3(x, transform.localEulerAngles.y, transform.localEulerAngles.z);
                    break;
                case VectorType.Angle:
                    transform.eulerAngles = new Vector3(x, transform.eulerAngles.y, transform.eulerAngles.z);
                    break;
                case VectorType.LocalScale:
                    transform.localScale = new Vector3(x, transform.localScale.y, transform.localScale.z);
                    break;
            }
        }

        /// <summary>
        /// 设置某个矢量y的值
        /// </summary>
        public static void SetY(this Transform transform, float y, VectorType type = VectorType.Pos)
        {
            switch (type)
            {
                case VectorType.LocalPos:
                    transform.localPosition = new Vector3(transform.localPosition.x, y, transform.localPosition.z);
                    break;
                case VectorType.Pos:
                    transform.position = new Vector3(transform.position.x, y, transform.position.z);
                    break;
                case VectorType.LocalAngle:
                    transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, y, transform.localEulerAngles.z);
                    break;
                case VectorType.Angle:
                    transform.eulerAngles = new Vector3(transform.eulerAngles.x, y, transform.eulerAngles.z);
                    break;
                case VectorType.LocalScale:
                    transform.localScale = new Vector3(transform.localScale.x, y, transform.localScale.z);
                    break;
            }
        }

        /// <summary>
        /// 设置某个矢量z的值
        /// </summary>
        public static void SetZ(this Transform transform, float z, VectorType type = VectorType.Pos)
        {
            switch (type)
            {
                case VectorType.LocalPos:
                    transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, z);
                    break;
                case VectorType.Pos:
                    transform.position = new Vector3(transform.position.x, transform.position.y, z);
                    break;
                case VectorType.LocalAngle:
                    transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, z);
                    break;
                case VectorType.Angle:
                    transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, z);
                    break;
                case VectorType.LocalScale:
                    transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, z);
                    break;
            }
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
#if UNITY_6000
            body.linearVelocity = zero;
#else
            body.velocity = zero;
#endif
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