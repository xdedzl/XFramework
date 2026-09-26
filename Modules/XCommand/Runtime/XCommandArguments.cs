using System;
using System.Collections.Generic;
using System.Globalization;

namespace XFramework.Command
{
    public sealed class XCommandArguments
    {
        private readonly Dictionary<string, string> m_Options = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> m_Flags = new HashSet<string>(StringComparer.Ordinal);

        public List<string> Values { get; } = new List<string>();

        public static XCommandArguments Parse(string commandLine, string[] valueOptions, string[] flagOptions)
        {
            return XCommandUtility.CommandLine.Parse(commandLine, valueOptions, flagOptions);
        }

        internal void AddFlag(string name)
        {
            m_Flags.Add(name);
        }

        internal void SetOption(string name, string value)
        {
            m_Options[name] = value;
        }

        public bool HasFlag(string name)
        {
            return m_Flags.Contains(name);
        }

        public string GetString(string name, string defaultValue = null)
        {
            return m_Options.TryGetValue(name, out string value) ? value : defaultValue;
        }

        public int GetInt(string name, int defaultValue)
        {
            string value = GetString(name);
            if (value == null)
                return defaultValue;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new ArgumentException($"--{name} 不是有效整数：{value}");
            return result;
        }

        public long GetLong(string name, long defaultValue)
        {
            string value = GetString(name);
            if (value == null)
                return defaultValue;
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
                throw new ArgumentException($"--{name} 不是有效整数：{value}");
            return result;
        }

        public float GetFloat(string name, float defaultValue)
        {
            string value = GetString(name);
            if (value == null)
                return defaultValue;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                throw new ArgumentException($"--{name} 不是有效数字：{value}");
            return result;
        }

        public void RequireValueCount(int count, string error)
        {
            if (Values.Count != count)
                throw new ArgumentException(error);
        }

    }
}
