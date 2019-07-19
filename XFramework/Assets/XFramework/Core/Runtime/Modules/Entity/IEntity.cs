namespace XFramework.Entity
{
    public interface IEntity
    {
        void OnInit();
        void OnUpdate(float elapseSeconds, float realElapseSeconds);
    }
}