using UnityEngine;

namespace XFramework.Resource
{
    public interface IResourceLoadHelper
    {
        string AssetPath { get; }
        LoadMode LoadMode { get; }
        T Load<T>(string assetName) where T : Object;
        T[] LoadAll<T>(string path) where T : Object;
        IProgress LoadAsync<T>(string assetName, System.Action<T> callback) where T : Object;
        void UnLoad(string name);
        void UnLoadAll();
    }

    public enum LoadMode
    {
        AssetBundle,
        AssetDataBase,
    }
}