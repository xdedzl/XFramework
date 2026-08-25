using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XFramework.AutoTest
{
    internal static class AutoTestState
    {
        [Serializable]
        private sealed class SceneStateResult
        {
            public string timestampUtc;
            public int sceneCount;
            public int activeSceneHandle;
            public string activeSceneName;
            public string activeScenePath;
            public List<SceneState> scenes = new List<SceneState>();
        }

        [Serializable]
        private sealed class SceneState
        {
            public int handle;
            public string name;
            public string path;
            public int buildIndex;
            public bool isLoaded;
            public bool isDirty;
            public bool isSubScene;
            public bool isActive;
            public int rootCount;
            public List<SceneRootState> roots = new List<SceneRootState>();
        }

        [Serializable]
        private sealed class SceneRootState
        {
            public int instanceId;
            public string name;
            public bool activeSelf;
            public bool activeInHierarchy;
        }

        [Serializable]
        private sealed class ApplicationStateResult
        {
            public string timestampUtc;
            public int processId;
            public string companyName;
            public string productName;
            public string version;
            public string unityVersion;
            public string platform;
            public bool isEditor;
            public bool isPlaying;
            public bool isFocused;
            public bool isBatchMode;
            public string systemLanguage;
            public int targetFrameRate;
            public TimeState time;
            public ScreenState screen;
            public SystemState system;
            public PathState paths;
        }

        [Serializable]
        private sealed class TimeState
        {
            public int frameCount;
            public float time;
            public float unscaledTime;
            public float realtimeSinceStartup;
            public float deltaTime;
            public float unscaledDeltaTime;
            public float timeScale;
            public float fixedDeltaTime;
        }

        [Serializable]
        private sealed class ScreenState
        {
            public int width;
            public int height;
            public float dpi;
            public bool fullScreen;
            public string fullScreenMode;
            public string orientation;
        }

        [Serializable]
        private sealed class SystemState
        {
            public string operatingSystem;
            public string deviceModel;
            public string deviceType;
            public int systemMemorySizeMb;
            public string processorType;
            public int processorCount;
            public string graphicsDeviceName;
            public string graphicsDeviceType;
            public int graphicsMemorySizeMb;
        }

        [Serializable]
        private sealed class PathState
        {
            public string dataPath;
            public string persistentDataPath;
            public string temporaryCachePath;
            public string streamingAssetsPath;
            public string consoleLogPath;
        }

        internal static string GetScenes(bool compact)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            var result = new SceneStateResult {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                sceneCount = SceneManager.sceneCount,
                activeSceneHandle = activeScene.handle,
                activeSceneName = activeScene.name,
                activeScenePath = activeScene.path,
            };
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                var state = new SceneState {
                    handle = scene.handle,
                    name = scene.name,
                    path = scene.path,
                    buildIndex = scene.buildIndex,
                    isLoaded = scene.isLoaded,
                    isDirty = scene.isDirty,
                    isSubScene = scene.isSubScene,
                    isActive = scene == activeScene,
                    rootCount = scene.rootCount,
                };
                if (scene.isLoaded)
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        state.roots.Add(new SceneRootState {
                            instanceId = root.GetInstanceID(),
                            name = root.name,
                            activeSelf = root.activeSelf,
                            activeInHierarchy = root.activeInHierarchy,
                        });
                    }
                }
                result.scenes.Add(state);
            }
            return JsonUtility.ToJson(result, !compact);
        }

        internal static string GetApplication(bool compact)
        {
            var result = new ApplicationStateResult {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                processId = Process.GetCurrentProcess().Id,
                companyName = Application.companyName,
                productName = Application.productName,
                version = Application.version,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                isEditor = Application.isEditor,
                isPlaying = Application.isPlaying,
                isFocused = Application.isFocused,
                isBatchMode = Application.isBatchMode,
                systemLanguage = Application.systemLanguage.ToString(),
                targetFrameRate = Application.targetFrameRate,
                time = new TimeState {
                    frameCount = Time.frameCount,
                    time = Time.time,
                    unscaledTime = Time.unscaledTime,
                    realtimeSinceStartup = Time.realtimeSinceStartup,
                    deltaTime = Time.deltaTime,
                    unscaledDeltaTime = Time.unscaledDeltaTime,
                    timeScale = Time.timeScale,
                    fixedDeltaTime = Time.fixedDeltaTime,
                },
                screen = new ScreenState {
                    width = Screen.width,
                    height = Screen.height,
                    dpi = Screen.dpi,
                    fullScreen = Screen.fullScreen,
                    fullScreenMode = Screen.fullScreenMode.ToString(),
                    orientation = Screen.orientation.ToString(),
                },
                system = new SystemState {
                    operatingSystem = SystemInfo.operatingSystem,
                    deviceModel = SystemInfo.deviceModel,
                    deviceType = SystemInfo.deviceType.ToString(),
                    systemMemorySizeMb = SystemInfo.systemMemorySize,
                    processorType = SystemInfo.processorType,
                    processorCount = SystemInfo.processorCount,
                    graphicsDeviceName = SystemInfo.graphicsDeviceName,
                    graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                    graphicsMemorySizeMb = SystemInfo.graphicsMemorySize,
                },
                paths = new PathState {
                    dataPath = Application.dataPath,
                    persistentDataPath = Application.persistentDataPath,
                    temporaryCachePath = Application.temporaryCachePath,
                    streamingAssetsPath = Application.streamingAssetsPath,
                    consoleLogPath = Application.consoleLogPath,
                },
            };
            return JsonUtility.ToJson(result, !compact);
        }
    }
}
