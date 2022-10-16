using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace XFramework.JsonConvter
{
    public class ColorConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v4 = value.Split(',');

            return new Color(float.Parse(v4[0]), float.Parse(v4[1]), float.Parse(v4[2]), float.Parse(v4[3]));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Color v = (Color)value;
            string v4Str = $"{v.r},{v.g},{v.b},{v.a}";

            serializer.Serialize(writer, v4Str);
        }
    }

    public class ColorArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = serializer.Deserialize<string[]>(reader);
            List<Color> values = new List<Color>();

            foreach (var item in value)
            {
                string[] colors = item.Split(',');
                var c = new Color(float.Parse(colors[0]), float.Parse(colors[1]), float.Parse(colors[2]), float.Parse(colors[3]));
                values.Add(c);
            }
            return values.ToArray();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Color[] values = (Color[])value;
            var valueString = new List<string>();
            foreach (var item in values)
            {
                valueString.Add($"{item.r},{item.g},{item.b},{item.a}");
            }
            serializer.Serialize(writer, valueString);
        }
    }

    public class Vector2Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v2 = value.Split(',');
            return new Vector2(float.Parse(v2[0]), float.Parse(v2[1]));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Vector2 v = (Vector2)value;
            string v3Str = $"{v.x},{v.y}";

            serializer.Serialize(writer, v3Str);
        }
    }

    public class Vector3Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v3 = value.Split(',');

            return new Vector3(float.Parse(v3[0]), float.Parse(v3[1]), float.Parse(v3[2]));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Vector3 v = (Vector3)value;
            string v3Str = $"{v.x},{v.y},{v.z}";

            serializer.Serialize(writer, v3Str);
        }
    }

    public class ListVector3Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = serializer.Deserialize<List<string>>(reader);
            List<Vector3> values = new List<Vector3>();

            foreach (var item in value)
            {
                string[] v3s = item.Split(',');
                var v3 = new Vector3(float.Parse(v3s[0]), float.Parse(v3s[1]), float.Parse(v3s[2]));
                values.Add(v3);
            }
            return values;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            List<Vector3> values = (List<Vector3>)value;
            var valueString = new List<string>();
            foreach (var item in values)
            {
                valueString.Add($"{item.x},{item.y},{item.z}");
            }
            serializer.Serialize(writer, valueString);
        }
    }

    public class Vector3ArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = serializer.Deserialize<string[]>(reader);
            List<Vector3> values = new List<Vector3>();

            foreach (var item in value)
            {
                string[] v3s = item.Split(',');
                var v3 = new Vector3(float.Parse(v3s[0]), float.Parse(v3s[1]), float.Parse(v3s[2]));
                values.Add(v3);
            }
            return values.ToArray();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Vector3[] values = (Vector3[])value;
            var valueString = new List<string>();
            foreach (var item in values)
            {
                valueString.Add($"{item.x},{item.y},{item.z}");
            }
            serializer.Serialize(writer, valueString);
        }
    }

    public class QuaternionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v4 = value.Split(',');

            return new Quaternion(float.Parse(v4[0]), float.Parse(v4[1]), float.Parse(v4[2]), float.Parse(v4[3]));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Quaternion v = (Quaternion)value;
            string q4Str = $"{v.x},{v.y},{v.z},{v.w}";

            serializer.Serialize(writer, q4Str);
        }
    }

    public class ListQuaternionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            List<string> value = serializer.Deserialize<List<string>>(reader);
            List<Quaternion> quaternions = new List<Quaternion>();
            foreach (var item in value)
            {
                string[] Quater = item.Split(',');
                var tmp = new Quaternion(float.Parse(Quater[0]), float.Parse(Quater[1]), float.Parse(Quater[2]), float.Parse(Quater[3]));
                quaternions.Add(tmp);
            }

            return quaternions;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Quaternion v = (Quaternion)value;
            string v4Str = $"{v.x},{v.y},{v.z},{v.w}";
            serializer.Serialize(writer, v4Str);
        }
    }
}