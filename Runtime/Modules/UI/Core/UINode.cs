using UnityEngine;

namespace XFramework.UI
{
    public abstract class UINodeBase : MonoBehaviour, IComponentFindIgnore
    {
        private ComponentFindHelper<XUIBase> m_ComponentFindHelper;

        public ComponentFindHelper<XUIBase> ComponentFindHelper
        {
            get
            {
                m_ComponentFindHelper ??= ComponentFindHelper<XUIBase>.CreateHelper(gameObject);
                return m_ComponentFindHelper;
            }
        }

        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public XUIBase this[string key] => ComponentFindHelper[key];

        public T Find<T>(string path) where T : XUIBase
        {
            return this[path] as T;
        }
    }

    public class UINode : UINodeBase
    {
        public static T FindNode<T>(Transform transform, string path) where T : UINodeBase
        {
            var child = transform.Find(path);
            return GetOrAddNode<T>(child.gameObject);
        }

        public T FindNode<T>(string path) where T : UINodeBase
        {
            return FindNode<T>(transform, path);
        }

        public static T GetOrAddNode<T>(GameObject go, bool forceReplace = true) where T : UINodeBase
        {
            var node = go.GetComponent<UINodeBase>();

            if (node == null)
            {
                node = go.AddComponent<T>();
            }
            else if (typeof(T) != node.GetType())
            {
                if (forceReplace)
                {
                    DestroyImmediate(node);
                    node = go.AddComponent<T>();
                }
                else
                {
                    throw new XFrameworkException($"[UI] AddNode type mismatch, name={go.name}, expect={typeof(T)}, actual={node.GetType()}");
                }
            }

            return node as T;
        }

        public static T GetOrAddNode<T>(Transform transform, bool forceReplace = true) where T : UINodeBase
        {
            return GetOrAddNode<T>(transform.gameObject, forceReplace);
        }
    }
}
