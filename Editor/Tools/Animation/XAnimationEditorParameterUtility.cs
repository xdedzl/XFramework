#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Editor
{
    internal static class XAnimationEditorParameterUtility
    {
        public static float ConvertParameterDefaultToFloat(object value)
        {
            if (value == null)
            {
                return 0f;
            }

            try
            {
                return Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return value switch
                {
                    float f => f,
                    double d => (float)d,
                    long l => l,
                    int i => i,
                    _ => 0f,
                };
            }
        }

        public static int ConvertParameterDefaultToInt(object value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return value switch
                {
                    int i => i,
                    long l => (int)l,
                    float f => Mathf.RoundToInt(f),
                    double d => (int)Math.Round(d),
                    _ => 0,
                };
            }
        }

        public static bool ConvertParameterDefaultToBool(object value)
        {
            if (value == null)
            {
                return false;
            }

            try
            {
                return Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return value is bool b && b;
            }
        }

        public static void RemoveStaleValues<T>(Dictionary<string, T> cache, HashSet<string> validKeys)
        {
            if (cache == null || cache.Count == 0)
            {
                return;
            }

            List<string> removedKeys = null;
            foreach (string key in cache.Keys)
            {
                if (validKeys.Contains(key))
                {
                    continue;
                }

                removedKeys ??= new List<string>();
                removedKeys.Add(key);
            }

            if (removedKeys == null)
            {
                return;
            }

            for (int i = 0; i < removedKeys.Count; i++)
            {
                cache.Remove(removedKeys[i]);
            }
        }
    }
}
#endif
