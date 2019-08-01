using UnityEditor;
using UnityEditor.UI;

namespace XFramework.UI.Editor
{
    [CustomEditor(typeof(LongOrDoubleBtn), true)]
    [CanEditMultipleObjects]
    public class LongPressBtnEditor : ButtonEditor
    {
        SerializedProperty m_maxTime;
        SerializedProperty m_isLongPressTrigger;
        SerializedProperty m_OnLongClick;
        SerializedProperty m_OnDoubleClick;
        

        protected override void OnEnable()
        {
            base.OnEnable();
            m_maxTime = serializedObject.FindProperty("maxTime");
            m_isLongPressTrigger = serializedObject.FindProperty("isLongPressTrigger");
            m_OnLongClick = serializedObject.FindProperty("onLongClick");
            m_OnDoubleClick = serializedObject.FindProperty("onDoubleClick");
        }

        public override void OnInspectorGUI()
        {
            bool temp = ((LongOrDoubleBtn)target).isLongPressTrigger;
            EditorGUILayout.PropertyField(m_isLongPressTrigger);
            if (temp)
            {
                EditorGUILayout.PropertyField(m_maxTime);
                
            }

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();

            if (temp)
            {
                EditorGUILayout.PropertyField(m_OnLongClick);
            }
            else
            {
                EditorGUILayout.PropertyField(m_OnDoubleClick);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}