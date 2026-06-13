using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Draws a serializable class or struct field inside a compact boxed group.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class PrettyBoxAttribute : PropertyAttribute
    {
    }
}
