using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using XFramework.Resource;
using XFramework;

namespace XFramework.Data
{
    public class DataResourcePath : Attribute
    {
        public string path;
        
        public DataResourcePath(string path)
        {
            this.path = path;
        }
    }
    
    public class TargetDataType : Attribute
    {
        public Type targetType;
        
        public TargetDataType(Type targetType)
        {
            this.targetType = targetType;
        }
    }
    
    public interface IData
    {
        
    }

    public interface IDataHasAlias : IData
    {
        public string Alias { get; }
    }

    public interface IDataHasKey<out TKey> : IData
    {
        public TKey PrimaryKey { get; }
    }

    public interface IDataHasAlias<out TKey> : IDataHasKey<TKey>, IDataHasAlias
    {
    }
    
    public partial class XDataTable
    {
        private static readonly Dictionary<Type, Type> S_DataToTableType = new();
        
        private static readonly Dictionary<Type, IList> S_DataListMap = new();
        private static readonly Dictionary<Type, IDictionary> S_DataDictMap = new();
        private static readonly Dictionary<Type, IDictionary> S_DataAliasDictMap = new();
        
        static XDataTable()
        {
            var types = Utility.Reflection.GetGenericTypes(typeof(XDataTable<>), 1,"Assembly-CSharp", "XFrameworkRuntime");
            
            var map = new Dictionary<Type, Type>();

            foreach (var tableType in types)
            {
                var targetDataTypeAttr = tableType.GetCustomAttribute<TargetDataType>(false);
                if (targetDataTypeAttr?.targetType != null)
                {
                    var targetDataType = targetDataTypeAttr.targetType;
                    if (!map.ContainsValue(targetDataType))
                    {
                        S_DataToTableType[targetDataType] = tableType;
                    }
                    else
                    {
                        var existtableType = map.First(kvp => kvp.Value == targetDataType).Key;
                        Debug.LogError($"Data type {targetDataType.FullName} has repeated ScriptObject types, {existtableType.FullName}, {tableType.FullName}");
                    }
                }
                else
                {
                    Debug.LogError($"Data type {tableType.FullName} missing TargetDataAttribute");
                }
            }
        }
        
        public static IReadOnlyList<T> LoadData<T>() where T : IData
        {
            var dataType = typeof(T);
            if (!S_DataListMap.ContainsKey(dataType))
            {
                if (!S_DataToTableType.TryGetValue(dataType, out var tableType))
                {
                    throw new Exception($"Data type {dataType.FullName} not registered");
                }

                var dataResourcePathAttr = tableType.GetCustomAttribute<DataResourcePath>(false);
                if (dataResourcePathAttr == null)
                {
                    throw new Exception($"Data type {tableType.FullName} missing DataResourcePath attribute");
                }
                var textAsset = ResourceManager.Instance.Load<TextAsset>(dataResourcePathAttr.path);
                if (textAsset == null)
                {
                    throw new Exception($"Data text asset missing at path: {dataResourcePathAttr.path}");
                }
                var so = textAsset.ToXTextAsset<XDataTable<T>>(tableType);
                S_DataListMap[dataType] = so.items;
            }
            
            var soItems = S_DataListMap[dataType];
            return (IReadOnlyList<T>)soItems;
        }

        public static IReadOnlyDictionary<int, TValue> LoadDictData<TValue>() where TValue : IDataHasKey<int>
        {
            return LoadDictData<int, TValue>();
        }

        public static IReadOnlyDictionary<string, TValue> LoadDictDataStr<TValue>() where TValue : IDataHasKey<string>
        {
            return LoadDictData<string, TValue>();
        }

        public static IReadOnlyDictionary<TKey, TValue> LoadDictData<TKey, TValue>() where TValue : IDataHasKey<TKey>
        {
            var dataType = typeof(TValue);
            if (!S_DataDictMap.ContainsKey(dataType))
            {
                var list = LoadData<TValue>();
                var dict = new Dictionary<TKey, TValue>();
                foreach (var item in list)
                {
                    if (!dict.TryAdd(item.PrimaryKey, item))
                    {
                        throw new Exception($"Data type {dataType.FullName} has repeated primary key {item.PrimaryKey}");
                    }
                }
                S_DataDictMap[dataType] = dict;
            }
            
            return (IReadOnlyDictionary<TKey, TValue>)S_DataDictMap[dataType];
        }

        public static IReadOnlyDictionary<string, TValue> LoadAliasDictData<TValue>() where TValue : IDataHasAlias
        {
            var dataType = typeof(TValue);
            if (S_DataAliasDictMap.TryGetValue(dataType, out var value))
            {
                return (IReadOnlyDictionary<string, TValue>)value;
            }
            
            var list = LoadData<TValue>();
            var dict = new Dictionary<string, TValue>();
            foreach (var item in list)
            {
                if (string.IsNullOrEmpty(item.Alias))
                {
                    continue;
                }
                if (!dict.TryAdd(item.Alias, item))
                {
                    throw new Exception($"Data type {dataType.FullName} has repeated alias {item.Alias}");
                }
            }
            S_DataAliasDictMap[dataType] = dict;
            return dict;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("XFramework/Data/Create Data Assets")]
        public static void CreateMissingDataAssets()
        {
            XFramework.Json.XJson.SetUnityDefaultSetting();
            var types = Utility.Reflection.GetGenericTypes(typeof(XDataTable<>), 1, "Assembly-CSharp", "XFrameworkRuntime");
            var createdCount = 0;
            foreach (var tableType in types)
            {
                var dataResourcePathAttr = tableType.GetCustomAttribute<DataResourcePath>(false);
                if (dataResourcePathAttr == null || string.IsNullOrEmpty(dataResourcePathAttr.path))
                {
                    continue;
                }
                
                var path = dataResourcePathAttr.path;
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null)
                {
                    continue;
                }
                
                // Create directory if not exists
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    UnityEditor.AssetDatabase.Refresh();
                }

                var instance = Activator.CreateInstance(tableType);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(instance, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(path, json);
                UnityEditor.AssetDatabase.Refresh();
                        
                asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                Debug.Log($"[DataManager] Created missing JSON asset at {path} for type {tableType.Name}", asset);
                createdCount++;
            }
            
            if (createdCount > 0)
            {
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
                Debug.Log($"[DataManager] Total created {createdCount} assets.");
            }
            else
            {
                Debug.Log("[DataManager] All data assets already exist.");
            }
        }
#endif
    }
}

