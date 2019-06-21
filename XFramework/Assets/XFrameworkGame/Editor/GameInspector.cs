using System;
using System.IO;
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
    private Type currentType;
    private ProcedureBase startPrcedureTemplate;
    private ProtocolBytes p = new ProtocolBytes();
    private string savePath;

    private void Awake()
    {
        entranceProcedureIndex = EditorPrefs.GetInt("index", 0);

        typeNames = typeof(ProcedureBase).GetSonNames();
        if (typeNames.Length == 0)
            return;

        if (entranceProcedureIndex > typeNames.Length - 1)
            entranceProcedureIndex = 0;
        
        game = target as Game;
        game.TypeName = typeNames[entranceProcedureIndex];

        startPrcedureTemplate = Utility.Reflection.CreateInstance<ProcedureBase>(GetType(typeNames[entranceProcedureIndex]));
        game.DeSerialize(startPrcedureTemplate);

        savePath = Application.persistentDataPath + "/" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "Procedure";

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
        savePath += "/";
    }

    public override void OnInspectorGUI()
    {
        typeNames = typeof(ProcedureBase).GetSonNames();
        if (typeNames.Length == 0)
            return;
        
        if (entranceProcedureIndex > typeNames.Length - 1)
            entranceProcedureIndex = 0;

        GUI.backgroundColor = new Color32(0, 170, 255, 30);
        GUILayout.BeginVertical("Box");
        GUI.backgroundColor = Color.white;

        int lastIndex = entranceProcedureIndex;
        entranceProcedureIndex = EditorGUILayout.Popup("Entrance Procedure", entranceProcedureIndex, typeNames);

        if(lastIndex != entranceProcedureIndex)
        {
            game.TypeName = typeNames[entranceProcedureIndex];
            startPrcedureTemplate = Utility.Reflection.CreateInstance<ProcedureBase>(GetType(typeNames[entranceProcedureIndex]));
            currentType = GetType(typeNames[entranceProcedureIndex]);
        }

        currentType = currentType ?? GetType(typeNames[entranceProcedureIndex]);
        startPrcedureTemplate = startPrcedureTemplate ?? Utility.Reflection.CreateInstance<ProcedureBase>(GetType(typeNames[entranceProcedureIndex]));

        if (!Application.isPlaying)
        {
            XEditorUtility.SerializableObj(startPrcedureTemplate);
            Serialize();
        }
        else
        {
            XEditorUtility.SerializableObj(Game.ProcedureModule.GetCurrentProcedure());
        }

        GUILayout.EndVertical();
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

    private void Serialize()
    {
        p.Clear();
        p.AddString(currentType.Name);
        foreach (var field in currentType.GetFields())
        {
            var arg = currentType.GetField(field.Name).GetValue(startPrcedureTemplate);
            switch (field.FieldType.ToString())
            {
                case "System.Int32":
                    p.AddInt32((int)arg);
                    break;
                case "System.Single":
                    p.AddFloat((float)arg);
                    break;
                case "System.Double":
                    p.AddDouble((double)arg);
                    break;
                case "System.Boolean":
                    p.AddBoolen((bool)arg);
                    break;
                case "System.String":
                    arg = arg ?? "";
                    p.AddString((string)arg);
                    break;
                case "System.Enum":
                    p.AddInt32(Convert.ToInt32(arg));
                    break;
                case "UnityEngine.Vector3":
                    p.AddVector3((Vector3)arg);
                    break;
                case "UnityEngine.Vector2":
                    p.AddVector2((Vector2)arg);
                    break;
                case "UnityEngine.GameObject":
                    GameObject obj = (GameObject)arg;
                    if (obj)
                    {
                        if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj)))
                        {
                            Debug.LogError("暂不支持给流程添加Assets下的GameObject,请直接拖拽场景中的GameObject");
                            continue;
                        }
                        Transform trans = obj.transform;
                        string path = obj.name;
                        while (trans.parent != null)
                        {
                            trans = trans.parent;
                            path = trans.name + "/" + path;
                        }
                        p.AddString(path);
                    }
                    else
                    {
                        p.AddString("");
                    }
                    break;
                case "UnityEngine.Transform":
                    Transform transform = (Transform)arg;
                    if (transform)
                    {
                        string path = transform.name;
                        while (transform.parent != null)
                        {
                            transform = transform.parent;
                            path = transform.name + "/" + path;
                        }
                        p.AddString(path);
                    }
                    else
                    {
                        p.AddString("");
                    }
                    break;
                default:
                    Debug.LogError("流程暂不支持在面板上修改" + field.FieldType.Name);
                    break;
            }
        }

        File.WriteAllBytes(savePath + currentType.Name, p.Encode());
    }
}