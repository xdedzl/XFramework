using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using XFramework.Data;
using XFramework.Json;
using XFramework.Resource;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XFramework.Editor
{
    public sealed class XDataTableRefMeta
    {
        public XDataTableRefMeta(
            Type tableType,
            Type dataType,
            PropertyInfo keyProperty,
            PropertyInfo aliasProperty,
            FieldInfo nameField,
            TextAsset tableAsset,
            IReadOnlyList<TextAsset> tableAssets,
            IReadOnlyList<object> rows)
            : this(tableType, dataType, keyProperty, aliasProperty, null, nameField, tableAsset, tableAssets, rows)
        {
        }

        public XDataTableRefMeta(
            Type tableType,
            Type dataType,
            PropertyInfo keyProperty,
            PropertyInfo aliasProperty,
            PropertyInfo nameProperty,
            FieldInfo nameField,
            TextAsset tableAsset,
            IReadOnlyList<TextAsset> tableAssets,
            IReadOnlyList<object> rows,
            IReadOnlyList<Type> rowTableTypes = null,
            IReadOnlyList<Type> tableAssetTypes = null,
            bool isUnionTable = false)
        {
            TableType = tableType;
            DataType = dataType;
            KeyProperty = keyProperty;
            AliasProperty = aliasProperty;
            NameProperty = nameProperty;
            NameField = nameField;
            TableAsset = tableAsset;
            TableAssets = tableAssets ?? Array.Empty<TextAsset>();
            Rows = rows ?? Array.Empty<object>();
            RowTableTypes = rowTableTypes ?? Enumerable.Repeat(tableType, Rows.Count).ToArray();
            TableAssetTypes = tableAssetTypes ?? Enumerable.Repeat(tableType, TableAssets.Count).ToArray();
            IsUnionTable = isUnionTable;
        }

        public Type TableType { get; }
        public Type DataType { get; }
        public PropertyInfo KeyProperty { get; }
        public PropertyInfo AliasProperty { get; }
        public PropertyInfo NameProperty { get; }
        public FieldInfo NameField { get; }
        public TextAsset TableAsset { get; }
        public IReadOnlyList<TextAsset> TableAssets { get; }
        public IReadOnlyList<Type> RowTableTypes { get; }
        public IReadOnlyList<Type> TableAssetTypes { get; }
        public IReadOnlyList<object> Rows { get; }
        public bool IsUnionTable { get; }
    }

    public readonly struct XDataTableRefOption
    {
        public XDataTableRefOption(int rowIndex, object keyValue, string alias, string name, string displayText)
            : this(rowIndex, keyValue, alias, name, displayText, null)
        {
        }

        public XDataTableRefOption(int rowIndex, object keyValue, string alias, string name, string displayText, Type sourceTableType)
        {
            RowIndex = rowIndex;
            KeyValue = keyValue;
            Alias = alias;
            Name = name;
            DisplayText = displayText;
            SourceTableType = sourceTableType;
        }

        public int RowIndex { get; }
        public object KeyValue { get; }
        public string Alias { get; }
        public string Name { get; }
        public string DisplayText { get; }
        public Type SourceTableType { get; }
    }

    public static class XDataTableRefResolver
    {
        private static readonly Dictionary<Type, XDataTableRefMeta> S_MetaCache = new();

        public static XDataTableRefMeta Resolve(FieldInfo ownerField, DataTableRefAttribute attribute, out string resolveError)
        {
            return Resolve(ownerField?.FieldType, ownerField?.Name, attribute?.tableType, out resolveError);
        }

        public static XDataTableRefMeta Resolve(Type ownerFieldType, string ownerFieldName, Type tableType, out string resolveError)
        {
            resolveError = null;
            if (tableType == null)
            {
                resolveError = $"{ownerFieldName ?? "字段"} 的 DataTableRef 缺少 tableType。";
                return null;
            }

            if (!typeof(XDataTable).IsAssignableFrom(tableType))
            {
                resolveError = $"{tableType.Name} 不是合法的 XDataTable 类型。";
                return null;
            }

            if (!IsDataTableRefTargetType(tableType))
            {
                resolveError = $"{tableType.Name} 必须继承 XDataTableHasKey<,> 或 XUnionDataTableHasKey<,,>。";
                return null;
            }

            if (!S_MetaCache.TryGetValue(tableType, out XDataTableRefMeta meta) || meta == null)
            {
                meta = CreateMeta(tableType, out resolveError);
                if (meta == null)
                {
                    return null;
                }

                S_MetaCache[tableType] = meta;
            }

            if (ownerFieldType != null && meta.KeyProperty?.PropertyType != ownerFieldType)
            {
                resolveError = $"{ownerFieldName ?? "字段"} 类型 {ownerFieldType.Name} 与 {meta.DataType.Name}.PrimaryKey 类型 {meta.KeyProperty.PropertyType.Name} 不一致。";
                return null;
            }

            return meta;
        }

        public static bool SupportsEmptyReference(Type fieldType)
        {
            return fieldType == typeof(int)
                   || fieldType == typeof(uint)
                   || fieldType == typeof(long);
        }

        public static bool IsEmptyReferenceValue(object value, Type fieldType)
        {
            if (value == null)
            {
                return true;
            }

            if (fieldType == typeof(int))
            {
                return (int)value == 0;
            }

            if (fieldType == typeof(uint))
            {
                return (uint)value == 0u;
            }

            if (fieldType == typeof(long))
            {
                return (long)value == 0L;
            }

            return false;
        }

        public static object GetEmptyReferenceValue(Type fieldType)
        {
            if (fieldType == typeof(uint))
            {
                return 0u;
            }

            if (fieldType == typeof(long))
            {
                return 0L;
            }

            if (fieldType == typeof(int))
            {
                return 0;
            }

            return null;
        }

        public static string GetDisplayText(XDataTableRefMeta meta, object keyValue, Type ownerFieldType)
        {
            if (meta == null)
            {
                return keyValue?.ToString() ?? string.Empty;
            }

            if (IsEmptyReferenceValue(keyValue, ownerFieldType))
            {
                return "None";
            }

            if (TryFindRow(meta, keyValue, out _, out object row))
            {
                return BuildReferenceDisplayText(
                    meta.KeyProperty.GetValue(row),
                    meta.AliasProperty?.GetValue(row) as string,
                    GetNameValue(meta, row));
            }

            return $"{keyValue} | Missing";
        }

        public static IReadOnlyList<XDataTableRefOption> GetOptions(XDataTableRefMeta meta, string searchText)
        {
            var options = new List<XDataTableRefOption>();
            if (meta == null)
            {
                return options;
            }

            string normalizedSearch = searchText?.Trim();
            bool hasSearch = !string.IsNullOrEmpty(normalizedSearch);
            for (int i = 0; i < meta.Rows.Count; i++)
            {
                object row = meta.Rows[i];
                object keyValue = meta.KeyProperty.GetValue(row);
                string alias = meta.AliasProperty?.GetValue(row) as string;
                string name = GetNameValue(meta, row);
                string displayText = BuildReferenceDisplayText(keyValue, alias, name);

                if (hasSearch)
                {
                    string keyText = keyValue?.ToString() ?? string.Empty;
                    bool matched = ContainsIgnoreCase(keyText, normalizedSearch)
                                   || ContainsIgnoreCase(alias, normalizedSearch)
                                   || ContainsIgnoreCase(name, normalizedSearch);
                    if (!matched)
                    {
                        continue;
                    }
                }

                options.Add(new XDataTableRefOption(i, keyValue, alias, name, displayText, GetRowTableType(meta, i)));
            }

            return options;
        }

        public static bool ContainsKey(XDataTableRefMeta meta, object keyValue)
        {
            return TryFindRow(meta, keyValue, out _, out _);
        }

        public static bool TryConvertReferenceValue(Type fieldType, object rawValue, out object converted)
        {
            converted = null;
            if (fieldType == null)
            {
                return false;
            }

            if (rawValue == null)
            {
                converted = GetEmptyReferenceValue(fieldType);
                return SupportsEmptyReference(fieldType);
            }

            if (rawValue.GetType() == fieldType)
            {
                converted = rawValue;
                return true;
            }

            try
            {
                converted = fieldType switch
                {
                    _ when fieldType == typeof(int) => Convert.ToInt32(rawValue),
                    _ when fieldType == typeof(uint) => Convert.ToUInt32(rawValue),
                    _ when fieldType == typeof(long) => Convert.ToInt64(rawValue),
                    _ => Convert.ChangeType(rawValue, fieldType)
                };
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }
        }

#if UNITY_EDITOR
        public static void PingReferencedTable(XDataTableRefMeta meta)
        {
            if (meta?.TableAsset == null)
            {
                return;
            }

            Selection.activeObject = meta.TableAsset;
            EditorGUIUtility.PingObject(meta.TableAsset);
        }

        public static void OpenReferencedTable(XDataTableRefMeta meta, object keyValue, Type ownerFieldType)
        {
            if (meta?.TableAsset == null)
            {
                return;
            }

            Type windowType = Type.GetType("XFramework.Editor.XDataTableEditorWindow, XFrameworkEditor");
            if (windowType == null)
            {
                AssetDatabase.OpenAsset(meta.TableAsset);
                return;
            }

            if (keyValue == null || IsEmptyReferenceValue(keyValue, ownerFieldType))
            {
                if (meta.IsUnionTable)
                {
                    MethodInfo showUnionWindowMethod = windowType.GetMethod(
                        "ShowUnionDataTableWindow",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    showUnionWindowMethod?.Invoke(null, new object[] { meta.TableType });
                    return;
                }

                MethodInfo showWindowMethod = windowType.GetMethod("ShowWindow", BindingFlags.Public | BindingFlags.Static);
                showWindowMethod?.Invoke(null, new object[] { meta.TableAsset });
                return;
            }

            TextAsset targetAsset = ResolveTableAssetForKey(meta, keyValue) ?? meta.TableAsset;
            MethodInfo showAndLocateMethod = windowType.GetMethod("ShowWindowAndLocate", BindingFlags.Public | BindingFlags.Static);
            if (showAndLocateMethod != null)
            {
                showAndLocateMethod.Invoke(null, new object[] { targetAsset, keyValue });
            }
            else
            {
                AssetDatabase.OpenAsset(targetAsset);
            }
        }
#endif

        private static XDataTableRefMeta CreateMeta(Type tableType, out string resolveError)
        {
            resolveError = null;

            Type dataType = ResolveDataType(tableType);
            if (dataType == null)
            {
                resolveError = $"{tableType.Name} 无法解析目标数据类型。";
                return null;
            }

            PropertyInfo keyProperty = ResolvePublicProperty(dataType, "PrimaryKey");
            if (keyProperty == null)
            {
                resolveError = $"{dataType.Name} 缺少 PrimaryKey 属性。";
                return null;
            }

            PropertyInfo aliasProperty = ResolvePublicProperty(dataType, "Alias");
            PropertyInfo nameProperty = ResolvePublicProperty(dataType, "Name");
            FieldInfo nameField = ResolveNameField(dataType);
            bool isUnionTable = IsUnionDataTableType(tableType);
            IReadOnlyList<TextAsset> tableAssets = ResolveDataTableTextAssets(tableType, dataType, out IReadOnlyList<Type> tableAssetTypes, out resolveError);
            if (tableAssets.Count == 0)
            {
                resolveError ??= $"{tableType.Name} 未找到可用的数据表资源。";
                return null;
            }

            IReadOnlyList<object> rows = LoadRows(tableType, dataType, tableAssets, tableAssetTypes, out IReadOnlyList<Type> rowTableTypes, out resolveError);
            if (rows == null)
            {
                return null;
            }

            return new XDataTableRefMeta(
                tableType,
                dataType,
                keyProperty,
                aliasProperty,
                nameProperty,
                nameField,
                tableAssets[0],
                tableAssets,
                rows,
                rowTableTypes,
                tableAssetTypes,
                isUnionTable);
        }

        private static IReadOnlyList<object> LoadRows(
            Type tableType,
            Type dataType,
            IReadOnlyList<TextAsset> tableAssets,
            IReadOnlyList<Type> tableAssetTypes,
            out IReadOnlyList<Type> rowTableTypes,
            out string resolveError)
        {
            rowTableTypes = null;
            resolveError = null;
            try
            {
                XJson.SetUnityDefaultSetting();
                var rows = new List<object>();
                var rowTypes = new List<Type>();
                for (int i = 0; i < tableAssets.Count; i++)
                {
                    TextAsset tableAsset = tableAssets[i];
                    Type sourceTableType = ResolveSourceTableType(tableType, tableAssetTypes, i);
                    FieldInfo itemsField = ResolveItemsField(sourceTableType);
                    XTextAsset typedTableAsset = tableAsset.ToXTextAsset<XTextAsset>(sourceTableType);
                    Array items = itemsField.GetValue(typedTableAsset) as Array;
                    if (items == null)
                    {
                        continue;
                    }

                    foreach (object item in items)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        Type itemType = item.GetType();
                        if (!dataType.IsAssignableFrom(itemType))
                        {
                            resolveError = $"{sourceTableType.Name} 的数据 {itemType.Name} 无法作为 {dataType.Name} 使用。";
                            return null;
                        }

                        rows.Add(item);
                        rowTypes.Add(sourceTableType);
                    }
                }

                rowTableTypes = rowTypes;
                return rows;
            }
            catch (Exception exception)
            {
                resolveError = $"加载 {tableType.Name} 引用数据失败: {exception.Message}";
                return null;
            }
        }

        private static bool TryFindRow(XDataTableRefMeta meta, object keyValue, out int rowIndex, out object row)
        {
            rowIndex = -1;
            row = null;
            if (meta == null)
            {
                return false;
            }

            for (int i = 0; i < meta.Rows.Count; i++)
            {
                object currentRow = meta.Rows[i];
                object rowKey = meta.KeyProperty.GetValue(currentRow);
                if (Equals(rowKey, keyValue))
                {
                    rowIndex = i;
                    row = currentRow;
                    return true;
                }
            }

            return false;
        }

        private static FieldInfo ResolveItemsField(Type tableType)
        {
            Type currentType = tableType;
            while (currentType != null && currentType != typeof(object))
            {
                FieldInfo field = currentType.GetField("items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"{tableType.FullName} 未找到 items 字段。");
        }

        private static IReadOnlyList<TextAsset> ResolveDataTableTextAssets(
            Type tableType,
            Type dataType,
            out IReadOnlyList<Type> tableAssetTypes,
            out string resolveError)
        {
#if UNITY_EDITOR
            resolveError = null;
            var assets = new List<TextAsset>();
            var assetTypes = new List<Type>();

            if (IsUnionDataTableType(tableType))
            {
                UnionDataTablesAttribute unionAttribute = tableType.GetCustomAttribute<UnionDataTablesAttribute>(false);
                if (unionAttribute?.tableTypes == null || unionAttribute.tableTypes.Length == 0)
                {
                    resolveError = $"{tableType.Name} 缺少 UnionDataTables。";
                    tableAssetTypes = assetTypes;
                    return assets;
                }

                foreach (Type childTableType in unionAttribute.tableTypes)
                {
                    if (childTableType == null)
                    {
                        resolveError = $"{tableType.Name} 包含空子表类型。";
                        tableAssetTypes = assetTypes;
                        return assets;
                    }

                    if (!typeof(XDataTable).IsAssignableFrom(childTableType))
                    {
                        resolveError = $"{tableType.Name} 的子表 {childTableType.Name} 不是合法的 XDataTable 类型。";
                        tableAssetTypes = assetTypes;
                        return assets;
                    }

                    Type childDataType = ResolveDataType(childTableType);
                    if (childDataType == null || !dataType.IsAssignableFrom(childDataType))
                    {
                        resolveError = $"{tableType.Name} 的子表 {childTableType.Name} 数据类型不能作为 {dataType.Name} 使用。";
                        tableAssetTypes = assetTypes;
                        return assets;
                    }

                    AppendDataTableAssets(childTableType, assets, assetTypes);
                }

                tableAssetTypes = assetTypes;
                if (assets.Count == 0)
                {
                    resolveError = $"{tableType.Name} 的 UnionDataTables 未找到可用子表资源。";
                }

                return assets;
            }

            AppendDataTableAssets(tableType, assets, assetTypes);
            tableAssetTypes = assetTypes;
            if (assets.Count == 0)
            {
                DataResourcePath pathAttribute = tableType.GetCustomAttribute<DataResourcePath>(false);
                resolveError = pathAttribute == null
                    ? $"{tableType.Name} 缺少 DataResourcePath。"
                    : $"未找到数据表资源: {string.Join(", ", pathAttribute.GetPaths())}";
            }

            return assets;
#else
            tableAssetTypes = Array.Empty<Type>();
            resolveError = null;
            return Array.Empty<TextAsset>();
#endif
        }

#if UNITY_EDITOR
        private static void AppendDataTableAssets(Type tableType, ICollection<TextAsset> assets, ICollection<Type> assetTypes)
        {
            DataResourcePath pathAttribute = tableType.GetCustomAttribute<DataResourcePath>(false);
            if (pathAttribute == null)
            {
                return;
            }

            foreach (string path in pathAttribute.GetPaths())
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                assets.Add(asset);
                assetTypes.Add(tableType);
            }
        }
#endif

#if UNITY_EDITOR
        private static TextAsset ResolveTableAssetForKey(XDataTableRefMeta meta, object keyValue)
        {
            if (meta == null || keyValue == null)
            {
                return null;
            }

            for (int i = 0; i < meta.TableAssets.Count; i++)
            {
                TextAsset tableAsset = meta.TableAssets[i];
                Type sourceTableType = ResolveSourceTableType(meta.TableType, meta.TableAssetTypes, i);
                if (tableAsset == null || !TryLoadRows(sourceTableType, tableAsset, out IReadOnlyList<object> rows))
                {
                    continue;
                }

                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    object rowKey = meta.KeyProperty.GetValue(rows[rowIndex]);
                    if (Equals(rowKey, keyValue))
                    {
                        return tableAsset;
                    }
                }
            }

            return null;
        }

        private static bool TryLoadRows(Type tableType, TextAsset tableAsset, out IReadOnlyList<object> rows)
        {
            rows = null;
            try
            {
                XJson.SetUnityDefaultSetting();
                XTextAsset typedTableAsset = tableAsset.ToXTextAsset<XTextAsset>(tableType);
                FieldInfo itemsField = ResolveItemsField(tableType);
                Array items = itemsField.GetValue(typedTableAsset) as Array;
                var loadedRows = new List<object>();
                if (items != null)
                {
                    foreach (object item in items)
                    {
                        if (item != null)
                        {
                            loadedRows.Add(item);
                        }
                    }
                }

                rows = loadedRows;
                return true;
            }
            catch
            {
                return false;
            }
        }
#endif

        private static bool IsSubclassOfGeneric(Type type, Type genericBaseType)
        {
            if (type == null || genericBaseType == null || !genericBaseType.IsGenericTypeDefinition)
            {
                return false;
            }

            Type currentType = type;
            while (currentType != null && currentType != typeof(object))
            {
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == genericBaseType)
                {
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }

        private static bool IsDataTableRefTargetType(Type tableType)
        {
            return IsSubclassOfGeneric(tableType, typeof(XDataTableHasKey<,>))
                   || IsSubclassOfGeneric(tableType, typeof(XUnionDataTableHasKey<,,>));
        }

        private static bool IsUnionDataTableType(Type tableType)
        {
            return IsSubclassOfGeneric(tableType, typeof(XUnionDataTable<,>))
                   || IsSubclassOfGeneric(tableType, typeof(XUnionDataTableHasKey<,,>))
                   || IsSubclassOfGeneric(tableType, typeof(XUnionDataTableHasAlias<,,>));
        }

        private static Type ResolveDataType(Type tableType)
        {
            Type currentType = tableType;
            while (currentType != null && currentType != typeof(object))
            {
                if (currentType.IsGenericType)
                {
                    Type genericTypeDefinition = currentType.GetGenericTypeDefinition();
                    if (genericTypeDefinition == typeof(XDataTable<>))
                    {
                        return currentType.GetGenericArguments()[0];
                    }

                    if (genericTypeDefinition == typeof(XDataTableHasKey<,>) || genericTypeDefinition == typeof(XDataTableHasAlias<,>))
                    {
                        return currentType.GetGenericArguments()[1];
                    }

                    if (genericTypeDefinition == typeof(XUnionDataTable<,>))
                    {
                        return currentType.GetGenericArguments()[1];
                    }

                    if (genericTypeDefinition == typeof(XUnionDataTableHasKey<,,>) || genericTypeDefinition == typeof(XUnionDataTableHasAlias<,,>))
                    {
                        return currentType.GetGenericArguments()[2];
                    }
                }

                currentType = currentType.BaseType;
            }

            return null;
        }

        private static Type ResolveSourceTableType(Type fallbackTableType, IReadOnlyList<Type> tableAssetTypes, int index)
        {
            return tableAssetTypes != null && index >= 0 && index < tableAssetTypes.Count && tableAssetTypes[index] != null
                ? tableAssetTypes[index]
                : fallbackTableType;
        }

        private static Type GetRowTableType(XDataTableRefMeta meta, int rowIndex)
        {
            return meta?.RowTableTypes != null && rowIndex >= 0 && rowIndex < meta.RowTableTypes.Count
                ? meta.RowTableTypes[rowIndex]
                : null;
        }

        private static PropertyInfo ResolvePublicProperty(Type type, string propertyName)
        {
            if (type == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                return property;
            }

            if (!type.IsInterface)
            {
                return null;
            }

            foreach (Type interfaceType in type.GetInterfaces())
            {
                property = interfaceType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static FieldInfo ResolveNameField(Type dataType)
        {
            return dataType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(field => string.Equals(field.Name, "name", StringComparison.OrdinalIgnoreCase)
                                         && field.FieldType == typeof(string));
        }

        private static string GetNameValue(XDataTableRefMeta meta, object row)
        {
            return meta?.NameProperty?.GetValue(row) as string
                   ?? meta?.NameField?.GetValue(row) as string;
        }

        private static string BuildReferenceDisplayText(object keyValue, string alias, string name)
        {
            string keyText = keyValue?.ToString() ?? "None";
            if (string.IsNullOrWhiteSpace(alias))
            {
                return string.IsNullOrWhiteSpace(name) ? keyText : $"{keyText} | {name}";
            }

            return string.IsNullOrWhiteSpace(name)
                ? $"{keyText} | {alias}"
                : $"{keyText} | {alias} | {name}";
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source)
                   && !string.IsNullOrWhiteSpace(value)
                   && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
