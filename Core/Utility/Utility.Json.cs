//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;


namespace XFramework
{
    public partial class Utility
    {
        public static class Json
        {
            /// <summary>
            /// 序列化多态数组
            /// </summary>
            /// <param name="list"></param>
            /// <param name="fullTypeName">是否使用类型全名存储</param>
            /// <param name="formatting">格式化</param>
            /// <returns>json文本</returns>
            //public static string SerializePolyArray(IList list, bool fullTypeName = false, Formatting formatting = Formatting.None)
            //{
            //    JArray jArray = new JArray();

            //    foreach (var item in list)
            //    {
            //        string typeName = fullTypeName ? item.GetType().FullName : item.GetType().Name;
            //        JObject jObject = new JObject
            //    {
            //        { "type", typeName },
            //        { "data", JToken.FromObject(item) }
            //    };
            //        jArray.Add(jObject);
            //    }
            //    return jArray.ToString(formatting);
            //}

            ///// <summary>
            ///// 反序列化多态数组
            ///// </summary>
            ///// <typeparam name="T">基类类型</typeparam>
            ///// <param name="json">json文本</param>
            ///// <param name="nameSpace">命名空间，(序列化时使用的是全名时传null即可)</param>
            ///// <returns>多态数组</returns>
            //public static T[] DeserializePolyArray<T>(string json, string nameSpace = null)
            //{
            //    JArray jArray = JArray.Parse(json);

            //    List<T> values = new List<T>();

            //    foreach (var jObject in jArray.Children<JObject>())
            //    {
            //        Type type;
            //        if (string.IsNullOrEmpty(nameSpace))
            //            type = Type.GetType(jObject.GetValue("type").Value<string>());
            //        else
            //            type = Type.GetType($"{nameSpace}.{jObject.GetValue("type").Value<string>()}");

            //        var value = jObject.GetValue("data").ToObject(type);

            //        values.Add((T)value);
            //    }

            //    return values.ToArray();
            //}
        }
    }
}