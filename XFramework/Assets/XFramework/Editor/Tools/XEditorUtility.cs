using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace XFramework.Editor
{
    public static class XEditorUtility
    {
        public static void SerializableObj(object obj)
        {
            Type type = obj.GetType();
            FieldInfo[] fieldInfos = type.GetFields();

            GUI.backgroundColor = new Color32(0, 170, 255, 30);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;
            foreach (var field in fieldInfos)
            {
                switch (field.FieldType.ToString())
                {
                    case "System.Int32":
                        field.SetValue(obj, EditorGUILayout.IntField(field.Name.AddSpace(), (int)field.GetValue(obj)));
                        Debug.Log((int)field.GetValue(obj));
                        break;
                    case "System.Single":
                        field.SetValue(obj, EditorGUILayout.FloatField(field.Name.AddSpace(), (float)field.GetValue(obj)));
                        break;
                    case "System.Double":
                        field.SetValue(obj, EditorGUILayout.DoubleField(field.Name.AddSpace(), (double)field.GetValue(obj)));
                        break;
                    case "System.Boolean":
                        field.SetValue(obj, EditorGUILayout.Toggle(field.Name.AddSpace(), (bool)field.GetValue(obj)));
                        break;
                    case "System.String":
                        field.SetValue(obj, EditorGUILayout.TextField(field.Name.AddSpace(), (string)field.GetValue(obj)));
                        break;
                    case "System.Enum":
                        field.SetValue(obj, EditorGUILayout.EnumPopup(field.Name.AddSpace(), (Enum)field.GetValue(obj)));
                        break;
                    case "UnityEngine.Transform":
                        field.SetValue(obj, EditorGUILayout.ObjectField(field.Name.AddSpace(), (Transform)field.GetValue(obj), typeof(Transform), true) as Transform);
                        break;
                    case "UnityEngine.Vector3":
                        field.SetValue(obj, EditorGUILayout.Vector3Field(field.Name.AddSpace(), (Vector3)field.GetValue(obj)));
                        break;
                    case "UnityEngine.Vector2":
                        field.SetValue(obj, EditorGUILayout.Vector2Field(field.Name, (Vector3)field.GetValue(obj)));
                        break;
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}