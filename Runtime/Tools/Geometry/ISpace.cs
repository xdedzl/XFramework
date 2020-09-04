using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 空间
    /// </summary>
    public interface ISpace
    {

    }

    /// <summary>
    /// 面
    /// </summary>
    public interface IPlane
    {
        /// <summary>
        /// 法线
        /// </summary>
        Vector3 normal { get; }
        /// <summary>
        /// 面积
        /// </summary>
        float Area { get; }
        SR R(Point point);
        SR R(MulitLineSegment point);
    }

    /// <summary>
    /// 体
    /// </summary>
    public interface IVolume
    {
        /// <summary>
        /// 体积
        /// </summary>
        float Volume { get; }
    }

    public enum SR
    {
        /// <summary>
        /// 相离
        /// </summary>
        Separation,
        /// <summary>
        /// 相接
        /// </summary>
        Connect,
        /// <summary>
        /// 重叠
        /// </summary>
        Overlap,
        /// <summary>
        /// 相等
        /// </summary>
        Equal,
        /// <summary>
        /// 包含
        /// </summary>
        Contain
    }
}