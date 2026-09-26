using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Command
{
    public static partial class XCommandUtility
    {
        internal static class GameObject
        {
            internal static string GetGameObjectPath(UnityEngine.GameObject gameObject)
            {
                if (gameObject == null)
                    return string.Empty;
                var names = new Stack<string>();
                for (Transform current = gameObject.transform; current != null; current = current.parent)
                    names.Push(current.name);
                return string.Join("/", names);
            }

            internal static string GetIndexedGameObjectPath(UnityEngine.GameObject gameObject)
            {
                if (gameObject == null)
                    return string.Empty;
                var names = new Stack<string>();
                for (Transform current = gameObject.transform; current != null; current = current.parent)
                    names.Push($"{current.name}[{current.GetSiblingIndex()}]");
                return string.Join("/", names);
            }
        }
    }
}
