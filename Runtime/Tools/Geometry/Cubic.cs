using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 立方体
    /// </summary>
    public struct Cubic : IVolume
    {
        /// <summary>
        /// 中心点
        /// </summary>
        public Vector3 center;
        /// <summary>
        /// 旋转角
        /// </summary>
        public Quaternion rotation;
        /// <summary>
        /// 长
        /// </summary>
        public float length;
        /// <summary>
        /// 宽
        /// </summary>
        public float width;
        /// <summary>
        /// 高
        /// </summary>
        public float height;

        public Cubic(Vector3 center, Quaternion rotation, float length, float width, float height)
        {
            this.center = center;
            this.rotation = rotation;
            this.length = length;
            this.width = width;
            this.height = height;
        }

        /// <summary>
        /// 体积
        /// </summary>
        public float Volume
        {
            get
            {
                return length * width * height;
            }
        }

        /// <summary>
        /// 欧拉角
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
        /// 
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
        /// 
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
        /// 
        /// </summary>
        public Vector3 forward
        {
            get
            {
                return rotation * Vector3.forward;
            }
            set
            {
                rotation = Quaternion.LookRotation(value);
            }
        }

        /// <summary>
        /// 上表面
        /// </summary>
        public Rectangle upRect
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        /// <summary>
        /// 下表面
        /// </summary>
        public Rectangle downRect
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        /// <summary>
        /// 右侧面
        /// </summary>
        public Rectangle rightRect
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        /// <summary>
        /// 左侧面
        /// </summary>
        public Rectangle leftRect
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        /// <summary>
        /// 正面
        /// </summary>
        public Rectangle forwardRect
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        /// <summary>
        /// 反面
        /// </summary>
        public Rectangle backwardRect
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Vector3[] GetBound()
        {
            throw new System.NotImplementedException();
        }
    }
}