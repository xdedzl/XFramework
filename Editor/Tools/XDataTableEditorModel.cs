using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using XFramework.Data;
using XFramework.Json;
using XFramework.Resource;

namespace XFramework.Editor
{
    internal enum XDataTableEditorCapability
    {
        None,
        HasKey,
        HasAlias
    }

    internal enum XDataTableEditorInlineKind
    {
        Summary,
        Text,
        Integer,
        UnsignedInteger,
        Float,
        Toggle,
        Enum,
        AssetPath
    }

    internal sealed class XDataTableEditorModel
    {
        private readonly XTextAsset m_TableAsset;
        private readonly FieldInfo m_ItemsField;
        private readonly PropertyInfo m_KeyProperty;
        private readonly PropertyInfo m_AliasProperty;

        private XDataTableEditorModel(
            TextAsset sourceAsset,
            Type tableType,
            Type dataType,
            XTextAsset tableAsset,
            FieldInfo itemsField,
            List<object> rows,
            List<XDataTableEditorColumn> columns,
            XDataTableEditorCapability capability,
            PropertyInfo keyProperty,
            PropertyInfo aliasProperty)
        {
            SourceAsset = sourceAsset;
            TableType = tableType;
            DataType = dataType;
            m_TableAsset = tableAsset;
            m_ItemsField = itemsField;
            Rows = rows;
            Columns = columns;
            Capability = capability;
            m_KeyProperty = keyProperty;
            m_AliasProperty = aliasProperty;
        }

        public TextAsset SourceAsset { get; }
        public Type TableType { get; }
        public Type DataType { get; }
        public List<object> Rows { get; }
        public List<XDataTableEditorColumn> Columns { get; }
        public XDataTableEditorCapability Capability { get; }

        public bool SupportsKey => Capability >= XDataTableEditorCapability.HasKey;
        public bool SupportsAlias => Capability >= XDataTableEditorCapability.HasAlias;
        public float TotalColumnWidth => Columns.Sum(column => column.Width);

        public string CapabilitySummary
        {
            get
            {
                return Capability switch
                {
                    XDataTableEditorCapability.HasAlias => "HasKey + HasAlias",
                    XDataTableEditorCapability.HasKey => "HasKey",
                    _ => "Base"
                };
            }
        }

        public static XDataTableEditorModel Create(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                throw new ArgumentNullException(nameof(textAsset));
            }

            XJson.SetUnityDefaultSetting();

            Type tableType = ResolveTableType(textAsset);
            if (tableType == null)
            {
                throw new InvalidOperationException($"未找到与 {AssetDatabase.GetAssetPath(textAsset)} 对应的 XDataTable 类型。");
            }

            FieldInfo itemsField = ResolveItemsField(tableType);
            Type dataType = itemsField.FieldType.GetElementType();
            if (dataType == null)
            {
                throw new InvalidOperationException($"{tableType.FullName} 的 items 字段不是数组。");
            }

            XTextAsset tableAsset = textAsset.ToXTextAsset<XTextAsset>(tableType);
            Array items = itemsField.GetValue(tableAsset) as Array;
            var rows = new List<object>();
            if (items != null)
            {
                foreach (object item in items)
                {
                    rows.Add(item);
                }
            }

            XDataTableEditorCapability capability = ResolveCapability(tableType);
            PropertyInfo keyProperty = capability >= XDataTableEditorCapability.HasKey
                ? dataType.GetProperty("PrimaryKey", BindingFlags.Instance | BindingFlags.Public)
                : null;
            PropertyInfo aliasProperty = capability >= XDataTableEditorCapability.HasAlias
                ? dataType.GetProperty("Alias", BindingFlags.Instance | BindingFlags.Public)
                : null;

            List<XDataTableEditorColumn> columns = BuildColumns(dataType, keyProperty, aliasProperty);
            return new XDataTableEditorModel(textAsset, tableType, dataType, tableAsset, itemsField, rows, columns, capability,
                keyProperty, aliasProperty);
        }

        public void Save()
        {
            XJson.SetUnityDefaultSetting();

            Array items = Array.CreateInstance(DataType, Rows.Count);
            for (int i = 0; i < Rows.Count; i++)
            {
                items.SetValue(Rows[i], i);
            }

            m_ItemsField.SetValue(m_TableAsset, items);
            m_TableAsset.SaveAsset();
        }

        public void AddRow()
        {
            Rows.Add(CreateDefaultRow());
        }

        public void DuplicateRow(int rowIndex)
        {
            if (!IsValidRowIndex(rowIndex))
            {
                return;
            }

            Rows.Insert(rowIndex + 1, CloneRow(Rows[rowIndex]));
        }

        public void RemoveRow(int rowIndex)
        {
            if (!IsValidRowIndex(rowIndex))
            {
                return;
            }

            Rows.RemoveAt(rowIndex);
        }

        public void MoveRow(int rowIndex, int direction)
        {
            if (!IsValidRowIndex(rowIndex))
            {
                return;
            }

            int targetIndex = Mathf.Clamp(rowIndex + direction, 0, Rows.Count - 1);
            if (targetIndex == rowIndex)
            {
                return;
            }

            object row = Rows[rowIndex];
            Rows.RemoveAt(rowIndex);
            Rows.Insert(targetIndex, row);
        }

        public void SortByColumn(XDataTableEditorColumn column, bool ascending)
        {
            if (column == null)
            {
                return;
            }

            Rows.Sort((left, right) =>
            {
                object leftValue = column.Field.GetValue(left);
                object rightValue = column.Field.GetValue(right);
                int result = CompareValues(leftValue, rightValue);
                return ascending ? result : -result;
            });
        }

        public object GetValue(int rowIndex, XDataTableEditorColumn column)
        {
            return IsValidRowIndex(rowIndex) && column != null
                ? column.Field.GetValue(Rows[rowIndex])
                : null;
        }

        public string GetDisplayValue(int rowIndex, XDataTableEditorColumn column)
        {
            object value = GetValue(rowIndex, column);
            return FormatValue(value, column);
        }

        public bool TrySetValue(int rowIndex, XDataTableEditorColumn column, object editorValue, out string error)
        {
            error = null;
            if (!IsValidRowIndex(rowIndex) || column == null)
            {
                error = "无效的行或列。";
                return false;
            }

            if (!TryConvertValue(column, editorValue, out object converted, out error))
            {
                return false;
            }

            column.Field.SetValue(Rows[rowIndex], converted);
            return true;
        }

        public bool TryFindRowByKey(string rawText, out int rowIndex, out string error)
        {
            rowIndex = -1;
            error = null;
            if (!SupportsKey || m_KeyProperty == null)
            {
                error = "当前表不支持 Key 定位。";
                return false;
            }

            if (!TryConvertSearchText(m_KeyProperty.PropertyType, rawText, out object targetValue, out error))
            {
                return false;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                object key = m_KeyProperty.GetValue(Rows[i]);
                if (Equals(key, targetValue))
                {
                    rowIndex = i;
                    return true;
                }
            }

            error = $"未找到 Key: {rawText}";
            return false;
        }

        public bool TryFindRowByAlias(string alias, out int rowIndex, out string error)
        {
            rowIndex = -1;
            error = null;
            if (!SupportsAlias || m_AliasProperty == null)
            {
                error = "当前表不支持 Alias 定位。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(alias))
            {
                error = "Alias 不能为空。";
                return false;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                string value = m_AliasProperty.GetValue(Rows[i]) as string;
                if (string.Equals(value, alias, StringComparison.Ordinal))
                {
                    rowIndex = i;
                    return true;
                }
            }

            error = $"未找到 Alias: {alias}";
            return false;
        }

        public XDataTableEditorValidationResult Validate()
        {
            var issues = new List<XDataTableEditorIssue>();

            if (SupportsKey && m_KeyProperty != null)
            {
                ValidateDuplicates(m_KeyProperty, Columns.FirstOrDefault(column => column.IsKey), "重复 Key", issues);
            }

            if (SupportsAlias && m_AliasProperty != null)
            {
                ValidateDuplicates(m_AliasProperty, Columns.FirstOrDefault(column => column.IsAlias), "重复 Alias", issues,
                    value => !string.IsNullOrWhiteSpace(value as string));
            }

            foreach (XDataTableEditorColumn column in Columns)
            {
                if (!column.IsAssetPath)
                {
                    continue;
                }

                for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
                {
                    string path = column.Field.GetValue(Rows[rowIndex]) as string;
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    Object asset = AssetDatabase.LoadAssetAtPath(path, column.AssetType ?? typeof(Object));
                    if (asset == null)
                    {
                        issues.Add(new XDataTableEditorIssue(rowIndex, column, $"资源不存在: {path}", true));
                        continue;
                    }

                    if (column.AssetType != null && !column.AssetType.IsInstanceOfType(asset))
                    {
                        issues.Add(new XDataTableEditorIssue(rowIndex, column,
                            $"资源类型不匹配，期望 {column.AssetType.Name}，实际 {asset.GetType().Name}", true));
                    }
                }
            }

            return new XDataTableEditorValidationResult(issues);
        }

        private static Type ResolveTableType(TextAsset textAsset)
        {
            string assetPath = AssetDatabase.GetAssetPath(textAsset);
            IEnumerable<Type> tableTypes = Utility.Reflection.GetGenericTypes(typeof(XDataTable<>), 5, "Assembly-CSharp", "XFrameworkRuntime");
            foreach (Type type in tableTypes)
            {
                DataResourcePath pathAttribute = type.GetCustomAttribute<DataResourcePath>(false);
                if (pathAttribute != null && string.Equals(pathAttribute.path, assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return type;
                }
            }

            return null;
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

        private static XDataTableEditorCapability ResolveCapability(Type tableType)
        {
            if (Utility.Reflection.IsSubclassOfGeneric(tableType, typeof(XDataTableHasAlias<,>)))
            {
                return XDataTableEditorCapability.HasAlias;
            }

            if (Utility.Reflection.IsSubclassOfGeneric(tableType, typeof(XDataTableHasKey<,>)))
            {
                return XDataTableEditorCapability.HasKey;
            }

            return XDataTableEditorCapability.None;
        }

        private static List<XDataTableEditorColumn> BuildColumns(Type dataType, PropertyInfo keyProperty, PropertyInfo aliasProperty)
        {
            FieldInfo[] fields = dataType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsStatic && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null))
                .OrderBy(field => field.MetadataToken)
                .ToArray();

            string keyFieldName = ResolveKeyFieldName(fields, keyProperty);
            string aliasFieldName = ResolveAliasFieldName(fields, aliasProperty);

            var columns = new List<XDataTableEditorColumn>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                AssetPathAttribute assetPathAttribute = field.GetCustomAttribute<AssetPathAttribute>();
                bool isList = typeof(IList).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(string);
                Type listElementType = isList && field.FieldType.IsGenericType ? field.FieldType.GetGenericArguments()[0] : null;

                var column = new XDataTableEditorColumn(
                    i,
                    field,
                    assetPathAttribute,
                    isList,
                    listElementType,
                    string.Equals(field.Name, keyFieldName, StringComparison.OrdinalIgnoreCase),
                    string.Equals(field.Name, aliasFieldName, StringComparison.OrdinalIgnoreCase));
                columns.Add(column);
            }

            return columns;
        }

        private static string ResolveKeyFieldName(IEnumerable<FieldInfo> fields, PropertyInfo keyProperty)
        {
            if (keyProperty == null)
            {
                return null;
            }

            List<FieldInfo> fieldList = fields.ToList();
            FieldInfo byId = fieldList.FirstOrDefault(field => string.Equals(field.Name, "id", StringComparison.OrdinalIgnoreCase));
            if (byId != null)
            {
                return byId.Name;
            }

            FieldInfo byKey = fieldList.FirstOrDefault(field => string.Equals(field.Name, "key", StringComparison.OrdinalIgnoreCase));
            if (byKey != null)
            {
                return byKey.Name;
            }

            FieldInfo bySuffix = fieldList.FirstOrDefault(field => field.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
            if (bySuffix != null)
            {
                return bySuffix.Name;
            }

            FieldInfo byType = fieldList.FirstOrDefault(field => field.FieldType == keyProperty.PropertyType);
            return byType?.Name;
        }

        private static string ResolveAliasFieldName(IEnumerable<FieldInfo> fields, PropertyInfo aliasProperty)
        {
            if (aliasProperty == null)
            {
                return null;
            }

            List<FieldInfo> fieldList = fields.ToList();
            FieldInfo aliasField = fieldList.FirstOrDefault(field => string.Equals(field.Name, "alias", StringComparison.OrdinalIgnoreCase));
            if (aliasField != null)
            {
                return aliasField.Name;
            }

            FieldInfo suffixField = fieldList.FirstOrDefault(field => field.Name.EndsWith("Alias", StringComparison.OrdinalIgnoreCase));
            if (suffixField != null)
            {
                return suffixField.Name;
            }

            FieldInfo firstString = fieldList.FirstOrDefault(field => field.FieldType == typeof(string));
            return firstString?.Name;
        }

        private static int CompareValues(object left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            if (left is IComparable comparable && right.GetType() == left.GetType())
            {
                return comparable.CompareTo(right);
            }

            return string.CompareOrdinal(left.ToString(), right.ToString());
        }

        private static bool TryConvertSearchText(Type targetType, string rawText, out object converted, out string error)
        {
            converted = null;
            error = null;

            if (string.IsNullOrWhiteSpace(rawText))
            {
                error = "检索内容不能为空。";
                return false;
            }

            if (targetType == typeof(string))
            {
                converted = rawText;
                return true;
            }

            if (targetType == typeof(int))
            {
                if (int.TryParse(rawText, out int value))
                {
                    converted = value;
                    return true;
                }
            }
            else if (targetType == typeof(uint))
            {
                if (uint.TryParse(rawText, out uint value))
                {
                    converted = value;
                    return true;
                }
            }
            else if (targetType == typeof(long))
            {
                if (long.TryParse(rawText, out long value))
                {
                    converted = value;
                    return true;
                }
            }
            else if (targetType == typeof(float))
            {
                if (float.TryParse(rawText, out float value))
                {
                    converted = value;
                    return true;
                }
            }
            else if (targetType.IsEnum)
            {
                try
                {
                    converted = Enum.Parse(targetType, rawText, true);
                    return true;
                }
                catch
                {
                    // Ignore.
                }
            }

            error = $"无法解析为 {targetType.Name}: {rawText}";
            return false;
        }

        private static bool TryConvertValue(XDataTableEditorColumn column, object editorValue, out object converted, out string error)
        {
            Type targetType = column.Field.FieldType;
            converted = null;
            error = null;

            if (column.IsList)
            {
                if (editorValue == null)
                {
                    converted = Activator.CreateInstance(targetType);
                    return true;
                }

                if (targetType.IsInstanceOfType(editorValue))
                {
                    converted = editorValue;
                    return true;
                }

                error = $"无法将值写入列表字段 {column.Field.Name}";
                return false;
            }

            if (column.IsAssetPath)
            {
                if (editorValue == null)
                {
                    converted = string.Empty;
                    return true;
                }

                if (editorValue is string path)
                {
                    converted = path;
                    return true;
                }

                if (editorValue is Object unityObject)
                {
                    if (column.AssetType != null && !column.AssetType.IsInstanceOfType(unityObject))
                    {
                        error = $"资源类型不匹配，期望 {column.AssetType.Name}";
                        return false;
                    }

                    string assetPath = AssetDatabase.GetAssetPath(unityObject);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        error = "只支持 Project 资源，不支持场景对象。";
                        return false;
                    }

                    converted = assetPath;
                    return true;
                }

                error = "资源字段只接受 ObjectField 选择结果。";
                return false;
            }

            if (targetType == typeof(string))
            {
                converted = editorValue as string ?? string.Empty;
                return true;
            }

            if (targetType == typeof(int))
            {
                if (editorValue is int intValue)
                {
                    converted = intValue;
                    return true;
                }
            }
            else if (targetType == typeof(uint))
            {
                if (editorValue is long longValue && longValue >= 0 && longValue <= uint.MaxValue)
                {
                    converted = (uint)longValue;
                    return true;
                }

                if (editorValue is int intValue && intValue >= 0)
                {
                    converted = (uint)intValue;
                    return true;
                }
            }
            else if (targetType == typeof(float))
            {
                if (editorValue is float floatValue)
                {
                    converted = floatValue;
                    return true;
                }
            }
            else if (targetType == typeof(bool))
            {
                if (editorValue is bool boolValue)
                {
                    converted = boolValue;
                    return true;
                }
            }
            else if (targetType.IsEnum)
            {
                if (editorValue != null && editorValue.GetType() == targetType)
                {
                    converted = editorValue;
                    return true;
                }

                if (editorValue is string enumString)
                {
                    try
                    {
                        converted = Enum.Parse(targetType, enumString, true);
                        return true;
                    }
                    catch
                    {
                        // Ignore.
                    }
                }
            }

            error = $"无法将值写入字段 {column.Field.Name}";
            return false;
        }

        private void ValidateDuplicates(
            PropertyInfo property,
            XDataTableEditorColumn column,
            string message,
            ICollection<XDataTableEditorIssue> issues,
            Func<object, bool> predicate = null)
        {
            var visited = new Dictionary<object, int>();
            for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
            {
                object value = property.GetValue(Rows[rowIndex]);
                if (predicate != null && !predicate(value))
                {
                    continue;
                }

                if (value == null)
                {
                    continue;
                }

                if (visited.TryGetValue(value, out int existingRow))
                {
                    if (!issues.Any(issue => issue.RowIndex == existingRow && issue.Column == column && issue.Message.Contains(message)))
                    {
                        issues.Add(new XDataTableEditorIssue(existingRow, column, $"{message}: {value}", true));
                    }

                    issues.Add(new XDataTableEditorIssue(rowIndex, column, $"{message}: {value}", true));
                }
                else
                {
                    visited.Add(value, rowIndex);
                }
            }
        }

        private object CreateDefaultRow()
        {
            return DataType.IsValueType
                ? Activator.CreateInstance(DataType)
                : Activator.CreateInstance(DataType, true);
        }

        private object CloneRow(object source)
        {
            object clone = CreateDefaultRow();
            foreach (XDataTableEditorColumn column in Columns)
            {
                object value = column.Field.GetValue(source);
                column.Field.SetValue(clone, CloneValue(value, column.Field.FieldType));
            }

            return clone;
        }

        private static object CloneValue(object value, Type fieldType)
        {
            if (value == null)
            {
                return null;
            }

            if (fieldType == typeof(string) || fieldType.IsValueType || typeof(Object).IsAssignableFrom(fieldType))
            {
                return value;
            }

            if (typeof(IList).IsAssignableFrom(fieldType))
            {
                IList sourceList = value as IList;
                IList clonedList = Activator.CreateInstance(fieldType) as IList;
                if (sourceList == null || clonedList == null)
                {
                    return value;
                }

                foreach (object item in sourceList)
                {
                    clonedList.Add(item);
                }

                return clonedList;
            }

            return value;
        }

        private bool IsValidRowIndex(int rowIndex)
        {
            return rowIndex >= 0 && rowIndex < Rows.Count;
        }

        public static string FormatValue(object value, XDataTableEditorColumn column)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (column.IsAssetPath)
            {
                string path = value as string;
                return string.IsNullOrEmpty(path) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(path);
            }

            if (column.IsList && value is IList list)
            {
                if (list.Count == 0)
                {
                    return "[]";
                }

                IEnumerable<string> preview = list.Cast<object>().Take(3).Select(item => item?.ToString() ?? "null");
                string suffix = list.Count > 3 ? ", ..." : string.Empty;
                return $"[{string.Join(", ", preview)}{suffix}] ({list.Count})";
            }

            if (value is string text)
            {
                string singleLine = text.Replace("\r", " ").Replace("\n", " ");
                return singleLine.Length > 40 ? $"{singleLine[..40]}..." : singleLine;
            }

            return value.ToString();
        }
    }

    internal sealed class XDataTableEditorColumn
    {
        public XDataTableEditorColumn(
            int index,
            FieldInfo field,
            AssetPathAttribute assetPathAttribute,
            bool isList,
            Type listElementType,
            bool isKey,
            bool isAlias)
        {
            Index = index;
            Field = field;
            AssetPathAttribute = assetPathAttribute;
            IsList = isList;
            ListElementType = listElementType;
            IsKey = isKey;
            IsAlias = isAlias;
            Header = field.Name;
            AssetType = assetPathAttribute?.targetType;
            InlineKind = ResolveInlineKind();
            Width = ResolveWidth();
        }

        public int Index { get; }
        public FieldInfo Field { get; }
        public string Header { get; }
        public AssetPathAttribute AssetPathAttribute { get; }
        public Type AssetType { get; }
        public bool IsList { get; }
        public Type ListElementType { get; }
        public bool IsKey { get; }
        public bool IsAlias { get; }
        public XDataTableEditorInlineKind InlineKind { get; }
        public float Width { get; }

        public bool IsAssetPath => AssetPathAttribute != null;

        public bool SupportsInlineEdit =>
            InlineKind is XDataTableEditorInlineKind.Text
                or XDataTableEditorInlineKind.Integer
                or XDataTableEditorInlineKind.UnsignedInteger
                or XDataTableEditorInlineKind.Float
                or XDataTableEditorInlineKind.Toggle
                or XDataTableEditorInlineKind.Enum
                or XDataTableEditorInlineKind.AssetPath;

        private XDataTableEditorInlineKind ResolveInlineKind()
        {
            Type fieldType = Field.FieldType;
            if (IsList)
            {
                return XDataTableEditorInlineKind.Summary;
            }

            if (IsAssetPath)
            {
                return XDataTableEditorInlineKind.AssetPath;
            }

            if (fieldType == typeof(string))
            {
                return XDataTableEditorInlineKind.Text;
            }

            if (fieldType == typeof(int))
            {
                return XDataTableEditorInlineKind.Integer;
            }

            if (fieldType == typeof(uint))
            {
                return XDataTableEditorInlineKind.UnsignedInteger;
            }

            if (fieldType == typeof(float))
            {
                return XDataTableEditorInlineKind.Float;
            }

            if (fieldType == typeof(bool))
            {
                return XDataTableEditorInlineKind.Toggle;
            }

            if (fieldType.IsEnum)
            {
                return XDataTableEditorInlineKind.Enum;
            }

            return XDataTableEditorInlineKind.Summary;
        }

        private float ResolveWidth()
        {
            if (IsKey)
            {
                return 90f;
            }

            if (IsAlias)
            {
                return 130f;
            }

            if (InlineKind == XDataTableEditorInlineKind.Toggle)
            {
                return 72f;
            }

            if (InlineKind == XDataTableEditorInlineKind.Integer
                || InlineKind == XDataTableEditorInlineKind.UnsignedInteger
                || InlineKind == XDataTableEditorInlineKind.Float)
            {
                return 110f;
            }

            if (InlineKind == XDataTableEditorInlineKind.Enum)
            {
                return 130f;
            }

            if (IsAssetPath)
            {
                return 220f;
            }

            if (IsList)
            {
                return 170f;
            }

            return 180f;
        }
    }

    internal sealed class XDataTableEditorIssue
    {
        public XDataTableEditorIssue(int rowIndex, XDataTableEditorColumn column, string message, bool blocking)
        {
            RowIndex = rowIndex;
            Column = column;
            Message = message;
            Blocking = blocking;
        }

        public int RowIndex { get; }
        public XDataTableEditorColumn Column { get; }
        public string Message { get; }
        public bool Blocking { get; }
    }

    internal sealed class XDataTableEditorValidationResult
    {
        public XDataTableEditorValidationResult(List<XDataTableEditorIssue> issues)
        {
            Issues = issues ?? new List<XDataTableEditorIssue>();
        }

        public List<XDataTableEditorIssue> Issues { get; }
        public bool HasBlockingIssues => Issues.Any(issue => issue.Blocking);

        public int GetIssueCount(int rowIndex, XDataTableEditorColumn column)
        {
            return Issues.Count(issue => issue.RowIndex == rowIndex && issue.Column == column);
        }

        public string GetIssueMessage(int rowIndex, XDataTableEditorColumn column)
        {
            return string.Join("\n", Issues
                .Where(issue => issue.RowIndex == rowIndex && issue.Column == column)
                .Select(issue => issue.Message));
        }
    }
}
