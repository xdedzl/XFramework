using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace XFramework.JsonConvter
{
    /// <summary>
    /// 用于多态列表的转化
    /// 支持列表和数组
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PolyListConverter<T> : JsonConverter
    {
        private string nameSpace;

        public PolyListConverter() { }

        public PolyListConverter(string nameSpace = null)
        {
            this.nameSpace = nameSpace;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsArray || objectType.GetGenericTypeDefinition() == typeof(List<>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            List<T> values = new List<T>();

            foreach (var item in jObject.Properties())
            {
                Type type;
                if (string.IsNullOrEmpty(nameSpace))
                    type = Type.GetType(item.Name);
                else
                    type = Type.GetType($"{nameSpace}.{item.Name}");

                var value = item.Value.ToObject(type);

                values.Add((T)value);
            }
            if (objectType.IsArray)
                return values.ToArray();
            else
                return values;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            IList<T> values;
            if (value.GetType().IsArray)
                values = (T[])value;
            else
                values = (List<T>)value;

            JObject jObject = new JObject();

            foreach (var item in values)
            {
                string typeName = string.IsNullOrEmpty(nameSpace) ? item.GetType().FullName : item.GetType().Name;
                jObject.Add(typeName, JToken.FromObject(item));
            }

            serializer.Serialize(writer, jObject);
        }
    }

    /// <summary>
    /// 用于多态序列化
    /// </summary>
    public class PolyConverter : JsonConverter
    {
        private string nameSpace;

        public PolyConverter() { }

        public PolyConverter(string nameSpace = null)
        {
            this.nameSpace = nameSpace;
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            foreach (var item in jObject.Properties())
            {
                Type type;
                if (string.IsNullOrEmpty(nameSpace))
                    type = Type.GetType(item.Name);
                else
                    type = Type.GetType($"{nameSpace}.{item.Name}");

                var value = item.Value.ToObject(type);

                return value;
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JObject jObject = new JObject();
            string typeName = string.IsNullOrEmpty(nameSpace) ? value.GetType().FullName : value.GetType().Name;
            jObject.Add(typeName, JToken.FromObject(value));

            serializer.Serialize(writer, jObject);
        }
    }
}