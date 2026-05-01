using System;
using System.Collections.Generic;
using System.Globalization;

namespace XFramework.Animation
{
    public sealed class XAnimationContext
    {
        private readonly Dictionary<string, XAnimationParameterType> m_ParameterTypes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> m_FloatValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_IntValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> m_BoolValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> m_TriggerValues = new(StringComparer.Ordinal);

        public XAnimationContext(IReadOnlyList<XAnimationCompiledParameter> parameters)
        {
            if (parameters == null)
            {
                return;
            }

            foreach (XAnimationCompiledParameter parameter in parameters)
            {
                string parameterName = parameter.Name;
                m_ParameterTypes[parameterName] = parameter.Type;
                switch (parameter.Type)
                {
                    case XAnimationParameterType.Float:
                        m_FloatValues[parameterName] = ConvertToFloat(parameter.Config.defaultValue);
                        break;
                    case XAnimationParameterType.Int:
                        m_IntValues[parameterName] = ConvertToInt(parameter.Config.defaultValue);
                        break;
                    case XAnimationParameterType.Bool:
                        m_BoolValues[parameterName] = ConvertToBool(parameter.Config.defaultValue);
                        break;
                    case XAnimationParameterType.Trigger:
                        m_TriggerValues[parameterName] = false;
                        break;
                    default:
                        throw new XFrameworkException($"Unsupported XAnimation parameter type '{parameter.Type}'.");
                }
            }
        }

        public void SetFloat(string key, float value)
        {
            EnsureType(key, XAnimationParameterType.Float);
            m_FloatValues[key] = value;
        }

        public void SetBool(string key, bool value)
        {
            EnsureType(key, XAnimationParameterType.Bool);
            m_BoolValues[key] = value;
        }

        public void SetInt(string key, int value)
        {
            EnsureType(key, XAnimationParameterType.Int);
            m_IntValues[key] = value;
        }

        public void SetTrigger(string key)
        {
            EnsureType(key, XAnimationParameterType.Trigger);
            m_TriggerValues[key] = true;
        }

        public void ResetTrigger(string key)
        {
            EnsureType(key, XAnimationParameterType.Trigger);
            m_TriggerValues[key] = false;
        }

        public bool TryGetFloat(string key, out float value)
        {
            return m_FloatValues.TryGetValue(key, out value);
        }

        public bool TryGetBool(string key, out bool value)
        {
            return m_BoolValues.TryGetValue(key, out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            return m_IntValues.TryGetValue(key, out value);
        }

        public bool ConsumeTrigger(string key)
        {
            EnsureType(key, XAnimationParameterType.Trigger);
            bool value = m_TriggerValues[key];
            m_TriggerValues[key] = false;
            return value;
        }

        private void EnsureType(string key, XAnimationParameterType type)
        {
            if (!m_ParameterTypes.TryGetValue(key, out XAnimationParameterType actualType))
            {
                throw new XFrameworkException($"XAnimation parameter '{key}' does not exist.");
            }

            if (actualType != type)
            {
                throw new XFrameworkException($"XAnimation parameter '{key}' expects {actualType}, but received {type}.");
            }
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
