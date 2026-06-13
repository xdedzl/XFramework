using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Draws a string field with a folder picker.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class FolderPathAttribute : PropertyAttribute
    {
    }
}
