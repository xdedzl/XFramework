using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XFramework.Resource
{
    /// <summary>
    /// 通过资源路径实例化一个GameObject。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("XFramework/Resource/Resource Instantiator")]
    public class ResourceInstantiator : MonoBehaviour
    {
        private const string EditorPreviewNamePrefix = "[ResourcePreview] ";

        [SerializeField, AssetPath(typeof(GameObject))] private string resource;
        [SerializeField] private bool isPool;

        private GameObject m_Instance;

#if UNITY_EDITOR
        private bool m_EditorRefreshQueued;
#endif

        public string Resource
        {
            get => resource;
            set
            {
                if (resource == value)
                {
                    return;
                }

                resource = value;
                Refresh();
            }
        }

        public bool IsPool
        {
            get => isPool;
            set
            {
                if (isPool == value)
                {
                    return;
                }

                isPool = value;
                Refresh();
            }
        }

        private void Awake()
        {
            Refresh();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                Refresh();
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                QueueEditorRefresh();
            }
        }

        private void OnDestroy()
        {
            ReleaseInstance();
        }

        private void Refresh()
        {
            ReleaseInstance();
            if (!Application.isPlaying)
            {
                ClearEditorPreview();
            }

            if (string.IsNullOrWhiteSpace(resource))
            {
                return;
            }

            if (Application.isPlaying)
            {
                ClearEditorPreview();
                m_Instance = isPool
                    ? ResourceManager.Instance.InstantiateByPool<GameObject>(resource, transform)
                    : ResourceManager.Instance.Instantiate<GameObject>(resource, transform);
            }
            else
            {
                CreateEditorPreview();
            }
        }

        private void ReleaseInstance()
        {
            if (m_Instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                ResourceManager.Instance.Release(m_Instance);
            }
            else
            {
                DestroyImmediate(m_Instance);
            }

            m_Instance = null;
        }

#if UNITY_EDITOR
        private void CreateEditorPreview()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(resource);
            if (prefab == null)
            {
                return;
            }

            m_Instance = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
            if (m_Instance == null)
            {
                m_Instance = Instantiate(prefab, transform);
            }

            m_Instance.name = EditorPreviewNamePrefix + prefab.name;
            m_Instance.hideFlags = HideFlags.NotEditable | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            foreach (Transform child in m_Instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = m_Instance.hideFlags;
            }
        }

        private void ClearEditorPreview()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (!child.name.StartsWith(EditorPreviewNamePrefix))
                {
                    continue;
                }

                if ((child.hideFlags & HideFlags.DontSaveInEditor) == 0)
                {
                    continue;
                }

                DestroyImmediate(child);
            }
        }

        private void QueueEditorRefresh()
        {
            if (m_EditorRefreshQueued)
            {
                return;
            }

            m_EditorRefreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                m_EditorRefreshQueued = false;
                if (this == null || Application.isPlaying)
                {
                    return;
                }

                Refresh();
            };
        }
#else
        private void CreateEditorPreview() { }
        private void ClearEditorPreview() { }
        private void QueueEditorRefresh() { }
#endif
    }
}
