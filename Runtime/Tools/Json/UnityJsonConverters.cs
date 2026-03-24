using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace XFramework.Json
{
    public class ColorConverter : JsonConverter<Color>
    {
        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v4 = value[1..^1].Split(',');

            return new Color(float.Parse(v4[0]), float.Parse(v4[1]), float.Parse(v4[2]), float.Parse(v4[3]));
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            string v2Str = $"({value.r},{value.g},{value.b},{value.a})";
            serializer.Serialize(writer, v2Str);
        }
    }

    public class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v2 = value.Split(',');

            return new Vector2(float.Parse(v2[0]), float.Parse(v2[1]));
        }

        public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
        {
            string v2Str = $"{value.x},{value.y}";
            serializer.Serialize(writer, v2Str);
        }
    }

    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v3 = value.Split(',');

            return new Vector3(float.Parse(v3[0]), float.Parse(v3[1]), float.Parse(v3[2]));
        }

        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            string v3Str = $"{value.x},{value.y},{value.z}";
            serializer.Serialize(writer, v3Str);
        }
    }

    public class Vector2IntConverter : JsonConverter<Vector2Int>
    {
        public override Vector2Int ReadJson(JsonReader reader, Type objectType, Vector2Int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v2 = value.Split(',');

            return new Vector2Int(int.Parse(v2[0]), int.Parse(v2[1]));
        }

        public override void WriteJson(JsonWriter writer, Vector2Int value, JsonSerializer serializer)
        {
            string v2Str = $"{value.x},{value.y}";
            serializer.Serialize(writer, v2Str);
        }
    }
    
    public class Vector3IntConverter : JsonConverter<Vector3Int>
    {
        public override Vector3Int ReadJson(JsonReader reader, Type objectType, Vector3Int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v3 = value.Split(',');

            return new Vector3Int(int.Parse(v3[0]), int.Parse(v3[1]), int.Parse(v3[2]));
        }

        public override void WriteJson(JsonWriter writer, Vector3Int value, JsonSerializer serializer)
        {
            string v3Str = $"{value.x},{value.y},{value.z}";
            serializer.Serialize(writer, v3Str);
        }
    }

    public class QuaternionConverter : JsonConverter<Quaternion>
    {
        public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue,JsonSerializer serializer)
        {
            string value = serializer.Deserialize<string>(reader);

            string[] v4 = value.Split(',');

            return new Quaternion(float.Parse(v4[0]), float.Parse(v4[1]), float.Parse(v4[2]), float.Parse(v4[3]));
        }

        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            Quaternion v = (Quaternion)value;
            string q4Str = $"{v.x},{v.y},{v.z},{v.w}";

            serializer.Serialize(writer, q4Str);
        }
    }
}