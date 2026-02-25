using System.Collections;
using System.Collections.Generic;

namespace XFramework
{
    # region 数据单元
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
            if (bindObject is null)
            {
                throw new System.ArgumentNullException(nameof(bindObject));
            }
            if (!_bindObjects.Contains(bindObject))
            {
                _bindObjects.Add(bindObject);
                bindObject.OnBind(this);
            }
        }

        public void ClearAllBind()
        {
            _bindObjects.Clear();
        }

        public void Unbind(IBindObject<T> bindObject)
        {
            if (_bindObjects.Contains(bindObject))
            {
                _bindObjects.Remove(bindObject);
            }
        }
    }
    # endregion
    
    # region 集合
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
    # endregion

    # region 数组
    public interface IBindableDataArray
    {
        int Count { get; }
    }

    public class BindableDataArray<T>: IBindableDataArray where T : BindableDataSet, new()
    {
        private readonly List<T> m_DataArray = new ();
        private readonly List<IBindArrayObject<T>> m_BindObjects = new();

        public int Count => m_DataArray.Count;
        
        public T this[int index]
        {
            get => m_DataArray[index];
        }
    
       public void Bind(IBindArrayObject<T> bindObject)
        {
            if (!m_BindObjects.Contains(bindObject))
            {
                m_BindObjects.Add(bindObject);
                NotifyArrayRebind();
            }
        }

        public void Unbind(IBindArrayObject<T> bindObject)
        {
            if (m_BindObjects.Contains(bindObject))
            {
                m_BindObjects.Remove(bindObject);
            }
        }

        public void Resize(int count)
        {
            if (count < m_DataArray.Count)
            {
                m_DataArray.RemoveRange(count, m_DataArray.Count - count);
            }
            else if (count > m_DataArray.Count)
            {
                for (int i = m_DataArray.Count; i < count; i++)
                {
                    m_DataArray.Add(new T());
                }
            }

            NotifyArrayRebind();
        }
        
        public void NotifyArrayRebind()
        {
            for (int i = 0; i < m_BindObjects.Count; i++)
            {
                m_BindObjects[i].OnBindArray(this);
            }
            
            for (int i = 0; i<m_DataArray.Count; i++)
            {
                NotifyItemRebind(i);
            }
        }
        
        public void NotifyItemRebind(int index)
        {
            for (int i = 0; i < m_BindObjects.Count; i++)
            {
                m_BindObjects[i].OnBindItem(m_DataArray[index], index);
            }
        }

        public void ClearAllBind()
        {
            m_BindObjects.Clear();
        }
    }
    # endregion

    # region 可绑定对象
    public interface IBindObject<T>
    {
        public void OnBind(IBindableDataCell<T> bindableData);
    }

    public interface IBindArrayObject<in T> where T : BindableDataSet
    {
        public void OnBindArray(IBindableDataArray bindableDataArray);
        public void OnBindItem(T item, int index);
    }
    # endregion
}