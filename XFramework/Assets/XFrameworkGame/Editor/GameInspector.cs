using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XFramework;
using XFramework.Editor;

[CustomEditor(typeof(Game))]
public class GameInspector : Editor
{
    private string[] typeNames = null;
    private int entranceProcedureIndex = 0;

    private Game game;

    private SerializedProperty startPrcedureTemplate;

    private void Awake()
    {
        startPrcedureTemplate = serializedObject.FindProperty("startPrcedureTemplate");
        Debug.Log(startPrcedureTemplate);

        typeNames = typeof(ProcedureBase).GetSonNames();
        if (typeNames.Length == 0)
            return;

        entranceProcedureIndex = EditorPrefs.GetInt("index", 0);
        if (entranceProcedureIndex > typeNames.Length - 1)
            entranceProcedureIndex = 0;
        
        game = target as Game;
        game.TypeName = typeNames[entranceProcedureIndex];
        game.startPrcedureTemplate = game.startPrcedureTemplate??Utility.Reflection.CreateInstance<GraphicsTest>(GetType(typeNames[entranceProcedureIndex]));

        Debug.Log(game.GetHashCode());
    }

    public override void OnInspectorGUI()
    {
        typeNames = typeof(ProcedureBase).GetSonNames();
        if (typeNames.Length == 0)
            return;
        GUI.backgroundColor = new Color32(0, 170, 255, 30);
        GUILayout.BeginVertical("Box");
        GUI.backgroundColor = Color.white;

        if (entranceProcedureIndex > typeNames.Length - 1)
            entranceProcedureIndex = 0;

        int lastIndex = entranceProcedureIndex;
        entranceProcedureIndex = EditorGUILayout.Popup("Entrance Procedure", entranceProcedureIndex, typeNames);

        if(lastIndex != entranceProcedureIndex)
        {
            game.TypeName = typeNames[entranceProcedureIndex];
        }

        game.startPrcedureTemplate = game.startPrcedureTemplate ?? Utility.Reflection.CreateInstance<GraphicsTest>(GetType(typeNames[entranceProcedureIndex]));

        if (!Application.isPlaying)
        {
            //XEditorUtility.SerializableObj(game.startPrcedureTemplate);
            EditorGUILayout.PropertyField(startPrcedureTemplate, true);
        }
        else
        {
            XEditorUtility.SerializableObj(Game.ProcedureModule.GetCurrentProcedure());
        }

        GUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }

    private void OnDestroy()
    {
        EditorPrefs.SetInt("index", entranceProcedureIndex);
    }

    // Type.GetType(string)在编辑器下貌似有些问题
    private Type GetType(string name)
    {
        Assembly assembly = Assembly.Load("Assembly-CSharp");
        Type[] allType = assembly.GetTypes();
        foreach (Type type in allType)
        {
            if (type.Name == name)
                return type;
        }
        return null;
    } 
}