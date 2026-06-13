using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Draws an int or string field as a Unity layer selector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class LayerAttribute : PropertyAttribute
    {
    }
}
