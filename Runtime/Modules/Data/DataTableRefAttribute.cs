using System;
using UnityEngine;

namespace XFramework
{
    public class DataTableRefAttribute : PropertyAttribute
    {
        public readonly Type tableType;

        public DataTableRefAttribute(Type tableType)
        {
            if (tableType == null)
            {
                throw new ArgumentNullException(nameof(tableType));
            }

            this.tableType = tableType;
        }
    }
}

