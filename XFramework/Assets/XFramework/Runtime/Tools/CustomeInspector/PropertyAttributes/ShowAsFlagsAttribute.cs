using System;
using UnityEngine;

/// <summary>
/// 将枚举以位掩码的形式显示在Inspector上。
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ShowAsFlagsAttribute : PropertyAttribute
{
}
