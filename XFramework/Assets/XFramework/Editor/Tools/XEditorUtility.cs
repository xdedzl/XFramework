using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace XFramework.Editor
{
    /// <summary>
    /// 编辑器工具方法
    /// </summary>
    public static class XEditorUtility
    {
        public static void SerializableObj(object obj)
        {
            if (obj == null)
                return;
            Type type = obj.GetType();
            FieldInfo[] fieldInfos = type.GetFields();

            GUI.backgroundColor = new Color32(0, 170, 255, 30);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;
            foreach (var field in fieldInfos)
            {
                switch (field.FieldType.Name)
                {
                    case "Int32":
                        field.SetValue(obj, EditorGUILayout.IntField(field.Name.AddSpace(), (int)field.GetValue(obj)));
                        break;
                    case "Single":
                        field.SetValue(obj, EditorGUILayout.FloatField(field.Name.AddSpace(), (float)field.GetValue(obj)));
                        break;
                    case "Double":
                        field.SetValue(obj, EditorGUILayout.DoubleField(field.Name.AddSpace(), (double)field.GetValue(obj)));
                        break;
                    case "Boolean":
                        field.SetValue(obj, EditorGUILayout.Toggle(field.Name.AddSpace(), (bool)field.GetValue(obj)));
                        break;
                    case "String":
                        field.SetValue(obj, EditorGUILayout.TextField(field.Name.AddSpace(), (string)field.GetValue(obj)));
                        break;
                    case "Enum":
                        field.SetValue(obj, EditorGUILayout.EnumPopup(field.Name.AddSpace(), (Enum)field.GetValue(obj)));
                        break;
                    case "Vector3":
                        field.SetValue(obj, EditorGUILayout.Vector3Field(field.Name.AddSpace(), (Vector3)field.GetValue(obj)));
                        break;
                    case "Vector2":
                        field.SetValue(obj, EditorGUILayout.Vector2Field(field.Name, (Vector3)field.GetValue(obj)));
                        break;
                    case "Color":
                    case "Color32":
                        field.SetValue(obj, EditorGUILayout.ColorField(field.Name, (Color)field.GetValue(obj)));
                        break;
                    case "Transform":
                        field.SetValue(obj, EditorGUILayout.ObjectField(field.Name.AddSpace(), (Transform)field.GetValue(obj), typeof(Transform), true) as Transform);
                        break;
                    case "GameObject":
                        field.SetValue(obj, EditorGUILayout.ObjectField(field.Name.AddSpace(), (GameObject)field.GetValue(obj), typeof(GameObject), true) as GameObject);
                        break;
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}