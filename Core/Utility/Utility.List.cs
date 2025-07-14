using System;
using System.Collections.Generic;
using System.Linq;

namespace XFramework
{
    public partial class Utility
    {
        public static class List
        {
            public static bool IsValidList<T>(IList<T> list, int validCount=1)
            {
                return list is not null && list.Count >= validCount;
            }
        }
    }
}
