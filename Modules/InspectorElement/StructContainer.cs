namespace XFramework.UI 
{
    public interface IDataContainer
    {
        [ElementIgnore]
        object Data { get; }
    }

    public class StructContainer<T> :  IDataContainer
    {
        public T data;

        public StructContainer(T data)
        {
            this.data = data;
        }

        public object Data => data;
    }
}