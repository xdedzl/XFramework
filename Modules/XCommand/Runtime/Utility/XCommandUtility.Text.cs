using System;
using System.Globalization;
using System.Text;

namespace XFramework.Command
{
    public static partial class XCommandUtility
    {
        public static class Text
        {
            public static bool ContainsIgnoreCase(string value, string expected)
            {
                return string.IsNullOrEmpty(expected) || (!string.IsNullOrEmpty(value) && value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            public static string Unescape(string value)
            {
                if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                    return value;

                var builder = new StringBuilder(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c != '\\' || i + 1 >= value.Length)
                    {
                        builder.Append(c);
                        continue;
                    }

                    char next = value[i + 1];
                    switch (next)
                    {
                        case 'u':
                            if (i + 5 < value.Length && ushort.TryParse(value.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code))
                            {
                                builder.Append((char)code);
                                i += 5;
                            }
                            else
                            {
                                builder.Append(next);
                                i++;
                            }
                            break;
                        case 'n':
                            builder.Append('\n');
                            i++;
                            break;
                        case 'r':
                            builder.Append('\r');
                            i++;
                            break;
                        case 't':
                            builder.Append('\t');
                            i++;
                            break;
                        case '"':
                            builder.Append('"');
                            i++;
                            break;
                        case '\\':
                            builder.Append('\\');
                            i++;
                            break;
                        default:
                            builder.Append(next);
                            i++;
                            break;
                    }
                }

                return builder.ToString();
            }
        }
    }
}
