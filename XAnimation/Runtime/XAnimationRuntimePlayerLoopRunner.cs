using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace XFramework.Animation
{
    internal static class XAnimationRuntimePlayerLoopRunner
    {
        private static readonly List<XAnimationDriver> s_Drivers = new();
        private static bool s_Installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureInstalled();
            s_Drivers.Clear();
        }

        internal static void EnsureInstalled()
        {
            if (s_Installed)
            {
                return;
            }

            PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            if (TryInsertIntoPreLateUpdate(ref playerLoop))
            {
                PlayerLoop.SetPlayerLoop(playerLoop);
                s_Installed = true;
            }
        }

        internal static void Register(XAnimationDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            EnsureInstalled();
            if (!s_Drivers.Contains(driver))
            {
                s_Drivers.Add(driver);
            }
        }

        internal static void Unregister(XAnimationDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            s_Drivers.Remove(driver);
        }

        private static void Tick()
        {
            float deltaTime = Time.deltaTime;
            for (int i = s_Drivers.Count - 1; i >= 0; i--)
            {
                XAnimationDriver driver = s_Drivers[i];
                if (driver == null || !driver.IsRegisteredForAutomaticUpdate)
                {
                    s_Drivers.RemoveAt(i);
                    continue;
                }

                driver.TickFromScheduler(deltaTime);
            }
        }

        private static bool TryInsertIntoPreLateUpdate(ref PlayerLoopSystem root)
        {
            if (root.subSystemList == null)
            {
                return false;
            }

            for (int i = 0; i < root.subSystemList.Length; i++)
            {
                if (root.subSystemList[i].type != typeof(PreLateUpdate))
                {
                    continue;
                }

                PlayerLoopSystem parent = root.subSystemList[i];
                List<PlayerLoopSystem> children = new(parent.subSystemList ?? Array.Empty<PlayerLoopSystem>());
                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                {
                    if (children[childIndex].type == typeof(XAnimationRuntimePlayerLoopRunner))
                    {
                        return true;
                    }
                }

                int insertIndex = children.FindIndex(system => system.type == typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate));
                if (insertIndex < 0)
                {
                    insertIndex = children.Count;
                }

                children.Insert(insertIndex, new PlayerLoopSystem
                {
                    type = typeof(XAnimationRuntimePlayerLoopRunner),
                    updateDelegate = Tick,
                });
                parent.subSystemList = children.ToArray();
                root.subSystemList[i] = parent;
                return true;
            }

            return false;
        }
    }
}
