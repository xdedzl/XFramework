using Newtonsoft.Json;

namespace XFramework.Json
{
    public static class XJson
    {
        public static void SetUnityDefaultSetting()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = 
                { 
                    new Vector2Converter(), new Vector2IntConverter(), 
                    new Vector3Converter(), new Vector3IntConverter(),
                    new QuaternionConverter(),
                    new ColorConverter(),
                },
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new WritablePropertiesOnlyResolver()
            };

        }
    }
}
