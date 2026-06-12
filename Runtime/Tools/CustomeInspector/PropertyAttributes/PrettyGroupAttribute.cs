using System;

namespace XFramework
{
    /// <summary>
    /// Groups multiple top-level serialized fields in XMonoBehaviourInspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class PrettyGroupAttribute : Attribute
    {
        public string Title { get; }

        public PrettyGroupAttribute(string title)
        {
            Title = title;
        }
    }
}
