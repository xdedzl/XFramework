using System;

namespace XFramework
{
    /// <summary>
    /// 标记流程或子流程所需的相机对象Key。
    /// 规则：优先查找子流程配置，若无则沿用父流程配置。
    /// 进入阶段时，将通过 UObjectFinder 查找并激活该相机，退出时关闭。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ProcedureCameraAttribute : Attribute
    {
        public string CameraName { get; }

        public ProcedureCameraAttribute(string cameraName)
        {
            CameraName = cameraName;
        }
    }
}
