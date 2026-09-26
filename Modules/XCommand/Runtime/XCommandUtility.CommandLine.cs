using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Command
{
    public static partial class XCommandUtility
    {
        /// <summary>
        /// CLI 命令参数工具。
        /// </summary>
        public static class CommandLine
        {
            public static XCommandArguments Parse(string commandLine, string[] valueOptions, string[] flagOptions)
            {
                var result = new XCommandArguments();
                var valueOptionSet = new HashSet<string>(valueOptions, StringComparer.Ordinal);
                var flagOptionSet = new HashSet<string>(flagOptions, StringComparer.Ordinal);
                List<string> tokens = Tokenize(commandLine ?? string.Empty);
                for (int i = 0; i < tokens.Count; i++)
                {
                    string token = tokens[i];
                    if (!token.StartsWith("--", StringComparison.Ordinal))
                    {
                        result.Values.Add(token);
                        continue;
                    }
                    string option = token.Substring(2);
                    int equalsIndex = option.IndexOf('=');
                    string name = equalsIndex >= 0 ? option.Substring(0, equalsIndex) : option;
                    if (flagOptionSet.Contains(name))
                    {
                        if (equalsIndex >= 0)
                            throw new ArgumentException($"标记 --{name} 不接受值。");
                        result.AddFlag(name);
                        continue;
                    }
                    if (!valueOptionSet.Contains(name))
                        throw new ArgumentException($"未知选项：--{name}");
                    string value;
                    if (equalsIndex >= 0)
                        value = option.Substring(equalsIndex + 1);
                    else if (++i < tokens.Count && !tokens[i].StartsWith("--", StringComparison.Ordinal))
                        value = tokens[i];
                    else
                        throw new ArgumentException($"选项 --{name} 缺少值。");
                    result.SetOption(name, value);
                }
                return result;
            }

            private static List<string> Tokenize(string value)
            {
                var tokens = new List<string>();
                int index = 0;
                while (index < value.Length)
                {
                    while (index < value.Length && char.IsWhiteSpace(value[index]))
                        index++;
                    if (index >= value.Length)
                        break;
                    var builder = new StringBuilder();
                    char quote = '\0';
                    while (index < value.Length)
                    {
                        char character = value[index];
                        if (quote == '\0' && char.IsWhiteSpace(character))
                            break;
                        if (character == '\'' || character == '"')
                        {
                            if (quote == '\0')
                            {
                                quote = character;
                                index++;
                                continue;
                            }
                            if (quote == character)
                            {
                                quote = '\0';
                                index++;
                                continue;
                            }
                        }
                        if (character == '\\' && index + 1 < value.Length && quote != '\0' && (value[index + 1] == quote || value[index + 1] == '\\'))
                        {
                            builder.Append(value[index + 1]);
                            index += 2;
                            continue;
                        }
                        builder.Append(character);
                        index++;
                    }
                    if (quote != '\0')
                        throw new ArgumentException("命令参数包含未闭合的引号。");
                    tokens.Add(builder.ToString());
                }
                return tokens;
            }
        }
    }
}
