using System.Collections.Generic;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace XFramework.NodeKit
{
    public class ValueNode<T> : XNodeBase, IValueNode<T>
    {
        public T value;
        public T GetValue()
        {
            return value;
        }
    }
    
    public class IntValueNode : ValueNode<int> {}
    
    public class FloatValueNode : ValueNode<float> {}

    public class BoolValueNode : ValueNode<bool> {}
    
    public class StringValueNode : ValueNode<string> {}

    public class UObjectValueNode : XNodeBase, IValueNode<UObject>
    {
        [InspectorName("查找Key")]
        public string key;

        public UObject GetValue()
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return XFramework.UObjectFinder.Find(key);
        }
    }
}
