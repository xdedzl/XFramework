using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Draws a string field as a password input.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class PasswordAttribute : PropertyAttribute
    {
    }
}
