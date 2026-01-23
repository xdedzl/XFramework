using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using XFramework.Resource;

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

    public interface IDataHasKey<out TKey> : IData
    {
        public TKey PrimaryKey { get; }
    }
    
    public static class DataManager
    {
        private static readonly Dictionary<Type, Type> S_DataTypeMap = new();
        
        private static readonly Dictionary<Type, IList> S_DataListMap = new();
        private static readonly Dictionary<Type, IDictionary> S_DataDictMap = new();
        
        static DataManager()
        {
            var types = Utility.Reflection.GetGenericTypes(typeof(DataScriptableObject<>), 1,"Assembly-CSharp", "XFrameworkRuntime");
            
            var map = new Dictionary<Type, Type>();

            foreach (var soType in types)
            {
                var targetDataTypeAttr = soType.GetCustomAttribute<TargetDataType>(false);
                if (targetDataTypeAttr?.targetType != null)
                {
                    var targetDataType = targetDataTypeAttr.targetType;
                    if (!map.ContainsValue(targetDataType))
                    {
                        S_DataTypeMap[targetDataType] = soType;
                    }
                    else
                    {
                        var existSoType = map.First(kvp => kvp.Value == targetDataType).Key;
                        Debug.LogError($"Data type {targetDataType.FullName} has repeated ScriptObject types, {existSoType.FullName}, {soType.FullName}");
                    }
                }
                else
                {
                    Debug.LogError($"Data type {soType.FullName} missing TargetDataAttribute");
                }
            }
        }
        
        public static IReadOnlyList<T> LoadData<T>() where T : IData
        {
            var dataType = typeof(T);
            if (!S_DataListMap.ContainsKey(dataType))
            {
                if (!S_DataTypeMap.ContainsKey(dataType))
                {
                    throw new Exception($"Data type {dataType.FullName} not registered");
                }
                var soType = S_DataTypeMap[dataType];
                var dataResourcePathAttr = soType.GetCustomAttribute<DataResourcePath>(false);
                if (dataResourcePathAttr == null)
                {
                    throw new Exception($"Data type {soType.FullName} missing DataResourcePath attribute");
                }
                var so = ResourceManager.Instance.Load<DataScriptableObject<T>>(dataResourcePathAttr.path);
                S_DataListMap[dataType] = so.items;
            }
            
            var soItems = S_DataListMap[dataType];
            return (IReadOnlyList<T>)soItems;
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
                    if (!dict.ContainsKey(item.PrimaryKey))
                    {
                        dict[item.PrimaryKey] = item;
                    }
                    else
                    {
                        throw new Exception($"Data type {typeof(TKey).FullName} has repeated primary key {item.PrimaryKey}");
                    }
                }
                S_DataDictMap[dataType] = dict;
            }
            
            return (IReadOnlyDictionary<TKey, TValue>)S_DataDictMap[dataType];
        }
    }
}

