using System;
using System.Collections.Generic;
using System.Globalization;

namespace XFramework.Animation
{
    public sealed class XAnimationContext
    {
        private readonly Dictionary<string, XAnimationParameterType> m_ParameterTypes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_ParameterIndices = new(StringComparer.Ordinal);
        private readonly XAnimationParameterType[] m_Types;
        private readonly float[] m_FloatValues;
        private readonly int[] m_IntValues;
        private readonly bool[] m_BoolValues;
        private readonly bool[] m_TriggerValues;

        public XAnimationContext(IReadOnlyList<XAnimationCompiledParameter> parameters)
        {
            int parameterCount = parameters?.Count ?? 0;
            m_Types = new XAnimationParameterType[parameterCount];
            m_FloatValues = new float[parameterCount];
            m_IntValues = new int[parameterCount];
            m_BoolValues = new bool[parameterCount];
            m_TriggerValues = new bool[parameterCount];

            if (parameterCount == 0)
            {
                return;
            }

            foreach (XAnimationCompiledParameter parameter in parameters)
            {
                string parameterName = parameter.Name;
                int parameterIndex = parameter.Index;
                m_ParameterTypes[parameterName] = parameter.Type;
                m_ParameterIndices[parameterName] = parameterIndex;
                m_Types[parameterIndex] = parameter.Type;
                switch (parameter.Type)
                {
                    case XAnimationParameterType.Float:
                        m_FloatValues[parameterIndex] = ConvertToFloat(parameter.Config.defaultValue);
                        break;
                    case XAnimationParameterType.Int:
                        m_IntValues[parameterIndex] = ConvertToInt(parameter.Config.defaultValue);
                        break;
                    case XAnimationParameterType.Bool:
                        m_BoolValues[parameterIndex] = ConvertToBool(parameter.Config.defaultValue);
                        break;
                    case XAnimationParameterType.Trigger:
                        m_TriggerValues[parameterIndex] = false;
                        break;
                    default:
                        throw new XAnimationException($"Unsupported XAnimation parameter type '{parameter.Type}'.");
                }
            }
        }

        public void SetFloat(string key, float value)
        {
            int index = EnsureType(key, XAnimationParameterType.Float);
            m_FloatValues[index] = value;
        }

        public void SetBool(string key, bool value)
        {
            int index = EnsureType(key, XAnimationParameterType.Bool);
            m_BoolValues[index] = value;
        }

        public void SetInt(string key, int value)
        {
            int index = EnsureType(key, XAnimationParameterType.Int);
            m_IntValues[index] = value;
        }

        public void SetTrigger(string key)
        {
            int index = EnsureType(key, XAnimationParameterType.Trigger);
            m_TriggerValues[index] = true;
        }

        public void ResetTrigger(string key)
        {
            int index = EnsureType(key, XAnimationParameterType.Trigger);
            m_TriggerValues[index] = false;
        }

        public bool TryGetFloat(string key, out float value)
        {
            value = 0f;
            return m_ParameterIndices.TryGetValue(key, out int index) && TryGetFloat(index, out value);
        }

        public bool TryGetBool(string key, out bool value)
        {
            value = false;
            return m_ParameterIndices.TryGetValue(key, out int index) && TryGetBool(index, out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            return m_ParameterIndices.TryGetValue(key, out int index) && TryGetInt(index, out value);
        }

        public bool TryGetTrigger(string key, out bool value)
        {
            value = false;
            return m_ParameterIndices.TryGetValue(key, out int index) && TryGetTrigger(index, out value);
        }

        public bool TryGetFloat(int index, out float value)
        {
            value = 0f;
            if (!IsValidIndex(index) || m_Types[index] != XAnimationParameterType.Float)
            {
                return false;
            }

            value = m_FloatValues[index];
            return true;
        }

        public bool TryGetBool(int index, out bool value)
        {
            value = false;
            if (!IsValidIndex(index) || m_Types[index] != XAnimationParameterType.Bool)
            {
                return false;
            }

            value = m_BoolValues[index];
            return true;
        }

        public bool TryGetInt(int index, out int value)
        {
            value = 0;
            if (!IsValidIndex(index) || m_Types[index] != XAnimationParameterType.Int)
            {
                return false;
            }

            value = m_IntValues[index];
            return true;
        }

        public bool TryGetTrigger(int index, out bool value)
        {
            value = false;
            if (!IsValidIndex(index) || m_Types[index] != XAnimationParameterType.Trigger)
            {
                return false;
            }

            value = m_TriggerValues[index];
            return true;
        }

        public bool ConsumeTrigger(string key)
        {
            int index = EnsureType(key, XAnimationParameterType.Trigger);
            bool value = m_TriggerValues[index];
            m_TriggerValues[index] = false;
            return value;
        }

        private int EnsureType(string key, XAnimationParameterType type)
        {
            if (!m_ParameterTypes.TryGetValue(key, out XAnimationParameterType actualType))
            {
                throw new XAnimationException($"XAnimation parameter '{key}' does not exist.");
            }

            if (actualType != type)
            {
                throw new XAnimationException($"XAnimation parameter '{key}' expects {actualType}, but received {type}.");
            }

            return m_ParameterIndices[key];
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < m_Types.Length;
        }

        private static float ConvertToFloat(object value)
        {
            if (value == null)
            {
                return 0f;
            }

            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        private static int ConvertToInt(object value)
        {
            if (value == null)
            {
                return 0;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static bool ConvertToBool(object value)
        {
            if (value == null)
            {
                return false;
            }

            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
    }
}
