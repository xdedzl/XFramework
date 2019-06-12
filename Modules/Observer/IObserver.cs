///<summary>
///观察者接口
///<summary>
namespace XFramework
{
    public interface IObserver
    {
        void OnDataChange(BaseData eventData, int type, object obj);
    }
}