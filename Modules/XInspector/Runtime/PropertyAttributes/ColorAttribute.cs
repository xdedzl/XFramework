using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 颜色检视器。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ColorAttribute : PropertyAttribute
    {
        public float R { get; private set; }
        public float G { get; private set; }
        public float B { get; private set; }
        public float A { get; private set; }

        public ColorAttribute(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }
}
