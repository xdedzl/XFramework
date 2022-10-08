using UnityEngine;

namespace XFramework
{
    public class UBinaryReader : BinaryReader
    {
        public UBinaryReader(byte[] _bytes) : base(_bytes) { }

        [DeserializeMethod("Vector3")]
        public Vector3 GetVector3()
        {
            float x = GetFloat();
            float y = GetFloat();
            float z = GetFloat();
            return new Vector3(x, y, z);
        }


        [DeserializeMethod("Vector2")]
        public Vector2 GetVector2()
        {
            float x = GetFloat();
            float y = GetFloat();
            return new Vector2(x, y);
        }

        [DeserializeMethod("Color")]
        public Color GetColor()
        {
            return new Color(GetFloat(), GetFloat(), GetFloat(), GetFloat());
        }

        [DeserializeMethod("Color32")]
        public Color32 GetColor32()
        {
            return new Color32(buffer[index++], buffer[index++], buffer[index++], buffer[index++]);
        }

        [DeserializeMethod("GameObject")]
        public GameObject GetGameObject()
        {
            return GetTransform().gameObject;
        }

        [DeserializeMethod("Transform")]
        public Transform GetTransform()
        {
            string tranStr = this.GetString();
            if (string.IsNullOrEmpty(tranStr))
                return null;

            return GameObject.Find(tranStr).transform;
        }
    }
}
