using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XFramework.Tasks;
using UObject = UnityEngine.Object;

namespace XFramework.Resource
{
    /// <summary>
    /// 资源管理器
    /// </summary>
    public partial class ResourceManager : PersistentMonoGameModuleBase<ResourceManager>
    {
        public const string BuildConfigAssetPath = "Assets/Configs/AssetBundleBuildConfig.asset";
        
        private readonly IResourceLoadHelper m_LoadHelper;
        private readonly Dictionary<string, string> m_PathMap;  // 资源路径映射
        public bool HasPathMap { get; }
        
        // 资源实例化对象池相关
        private readonly Dictionary<UObject, UObject> m_InstanceToAsset = new ();
        private readonly Dictionary<UObject, string> m_InstanceToAssetName = new ();  // 实例到资源名的映射
        private readonly Dictionary<string, Queue<UObject>> m_FreeInstances = new ();
        private readonly Dictionary<UObject, float> m_InstanceToFreeTime = new ();
        private readonly LinkedList<UObject> m_InstanceLruList = new();
        private readonly Dictionary<UObject, LinkedListNode<UObject>> m_InstanceLruNodes = new();
        
        private const float PooledInstanceExpireTime = 60f;  // 对象池中空闲对象的过期时间（秒）
        private const int MaxCleanupPerFrame = 5;            // 每帧最多清理的过期对象数量
        
        public ResourceManager()
        {
#if UNITY_EDITOR
            if (XApplication.Setting.UseABInEditor)
            {
                m_LoadHelper = new AssetBundleLoadHelper();
            }
            else
            {
                m_LoadHelper = new AssetDataBaseLoadHelper();
            }
#else
            m_LoadHelper = new AssetBundleLoadHelper();
#endif
            var mapInfoPath = "";
            if (File.Exists(mapInfoPath))
            {
                m_PathMap = new Dictionary<string, string>();
                InitPathMapWithText(mapInfoPath);
                HasPathMap = true;
            }
            else
            {
                HasPathMap = false;
            }
        }

        private void InitPathMapWithText(string mapInfoPath)
        {
            string pathMapInfo = File.ReadAllText(mapInfoPath);
            string[] keyValues = pathMapInfo.Split('\n');
            foreach (var item in keyValues)
            {
                string[] keyValue = item.Split(':');
                m_PathMap.Add(keyValue[0], keyValue[1]);
            }
        }

        /// <summary>
        /// 资源路径
        /// </summary>
        public string AssetPath => m_LoadHelper.AssetPath;

        public bool IsAssetExist(string assetName)
        {
            return m_LoadHelper.IsAssetExist(assetName);
        }

        #region 资源加载
        
        public T LoadInResources<T>(string assetName) where T : UObject
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new XFrameworkException("load path is null");
            }
            assetName = assetName.Split('.')[0];
            return Resources.Load<T>(assetName);
        }
        
        public T[] LoadAllInResources<T>(string path, bool isTopOnly = true) where T : UObject
        {
            path = path.Split('.')[0];
            return Resources.LoadAll<T>(path);
        }
        
        public IProgress LoadAsyncInResources<T>(string assetName, Action<T> callBack) where T : UObject
        {
            var request = Resources.LoadAsync(assetName);

            var task = XTask.WaitUntil(() => request.isDone);
            task.ContinueWith(() =>
            {
                callBack.Invoke(request.asset as T);
            });
            task.Start();
            return new ResourceRequestProgress(request);
        }
        
        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns>资源</returns>
        public T Load<T>(string assetName) where T : UObject
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new XFrameworkException("load path is null");
            }

            assetName = Path2RealPath(assetName);
            return m_LoadHelper.Load<T>(assetName);
        }
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="assetName">资源名称</param>
        /// <returns>加载任务</returns>
        // public IProgressTask<T> LoadAsync<T>(string assetName) where T : Object
        // {
        //     assetName = Path2RealPath(assetName);
        //     return m_LoadHelper.LoadAsync<T>(assetName);
        // }
        
        public void LoadAsync<T>(string assetName, LoadAssetDelegate<T> callback) where T : UObject
        {
            m_LoadHelper.LoadAsync<T>(assetName, callback);
        }
        
        public XAwaitableTask<T> LoadAsync<T>(string assetName) where T : UObject
        {
            return m_LoadHelper.LoadAsync<T>(assetName);
        }

        /// <summary>
        /// 将传入路径转为Assets路径
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string Path2RealPath(string path)
        {
            if (HasPathMap)
            {
                if(m_PathMap.TryGetValue(path, out string realPath))
                {
                    return realPath;
                }
                else
                {
                    throw new XFrameworkException($"[Resource] There is no resource which path is {path} or PathMapInfo is obsolete");
                }
            }
            else
            {
                return path;
            }
        }

        #endregion

        #region 资源实例化
        
        public T Instantiate<T>(string assetName) where T : UObject
        {
            return Instantiate<T>(assetName, null);
        }
        
        public T Instantiate<T>(string assetName, Transform parent) where T : UObject
        {
            return Instantiate<T>(assetName, default, default, parent);
        }
        
        public T Instantiate<T>(string assetName, Vector3 position, Quaternion quaternion) where T : UObject
        {
            return Instantiate<T>(assetName, position, quaternion, null);
        }

        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="position">位置</param>
        /// <param name="quaternion">方向</param>
        /// <param name="parent">父物体</param>
        /// <returns>资源的实例</returns>
        public T Instantiate<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : UObject
        {
            T asset = Load<T>(assetName);
            T obj = UObject.Instantiate<T>(asset, position, quaternion, parent);
            return obj;
        }
        
        public void InstantiateAsync<T>(string assetName, Action<T> callBack = null) where T : UObject
        {
            InstantiateAsync<T>(assetName, null, callBack);
        }
        
        public void InstantiateAsync<T>(string assetName, Transform parent, Action<T> callBack = null) where T : UObject
        {
            InstantiateAsync<T>(assetName, default, default, parent, callBack);
        }
        
        public void InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, Action<T> callBack = null) where T : UObject
        {
            InstantiateAsync<T>(assetName, position, quaternion, null, callBack);
        }

        /// <summary>
        /// 异步实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="position">位置</param>
        /// <param name="quaternion">方向</param>
        /// <param name="parent">父物体</param>
        /// <param name="callBack">实例化完成回调</param>
        public void InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent, Action<T> callBack = null) where T : UObject
        {
            LoadAsync<T>(assetName, (success, asset) =>
            {
                if (success)
                {
                    T obj = UObject.Instantiate(asset, position, quaternion, parent);
                    callBack?.Invoke(obj);
                }
                else
                {
                    Debug.LogError("[Resource] There is no resource which path is " + assetName);
                }
            });
        }
        
        
        public T InstantiateByPool<T>(string assetName) where T : UObject
        {
            return InstantiateByPool<T>(assetName, null);
        }
        
        public T InstantiateByPool<T>(string assetName, Transform parent) where T : UObject
        {
            return InstantiateByPool<T>(assetName, default, default, parent);
        }
        
        public T InstantiateByPool<T>(string assetName, Vector3 position, Quaternion quaternion) where T : UObject
        {
            return InstantiateByPool<T>(assetName, position, quaternion, null);
        }
        
        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="position">位置</param>
        /// <param name="quaternion">方向</param>
        /// <param name="parent">父物体</param>
        /// <returns>资源的实例</returns>
        public T InstantiateByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : UObject
        {
            // 先尝试从空闲池中获取
            if (m_FreeInstances.TryGetValue(assetName, out var queue) && queue.Count > 0)
            {
                var instance = queue.Dequeue();
                
                // 从LRU列表中移除
                var node = m_InstanceLruNodes[instance];
                m_InstanceLruList.Remove(node);
                m_InstanceLruNodes.Remove(instance);
                m_InstanceToFreeTime.Remove(instance);
                
                // 设置位置、旋转和父物体
                if (instance is GameObject go)
                {
                    go.transform.SetParent(parent);
                    go.transform.position = position;
                    go.transform.rotation = quaternion;
                    go.SetActive(true);
                }
                else if (instance is Component comp)
                {
                    comp.transform.SetParent(parent);
                    comp.transform.position = position;
                    comp.transform.rotation = quaternion;
                    comp.gameObject.SetActive(true);
                }
                
                return instance as T;
            }
            
            
            T asset = Load<T>(assetName);
            
            T obj = UObject.Instantiate(asset, position, quaternion, parent);
            m_InstanceToAsset[obj] = asset;
            m_InstanceToAssetName[obj] = assetName;  // 记录实例到资源名的映射
                    
            // 确保有对应的空闲队列
            if (!m_FreeInstances.ContainsKey(assetName))
            {
                m_FreeInstances[assetName] = new Queue<UObject>();
            }
            return obj;
        }
        
        
        public void InstantiateAsyncByPool<T>(string assetName, Action<T> callBack = null) where T : UObject
        {
            InstantiateAsyncByPool<T>(assetName, null, callBack);
        }
        
        public void InstantiateAsyncByPool<T>(string assetName, Transform parent, Action<T> callBack = null) where T : UObject
        {
            InstantiateAsyncByPool<T>(assetName, default, default, parent, callBack);
        }
        
        public void InstantiateAsyncByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Action<T> callBack = null) where T : UObject
        {
            InstantiateAsyncByPool<T>(assetName, position, quaternion, null, callBack);
        }
        
        /// <summary>
        /// 通过对象池异步实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="position">位置</param>
        /// <param name="quaternion">方向</param>
        /// <param name="parent">父物体</param>
        /// <param name="callBack">实例化完成回调</param>
        public void InstantiateAsyncByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent, Action<T> callBack = null) where T : UObject
        {
            // 先尝试从空闲池中获取
            if (m_FreeInstances.TryGetValue(assetName, out var queue) && queue.Count > 0)
            {
                var instance = queue.Dequeue();
                
                // 从LRU列表中移除
                var node = m_InstanceLruNodes[instance];
                m_InstanceLruList.Remove(node);
                m_InstanceLruNodes.Remove(instance);
                m_InstanceToFreeTime.Remove(instance);
                
                // 设置位置、旋转和父物体
                if (instance is GameObject go)
                {
                    go.transform.SetParent(parent);
                    go.transform.position = position;
                    go.transform.rotation = quaternion;
                    go.SetActive(true);
                }
                else if (instance is Component comp)
                {
                    comp.transform.SetParent(parent);
                    comp.transform.position = position;
                    comp.transform.rotation = quaternion;
                    comp.gameObject.SetActive(true);
                }
                
                callBack?.Invoke(instance as T);
                return;
            }
            
            // 池中没有可用实例，异步加载并创建新的
            LoadAsync<T>(assetName, (success, asset) =>
            {
                if (success)
                {
                    T obj = UObject.Instantiate(asset, position, quaternion, parent);
                    m_InstanceToAsset[obj] = asset;
                    m_InstanceToAssetName[obj] = assetName;  // 记录实例到资源名的映射
                    
                    // 确保有对应的空闲队列
                    if (!m_FreeInstances.ContainsKey(assetName))
                    {
                        m_FreeInstances[assetName] = new Queue<UObject>();
                    }
                    
                    callBack?.Invoke(obj);
                }
                else
                {
                    Debug.LogError("[Resource] There is no resource which path is " + assetName);
                }
            });
        }
        #endregion
        
        # region 资源释放
        
        /// <summary>
        /// 释放实例（如果是池化对象则回收到池中）
        /// </summary>
        /// <param name="unityObject">要释放的对象</param>
        public void Release(UObject unityObject)
        {
            if (unityObject == null) return;
            
            if (m_InstanceToAssetName.TryGetValue(unityObject, out string assetName))
            {
                // 禁用对象
                if (unityObject is GameObject go)
                {
                    go.SetActive(false);
                    go.transform.SetParent(null);
                }
                else if (unityObject is Component comp)
                {
                    comp.gameObject.SetActive(false);
                    comp.transform.SetParent(null);
                }
                
                // 池化对象，回收到池中
                if (!m_FreeInstances.TryGetValue(assetName, out var queue))
                {
                    queue = new Queue<UObject>();
                    m_FreeInstances[assetName] = queue;
                }
                queue.Enqueue(unityObject);
                
                m_InstanceToFreeTime[unityObject] = Time.time;
                var node = m_InstanceLruList.AddLast(unityObject);
                m_InstanceLruNodes[unityObject] = node;
            }
            else
            {
                // 非池化对象，直接释放
                m_LoadHelper.Release(unityObject);
            }
        }
        
        /// <summary>
        /// 清理过期的对象池实例
        /// </summary>
        private void UpdateExpiredPooledInstances()
        {
            if (m_InstanceLruList.Count == 0) return;
            
            float currentTime = Time.time;
            int cleanedCount = 0;
            
            // 从LRU列表头部开始检查（头部是最早释放的）
            var currentNode = m_InstanceLruList.First;
            while (currentNode != null && cleanedCount < MaxCleanupPerFrame)
            {
                var instance = currentNode.Value;
                var nextNode = currentNode.Next;
                
                // 检查是否过期
                if (m_InstanceToFreeTime.TryGetValue(instance, out float freeTime))
                {
                    if (currentTime - freeTime >= PooledInstanceExpireTime)
                    {
                        // 从空闲队列中移除
                        if (m_InstanceToAssetName.TryGetValue(instance, out string assetName))
                        {
                            if (m_FreeInstances.TryGetValue(assetName, out var queue) && queue.Count > 0)
                            {
                                // 检查队列头部是否就是要移除的对象
                                if (queue.Peek() == instance)
                                {
                                    queue.Dequeue();
                                }
                                else
                                {
                                    // 队列头部不是要移除的对象，理论上有错误，先抛出异常，后续可以改为更健壮的处理方式，比如重建队列
                                    throw new Exception("FreeInstances queue is out of sync with LRU list. This should not happen.");
                                }
                            }
                        }
                        
                        // 销毁实例
                        
                        var sourceAsset = m_InstanceToAsset[instance];
                        m_LoadHelper.Release(sourceAsset);
                        
                        var node = m_InstanceLruNodes[instance];
                        m_InstanceLruList.Remove(node);
                        m_InstanceLruNodes.Remove(instance);
                        m_InstanceToFreeTime.Remove(instance);
                        m_InstanceToAsset.Remove(instance);
                        m_InstanceToAssetName.Remove(instance);
            
                        if (instance is GameObject go)
                        {
                            UObject.Destroy(go);
                        }
                        else if (instance is Component comp)
                        {
                            UObject.Destroy(comp.gameObject);
                        }
                        else
                        {
                            UObject.Destroy(instance);
                        }
                        cleanedCount++;
                    }
                    else
                    {
                        // 由于LRU是按时间排序的，后面的都不会过期
                        break;
                    }
                }
                
                currentNode = nextNode;
            }
        }

        # endregion
        
        #region 接口实现

        public override void Shutdown()
        {
            m_LoadHelper.ReleaseAll();
        }

        public override int Priority => 1000;

        public override void Update()
        {
            m_LoadHelper.OnUpdate();
            UpdateExpiredPooledInstances();
        }

        #endregion

    }
}