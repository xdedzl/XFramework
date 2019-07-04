using UnityEngine;

namespace XFramework.Pool
{
    public class MonoObjectFactory<T> : IObjectFactory<T> where T : MonoBehaviour
    {

        public T Create(object data)
        {
            GameObject obj = Object.Instantiate(data as GameObject);
            return obj.AddComponent<T>();
        }
    }
}