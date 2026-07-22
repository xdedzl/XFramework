using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;
using XFramework.Tasks;
using UObject = UnityEngine.Object;


namespace XFramework.Resource
{
    public partial class ResourceManager
    {
        private class ResourceInstantiateHelper
        {
            private readonly Dictionary<UObject, UObject> m_InstanceToAsset = new ();                   // 实例到原始资源对象的映射
            private readonly Dictionary<UObject, string> m_InstanceToAssetName = new ();                // 实例到资源名的映射
            private readonly Dictionary<GameObject, UObject> m_GameObjectToInstance = new ();           // GameObject到实际实例对象的映射，兼容Component实例

            private readonly HashSet<UObject> m_PooledInstances = new ();                               // 当前由对象池托管的实例集合
            private readonly Dictionary<string, Queue<UObject>> m_FreeInstances = new ();               // 资源名到空闲实例队列的映射
            private readonly Dictionary<UObject, float> m_InstanceToFreeTime = new ();                  // 空闲实例进入对象池时的时间
            private readonly LinkedList<UObject> m_InstanceLruList = new();                             // 空闲实例的LRU链表，头部最早释放
            private readonly Dictionary<UObject, LinkedListNode<UObject>> m_InstanceLruNodes = new();   // 空闲实例到LRU节点的映射，用于O(1)移除

            private const float PooledInstanceExpireTime = 60f;  // 对象池中空闲对象的过期时间（秒）
            private const int MaxCleanupPerFrame = 5;            // 每帧最多清理的过期对象数量
            
            private readonly IResourceLoadHelper m_LoadHelper;
            private Transform m_NormalInstanceRoot;
            private Transform m_PooledActiveRoot;
            private Transform m_PooledFreeRoot;

            private Transform NormalInstanceRoot => m_NormalInstanceRoot ??= CreateRoot("ResourceManager_NormalInstances");
            private Transform PooledActiveRoot => m_PooledActiveRoot ??= CreateRoot("ResourceManager_PooledActiveInstances");
            private Transform PooledFreeRoot => m_PooledFreeRoot ??= CreateRoot("ResourceManager_PooledFreeInstances");
            
            public ResourceInstantiateHelper(IResourceLoadHelper loadHelper)
            {
                m_LoadHelper = loadHelper;
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
                T asset = m_LoadHelper.Load<T>(assetName);
                T obj = UObject.Instantiate<T>(asset, position, quaternion, parent ?? NormalInstanceRoot);
                RecordInstance(assetName, asset, obj, false);
                return obj;
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
            public void InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent, Action<T> callBack) where T : UObject
            {
                m_LoadHelper.LoadAsync<T>(assetName, (success, asset) =>
                {
                    if (success)
                    {
                        T obj = UObject.Instantiate(asset, position, quaternion, parent ?? NormalInstanceRoot);
                        RecordInstance(assetName, asset, obj, false);
                        callBack?.Invoke(obj);
                    }
                    else
                    {
                        Debug.LogError("[Resource] There is no resource which path is " + assetName);
                    }
                });
            }
            
            /// <summary>
            /// 异步实例化资源(支持await关键字)
            /// </summary>
            public XAwaitableTask<T> InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : UObject
            {
                var task = new XAwaitableTask<T>(null);
                InstantiateAsync<T>(assetName, position, quaternion, parent, (instance) =>
                {
                    task.SetResult(instance);
                });
                return task;
            }
            
            /// <summary>
            /// 通过对象池实例化资源
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
                        go.transform.SetParent(parent ?? PooledActiveRoot);
                        go.transform.position = position;
                        go.transform.rotation = quaternion;
                        go.SetActive(true);
                    }
                    else if (instance is Component comp)
                    {
                        comp.transform.SetParent(parent ?? PooledActiveRoot);
                        comp.transform.position = position;
                        comp.transform.rotation = quaternion;
                        comp.gameObject.SetActive(true);
                    }
                    
                    return instance as T;
                }
                
                
                T asset = m_LoadHelper.Load<T>(assetName);
                
                T obj = UObject.Instantiate(asset, position, quaternion, parent ?? PooledActiveRoot);
                RecordInstance(assetName, asset, obj, true);
                        
                // 确保有对应的空闲队列
                if (!m_FreeInstances.ContainsKey(assetName))
                {
                    m_FreeInstances[assetName] = new Queue<UObject>();
                }
                return obj;
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
            public void InstantiateAsyncByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent, Action<T> callBack) where T : UObject
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
                        go.transform.SetParent(parent ?? PooledActiveRoot);
                        go.transform.position = position;
                        go.transform.rotation = quaternion;
                        go.SetActive(true);
                    }
                    else if (instance is Component comp)
                    {
                        comp.transform.SetParent(parent ?? PooledActiveRoot);
                        comp.transform.position = position;
                        comp.transform.rotation = quaternion;
                        comp.gameObject.SetActive(true);
                    }
                    
                    callBack?.Invoke(instance as T);
                    return;
                }
                
                // 池中没有可用实例，异步加载并创建新的
                m_LoadHelper.LoadAsync<T>(assetName, (success, asset) =>
                {
                    if (success)
                    {
                        T obj = UObject.Instantiate(asset, position, quaternion, parent ?? PooledActiveRoot);
                        RecordInstance(assetName, asset, obj, true);
                        
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
            
            /// <summary>
            /// 通过对象池异步实例化资源(支持await关键字)
            /// </summary>
            public XAwaitableTask<T> InstantiateAsyncByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : UObject
            {
                var task = new XAwaitableTask<T>(null);
                InstantiateAsyncByPool<T>(assetName, position, quaternion, parent, (instance) =>
                {
                    task.SetResult(instance);
                });
                return task;
            }

            
            public void OnUpdate()
            {
                UpdateExpiredPooledInstances();
            }

            public void Shutdown()
            {
                DestroyRoot(m_NormalInstanceRoot);
                DestroyRoot(m_PooledActiveRoot);
                DestroyRoot(m_PooledFreeRoot);
            }

            public bool Release(UObject unityObject)
            {
                if (!TryResolveInstance(unityObject, out var instance))
                {
                    return false;
                }

                if (!m_PooledInstances.Contains(instance))
                {
                    UObject sourceAsset = m_InstanceToAsset.TryGetValue(instance, out UObject asset)
                        ? asset
                        : null;
                    RemoveInstanceRecord(instance);
                    if (sourceAsset != null)
                    {
                        m_LoadHelper.Release(sourceAsset);
                    }

                    DestroyInstance(instance);
                    return true;
                }

                if (m_InstanceToAssetName.TryGetValue(instance, out string assetName))
                {
                    // 禁用对象
                    if (instance is GameObject go)
                    {
                        go.SetActive(false);
                        go.transform.SetParent(PooledFreeRoot);
                    }
                    else if (instance is Component comp)
                    {
                        comp.gameObject.SetActive(false);
                        comp.transform.SetParent(PooledFreeRoot);
                    }
                
                    // 池化对象，回收到池中
                    if (!m_FreeInstances.TryGetValue(assetName, out var queue))
                    {
                        queue = new Queue<UObject>();
                        m_FreeInstances[assetName] = queue;
                    }
                    queue.Enqueue(instance);
                
                    m_InstanceToFreeTime[instance] = Time.time;
                    var node = m_InstanceLruList.AddLast(instance);
                    m_InstanceLruNodes[instance] = node;

                    return true;
                }

                return false;
            }

            public bool TryGetAssetName(UObject unityObject, out string assetName)
            {
                assetName = null;
                return TryResolveInstance(unityObject, out var instance) &&
                       m_InstanceToAssetName.TryGetValue(instance, out assetName);
            }

            public bool TryGetAsset(UObject unityObject, out UObject asset)
            {
                asset = null;
                return TryResolveInstance(unityObject, out var instance) &&
                       m_InstanceToAsset.TryGetValue(instance, out asset);
            }

            public bool TryGetAsset<T>(UObject unityObject, out T asset) where T : UObject
            {
                asset = null;
                if (!TryGetAsset(unityObject, out var sourceAsset))
                {
                    return false;
                }

                asset = sourceAsset as T;
                return asset != null;
            }

            public void CollectDebugSnapshots(
                List<ResourceInstanceGroupDebugSnapshot> groups,
                List<ResourceInstanceDebugSnapshot> instances)
            {
                var freeQueueOrders = new Dictionary<UObject, int>();
                var lruOrders = new Dictionary<UObject, int>();
                var groupNames = new HashSet<string>(m_FreeInstances.Keys, StringComparer.Ordinal);
                var normalActiveCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                var pooledActiveCounts = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (KeyValuePair<string, Queue<UObject>> pair in m_FreeInstances)
                {
                    int order = 1;
                    foreach (UObject instance in pair.Value)
                    {
                        freeQueueOrders[instance] = order++;
                    }
                }

                int lruOrder = 1;
                foreach (UObject instance in m_InstanceLruList)
                {
                    lruOrders[instance] = lruOrder++;
                }

                foreach (KeyValuePair<UObject, string> pair in m_InstanceToAssetName)
                {
                    UObject instance = pair.Key;
                    string assetName = pair.Value;
                    groupNames.Add(assetName);

                    ResourceInstanceDebugState state;
                    float freeDuration = 0f;
                    int freeQueueOrder = -1;
                    int instanceLruOrder = -1;
                    if (m_InstanceToFreeTime.TryGetValue(instance, out float freeTime))
                    {
                        state = ResourceInstanceDebugState.PooledFree;
                        freeDuration = Time.time - freeTime;
                        freeQueueOrder = freeQueueOrders[instance];
                        instanceLruOrder = lruOrders[instance];
                    }
                    else if (m_PooledInstances.Contains(instance))
                    {
                        state = ResourceInstanceDebugState.PooledActive;
                        IncrementCount(pooledActiveCounts, assetName);
                    }
                    else
                    {
                        state = ResourceInstanceDebugState.NormalActive;
                        IncrementCount(normalActiveCounts, assetName);
                    }

                    instances.Add(new ResourceInstanceDebugSnapshot(
                        instance.GetInstanceID(),
                        assetName,
                        instance,
                        m_InstanceToAsset[instance],
                        state,
                        freeDuration,
                        freeQueueOrder,
                        instanceLruOrder));
                }

                instances.Sort(CompareDebugInstances);

                var sortedGroupNames = new List<string>(groupNames);
                sortedGroupNames.Sort(StringComparer.Ordinal);
                foreach (string assetName in sortedGroupNames)
                {
                    int normalActiveCount = normalActiveCounts.TryGetValue(assetName, out int normalCount) ? normalCount : 0;
                    int pooledActiveCount = pooledActiveCounts.TryGetValue(assetName, out int pooledCount) ? pooledCount : 0;
                    int freeCount = m_FreeInstances.TryGetValue(assetName, out Queue<UObject> queue) ? queue.Count : 0;
                    groups.Add(new ResourceInstanceGroupDebugSnapshot(
                        assetName,
                        normalActiveCount,
                        pooledActiveCount,
                        freeCount));
                }
            }

            private static void IncrementCount(Dictionary<string, int> counts, string assetName)
            {
                counts.TryGetValue(assetName, out int count);
                counts[assetName] = count + 1;
            }

            private static int CompareDebugInstances(ResourceInstanceDebugSnapshot left, ResourceInstanceDebugSnapshot right)
            {
                int assetResult = string.Compare(left.AssetName, right.AssetName, StringComparison.Ordinal);
                if (assetResult != 0)
                {
                    return assetResult;
                }

                int stateResult = left.State.CompareTo(right.State);
                if (stateResult != 0)
                {
                    return stateResult;
                }

                if (left.State == ResourceInstanceDebugState.PooledFree)
                {
                    int lruResult = left.LruOrder.CompareTo(right.LruOrder);
                    if (lruResult != 0)
                    {
                        return lruResult;
                    }
                }

                return left.InstanceId.CompareTo(right.InstanceId);
            }

            private bool TryResolveInstance(UObject unityObject, out UObject instance)
            {
                instance = null;
                if (unityObject == null)
                {
                    return false;
                }

                if (m_InstanceToAssetName.ContainsKey(unityObject))
                {
                    instance = unityObject;
                    return true;
                }

                GameObject go = unityObject.GetGameObject();
                return go != null && m_GameObjectToInstance.TryGetValue(go, out instance);
            }

            private static Transform CreateRoot(string name)
            {
                var root = new GameObject(name);
                if (Application.isPlaying)
                {
                    UObject.DontDestroyOnLoad(root);
                }
                return root.transform;
            }

            private static void DestroyRoot(Transform root)
            {
                if (root == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    UObject.Destroy(root.gameObject);
                }
                else
                {
                    UObject.DestroyImmediate(root.gameObject);
                }
            }

            private static void DestroyInstance(UObject instance)
            {
                if (instance == null)
                {
                    return;
                }

                GameObject go = instance.GetGameObject();
                if (go != null)
                {
                    if (Application.isPlaying)
                    {
                        UObject.Destroy(go);
                    }
                    else
                    {
                        UObject.DestroyImmediate(go);
                    }

                    return;
                }

                if (Application.isPlaying)
                {
                    UObject.Destroy(instance);
                }
                else
                {
                    UObject.DestroyImmediate(instance);
                }
            }

            private void RecordInstance(string assetName, UObject asset, UObject instance, bool pooled)
            {
                if (instance == null)
                {
                    return;
                }

                m_InstanceToAsset[instance] = asset;
                m_InstanceToAssetName[instance] = assetName;

                if (pooled)
                {
                    m_PooledInstances.Add(instance);
                }
                else
                {
                    UnityEngine.Assertions.Assert.IsFalse(
                        m_PooledInstances.Contains(instance),
                        "[Resource] Non-pooled instance is already marked as pooled.");
                }

                var go = instance.GetGameObject();
                if (go != null)
                {
                    m_GameObjectToInstance[go] = instance;
                }
            }

            private void RemoveInstanceRecord(UObject instance)
            {
                m_InstanceToAsset.Remove(instance);
                m_InstanceToAssetName.Remove(instance);
                m_PooledInstances.Remove(instance);

                var go = instance.GetGameObject();
                if (go != null)
                {
                    m_GameObjectToInstance.Remove(go);
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
                            RemoveInstanceRecord(instance);
                
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
        }
        
        #region Instantiate No Pool
        public T Instantiate<T>(string assetName) where T : UObject
        {
            return m_InstantiateHelper.Instantiate<T>(assetName, default, default, null);
        }
            
        public T Instantiate<T>(string assetName, Transform parent) where T : UObject
        {
            return m_InstantiateHelper.Instantiate<T>(assetName, default, default, parent);
        }
            
        public T Instantiate<T>(string assetName, Vector3 position, Quaternion quaternion) where T : UObject
        {
            return m_InstantiateHelper.Instantiate<T>(assetName, position, quaternion, null);
        }

        public T Instantiate<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : UObject
        {
            return m_InstantiateHelper.Instantiate<T>(assetName, position, quaternion, parent);
        }
        
        
        public void InstantiateAsync<T>(string assetName, Action<T> callBack) where T : UObject
        {
            m_InstantiateHelper.InstantiateAsync<T>(assetName, default, default, null, callBack);
        }
            
        public void InstantiateAsync<T>(string assetName, Transform parent, Action<T> callBack) where T : UObject
        {
            m_InstantiateHelper.InstantiateAsync<T>(assetName, default, default, parent, callBack);
        }
            
        public void InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, Action<T> callBack) where T : UObject
        {
            m_InstantiateHelper.InstantiateAsync<T>(assetName, position, quaternion, null, callBack);
        }
        
        public void InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent, Action<T> callBack) where T : UObject
        {
            m_InstantiateHelper.InstantiateAsync<T>(assetName, position, quaternion, parent, callBack);
        }
        
        public XAwaitableTask<T> InstantiateAsync<T>(string assetName) where T : UObject
        {
            return m_InstantiateHelper.InstantiateAsync<T>(assetName, default, default, parent: null);
        }
            
        public XAwaitableTask<T> InstantiateAsync<T>(string assetName, Transform parent) where T : UObject
        {
            return m_InstantiateHelper.InstantiateAsync<T>(assetName, default, default, parent);
        }
            
        public XAwaitableTask<T> InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion) where T : UObject
        {
            return m_InstantiateHelper.InstantiateAsync<T>(assetName, position, quaternion, parent: null);
        }
            
        public XAwaitableTask<T> InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : UObject
        {
            return m_InstantiateHelper.InstantiateAsync<T>(assetName, position, quaternion, parent: parent);
        } 
        # endregion
        
        #region Instantiate By Pool
        public T InstantiateByPool<T>(string assetName) where T : UObject
        {
            return m_InstantiateHelper.InstantiateByPool<T>(assetName, default, default,null);
        }
        
        public T InstantiateByPool<T>(string assetName, Transform parent) where T : UObject
        {
            return m_InstantiateHelper.InstantiateByPool<T>(assetName, default, default, parent);
        }
        
        public T InstantiateByPool<T>(string assetName, Vector3 position, Quaternion quaternion) where T : UObject
        {
            return m_InstantiateHelper.InstantiateByPool<T>(assetName, position, quaternion, null);
        }
        
        public T InstantiateByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : UObject
        {
            return m_InstantiateHelper.InstantiateByPool<T>(assetName, position, quaternion, parent);
        }
        
        
        public void InstantiateAsyncByPool<T>(string assetName, Action<T> callBack) where T : UObject
        {
            m_InstantiateHelper.InstantiateAsyncByPool<T>(assetName, default, default, null, callBack);
        }
        
        public void InstantiateAsyncByPool<T>(string assetName, Transform parent, Action<T> callBack) where T : UObject
        {
            m_InstantiateHelper.InstantiateAsyncByPool<T>(assetName, default, default, parent, callBack);
        }
        
        public void InstantiateAsyncByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Action<T> callBack) where T : UObject
        {
            m_InstantiateHelper.InstantiateAsyncByPool<T>(assetName, position, quaternion, null, callBack);
        }
        
        public void InstantiateAsyncByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent, Action<T> callBack) where T : UObject
        {
            m_InstantiateHelper.InstantiateAsyncByPool<T>(assetName, position, quaternion, parent, callBack);
        }
        
        
        public XAwaitableTask<T> InstantiateAsyncByPool<T>(string assetName) where T : UObject
        {
            return m_InstantiateHelper.InstantiateAsyncByPool<T>(assetName, default, default, null);
        }
        
        public XAwaitableTask<T> InstantiateAsyncByPool<T>(string assetName, Transform parent) where T : UObject
        {
            return m_InstantiateHelper.InstantiateAsyncByPool<T>(assetName, default, default, parent);
        }
        
        public XAwaitableTask<T> InstantiateAsyncByPool<T>(string assetName, Vector3 position, Quaternion quaternion) where T : UObject
        {
            return m_InstantiateHelper.InstantiateAsyncByPool<T>(assetName, position, quaternion, null);
        }
        
        public XAwaitableTask<T> InstantiateAsyncByPool<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : UObject
        {
            return m_InstantiateHelper.InstantiateAsyncByPool<T>(assetName, position, quaternion, parent);
        }
        #endregion

        #region Instance Asset Query
        /// <summary>
        /// 尝试获取由 ResourceManager 实例化出的对象对应的资源名。
        /// </summary>
        public bool TryGetInstanceAssetName(UObject instance, out string assetName)
        {
            return m_InstantiateHelper.TryGetAssetName(instance, out assetName);
        }

        /// <summary>
        /// 尝试获取由 ResourceManager 实例化出的对象对应的原始资源。
        /// </summary>
        public bool TryGetInstanceAsset(UObject instance, out UObject asset)
        {
            return m_InstantiateHelper.TryGetAsset(instance, out asset);
        }

        /// <summary>
        /// 尝试获取由 ResourceManager 实例化出的对象对应的原始资源。
        /// </summary>
        public bool TryGetInstanceAsset<T>(UObject instance, out T asset) where T : UObject
        {
            return m_InstantiateHelper.TryGetAsset(instance, out asset);
        }
        #endregion
    }
}
