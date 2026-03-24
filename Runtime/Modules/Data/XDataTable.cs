using System;
using XFramework.Resource;

namespace XFramework.Data
{
    public abstract partial class XDataTable : XTextAsset
    {

    }
    
    [Serializable]
    public abstract class XDataTable<T> : XDataTable where T : IData
    {
        public T[] items;
    }
}
