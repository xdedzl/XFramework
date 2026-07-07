using System;
using UnityEngine;

namespace XFramework
{
    public class PolymorphicListAttribute : PropertyAttribute
    {
        public readonly Type[] types;

        public PolymorphicListAttribute(params Type[] types) : base(true)
        {
            this.types = types ?? Type.EmptyTypes;
        }
    }
}
