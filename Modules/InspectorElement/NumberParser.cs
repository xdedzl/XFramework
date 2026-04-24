using System;
using UnityEngine;

/// <summary>
/// 数字转换辅助类
/// </summary>
public readonly struct NumberParser
{
    private delegate bool ParseFunc(string input, out object value);
    private delegate bool EqualsFunc(object value1, object value2);

    private readonly ParseFunc parseFunction;
    private readonly EqualsFunc equalsFunction;

    public NumberParser(Type fieldType)
    {
        // 整型
        if (fieldType == typeof(int))
        {
            parseFunction = (string input, out object value) => { bool result = int.TryParse(input, out int parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (int)value1 == (int)value2;
        }
        else if (fieldType == typeof(uint))
        {
            parseFunction = (string input, out object value) => { bool result = uint.TryParse(input, out uint parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (uint)value1 == (uint)value2;
        }
        else if (fieldType == typeof(long))
        {
            parseFunction = (string input, out object value) => { bool result = long.TryParse(input, out long parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (long)value1 == (long)value2;
        }
        else if (fieldType == typeof(ulong))
        {
            parseFunction = (string input, out object value) => { bool result = ulong.TryParse(input, out ulong parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (ulong)value1 == (ulong)value2;
        }
        else if (fieldType == typeof(byte))
        {
            parseFunction = (string input, out object value) => { bool result = byte.TryParse(input, out byte parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (byte)value1 == (byte)value2;
        }
        else if (fieldType == typeof(sbyte))
        {
            parseFunction = (string input, out object value) => { bool result = sbyte.TryParse(input, out sbyte parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (sbyte)value1 == (sbyte)value2;
        }
        else if (fieldType == typeof(short))
        {
            parseFunction = (string input, out object value) => { bool result = short.TryParse(input, out short parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (short)value1 == (short)value2;
        }
        else if (fieldType == typeof(ushort))
        {
            parseFunction = (string input, out object value) => { bool result = ushort.TryParse(input, out ushort parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (ushort)value1 == (ushort)value2;
        }
        else if (fieldType == typeof(char))
        {
            parseFunction = (string input, out object value) => { bool result = char.TryParse(input, out char parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (char)value1 == (char)value2;
        }

        // 浮点型
        else if (fieldType == typeof(float))
        {
            parseFunction = (string input, out object value) => { bool result = float.TryParse(input, out float parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => Mathf.Approximately((float)value1, (float)value2);
        }
        else if (fieldType == typeof(double))
        {
            parseFunction = (string input, out object value) => { bool result = double.TryParse(input, out double parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (double)value1 == (double)value2;
        }
        else if (fieldType == typeof(decimal))
        {
            parseFunction = (string input, out object value) => { bool result = decimal.TryParse(input, out decimal parsedVal); value = parsedVal; return result; };
            equalsFunction = (object value1, object value2) => (decimal)value1 == (decimal)value2;
        }
        else
        {
            throw new Exception("Type必须是数字类型");
        }
    }

    public bool TryParse(string input, out object value)
    {
        return parseFunction(input, out value);
    }

    public bool ValuesAreEqual(object value1, object value2)
    {
        return equalsFunction(value1, value2);
    }
}