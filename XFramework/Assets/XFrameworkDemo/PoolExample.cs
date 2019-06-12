using UnityEngine;
using XFramework.Pool;

public class PoolExample : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Game.PoolModule.CreatePool<TestPoolObj>();
        TestPoolObj aa = Game.PoolModule.Allocate<TestPoolObj>();
        Debug.Log(aa.GetHashCode());
        Game.PoolModule.Recycle(aa);
        aa = Game.PoolModule.Allocate<TestPoolObj>();
        Game.PoolModule.Recycle(aa);
        Debug.Log(aa.GetHashCode());
        aa = Game.PoolModule.Allocate<TestPoolObj>();
        Game.PoolModule.Recycle(aa);
        Debug.Log(aa.GetHashCode());
        aa = Game.PoolModule.Allocate<TestPoolObj>();
        Game.PoolModule.Recycle(aa);
        Debug.Log(aa.GetHashCode()); 
        aa = Game.PoolModule.Allocate<TestPoolObj>();
        Game.PoolModule.Recycle(aa);
        Debug.Log(aa.GetHashCode());
        aa = Game.PoolModule.Allocate<TestPoolObj>();
        Game.PoolModule.Recycle(aa);
        Debug.Log(aa.GetHashCode());
        aa = Game.PoolModule.Allocate<TestPoolObj>();
        Game.PoolModule.Recycle(aa); 
        Debug.Log(aa.GetHashCode());
    }

    // Update is called once per frame
    void Update()
    {

    }

    public class TestPoolObj : IPoolable
    {
        public bool IsRecycled { get; set; }
        public bool IsLocked { get; set; }

        public void OnRecycled()
        {
            Debug.Log("我被回收了");
        }
    }
}
