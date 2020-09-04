using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 长方形
    /// </summary>
    public struct Rectangle : IPlane
    {
        /// <summary>
        /// 中心点
        /// </summary>
        public Vector3 centerPos;
        ///<summary>
        ///旋转角
        /// </summary>
        public Quaternion rotation;
        ///<summary>
        ///旋转角
        /// </summary>
        public float length;
        /// <summary>
        /// 宽
        /// </summary>
        public float width;

        public Rectangle(Vector3 centerPos, Quaternion rotation, float length, float width)
        {
            this.centerPos = centerPos;
            this.rotation = rotation;
            this.length = length;
            this.width = width;
        }

        /// <summary>
        /// 角度
        /// </summary>
        public Vector3 angle
        {
            get
            {
                return rotation.eulerAngles;
            }
            set
            {
                rotation = Quaternion.Euler(value);
            }
        }

        /// <summary>
        /// 面积
        /// </summary>
        public float Area
        {
            get
            {
                return length * width;
            }
        }

        /// <summary>
        /// 法线
        /// </summary>
        public Vector3 normal
        {
            get
            {
                return up;
            }
        }

        /// <summary>
        /// 矩形框右方向
        /// </summary>
        public Vector3 right
        {
            get
            {

                return rotation * Vector3.right;
            }
            set
            {
                rotation = Quaternion.FromToRotation(Vector3.right, value);
            }

        }

        /// <summary>
        /// 矩形框前方向
        /// </summary>
        public Vector3 forward
        {
            get
            {

                return rotation * Vector3.forward;
            }
            set
            {
                rotation = Quaternion.FromToRotation(Vector3.forward, value);
            }
        }

        /// <summary>
        /// 矩形框上方向
        /// </summary>
        public Vector3 up
        {
            get
            {

                return rotation * Vector3.up;
            }
            set
            {
                rotation = Quaternion.FromToRotation(Vector3.up, value);
            }
        }

        /// <summary>
        /// 左下点
        /// </summary>
        public Vector3 leftDown
        {
            get
            {
                return centerPos + 0.5f * (-right * width + -forward * length);
            }
        }

        /// <summary>
        /// 左上点
        /// </summary>
        public Vector3 leftUp
        {
            get
            {
                return centerPos + 0.5f * (-right * width + forward * length);
            }
        }

        /// <summary>
        /// 右上点
        /// </summary>
        public Vector3 rightUp
        {
            get
            {
                return centerPos + 0.5f * (right * width + forward * length);
            }
        }

        /// <summary>
        /// 右下点
        /// </summary>
        public Vector3 rightDown
        {
            get
            {
                return centerPos + 0.5f * (right * width - forward * length);
            }
        }

        /// <summary>
        /// 获取矩形的四个点
        /// </summary>
        /// <returns></returns>
        public Vector3[] GetBound()
        {
            return new Vector3[4]
            {
                leftDown,
                leftUp,
                rightUp,
                rightDown
            };
        }

        public SR R(Point point)
        {
            throw new System.NotImplementedException();
        }

        public SR R(MulitLineSegment point)
        {
            throw new System.NotImplementedException();
        }
    }
}