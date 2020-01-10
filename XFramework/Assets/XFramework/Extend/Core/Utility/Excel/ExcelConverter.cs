using System;

namespace XFramework.Excel
{
    public abstract class ExcelConverter
    {
        public abstract object ReadExcel(string value);
    }

    public class ExcelConverterAttribute : Attribute
    {
        public Type type;

        public ExcelConverterAttribute(Type type)
        {
            if (!type.IsSubclassOf(typeof(ExcelConverter)))
            {
                throw new Exception($"{type.Name}不是ExcelConverter的派生类");
            }

            this.type = type;
        }
    }
}