using System;
using System.Data;
using System.IO;
using System.Reflection;
using ExcelDataReader;
using UnityEngine;

namespace XFramework.Excel
{
    public class ExcelConvert
    {
        public static T[] DeserializeObject<T>(string xlsxPath)
        {
            var objs = DeserializeObject(typeof(T), xlsxPath);

            T[] values = new T[objs.Length];

            for (int i = 0; i < objs.Length; i++)
            {
                values[i] = (T)objs[i];
            }

            return values;
        }

        /// <summary>
        /// 读取一个Excel文件
        /// </summary>
        /// <param name="xlsxPath"></param>
        public static object[] DeserializeObject(Type type, string xlsxPath)
        {
            if (!xlsxPath.EndsWith(".xlsx"))
            {
                throw new Exception(xlsxPath + "不是Excel文件");
            }

            using (var stream = File.Open(xlsxPath, FileMode.Open, FileAccess.Read))
            {
                using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
                {
                    DataSet result = reader.AsDataSet();

                    DataTableCollection tc = result.Tables;

                    if (tc.Count <= 0)
                        throw new Exception("没有表格");

                    if (tc.Count != 1)
                    {
                        Debug.LogWarning("一个文件只能对应一个表格");
                    }
                    return ReadSheet(type, result.Tables[0]);
                }
            }
        }

        /// <summary>
        /// 读取Excel中的一张表
        /// </summary>
        /// <param name="dataTable"></param>
        /// <param name="jsonPath"></param>
        private static object[] ReadSheet(Type type, DataTable dataTable)
        {
            int row = dataTable.Rows.Count;
            int column = dataTable.Columns.Count;

            DataRowCollection collect = dataTable.Rows;
            string[] filedNames = new string[column];

            for (int i = 0; i < column; i++)
            {
                filedNames[i] = collect[0][i].ToString();
            }

            object[] objs = new object[row - 2];

            for (int i = 2; i < row; i++)
            {
                object obj = Activator.CreateInstance(type);

                for (int j = 0; j < column; j++)
                {
                    FieldInfo field = type.GetField(filedNames[j]);
                    if (field != null)
                    {
                        string value = collect[i][j].ToString();

                        var excelConverter = field.GetCustomAttribute<ExcelConverterAttribute>();
                        if (excelConverter != null)         // 有自定义序列化方式
                        {
                            var converter = Activator.CreateInstance(excelConverter.type) as ExcelConverter;
                            field.SetValue(obj, converter.ReadExcel(value));
                        }
                        else if (field.FieldType.IsEnum)    // 是枚举
                        {
                            field.SetValue(obj, int.Parse(value));
                        }
                        else                                // 基础类型
                        {
                            switch (field.FieldType.Name)
                            {
                                case "Int32":
                                    field.SetValue(obj, int.Parse(value));
                                    break;
                                case "Single":
                                    field.SetValue(obj, float.Parse(value));
                                    break;
                                case "Double":
                                    field.SetValue(obj, double.Parse(value));
                                    break;
                                case "Boolean":
                                    field.SetValue(obj, bool.Parse(value));
                                    break;
                                case "String":
                                    field.SetValue(obj, value);
                                    break;
                                case "String[]":
                                    field.SetValue(obj, value.Split('\n'));
                                    break;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("字符串和变量名不匹配");
                    }
                }
                objs[i - 2] = obj;
            }

            return objs;
        }
    }
}