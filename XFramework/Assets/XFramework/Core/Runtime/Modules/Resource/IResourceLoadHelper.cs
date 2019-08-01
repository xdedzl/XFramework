using UnityEngine;

namespace XFramework.Resource
{
    public interface IResourceLoadHelper
    {
        T Load<T>(string assetName) where T : Object;
        T[] LoadAll<T>(string path) where T : Object;
        IProgress LoadAsync<T>(string assetName, System.Action<T> callback) where T : Object;
        string AssetPath { get; }
    }
}