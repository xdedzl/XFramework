using UnityEngine;

namespace XFramework.Fsm
{
    internal static class FsmUnityClock
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            FsmClock.Configure(() => Time.frameCount, () => Time.realtimeSinceStartup);
        }
    }
}
