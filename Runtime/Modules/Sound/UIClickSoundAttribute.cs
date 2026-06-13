using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XFramework
{
    public class UIClickSoundAttribute : PropertyAttribute { }
    
    
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(UIClickSoundAttribute))]
    public class UIClickSoundDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, $"{label.text} 只能在 string 字段上使用 UIClickSound", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            XFrameworkSetting setting = XApplication.Setting;
            if (!setting)
            {
                EditorGUI.HelpBox(position, "未找到 XFrameworkSetting", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            List<string> keys = GetKeys(setting);
            string currentKey = property.stringValue ?? string.Empty;
            if (!keys.Contains(string.Empty))
            {
                keys.Insert(0, string.Empty);
            }

            if (!string.IsNullOrEmpty(currentKey) && !keys.Contains(currentKey))
            {
                keys.Insert(0, currentKey);
            }

            const float locateButtonWidth = 70f;
            Rect popupRect = new Rect(
                position.x,
                position.y,
                position.width - locateButtonWidth - EditorGUIUtility.standardVerticalSpacing,
                EditorGUIUtility.singleLineHeight);
            Rect locateButtonRect = new Rect(
                popupRect.xMax + EditorGUIUtility.standardVerticalSpacing,
                position.y,
                locateButtonWidth,
                EditorGUIUtility.singleLineHeight);
            Rect audioRect = new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            DrawKeyPopup(property, label, keys, currentKey, popupRect);
            DrawLocateButton(setting, locateButtonRect);

            string key = property.stringValue ?? string.Empty;
            if (setting.ContainsUIClickSoundKey(key))
            {
                DrawAudioField(setting, key, audioRect);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            string key = property.stringValue ?? string.Empty;
            if (XApplication.Setting && XApplication.Setting.ContainsUIClickSoundKey(key))
            {
                return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
            }

            return EditorGUIUtility.singleLineHeight;
        }

        private static List<string> GetKeys(XFrameworkSetting setting)
        {
            List<string> keys = new List<string>();
            foreach (string key in setting.GetUIClickSoundKeys())
            {
                if (!keys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            return keys;
        }

        private static void DrawKeyPopup(
            SerializedProperty property,
            GUIContent label,
            IReadOnlyList<string> keys,
            string currentKey,
            Rect popupRect)
        {
            if (keys.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.Popup(popupRect, label.text, 0, new[] { "No Options" });
                }

                return;
            }

            int currentIndex = Mathf.Max(0, IndexOf(keys, currentKey));
            string[] displayOptions = CreateDisplayOptions(keys);

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popupRect, label.text, currentIndex, displayOptions);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = keys[Mathf.Clamp(newIndex, 0, keys.Count - 1)];
            }
            EditorGUI.showMixedValue = false;
        }

        private static void DrawLocateButton(XFrameworkSetting setting, Rect locateButtonRect)
        {
            if (GUI.Button(locateButtonRect, "定位配置"))
            {
                Selection.activeObject = setting;
                EditorGUIUtility.PingObject(setting);
            }
        }

        private static void DrawAudioField(XFrameworkSetting setting, string key, Rect audioRect)
        {
            string path = setting.GetUIClickSoundPath(key);
            AudioClip clip = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            EditorGUI.BeginChangeCheck();
                AudioClip newClip = (AudioClip)EditorGUI.ObjectField(audioRect, "点击音效资源", clip, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                string newPath = newClip ? AssetDatabase.GetAssetPath(newClip) : string.Empty;
                Undo.RecordObject(setting, "修改 UI 点击音效");
                if (setting.SetUIClickSoundPath(key, newPath))
                {
                    EditorUtility.SetDirty(setting);
                }
            }
        }

        private static int IndexOf(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string[] CreateDisplayOptions(IReadOnlyList<string> choices)
        {
            string[] displayOptions = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                displayOptions[i] = string.IsNullOrEmpty(choices[i]) ? "None" : choices[i];
            }

            return displayOptions;
        }
    }
#endif
}

