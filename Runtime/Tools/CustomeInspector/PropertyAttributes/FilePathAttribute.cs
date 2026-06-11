using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Draws a string field with a file picker.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class FilePathAttribute : PropertyAttribute
    {
        public string Extension { get; }

        public FilePathAttribute(string extension = "*.*")
        {
            Extension = extension;
        }
    }
}
