using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace XFramework.Json
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
            if (objectType.IsArray) return true;
            if (objectType.IsGenericType)
            {
                var typeDef = objectType.GetGenericTypeDefinition();
                return typeDef == typeof(List<>) || typeDef == typeof(IList<>) || typeDef == typeof(IEnumerable<>) || typeDef == typeof(ICollection<>);
            }
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jArray = JArray.Load(reader);

            List<T> values = new List<T>();

            foreach (var jObject in jArray.Children<JObject>())
            {
                string typeName = jObject.GetValue("type").Value<string>();
                Type type;
                if (string.IsNullOrEmpty(nameSpace))
                    type = Utility.Reflection.GetType(typeName, "Assembly-CSharp", "XFrameworkRuntime");
                else
                    type = Utility.Reflection.GetType($"{nameSpace}.{typeName}", "Assembly-CSharp", "XFrameworkRuntime");

                var value = jObject.GetValue("data").ToObject(type, serializer);

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

            JArray jArray = new JArray();

            foreach (var item in values)
            {
                string typeName = string.IsNullOrEmpty(nameSpace) ? item.GetType().FullName : item.GetType().Name;
                JObject itemObject = new JObject
                {
                    { "type", typeName },
                    { "data", JToken.FromObject(item, serializer) }
                };
                jArray.Add(itemObject);
            }

            serializer.Serialize(writer, jArray);
        }
    }

    /// <summary>
    /// 用于多态序列化
    /// </summary>
    public class PolyConverter : JsonConverter
    {
        private readonly string nameSpace;
        private readonly string assembly;

        public PolyConverter() { }

        public PolyConverter(string nameSpace=null)
        {
            this.nameSpace = nameSpace;
            this.assembly = "Assembly-CSharp";
        }

        public PolyConverter(string nameSpace, string assembly)
        {
            this.nameSpace = nameSpace;
            this.assembly = assembly;
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);
            if (jObject == null)
            {
                return null;
            }

            var assembly = Assembly.Load(this.assembly);
            string typeName = jObject["type"]?.Value<string>();
            Type type;
            if (string.IsNullOrEmpty(nameSpace))
                type = assembly.GetType(typeName);
            else
                type = assembly.GetType($"{nameSpace}.{typeName}");

            JToken dataToken = jObject["data"];
            if (dataToken == null || dataToken.Type == JTokenType.Null)
                return null;
            return dataToken.ToObject(type, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
            {
                JObject jObject = new JObject();
                string typeName = string.IsNullOrEmpty(nameSpace) ? value.GetType().FullName : value.GetType().Name;
                jObject.Add("type", typeName);
                jObject.Add("data", JToken.FromObject(value));

                serializer.Serialize(writer, jObject);
            }
        }
    }

    public class PolyListConverter : JsonConverter
    {
        private readonly string nameSpace;
        private readonly string assembly;

        public PolyListConverter() { }

        public PolyListConverter(string nameSpace = null)
        {
            this.nameSpace = nameSpace;
            this.assembly = "Assembly-CSharp";
        }

        public PolyListConverter(string nameSpace, string assembly)
        {
            this.nameSpace = nameSpace;
            this.assembly = assembly;
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jArray = JArray.Load(reader);
            if (jArray == null)
            {
                return null;
            }

            var assemblyObj = Assembly.Load(this.assembly);

            if (objectType.IsArray)
            {
                var array = Array.CreateInstance(objectType.GetElementType(), jArray.Count);
                for (int i = 0; i < jArray.Count; i++)
                {
                    var jObject = (JObject)jArray[i];
                    string typeName = jObject["type"]?.Value<string>();
                    Type type;
                    if (string.IsNullOrEmpty(nameSpace))
                        type = assemblyObj.GetType(typeName);
                    else
                        type = assemblyObj.GetType($"{nameSpace}.{typeName}");

                    JToken dataToken = jObject["data"];
                    var item = dataToken?.ToObject(type, serializer);
                    array.SetValue(item, i);
                }
                return array;
            }
            else
            {
                Type listType;
                if (objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    listType = objectType;
                }
                else
                {
                    // Fallback to ArrayList or infer list type if possible.
                    // Usually this converter is applied to List<T> or Array.
                    listType = typeof(ArrayList); 
                    if (objectType.GetGenericArguments().Length > 0)
                    {
                        listType = typeof(List<>).MakeGenericType(objectType.GetGenericArguments()[0]);
                    }
                }

                IList list = (IList)Activator.CreateInstance(listType);
                for (int i = 0; i < jArray.Count; i++)
                {
                    var jObject = (JObject)jArray[i];
                    string typeName = jObject["type"]?.Value<string>();
                    Type type;
                    if (string.IsNullOrEmpty(nameSpace))
                        type = assemblyObj.GetType(typeName);
                    else
                        type = assemblyObj.GetType($"{nameSpace}.{typeName}");

                    JToken dataToken = jObject["data"];
                    var item = dataToken?.ToObject(type, serializer);
                    list.Add(item);
                }
                return list;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
            {
                JArray jArray = new JArray();
                foreach (var item in (IEnumerable)value)
                {
                    string typeName = string.IsNullOrEmpty(nameSpace) ? item.GetType().FullName : item.GetType().Name;
                    JObject jObject = new JObject
                    {
                        { "type", typeName },
                        { "data", JToken.FromObject(item, serializer) }
                    };
                    jArray.Add(jObject);
                }
                serializer.Serialize(writer, jArray);
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}