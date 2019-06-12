using UnityEditor;
using UnityEngine;
using XFramework;

[CustomEditor(typeof(Game))]
public class GameInspector : Editor
{
    private string[] typeNames = null;
    private int entranceProcedureIndex = 0;

    private void Awake()
    {
        entranceProcedureIndex = EditorPrefs.GetInt("index", 0);
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

        entranceProcedureIndex = EditorGUILayout.Popup("Entrance Procedure", entranceProcedureIndex, typeNames);

        (target as Game).TypeName = typeNames[entranceProcedureIndex];
        
        GUILayout.EndVertical();
    }

    private void OnDestroy()
    {
        EditorPrefs.SetInt("index", entranceProcedureIndex);
    } 
}