using System;
using Newtonsoft.Json;
using UnityEngine;
using XFramework.Json;

namespace XFramework.Resource
{
    public struct XTextMetaInfo
    {
        // public string typeFullName;
        public string assetPath;
    }
    
    public class XTextAsset
    {
        [JsonProperty]
        private XTextMetaInfo m_MetaInfo;
        
        public string Serialize()
        {
            m_MetaInfo = new XTextMetaInfo
            {
                // typeFullName = GetType().AssemblyQualifiedName,
                assetPath = m_MetaInfo.assetPath
            };
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
        
#if UNITY_EDITOR
        public void SetAssetPath(string path)
        {
            m_MetaInfo = new XTextMetaInfo
            {
                // typeFullName = m_MetaInfo.typeFullName,
                assetPath = path
            };
        }

        public void SaveAsset()
        {
            string json = Serialize();
            string path = m_MetaInfo.assetPath;
            if (string.IsNullOrEmpty(path))
            {
                throw new Exception("Asset path is not set. Please call SetAssetPath before saving.");
            }
            System.IO.File.WriteAllText(path, json);
            UnityEditor.AssetDatabase.Refresh();
        }
#endif
    }
    

    public static class XTextUtility
    {
        public static T ToXTextAsset<T>(this TextAsset textAsset) where T : XTextAsset
        {
            var asset = JsonConvert.DeserializeObject<T>(textAsset.text);
#if UNITY_EDITOR
            if (asset != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(textAsset);
                asset.SetAssetPath(path);
            }
#endif
            return asset;
        }

        public static T ToXTextAsset<T>(this TextAsset textAsset, Type type) where T : XTextAsset
        {
            var _asset = JsonConvert.DeserializeObject(textAsset.text, type);
            var asset = _asset as T;
#if UNITY_EDITOR
            if (asset != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(textAsset);
                asset.SetAssetPath(path);
            }
#endif
            return asset;
        }
    }
}