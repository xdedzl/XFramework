using UnityEngine;

public interface IBindableData<T>
{
    public T Value { get; set; }
    
    public void Bind(IBindObject<T> bindObject);
}

public class BindableData<T> : IBindableData<T>
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

    private readonly System.Collections.Generic.List<IBindObject<T>> _bindObjects = new();

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
        
    }
}

public interface IBindObject<T>
{
    public void OnBind(IBindableData<T> bindableData);
}
