using UnityEngine;

namespace XFramework
{
    #if UNITY_EDITOR
    using UEditorPrefs = UnityEditor.EditorPrefs;
    public partial class UUtility
    {
        public static class EditorPrefs
        {
            public static void SetVector2(string key, Vector2 value)
            {
                UEditorPrefs.SetString(key, value.ToString());
            }
            
            public static Vector2 GetVector2(string key, Vector2 defaultValue)
            {
                var value = UEditorPrefs.GetString(key);
                if (Vector.TryParseVector2(value, out var vector))
                {
                    return vector;
                }
                return defaultValue;
            }
            
            public static Vector2 GetVector2(string key) => EditorPrefs.GetVector2(key, Vector3.zero);
 
            
            public static void SetVector3(string key, Vector3 value)
            {
                UEditorPrefs.SetString(key, value.ToString());
            }

            public static Vector3 GetVector3(string key, Vector3 defaultValue)
            {
                var value = UEditorPrefs.GetString(key);
                if (Vector.TryParseVector3(value, out var vector))
                {
                    return vector;
                }
                return defaultValue;
            }
            
            public static Vector3 GetVector3(string key) => EditorPrefs.GetVector3(key, Vector3.zero);
        }
    }
    #endif
}