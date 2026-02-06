using System;
using System.Collections.Generic;

namespace XFramework.Tools
{
    public class BlackBoard
    {
        private readonly Dictionary<string, object> _data = new();

        public void SetValue<T>(string key, T value)
        {
            _data[key] = value;
        }

        public T GetValue<T>(string key, T defaultValue)
        {
            if (_data.TryGetValue(key, out var value))
            {
                if (value is T tValue)
                {
                    return tValue;
                }
                else
                {
                    throw new InvalidCastException($"Data with key '{key}' is not of type {typeof(T).Name}.");
                }
            }
            else
            {
                return defaultValue;
            }
        }

        public bool TryGetData<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var objValue) && objValue is T tValue)
            {
                value = tValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool ContainsKey(string key)
        {
            return _data.ContainsKey(key);
        }

        public void RemoveData(string key)
        {
            _data.Remove(key);
        }

        public void Clear()
        {
            _data.Clear();
        }
        
        public string Serialize()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(_data);
        }
        
        public void Deserialize(string json)
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (data != null)
            {
                _data.Clear();
                foreach (var kvp in data)
                {
                    _data[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}