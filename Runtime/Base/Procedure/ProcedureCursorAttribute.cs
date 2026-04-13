using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 流程鼠标状态特性，用于在进入流程时自动设置鼠标锁定状态和显隐。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProcedureCursorAttribute : Attribute
    {
        public CursorLockMode CursorLockMode { get; private set; }
        public bool Visible { get; private set; }

        public ProcedureCursorAttribute(CursorLockMode cursorLockMode, bool visible = true)
        {
            CursorLockMode = cursorLockMode;
            Visible = visible;
        }
    }
}
