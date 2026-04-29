using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using XFramework.Json;

namespace XFramework.Resource
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class XTextAssetAliasAttribute : Attribute
    {
        public XTextAssetAliasAttribute(string alias)
        {
            Alias = alias;
        }

        public string Alias { get; }
    }

    public struct XTextMetaInfo
    {
        public string typeAlias;
        public string assetPath;
    }
    
    public class XTextAsset
    {
        [JsonProperty]
        private XTextMetaInfo m_MetaInfo;
        
        public string Serialize()
        {
            XTextAssetAliasAttribute aliasAttribute = GetType().GetCustomAttribute<XTextAssetAliasAttribute>(true);
            m_MetaInfo = new XTextMetaInfo
            {
                typeAlias = aliasAttribute?.Alias ?? m_MetaInfo.typeAlias,
                assetPath = m_MetaInfo.assetPath
            };
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
        
#if UNITY_EDITOR
        public void SetAssetPath(string path)
        {
            m_MetaInfo = new XTextMetaInfo
            {
                typeAlias = m_MetaInfo.typeAlias,
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
        public static bool TryReadMetaInfo(string text, out XTextMetaInfo metaInfo)
        {
            metaInfo = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                JObject root = JObject.Parse(text);
                JToken token = root["m_MetaInfo"];
                if (token == null || token.Type != JTokenType.Object)
                {
                    return false;
                }

                XTextMetaInfo parsedInfo = token.ToObject<XTextMetaInfo>();
                metaInfo = parsedInfo;
                return !string.IsNullOrWhiteSpace(parsedInfo.typeAlias);
            }
            catch (JsonException)
            {
                return false;
            }
        }

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
