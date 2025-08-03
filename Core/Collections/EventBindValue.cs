using System;
using System.Collections.Generic;

namespace XFramework
{
    public class EventBindList<T>
    {
        private readonly List<T> list;
        private readonly HashSet<Action<List<T>>> callbacks = new();

        public EventBindList()
        {
            list = new List<T>();
        }
        public EventBindList(IEnumerable<T> collection)
        {
            list = new List<T>(collection);

        }
        public EventBindList(int capacity)
        {
            list = new List<T>(capacity);
        }

        public void Bind(Action<List<T>> cb)
        {
            callbacks.Add(cb);
            cb.Invoke(this.list);
        }

        public void UnBind(Action<List<T>> cb)
        {
            callbacks.Remove(cb);
        }

        private void OnValueChange()
        {
            foreach (var item in callbacks)
            {
                item.Invoke(list);
            }
        }

        public void Add(T value)
        {
            list.Add(value);
            OnValueChange();
        }

        public void Remove(T value)
        {
            list.Remove(value);
            OnValueChange();
        }

        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
            OnValueChange();
        }

        public T this[int index]
        {
            get
            {
                return list[index];
            }
            set
            {
                list[index] = value;
                OnValueChange();
            }
        }

        public void Clear()
        {
            list.Clear();
            OnValueChange();
        }
    }

    public class EventBindValue<T>
    {
        private T _value;

        public T value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnValueChange();
            }
        }

        private readonly HashSet<Action<T>> callbacks = new();

        public void Bind(Action<T> cb)
        {
            callbacks.Add(cb);
            cb.Invoke(this.value);
        }

        public void UnBind(Action<T> cb)
        {
            callbacks.Remove(cb);
        }

        private void OnValueChange()
        {
            foreach (var cb in callbacks)
            {
                cb.Invoke(value);
            }
        }

        public static implicit operator T(EventBindValue<T> celsius)
        {
            return celsius.value;
        }

        public override string ToString()
        {
            return value.ToString();
        }

    }
}