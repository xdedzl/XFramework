using System;

namespace XFramework.Resource
{
    [Serializable]
    public struct Asset2AB
    {
        public string assetPath;
        public string abName;
    } 
    
    public class AssetManifest
    {
        public SingleDependenciesData[] dependencies;
        public Asset2AB[] asset2Abs;
    }
}