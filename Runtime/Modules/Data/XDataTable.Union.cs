using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XFramework.Data
{
    public sealed class UnionDataTablesAttribute : Attribute
    {
        public Type[] tableTypes;

        public UnionDataTablesAttribute(params Type[] tableTypes)
        {
            this.tableTypes = tableTypes ?? Array.Empty<Type>();
        }
    }

    abstract partial class XDataTable
    {
        private static readonly Dictionary<Type, IList> S_UnionDataListMap = new();
        private static readonly Dictionary<Type, IDictionary> S_UnionDataDictMap = new();
        private static readonly Dictionary<Type, IDictionary> S_UnionDataAliasDictMap = new();

        internal static IReadOnlyList<TData> LoadUnionData<TTable, TData>()
            where TTable : XUnionDataTable<TTable, TData>
            where TData : IData
        {
            var unionTableType = typeof(TTable);
            if (!S_UnionDataListMap.TryGetValue(unionTableType, out var cachedList))
            {
                cachedList = BuildUnionDataList<TTable, TData>();
                S_UnionDataListMap.Add(unionTableType, cachedList);
            }

            return (IReadOnlyList<TData>)cachedList;
        }

        internal static IReadOnlyDictionary<TKey, TData> LoadUnionDictData<TTable, TKey, TData>()
            where TTable : XUnionDataTableHasKey<TTable, TKey, TData>
            where TData : IDataHasKey<TKey>
        {
            var unionTableType = typeof(TTable);
            if (!S_UnionDataDictMap.TryGetValue(unionTableType, out var cachedDict))
            {
                cachedDict = BuildUnionDictData<TKey, TData>(unionTableType);
                S_UnionDataDictMap.Add(unionTableType, cachedDict);
            }

            return (IReadOnlyDictionary<TKey, TData>)cachedDict;
        }

        internal static TData GetUnionData<TTable, TKey, TData>(TKey key)
            where TTable : XUnionDataTableHasKey<TTable, TKey, TData>
            where TData : IDataHasKey<TKey>
        {
            if (LoadUnionDictData<TTable, TKey, TData>().TryGetValue(key, out var value))
            {
                return value;
            }

            throw new Exception($"Union data table {typeof(TTable).FullName} missing key {key}");
        }

        internal static IReadOnlyDictionary<string, TData> LoadUnionAliasDictData<TTable, TKey, TData>()
            where TTable : XUnionDataTableHasAlias<TTable, TKey, TData>
            where TData : IDataHasAlias<TKey>
        {
            var unionTableType = typeof(TTable);
            if (!S_UnionDataAliasDictMap.TryGetValue(unionTableType, out var cachedDict))
            {
                cachedDict = BuildUnionAliasDictData<TData>(unionTableType);
                S_UnionDataAliasDictMap.Add(unionTableType, cachedDict);
            }

            return (IReadOnlyDictionary<string, TData>)cachedDict;
        }

        internal static TData GetUnionDataByAlias<TTable, TKey, TData>(string alias)
            where TTable : XUnionDataTableHasAlias<TTable, TKey, TData>
            where TData : IDataHasAlias<TKey>
        {
            var dict = LoadUnionAliasDictData<TTable, TKey, TData>();
            if (dict.TryGetValue(alias, out var value))
            {
                return value;
            }

            throw new Exception($"Union data table {typeof(TTable).FullName} missing alias {alias}");
        }

        private static List<TData> BuildUnionDataList<TTable, TData>()
            where TTable : XUnionDataTable<TTable, TData>
            where TData : IData
        {
            var unionTableType = typeof(TTable);
            var result = new List<TData>();
            foreach (var childTableType in GetUnionChildTableTypes(unionTableType))
            {
                AppendUnionChildTableData<TData>(unionTableType, childTableType, result);
            }

            return result;
        }

        private static void AppendUnionChildTableData<TData>(Type unionTableType, Type childTableType, List<TData> result)
            where TData : IData
        {
            var childDataType = ResolveUnionChildDataType<TData>(unionTableType, childTableType);
            var genericTableType = typeof(XDataTable<>).MakeGenericType(childDataType);
            var table = LoadTableByType(childTableType);
            var itemsField = genericTableType.GetField(nameof(XDataTable<IData>.items), BindingFlags.Instance | BindingFlags.Public);
            if (itemsField == null)
            {
                throw new Exception($"Union data table {unionTableType.FullName} cannot find items field on {genericTableType.FullName}");
            }

            if (itemsField.GetValue(table) is not IEnumerable items)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item is TData data)
                {
                    result.Add(data);
                    continue;
                }

                throw new Exception($"Union data table {unionTableType.FullName} child item from {childTableType.FullName} cannot convert to {typeof(TData).FullName}");
            }
        }

        private static XUnionDataTableDictionary<TKey, TData> BuildUnionDictData<TKey, TData>(Type unionTableType)
            where TData : IDataHasKey<TKey>
        {
            var childDicts = new List<IReadOnlyDictionary<TKey, TData>>();
            var seenKeys = new HashSet<TKey>();
            foreach (var childTableType in GetUnionChildTableTypes(unionTableType))
            {
                var childDataType = ResolveUnionChildDataType<TData>(unionTableType, childTableType);
                var childDict = LoadChildDictData<TKey, TData>(childDataType);
                foreach (var key in childDict.Keys)
                {
                    if (!seenKeys.Add(key))
                    {
                        throw new Exception($"Union data table {unionTableType.FullName} has repeated primary key {key}");
                    }
                }

                childDicts.Add(childDict);
            }

            return new XUnionDataTableDictionary<TKey, TData>(childDicts);
        }

        private static XUnionDataTableDictionary<string, TData> BuildUnionAliasDictData<TData>(Type unionTableType)
            where TData : IDataHasAlias
        {
            var childDicts = new List<IReadOnlyDictionary<string, TData>>();
            var seenAliases = new HashSet<string>();
            foreach (var childTableType in GetUnionChildTableTypes(unionTableType))
            {
                var childDataType = ResolveUnionChildDataType<TData>(unionTableType, childTableType);
                var childDict = LoadChildAliasDictData<TData>(childDataType);
                foreach (var alias in childDict.Keys)
                {
                    if (string.IsNullOrEmpty(alias))
                    {
                        continue;
                    }

                    if (!seenAliases.Add(alias))
                    {
                        throw new Exception($"Union data table {unionTableType.FullName} has repeated alias {alias}");
                    }
                }

                childDicts.Add(childDict);
            }

            return new XUnionDataTableDictionary<string, TData>(childDicts);
        }

        private static Type[] GetUnionChildTableTypes(Type unionTableType)
        {
            var unionDataTablesAttr = unionTableType.GetCustomAttribute<UnionDataTablesAttribute>(false);
            if (unionDataTablesAttr == null || unionDataTablesAttr.tableTypes.Length == 0)
            {
                throw new Exception($"Union data table {unionTableType.FullName} missing UnionDataTables attribute");
            }

            return unionDataTablesAttr.tableTypes;
        }

        private static Type ResolveUnionChildDataType<TData>(Type unionTableType, Type childTableType)
            where TData : IData
        {
            if (childTableType == null)
            {
                throw new Exception($"Union data table {unionTableType.FullName} contains null child table type");
            }

            if (!typeof(XDataTable).IsAssignableFrom(childTableType))
            {
                throw new Exception($"Union data table {unionTableType.FullName} child type {childTableType.FullName} is not an XDataTable");
            }

            var targetDataTypeAttr = childTableType.GetCustomAttribute<TargetDataType>(false);
            if (targetDataTypeAttr?.targetType == null)
            {
                throw new Exception($"Union data table {unionTableType.FullName} child table {childTableType.FullName} missing TargetDataType attribute");
            }

            var childDataType = targetDataTypeAttr.targetType;
            if (!typeof(TData).IsAssignableFrom(childDataType))
            {
                throw new Exception($"Union data table {unionTableType.FullName} child data type {childDataType.FullName} cannot convert to {typeof(TData).FullName}");
            }

            var genericTableType = typeof(XDataTable<>).MakeGenericType(childDataType);
            if (!genericTableType.IsAssignableFrom(childTableType))
            {
                throw new Exception($"Union data table {unionTableType.FullName} child table {childTableType.FullName} does not inherit {genericTableType.FullName}");
            }

            return childDataType;
        }

        private static IReadOnlyDictionary<TKey, TData> LoadChildDictData<TKey, TData>(Type childDataType)
            where TData : IDataHasKey<TKey>
        {
            var method = typeof(XDataTable)
                .GetMethod(nameof(LoadDictData), BindingFlags.Static | BindingFlags.Public)
                ?.MakeGenericMethod(typeof(TKey), childDataType);
            if (method == null)
            {
                throw new Exception($"Cannot find {nameof(LoadDictData)} method for {childDataType.FullName}");
            }

            var childDict = method.Invoke(null, null);
            return new XUnionDataTableDictionaryAdapter<TKey, TData>(childDict);
        }

        private static IReadOnlyDictionary<string, TData> LoadChildAliasDictData<TData>(Type childDataType)
            where TData : IDataHasAlias
        {
            var method = typeof(XDataTable)
                .GetMethod(nameof(LoadAliasDictData), BindingFlags.Static | BindingFlags.Public)
                ?.MakeGenericMethod(childDataType);
            if (method == null)
            {
                throw new Exception($"Cannot find {nameof(LoadAliasDictData)} method for {childDataType.FullName}");
            }

            var childDict = method.Invoke(null, null);
            return new XUnionDataTableDictionaryAdapter<string, TData>(childDict);
        }

        private sealed class XUnionDataTableDictionary<TKey, TData> : IReadOnlyDictionary<TKey, TData>, IDictionary
        {
            private readonly IReadOnlyList<IReadOnlyDictionary<TKey, TData>> m_Dicts;

            public XUnionDataTableDictionary(IReadOnlyList<IReadOnlyDictionary<TKey, TData>> dicts)
            {
                m_Dicts = dicts ?? Array.Empty<IReadOnlyDictionary<TKey, TData>>();
            }

            public IEnumerable<TKey> Keys => m_Dicts.SelectMany(dict => dict.Keys);
            public IEnumerable<TData> Values => m_Dicts.SelectMany(dict => dict.Values);
            public int Count => m_Dicts.Sum(dict => dict.Count);

            public TData this[TKey key]
            {
                get
                {
                    if (TryGetValue(key, out var value))
                    {
                        return value;
                    }

                    throw new KeyNotFoundException($"Union data table missing key {key}");
                }
            }

            public bool ContainsKey(TKey key)
            {
                return m_Dicts.Any(dict => dict.ContainsKey(key));
            }

            public bool TryGetValue(TKey key, out TData value)
            {
                foreach (var dict in m_Dicts)
                {
                    if (dict.TryGetValue(key, out value))
                    {
                        return true;
                    }
                }

                value = default;
                return false;
            }

            public IEnumerator<KeyValuePair<TKey, TData>> GetEnumerator()
            {
                return m_Dicts.SelectMany(dict => dict).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            bool IDictionary.IsFixedSize => true;
            bool IDictionary.IsReadOnly => true;
            ICollection IDictionary.Keys => Keys.ToArray();
            ICollection IDictionary.Values => Values.ToArray();
            object IDictionary.this[object key]
            {
                get
                {
                    if (key is TKey typedKey && TryGetValue(typedKey, out var value))
                    {
                        return value;
                    }

                    return null;
                }
                set => throw new NotSupportedException();
            }

            void IDictionary.Add(object key, object value)
            {
                throw new NotSupportedException();
            }

            void IDictionary.Clear()
            {
                throw new NotSupportedException();
            }

            bool IDictionary.Contains(object key)
            {
                return key is TKey typedKey && ContainsKey(typedKey);
            }

            IDictionaryEnumerator IDictionary.GetEnumerator()
            {
                return new XUnionDataTableDictionaryEnumerator<TKey, TData>(GetEnumerator());
            }

            void IDictionary.Remove(object key)
            {
                throw new NotSupportedException();
            }

            void ICollection.CopyTo(Array array, int index)
            {
                foreach (var pair in this)
                {
                    array.SetValue(pair, index++);
                }
            }

            bool ICollection.IsSynchronized => false;
            object ICollection.SyncRoot => this;
        }

        private sealed class XUnionDataTableDictionaryAdapter<TKey, TData> : IReadOnlyDictionary<TKey, TData>
        {
            private readonly object m_Dict;
            private readonly Type m_ReadOnlyDictType;
            private readonly PropertyInfo m_CountProperty;
            private readonly PropertyInfo m_KeysProperty;
            private readonly PropertyInfo m_ValuesProperty;
            private readonly MethodInfo m_ContainsKeyMethod;
            private readonly MethodInfo m_TryGetValueMethod;

            public XUnionDataTableDictionaryAdapter(object dict)
            {
                m_Dict = dict ?? throw new ArgumentNullException(nameof(dict));
                var dictType = dict.GetType();
                m_ReadOnlyDictType = dictType
                    .GetInterfaces()
                    .FirstOrDefault(type =>
                    {
                        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IReadOnlyDictionary<,>))
                        {
                            return false;
                        }

                        Type[] args = type.GetGenericArguments();
                        return args[0] == typeof(TKey) && typeof(TData).IsAssignableFrom(args[1]);
                    });
                if (m_ReadOnlyDictType == null)
                {
                    throw new Exception($"Child data dictionary cannot convert to {typeof(IReadOnlyDictionary<TKey, TData>).FullName}");
                }

                Type valueType = m_ReadOnlyDictType.GetGenericArguments()[1];
                m_CountProperty = m_ReadOnlyDictType.GetProperty(nameof(Count));
                m_KeysProperty = m_ReadOnlyDictType.GetProperty(nameof(Keys));
                m_ValuesProperty = m_ReadOnlyDictType.GetProperty(nameof(Values));
                m_ContainsKeyMethod = m_ReadOnlyDictType.GetMethod(nameof(ContainsKey), new[] { typeof(TKey) });
                m_TryGetValueMethod = m_ReadOnlyDictType.GetMethod(nameof(TryGetValue), new[] { typeof(TKey), valueType.MakeByRefType() });
                if (m_CountProperty == null
                    || m_KeysProperty == null
                    || m_ValuesProperty == null
                    || m_ContainsKeyMethod == null
                    || m_TryGetValueMethod == null)
                {
                    throw new Exception($"Child data dictionary cannot convert to {typeof(IReadOnlyDictionary<TKey, TData>).FullName}");
                }
            }

            public IEnumerable<TKey> Keys => ((IEnumerable)m_KeysProperty.GetValue(m_Dict)).Cast<TKey>();
            public IEnumerable<TData> Values => ((IEnumerable)m_ValuesProperty.GetValue(m_Dict)).Cast<TData>();
            public int Count => (int)m_CountProperty.GetValue(m_Dict);

            public TData this[TKey key]
            {
                get
                {
                    if (TryGetValue(key, out var value))
                    {
                        return value;
                    }

                    throw new KeyNotFoundException($"Child data dictionary missing key {key}");
                }
            }

            public bool ContainsKey(TKey key)
            {
                return (bool)m_ContainsKeyMethod.Invoke(m_Dict, new object[] { key });
            }

            public bool TryGetValue(TKey key, out TData value)
            {
                object[] args = { key, default(TData) };
                bool result = (bool)m_TryGetValueMethod.Invoke(m_Dict, args);
                value = result ? (TData)args[1] : default;
                return result;
            }

            public IEnumerator<KeyValuePair<TKey, TData>> GetEnumerator()
            {
                foreach (var key in Keys)
                {
                    yield return new KeyValuePair<TKey, TData>(key, this[key]);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class XUnionDataTableDictionaryEnumerator<TKey, TData> : IDictionaryEnumerator
        {
            private readonly IEnumerator<KeyValuePair<TKey, TData>> m_Enumerator;

            public XUnionDataTableDictionaryEnumerator(IEnumerator<KeyValuePair<TKey, TData>> enumerator)
            {
                m_Enumerator = enumerator;
            }

            public DictionaryEntry Entry => new(Key, Value);
            public object Key => m_Enumerator.Current.Key;
            public object Value => m_Enumerator.Current.Value;
            public object Current => Entry;

            public bool MoveNext()
            {
                return m_Enumerator.MoveNext();
            }

            public void Reset()
            {
                m_Enumerator.Reset();
            }
        }
    }

    public abstract class XUnionDataTable<TTable, TData> : XDataTable
        where TTable : XUnionDataTable<TTable, TData>
        where TData : IData
    {
        public static IReadOnlyList<TData> LoadData()
        {
            return XDataTable.LoadUnionData<TTable, TData>();
        }
    }

    public abstract class XUnionDataTableHasKey<TTable, TKey, TData> : XUnionDataTable<TTable, TData>
        where TTable : XUnionDataTableHasKey<TTable, TKey, TData>
        where TData : IDataHasKey<TKey>
    {
        public static IReadOnlyDictionary<TKey, TData> LoadDictData()
        {
            return XDataTable.LoadUnionDictData<TTable, TKey, TData>();
        }

        public static TData GetData(TKey key)
        {
            return XDataTable.GetUnionData<TTable, TKey, TData>(key);
        }
    }

    public abstract class XUnionDataTableHasAlias<TTable, TKey, TData> : XUnionDataTableHasKey<TTable, TKey, TData>
        where TTable : XUnionDataTableHasAlias<TTable, TKey, TData>
        where TData : IDataHasAlias<TKey>
    {
        public static IReadOnlyDictionary<string, TData> LoadAliasDictData()
        {
            return XDataTable.LoadUnionAliasDictData<TTable, TKey, TData>();
        }

        public static TData GetDataByAlias(string alias)
        {
            return XDataTable.GetUnionDataByAlias<TTable, TKey, TData>(alias);
        }

        public static string[] GetAllAlias()
        {
            IReadOnlyDictionary<string, TData> dict = LoadAliasDictData();
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
