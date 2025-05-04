using UnityEngine;

namespace XFramework.Resource
{
    public class AsyncOperationsProgress : IProgress
    {
        private AsyncOperation[] m_Operations;

        public AsyncOperationsProgress(AsyncOperation[] asyncOperations)
        {
            m_Operations = asyncOperations;
        }

        public bool IsDone
        {
            get
            {
                foreach (var item in m_Operations)
                {
                    if (!item.isDone)
                    {
                        return false;
                    }
                }
                return true;
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
                return p / m_Operations.Length;
            }
        }
    }

    public class AsyncOperationProgress : IProgress
    {
        private AsyncOperation m_Operation;

        public AsyncOperationProgress(AsyncOperation asyncOperation)
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
    }

    public class ResourceRequestProgress : IProgress
    {
        private ResourceRequest m_ResourceRequest;

        public ResourceRequestProgress(ResourceRequest resourceRequest)
        {
            m_ResourceRequest = resourceRequest;
        }

        public bool IsDone => m_ResourceRequest.asset != null;

        public float Progress => m_ResourceRequest.asset != null ? 1 : 0;
    }
}