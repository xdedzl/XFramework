using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Resource
{
    public abstract class AsyncOperationsProgress<T, K> : IProgress<K> where T : AsyncOperation
    {
        protected IList<T> m_Operations;

        protected AsyncOperationsProgress(IList<T> asyncOperations)
        {
            m_Operations = asyncOperations;
        }

        public bool IsDone
        {
            get
            {
                return m_Operations.All(item => item.isDone);
            }
        }

        public float Progress
        {
            get
            {
                float p = 0;
                foreach (var item in m_Operations)
                {
                    p += item.progress;
                }
                return p / m_Operations.Count;
            }
        }

        public abstract K Result { get; }
    }

    public class AssetBundleCreateRequestsProgress : AsyncOperationsProgress<AssetBundleCreateRequest, AssetBundle>
    {
        public AssetBundleCreateRequestsProgress(IList<AssetBundleCreateRequest> asyncOperations) : base(asyncOperations) {}
        
        public override AssetBundle Result => m_Operations[0].assetBundle;
    }
    
    public abstract class AsyncOperationProgress<T, K> : IProgress<K> where T : AsyncOperation
    {
        protected T m_Operation;

        protected AsyncOperationProgress(T asyncOperation)
        {
            m_Operation = asyncOperation;
        }

        public bool IsDone
        {
            get
            {
                return m_Operation.isDone;
            }
        }

        public float Progress
        {
            get
            {
                return m_Operation.progress;
            }
        }

        public abstract K Result { get; }
    }
    
    public class AssetBundleCreateRequestProgress : AsyncOperationProgress<AssetBundleCreateRequest, AssetBundle>
    {
        public AssetBundleCreateRequestProgress(AssetBundleCreateRequest asyncOperation) : base(asyncOperation)
        {
        }
        
        public override AssetBundle Result => m_Operation.assetBundle;
    }
    
    public class AssetBundleRequestProgress : AsyncOperationProgress<AssetBundleRequest, Object>
    {
        public AssetBundleRequestProgress(AssetBundleRequest asyncOperation) : base(asyncOperation)
        {
        }
        
        public override Object Result => m_Operation.asset;
    }
    
    public class ResourceRequestProgress : IProgress
    {
        private readonly ResourceRequest m_ResourceRequest;

        public ResourceRequestProgress(ResourceRequest resourceRequest)
        {
            m_ResourceRequest = resourceRequest;
        }

        public bool IsDone => m_ResourceRequest.asset != null;

        public float Progress => m_ResourceRequest.asset != null ? 1 : 0;
    }
}