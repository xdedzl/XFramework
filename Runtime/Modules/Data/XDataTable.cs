using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.Data
{
    abstract partial class XDataTable : XTextAsset
    {
        private static readonly Dictionary<Type, Type> S_DataToTableType = new();
        private static readonly Dictionary<Type, XDataTable> S_DataTableMap = new();
        
        private static readonly Dictionary<Type, IList> S_DataListMap = new();
        private static readonly Dictionary<Type, IDictionary> S_DataDictMap = new();
        private static readonly Dictionary<Type, IDictionary> S_DataAliasDictMap = new();
        
        static XDataTable()
        {
            var types = Utility.Reflection.GetGenericTypes(typeof(XDataTable<>), 5,"Assembly-CSharp", "XFrameworkRuntime");
            
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
#if UNITY_EDITOR
        // 编辑器下应对关闭 Reload Domain
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache()
        {
            S_DataTableMap.Clear();
            S_DataListMap.Clear();
            S_DataDictMap.Clear();
            S_DataAliasDictMap.Clear();
            S_UnionDataListMap.Clear();
            S_UnionDataDictMap.Clear();
            S_UnionDataAliasDictMap.Clear();
        }
#endif

        protected static IReadOnlyList<T> LoadData<T>() where T : IData
        {
            var dataType = typeof(T);
            if (!S_DataListMap.ContainsKey(dataType))
            {
                if (!S_DataToTableType.TryGetValue(dataType, out var tableType))
                {
                    throw new Exception($"Data type {dataType.FullName} not registered");
                }

                if (LoadTable(tableType) is not XDataTable<T> so)
                {
                    throw new Exception($"Data table type {tableType.FullName} cannot convert to {typeof(XDataTable<T>).FullName}");
                }
                S_DataListMap[dataType] = so.items;
            }
            
            var soItems = S_DataListMap[dataType];
            return (IReadOnlyList<T>)soItems;
        }

        public static TTable LoadTable<TTable>() where TTable : XDataTable
        {
            return (TTable)LoadTable(typeof(TTable));
        }

        protected virtual void AfterLoad()
        {
        }

        private static XDataTable LoadTable(Type tableType)
        {
            if (S_DataTableMap.TryGetValue(tableType, out var table))
            {
                return table;
            }

            var dataResourcePathAttr = tableType.GetCustomAttribute<DataResourcePath>(false);
            if (dataResourcePathAttr == null)
            {
                throw new Exception($"Data type {tableType.FullName} missing DataResourcePath attribute");
            }

            string[] paths = dataResourcePathAttr.GetPaths()
                .Where(dataPath => !string.IsNullOrWhiteSpace(dataPath))
                .ToArray();
            if (paths.Length == 0)
            {
                throw new Exception($"Data type {tableType.FullName} DataResourcePath has no valid path");
            }

            table = LoadAndMergeTable(tableType, paths);
            ValidateMergedTable(tableType, table);

            table.AfterLoad();
            S_DataTableMap[tableType] = table;
            return table;
        }

        internal static XDataTable LoadTableByType(Type tableType)
        {
            return LoadTable(tableType);
        }

        private static XDataTable LoadAndMergeTable(Type tableType, IReadOnlyList<string> paths)
        {
            XDataTable table = null;
            FieldInfo itemsField = ResolveItemsField(tableType);
            Type dataType = itemsField.FieldType.GetElementType();
            if (dataType == null)
            {
                throw new Exception($"Data table type {tableType.FullName} items field is not an array");
            }

            var mergedItems = new List<object>();
            for (int i = 0; i < paths.Count; i++)
            {
                string dataPath = paths[i];
                TextAsset textAsset = ResourceManager.Instance.Load<TextAsset>(dataPath);
                if (textAsset == null)
                {
                    throw new Exception($"Data text asset missing at path: {dataPath}");
                }

                XDataTable loadedTable = textAsset.ToXTextAsset<XDataTable>(tableType);
                if (loadedTable == null)
                {
                    throw new Exception($"Data text asset at path {dataPath} cannot convert to {tableType.FullName}");
                }

                table ??= loadedTable;
                if (itemsField.GetValue(loadedTable) is not Array items)
                {
                    continue;
                }

                foreach (object item in items)
                {
                    if (item != null)
                    {
                        mergedItems.Add(item);
                    }
                }
            }

            if (table == null)
            {
                throw new Exception($"Data table type {tableType.FullName} has no loaded assets");
            }

            if (paths.Count > 1)
            {
                Array mergedArray = Array.CreateInstance(dataType, mergedItems.Count);
                for (int i = 0; i < mergedItems.Count; i++)
                {
                    mergedArray.SetValue(mergedItems[i], i);
                }

                itemsField.SetValue(table, mergedArray);
            }

            return table;
        }

        private static FieldInfo ResolveItemsField(Type tableType)
        {
            Type currentType = tableType;
            while (currentType != null && currentType != typeof(object))
            {
                FieldInfo field = currentType.GetField(nameof(XDataTable<IData>.items), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            throw new Exception($"Data table type {tableType.FullName} cannot find items field");
        }

        private static void ValidateMergedTable(Type tableType, XDataTable table)
        {
            FieldInfo itemsField = ResolveItemsField(tableType);
            if (itemsField.GetValue(table) is not IEnumerable items)
            {
                return;
            }

            Type dataType = itemsField.FieldType.GetElementType();
            if (dataType == null)
            {
                return;
            }

            ValidateDuplicateKey(tableType, dataType, items);
            ValidateDuplicateAlias(tableType, dataType, items);
        }

        private static void ValidateDuplicateKey(Type tableType, Type dataType, IEnumerable items)
        {
            if (!Utility.Reflection.IsSubclassOfGeneric(tableType, typeof(XDataTableHasKey<,>)))
            {
                return;
            }

            PropertyInfo keyProperty = dataType.GetProperty(nameof(IDataHasKey<int>.PrimaryKey), BindingFlags.Instance | BindingFlags.Public);
            if (keyProperty == null)
            {
                return;
            }

            var seenKeys = new HashSet<object>();
            foreach (object item in items)
            {
                object key = keyProperty.GetValue(item);
                if (!seenKeys.Add(key))
                {
                    throw new Exception($"Data table {tableType.FullName} has repeated primary key {key}");
                }
            }
        }

        private static void ValidateDuplicateAlias(Type tableType, Type dataType, IEnumerable items)
        {
            if (!Utility.Reflection.IsSubclassOfGeneric(tableType, typeof(XDataTableHasAlias<,>)))
            {
                return;
            }

            PropertyInfo aliasProperty = dataType.GetProperty(nameof(IDataHasAlias.Alias), BindingFlags.Instance | BindingFlags.Public);
            if (aliasProperty == null)
            {
                return;
            }

            var seenAliases = new HashSet<string>();
            foreach (object item in items)
            {
                string alias = aliasProperty.GetValue(item) as string;
                if (string.IsNullOrEmpty(alias))
                {
                    continue;
                }

                if (!seenAliases.Add(alias))
                {
                    throw new Exception($"Data table {tableType.FullName} has repeated alias {alias}");
                }
            }
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

        public static TValue GetData<TValue>(int id) where TValue : IDataHasKey<int>
        {
            return GetData<int, TValue>(id);
        }

        public static TValue GetData<TKey, TValue>(TKey key) where TValue : IDataHasKey<TKey>
        {
            if (LoadDictData<TKey, TValue>().TryGetValue(key, out var value))
            {
                return value;
            }
            throw new Exception($"Data type {typeof(TValue).FullName} missing key {key}");
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

        public static TValue GetDataByAlias<TValue>(string alias) where TValue : IDataHasAlias
        {
            var dict = LoadAliasDictData<TValue>();
            if (dict.TryGetValue(alias, out var value))
            {
                return value;
            }
            throw new Exception($"Data type {typeof(TValue).FullName} missing alias {alias}");
        }
    }
    
    [Serializable]
    [XTextAssetAlias("xframework.data-table")]
    public abstract class XDataTable<TData> : XDataTable where TData : IData
    {
        public TData[] items;

        public static IReadOnlyList<TData> LoadData()
        {
            return XDataTable.LoadData<TData>();
        }
    }
    
    [Serializable]
    [XTextAssetAlias("xframework.data-table-haskey")]
    public abstract class XDataTableHasKey<TKey, TData> : XDataTable<TData> where TData : IDataHasKey<TKey>
    {
        public static TData GetData(TKey key)
        {
            return XDataTable.GetData<TKey, TData>(key);
        }
    }
    
    [Serializable]
    [XTextAssetAlias("xframework.data-table-hasalias")]
    public abstract class XDataTableHasAlias<TKey, TData> : XDataTableHasKey<TKey, TData> where TData : IDataHasAlias<TKey>
    {
        public static TData GetDataByAlias(string alias)
        {
            return XDataTable.GetDataByAlias<TData>(alias);
        }

        public static string[] GetAllAlias()
        {
            IReadOnlyDictionary<string, TData> dict = LoadAliasDictData<TData>();
            string[] aliases = new string[dict.Count];
            int index = 0;
            foreach (string alias in dict.Keys)
            {
                aliases[index++] = alias;
            }
            return aliases;
        }
    }
}
