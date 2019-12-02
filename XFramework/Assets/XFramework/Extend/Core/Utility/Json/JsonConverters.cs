using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace XFramework.JsonConvter
{
    /// <summary>
    /// 用于多态列表的转化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PolyListConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            List<T> values = new List<T>();

            foreach (var item in jObject.Properties())
            {
                Type type = Type.GetType(item.Name);

                var value = item.Value.ToObject(type);

                values.Add((T)value);
            }

            return values;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var values = (List<T>)value;

            JObject jObject = new JObject();

            foreach (var item in values)
            {
                jObject.Add(item.GetType().FullName, JToken.FromObject(item));
            }

            var p = jObject.Properties();
            foreach (var item in p)
            {
                Debug.Log(item.Name);
            }

            serializer.Serialize(writer, jObject);
        }
    }

    /// <summary>
    /// 用于多态序列化
    /// </summary>
    public class PolyConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            foreach (var item in jObject.Properties())
            {
                Type type = Type.GetType(item.Name);

                var value = item.Value.ToObject(type);

                return value;
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JObject jObject = new JObject();

            jObject.Add(value.GetType().FullName, JToken.FromObject(value));

            serializer.Serialize(writer, jObject);
        }
    }
}