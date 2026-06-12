using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Draws a string field as a clickable hyperlink.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class HyperlinkAttribute : PropertyAttribute
    {
        public string Name { get; private set; }

        public HyperlinkAttribute(string name)
        {
            Name = name;
        }
    }
}
