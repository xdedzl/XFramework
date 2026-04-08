using UnityEditor;
using UnityEngine;
using XFramework;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Object = UnityEngine.Object;

[CustomEditor(typeof(GameBase), true)]
public class GameInspector : Editor
{
    private string[] typeNames = null;      // full names (for storage)
    private string[] displayNames = null;   // short names (for popup display)
    private int entranceProcedureIndex = 0;
    
    private string savePath;

    private GameBase gameInstance
    {
        get
        {
            return target as GameBase;
        }
    }

    /// <summary>
    /// 根据上一次操作初始化流程参数
    /// </summary>
    private void Awake()
    {
        entranceProcedureIndex = EditorPrefs.GetInt("index", 0);

        RefreshTypeNames();
        if (typeNames.Length == 0)
            return;

        UpdateGame();

        if (entranceProcedureIndex > typeNames.Length - 1)
            entranceProcedureIndex = 0;

        gameInstance.startTypeName = typeNames[entranceProcedureIndex];
        gameInstance.startProcedure = Utility.Reflection.CreateInstance<ProcedureBase>(GetType(typeNames[entranceProcedureIndex]));

        savePath = Application.persistentDataPath + "/" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "Procedure";

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
        savePath += "/";
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying)
            Repaint();
    }

    public override void OnInspectorGUI()
    {
        RefreshTypeNames();
        if (typeNames.Length == 0)
        {
            EditorGUILayout.HelpBox("未找到任何 ProcedureBase 子类", MessageType.Warning);
            return;
        }

        if (entranceProcedureIndex > typeNames.Length - 1)
            entranceProcedureIndex = 0;

        // ── Entrance Procedure ──────────────────────────────────
        GUI.backgroundColor = new Color32(0, 170, 255, 30);
        GUILayout.BeginVertical("Box");
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField("Entrance Procedure", EditorStyles.boldLabel);

        int lastIndex = entranceProcedureIndex;
        entranceProcedureIndex = EditorGUILayout.Popup("Start Procedure", entranceProcedureIndex, displayNames);

        // script 快速跳转
        string fullClassName = gameInstance.startTypeName;
        // FindAssets 按文件名匹配，需要用简单类名（去掉命名空间）
        string simpleClassName = fullClassName.Contains('.') ? fullClassName[(fullClassName.LastIndexOf('.') + 1)..] : fullClassName;
        string[] guids = AssetDatabase.FindAssets(simpleClassName + " t:MonoScript");
        MonoScript script = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript _script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (_script != null && _script.GetClass() != null && _script.GetClass().FullName == fullClassName)
            {
                script = _script;
                break;
            }
        }
        EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);

        GUILayout.EndVertical();

        if (lastIndex != entranceProcedureIndex)
        {
            UpdateGame();
        }

        // ── Runtime Status ──────────────────────────────────────
        if (Application.isPlaying && ProcedureManager.IsValid)
        {
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color32(0, 255, 100, 30);
            GUILayout.BeginVertical("Box");
            GUI.backgroundColor = Color.white;

            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);

            var current = ProcedureManager.Instance.CurrentProcedure;
            if (current != null)
            {
                var type = current.GetType();
                EditorGUILayout.LabelField("Current Procedure", type.Name, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Full Name", type.FullName, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;

                var sub = ProcedureManager.Instance.CurrenSubProcedure;
                if (sub != null)
                {
                    EditorGUILayout.LabelField("Sub Procedure", sub.GetType().Name, EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Full Name", sub.GetType().FullName, EditorStyles.miniLabel);
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField("Sub Procedure", "None");
                }
            }
            else
            {
                EditorGUILayout.LabelField("Current Procedure", "None");
            }

            GUILayout.EndVertical();
        }

        base.OnInspectorGUI();
    }

    private void UpdateGame()
    {
        if (!gameInstance)
        {
            return;
        }
        gameInstance.startTypeName = typeNames[entranceProcedureIndex];
        gameInstance.startProcedure = Utility.Reflection.CreateInstance<ProcedureBase>(GetType(typeNames[entranceProcedureIndex]));
    }

    private void RefreshTypeNames()
    {
        var fullNames = new List<string>();
        var shortNames = new List<string>();
        Type typeBase = typeof(ProcedureBase);
        Assembly assembly;
        try { assembly = Assembly.Load("Assembly-CSharp"); }
        catch { typeNames = new string[0]; displayNames = new string[0]; return; }

        if (assembly == null) { typeNames = new string[0]; displayNames = new string[0]; return; }

        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeBase) && type.GetCustomAttribute<HideInEditor>() == null)
            {
                fullNames.Add(type.FullName);
                // namespace を / 区切りに変換して表示 (例: GeoPet/LaunchProcedure)
                shortNames.Add(string.IsNullOrEmpty(type.Namespace)
                    ? type.Name
                    : type.Namespace.Replace(".", "/") + "/" + type.Name);
            }
        }
        // sort by full name, keep both lists in sync
        var paired = new List<(string full, string display)>();
        for (int i = 0; i < fullNames.Count; i++)
            paired.Add((fullNames[i], shortNames[i]));
        paired.Sort((a, b) => string.Compare(a.full, b.full, StringComparison.Ordinal));

        typeNames = new string[paired.Count];
        displayNames = new string[paired.Count];
        for (int i = 0; i < paired.Count; i++)
        {
            typeNames[i] = paired[i].full;
            displayNames[i] = paired[i].display;
        }
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
}
