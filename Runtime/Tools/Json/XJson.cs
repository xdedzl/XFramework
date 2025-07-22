using Newtonsoft.Json;

namespace XFramework.Json
{
    public static class XJson
    {
        public static void SetUnityDefaultSetting()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = { new Vector2IntConverter(), new Vector2Converter() },
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new WritablePropertiesOnlyResolver()
            };

        }
    }
}
