using System.Collections;
using System.Collections.Generic;

namespace XFramework
{
    public interface IBindableDataCell
    {
    
    }

    public interface IBindableDataCell<T> : IBindableDataCell
    {
        public T Value { get; set; }
    
        public void Bind(IBindObject<T> bindObject);
    }

    public class BindableDataCell<T> : IBindableDataCell<T>
    {
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                foreach (var bindObject in _bindObjects)
                {
                    bindObject.OnBind(this);
                }
            }
        }

        private readonly List<IBindObject<T>> _bindObjects = new();

        public void Bind(IBindObject<T> bindObject)
        {
            if (!_bindObjects.Contains(bindObject))
            {
                _bindObjects.Add(bindObject);
                bindObject.OnBind(this);
            }
        }

        public void Unbind(IBindObject<T> bindObject)
        {
            if (_bindObjects.Contains(bindObject))
            {
                _bindObjects.Remove(bindObject);
            }
        }
    }



    public interface IBindableDataSet: IEnumerable<KeyValuePair<string, IBindableDataCell>>
    {
        public IBindableDataCell GetCell(string key);
    }

    public abstract class BindableDataSet : IBindableDataSet
    {
        public abstract IEnumerator<KeyValuePair<string, IBindableDataCell>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public abstract IBindableDataCell GetCell(string key);
    }



    public interface IBindableDataArray
    {
        int Count { get; }
    }

    public class BindableDataArray<T>: IBindableDataArray where T : BindableDataSet
    {
        private readonly List<T> m_DataArray = new ();
        private readonly List<IBindArrayObject<T>> _bindObjects = new();

        public int Count => m_DataArray.Count;
    
        public void Bind(IBindArrayObject<T> bindObject)
        {
            if (!_bindObjects.Contains(bindObject))
            {
                _bindObjects.Add(bindObject);
                bindObject.OnBindArray(this);
            }
        }

        public void Unbind(IBindArrayObject<T> bindObject)
        {
            if (_bindObjects.Contains(bindObject))
            {
                _bindObjects.Remove(bindObject);
            }
        }
    }



    public interface IBindObject<T>
    {
        public void OnBind(IBindableDataCell<T> bindableData);
    }

    public interface IBindArrayObject<in T> where T : BindableDataSet
    {
        public void OnBindArray(IBindableDataArray bindableDataArray);
        public void OnBindItem(T item, int index);
    }
}