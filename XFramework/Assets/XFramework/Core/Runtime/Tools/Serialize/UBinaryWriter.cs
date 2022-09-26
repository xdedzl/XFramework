using UnityEngine;

namespace XFramework
{

    public class UBinaryWriter : BinaryWriter
    {
        public void AddVector3(Vector3 v)
        {
            AddFloat(v.x);
            AddFloat(v.y);
            AddFloat(v.z);
        }

        public void AddVector2(Vector2 v)
        {
            AddFloat(v.x);
            AddFloat(v.y);
        }

        public void AddColor(Color c)
        {
            AddFloat(c.r);
            AddFloat(c.g);
            AddFloat(c.b);
            AddFloat(c.a);
        }

        public void AddColor32(Color32 c)
        {
            bufferList.Add(c.r);
            bufferList.Add(c.g);
            bufferList.Add(c.b);
            bufferList.Add(c.a);
        }

        public void AddGameObject(GameObject gameObj)
        {
            if (!gameObj)
            {
                AddString("");
                return;
            }
            Transform transform = gameObj.transform;
            AddTransform(transform);
        }

        public void AddTransform(Transform transform)
        {
            if (!transform)
            {
                this.AddString("");
                return;
            }
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            AddString(path);
        }
    }
}
