using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(LayerAttribute))]
    public sealed class LayerDrawer : PropertyDrawer
    {
        private const string ErrorMessage = "[Layer] can only be used on int or string fields.";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                var field = new LayerField(property.displayName);
                field.BindProperty(property);
                return field;
            }

            if (property.propertyType == SerializedPropertyType.String)
            {
                var field = new LayerField(property.displayName, LayerNameToIndex(property.stringValue));
                field.showMixedValue = property.hasMultipleDifferentValues;
                field.RegisterValueChangedCallback(evt =>
                {
                    property.serializedObject.Update();
                    property.stringValue = LayerMask.LayerToName(evt.newValue);
                    property.serializedObject.ApplyModifiedProperties();
                });

                field.TrackPropertyValue(property, trackedProperty =>
                {
                    field.showMixedValue = trackedProperty.hasMultipleDifferentValues;
                    field.SetValueWithoutNotify(LayerNameToIndex(trackedProperty.stringValue));
                });

                return field;
            }

            return new HelpBox(ErrorMessage, HelpBoxMessageType.Error);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer &&
                property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, ErrorMessage, MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();

            int layer = property.propertyType == SerializedPropertyType.Integer
                ? ClampLayer(property.intValue)
                : LayerNameToIndex(property.stringValue);
            int newLayer = EditorGUI.LayerField(position, label, layer);

            if (EditorGUI.EndChangeCheck())
            {
                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    property.intValue = newLayer;
                }
                else
                {
                    property.stringValue = LayerMask.LayerToName(newLayer);
                }
            }

            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.propertyType == SerializedPropertyType.Integer ||
                property.propertyType == SerializedPropertyType.String
                    ? EditorGUIUtility.singleLineHeight
                    : EditorGUIUtility.singleLineHeight * 2f;
        }

        private static int LayerNameToIndex(string layerName)
        {
            return ClampLayer(LayerMask.NameToLayer(layerName));
        }

        private static int ClampLayer(int layer)
        {
            return Mathf.Clamp(layer, 0, 31);
        }
    }
}
