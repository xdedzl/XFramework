using UnityEngine;
using System;
using System.Reflection;
using XFramework;
using XFramework.Resource;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TargetAssetPathAttribute : Attribute
{
    public string AssetPath { get; }
    public TargetAssetPathAttribute(string assetPath)
    {
        AssetPath = assetPath;
    }
}

public abstract class ScriptableObjectSingleton<T> : ScriptableObject where T : ScriptableObject
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            } 
            
            var attr = typeof(T).GetCustomAttribute<TargetAssetPathAttribute>();
            var path = attr.AssetPath;
            _instance = ResourceManager.Instance.Load<T>(path);
            if (_instance != null)
            {
                return _instance;
            } 
            
            _instance = CreateInstance<T>();
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.CreateAsset(_instance, path);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            return _instance;
        }
    }
}


#if UNITY_EDITOR

public static class ScriptableSingletonAssetCreator
{
    [UnityEditor.MenuItem("Tools/ScriptableSingleton/Auto Create All Singleton SOs")]
    public static void CreateAllSingletonAssets()
    {
        var soTypes = Utility.Reflection.GetGenericTypes(typeof(ScriptableObjectSingleton<>), 1, "Assembly-CSharp", "XFrameworkRuntime");
 
        foreach (var type in soTypes)
        {
            var attr = type.GetCustomAttribute<TargetAssetPathAttribute>();
            string path = attr != null ? attr.AssetPath : $"Assets/Resources/{type.Name}.asset";
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                var instance = ScriptableObject.CreateInstance(type);
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                UnityEditor.AssetDatabase.CreateAsset(instance, path);
                UnityEngine.Debug.Log($"Created SO: {type.Name} at {path}");
            }
        }
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
    }
 
    private static bool IsSubclassOfRawGeneric(Type toCheck, Type generic)
    {
        while (toCheck != null && toCheck != typeof(object))
        {
            var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
            if (generic == cur)
                return true;
            toCheck = toCheck.BaseType;
        }
        return false;
    }
}
#endif
