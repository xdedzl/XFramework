using UnityEngine;
using XFramework.Data;

namespace XFramework.Data
{
    public abstract class DataScriptableObject<T> : ScriptableObject where T : IData
    {
        public T[] items;
    }
}
