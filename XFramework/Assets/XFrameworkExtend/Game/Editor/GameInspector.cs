using UnityEditor;
using UnityEngine;
using XFramework;
using System;
using System.IO;
using System.Reflection;
using XFramework.Editor;
using System.Collections.Generic;

[CustomEditor(typeof(Game))]
public class GameInspector : Editor
{
    private string[] typeNames = null;
    private int entranceProcedureIndex = 0;

    private Game game;
    public ProcedureBase startPrcedureTemplate;
    private string savePath;

    /// <summary>
    /// 根据上一次操作初始化流程参数
    /// </summary>
    private void Awake()
    {
        entranceProcedureIndex = EditorPrefs.GetInt("index", 0);

        typeNames = GetSonNames();
        if (typeNames.Length == 0)
            return;

        if (entranceProcedureIndex > typeNames.Length - 1)
            entranceProcedureIndex = 0;

        game = target as Game;
        game.typeName = typeNames[entranceProcedureIndex];
        game.startProcedure = Utility.Reflection.CreateInstance<ProcedureBase>(GetType(typeNames[entranceProcedureIndex]));

        savePath = Application.persistentDataPath + "/" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "Procedure";

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
        savePath += "/";
    }

    public override void OnInspectorGUI()
    {
        typeNames = GetSonNames();
        if (typeNames.Length == 0)
            return;

        if (entranceProcedureIndex > typeNames.Length - 1)
            entranceProcedureIndex = 0;

        GUI.backgroundColor = new Color32(0, 170, 255, 30);
        GUILayout.BeginVertical("Box");
        GUI.backgroundColor = Color.white;

        int lastIndex = entranceProcedureIndex;
        entranceProcedureIndex = EditorGUILayout.Popup("Entrance Procedure", entranceProcedureIndex, typeNames);

        GUILayout.EndVertical();

        if (lastIndex != entranceProcedureIndex)
        {
            game.typeName = typeNames[entranceProcedureIndex];
            game.startProcedure = Utility.Reflection.CreateInstance<ProcedureBase>(GetType(typeNames[entranceProcedureIndex]));
        }

        base.OnInspectorGUI();
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
            if (type.FullName == name)
                return type;
        }
        return null;
    }

    private string[] GetSonNames()
    {
        List<string> typeNames = new List<string>();
        Type typeBase = typeof(ProcedureBase);
        Assembly assembly = Assembly.Load("Assembly-CSharp");

        if (assembly == null)
        {
            return new string[0];
        }

        Type[] types = assembly.GetTypes();
        foreach (Type type in types)
        {
            if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeBase) && type.GetCustomAttribute<HideInEditor>() == null)
            {
                typeNames.Add(type.FullName);
            }
        }
        typeNames.Sort();
        return typeNames.ToArray();
    }
}