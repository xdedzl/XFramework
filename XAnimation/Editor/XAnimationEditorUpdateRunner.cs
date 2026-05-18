#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XFramework.Animation
{
    [InitializeOnLoad]
    internal static class XAnimationEditorUpdateRunner
    {
        private static readonly List<XAnimationDriver> s_Drivers = new();
        private static double s_LastTime;

        static XAnimationEditorUpdateRunner()
        {
            s_LastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        internal static void Register(XAnimationDriver driver)
        {
            if (driver == null)
            {
                return;
            }

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

        private static void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)System.Math.Max(0d, now - s_LastTime);
            s_LastTime = now;

            if (Application.isPlaying)
            {
                return;
            }

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
    }
}
#endif
