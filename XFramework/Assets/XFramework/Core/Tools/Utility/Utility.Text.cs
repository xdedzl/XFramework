using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public static partial class Utility
    {
        public static class Text
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="path"></param>
            /// <returns></returns>
            public static string[] SplitPathName(string path)
            {
                // "[^/]{1,}$"

                int index = -1;
                for (int i = path.Length - 1; i >= 0; i--)
                {
                    if (path[i] == '/')
                    {
                        index = i;
                        break;
                    }
                }
                if(index != -1)
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
        }
    }
}