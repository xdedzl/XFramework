// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-10-10 09:51:30
// 版本： V 1.0
// ==========================================
using UnityEditor;
using UnityEngine;

public class AutoImporterWindow : EditorWindow
{

    private void OnGUI()
    {
        GUILayout.Space(10);
        TextureImporter();
        ModelImporter();
    }

    public static TextureImporterType textureType;
    public static TextureMaxSize textureMaxSize = TextureMaxSize._1024;
    public static bool textureIsOn;
    private Vector2 _scroll;

    private void TextureImporter()
    {
        GUILayout.BeginVertical();
        GUI.skin.label.alignment = TextAnchor.MiddleLeft;
        GUILayout.Label("Texture Importer Setting");
        textureIsOn = GUILayout.Toggle(textureIsOn,"IsOn");
        if (textureIsOn)
        {
            // 导入图片类型设置
            textureType = (TextureImporterType)EditorGUILayout.EnumPopup("TextureType", textureType);
            // 导入图片大小设置
            textureMaxSize = (TextureMaxSize)EditorGUILayout.EnumPopup("TextureMaxSize", textureMaxSize);
        }

        GUILayout.EndVertical();
        GUILayout.Space(10);
    }

    public static ModelImporterMaterialLocation location;
    public static bool modelIsOn;

    private void ModelImporter()
    {
        GUILayout.BeginVertical();
        GUI.skin.label.alignment = TextAnchor.MiddleLeft;
        GUILayout.Label("Model Importer Setting");

        modelIsOn = GUILayout.Toggle(modelIsOn, "IsOn");
        if (modelIsOn)
        {
            // 模型材质设置
            location = (ModelImporterMaterialLocation)EditorGUILayout.EnumPopup("location", location);
        }

        GUILayout.EndVertical();
    }

    [MenuItem("XFramework/AssetImporter/AutoImporterWindow")]
    public static void ImporterSet()
    {
        EditorWindow.GetWindow(typeof(AutoImporterWindow));
    }
}

/// <summary>
/// 导入资源自动设置
/// 相关函数参阅AssetPostprocessor 官方API
/// </summary>
public class AutoSetImpotor : AssetPostprocessor
{
    // 贴图导入自动设置;
    private void OnPreprocessTexture()
    {
        if (AutoImporterWindow.textureIsOn)
        {
            TextureImporter textureImporter = (TextureImporter)assetImporter;

            textureImporter.textureType = AutoImporterWindow.textureType;
            textureImporter.maxTextureSize = (int)AutoImporterWindow.textureMaxSize;
        }
    }

    private void OnPreprocessModel()
    {
        if (AutoImporterWindow.modelIsOn)
        {
            ModelImporter importer = assetImporter as ModelImporter;
            // 导入时材质和模型分离
            importer.materialLocation = AutoImporterWindow.location;
        }
    }
}

public enum TextureMaxSize
{
    _128 = 128,
    _256 = 256,
    _512 = 512,
    _1024 = 1024,
    _2048 = 2048,
}