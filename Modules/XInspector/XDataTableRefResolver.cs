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
            IReadOnlyList<object> rows)
        {
            TableType = tableType;
            DataType = dataType;
            KeyProperty = keyProperty;
            AliasProperty = aliasProperty;
            NameField = nameField;
            TableAsset = tableAsset;
            Rows = rows ?? Array.Empty<object>();
        }

        public Type TableType { get; }
        public Type DataType { get; }
        public PropertyInfo KeyProperty { get; }
        public PropertyInfo AliasProperty { get; }
        public FieldInfo NameField { get; }
        public TextAsset TableAsset { get; }
        public IReadOnlyList<object> Rows { get; }
    }

    public readonly struct XDataTableRefOption
    {
        public XDataTableRefOption(int rowIndex, object keyValue, string alias, string name, string displayText)
        {
            RowIndex = rowIndex;
            KeyValue = keyValue;
            Alias = alias;
            Name = name;
            DisplayText = displayText;
        }

        public int RowIndex { get; }
        public object KeyValue { get; }
        public string Alias { get; }
        public string Name { get; }
        public string DisplayText { get; }
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

            if (!IsSubclassOfGeneric(tableType, typeof(XDataTableHasKey<,>)))
            {
                resolveError = $"{tableType.Name} 必须继承 XDataTableHasKey<,>。";
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
                    meta.NameField?.GetValue(row) as string);
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
                string name = meta.NameField?.GetValue(row) as string;
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

                options.Add(new XDataTableRefOption(i, keyValue, alias, name, displayText));
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
                MethodInfo showWindowMethod = windowType.GetMethod("ShowWindow", BindingFlags.Public | BindingFlags.Static);
                showWindowMethod?.Invoke(null, new object[] { meta.TableAsset });
                return;
            }

            MethodInfo showAndLocateMethod = windowType.GetMethod("ShowWindowAndLocate", BindingFlags.Public | BindingFlags.Static);
            if (showAndLocateMethod != null)
            {
                showAndLocateMethod.Invoke(null, new[] { meta.TableAsset, keyValue });
            }
            else
            {
                AssetDatabase.OpenAsset(meta.TableAsset);
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

            PropertyInfo keyProperty = dataType.GetProperty("PrimaryKey", BindingFlags.Instance | BindingFlags.Public);
            if (keyProperty == null)
            {
                resolveError = $"{dataType.Name} 缺少 PrimaryKey 属性。";
                return null;
            }

            PropertyInfo aliasProperty = dataType.GetProperty("Alias", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo nameField = ResolveNameField(dataType);
            TextAsset tableAsset = ResolveDataTableTextAsset(tableType);
            if (tableAsset == null)
            {
                DataResourcePath pathAttribute = tableType.GetCustomAttribute<DataResourcePath>(false);
                resolveError = pathAttribute == null
                    ? $"{tableType.Name} 缺少 DataResourcePath。"
                    : $"未找到数据表资源: {pathAttribute.path}";
                return null;
            }

            IReadOnlyList<object> rows = LoadRows(tableType, dataType, tableAsset, out resolveError);
            if (rows == null)
            {
                return null;
            }

            return new XDataTableRefMeta(tableType, dataType, keyProperty, aliasProperty, nameField, tableAsset, rows);
        }

        private static IReadOnlyList<object> LoadRows(Type tableType, Type dataType, TextAsset tableAsset, out string resolveError)
        {
            resolveError = null;
            try
            {
                XJson.SetUnityDefaultSetting();
                XTextAsset typedTableAsset = tableAsset.ToXTextAsset<XTextAsset>(tableType);
                FieldInfo itemsField = ResolveItemsField(tableType);
                Array items = itemsField.GetValue(typedTableAsset) as Array;
                var rows = new List<object>();
                if (items != null)
                {
                    foreach (object item in items)
                    {
                        rows.Add(item);
                    }
                }

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

        private static TextAsset ResolveDataTableTextAsset(Type tableType)
        {
#if UNITY_EDITOR
            DataResourcePath pathAttribute = tableType.GetCustomAttribute<DataResourcePath>(false);
            if (pathAttribute == null || string.IsNullOrWhiteSpace(pathAttribute.path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<TextAsset>(pathAttribute.path);
#else
            return null;
#endif
        }

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
                }

                currentType = currentType.BaseType;
            }

            return null;
        }

        private static FieldInfo ResolveNameField(Type dataType)
        {
            return dataType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(field => string.Equals(field.Name, "name", StringComparison.OrdinalIgnoreCase)
                                         && field.FieldType == typeof(string));
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
