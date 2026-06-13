using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 使字段在同一排显示。
    /// </summary>
    public class ShowInRowAttribute : PropertyAttribute
    {
        public readonly string[] FieldNames;

        /// <summary>
        /// 使字段在同一排显示。
        /// </summary>
        /// <param name="fieldNames">要显示的字段名</param>
        public ShowInRowAttribute(string[] fieldNames)
        {
            FieldNames = fieldNames;
        }
    }
}
