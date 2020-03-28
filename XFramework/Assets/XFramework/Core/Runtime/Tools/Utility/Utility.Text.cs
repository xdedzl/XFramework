namespace XFramework
{
    public static partial class Utility
    {
        /// <summary>
        /// 文本相关工具
        /// </summary>
        public static class Text
        {
            /// <summary>
            /// 将文件完整路径分割成路径和文件名  a/b/c/d.txt  => a/b/c,d.txt
            /// </summary>
            /// <param name="path"></param>
            /// <returns></returns>
            public static string[] SplitPathName(string path)
            {
                // "[^/]{1,}$"

                int index = -1;
                for (int i = path.Length - 1; i >= 0; i--)
                {
                    if (path[i] == '/' || path[i] == '\\')
                    {
                        index = i;
                        break;
                    }
                }
                if (index != -1)
                {
                    return new string[]
                    {
                        path.Substring(0, index),
                        path.Substring(index + 1, path.Length - index - 1),
                    };
                }
                else
                {
                    return new string[]
                    {
                        "",
                        path,
                    };
                }
            }

            /// <summary>
            /// 从orgStr截取value后面的字符串
            /// </summary>
            /// <param name="orgStr">原字符串</param>
            /// <param name="value">截取点</param>
            /// <param name="includeSelf">是否截取value</param>
            /// <returns></returns>
            public static string GetAfterStr(string orgStr, string value, bool includeSelf = true)
            {
                int index = orgStr.IndexOf(value);

                if (index < 0)
                {
                    throw new System.Exception($"{orgStr}中不包含{value}");
                }

                if (includeSelf)
                {
                    return orgStr.Substring(index, orgStr.Length - index);
                }
                else
                {
                    return orgStr.Substring(index, orgStr.Length - index + value.Length);
                }
            }

            /// <summary>
            /// 去除后缀名
            /// </summary>
            /// <param name="name"></param>
            /// <returns></returns>
            public static string RemoveSuffix(string name)
            {
                string[] strs = name.Split('.');
                if (strs.Length != 2)
                {
                    throw new System.Exception("格式不正确");
                }
                return strs[0];
            }
        }
    }
}