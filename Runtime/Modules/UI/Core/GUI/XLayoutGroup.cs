using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;

namespace XFramework.UI
{
    [RequireComponent(typeof(LayoutGroup))]
    [UnityEngine.AddComponentMenu("XFramework/XLayoutGroup")]
    public class XLayoutGroup : XUIBase, IBindArrayObject<BindableDataSet>
    {
        public LayoutGroup layoutGroup;
        
        private GameObject m_ItemTemplate;
        private ItemChangeEvent m_OnItemChange;
        private ItemBindEvent m_OnItemBind;
        public int itemCount => m_Items.Count;

        private readonly List<UINode> m_Items = new (); 

        private void Awake()
        {
            m_OnItemChange = new ItemChangeEvent();
            m_OnItemBind = new ItemBindEvent();
            

            if (m_ItemTemplate == null)
            {
                m_ItemTemplate = transform.GetChild(0).gameObject;
            }

            if (m_ItemTemplate == null)
            {
                throw new System.Exception("XLayoutGroup需要一个实体模板，请在编辑器中指定，或在运行时将一个子物体作为模板");
            }

            UINode.GetOrAddNode<UINode>(m_ItemTemplate); 
            
            m_ItemTemplate.name += "(Template)";
            m_ItemTemplate.SetActive(false);

            if (transform.childCount > 1)
            {
                for (int i = transform.childCount - 1; i > 0; i--)
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }
        }

        private void Reset()
        {
            layoutGroup = transform.GetComponent<LayoutGroup>();
        }
        
        public void SetItemType<T>() where T : UINode
        {
            UINode.GetOrAddNode<T>(m_ItemTemplate);
        }
        
        /// <summary>
        /// 配置数量
        /// </summary>
        public void SetItemCount(int targetCount)
        {
            if (targetCount < 0)
            {
                targetCount = 0;
            }

            while (transform.childCount - 1 < targetCount)
            {
                CreateItem().SetActive(false);
            }

            m_Items.Clear();
            for (int i = 1; i < transform.childCount; i++)
            {
                bool shouldActive = m_Items.Count < targetCount;
                var go = transform.GetChild(i).gameObject;
                go.SetActive(shouldActive);

                if (!shouldActive)
                {
                    continue;
                }

                var item = go.GetComponent<UINode>();
                m_OnItemChange.Invoke(m_Items.Count, item);
                m_Items.Add(item);
            }
        }
        
        public void SetOnItemChange(UnityAction<int, UINode> callback)
        {
            m_OnItemChange.RemoveAllListeners();
            m_OnItemChange.AddListener(callback);
        }
        
        /// <summary>
        /// 新创建一个实体并返回
        /// </summary>
        /// <returns></returns>
        private GameObject CreateItem()
        {
            GameObject obj = Instantiate(m_ItemTemplate, transform);
            return obj;
        }

        /// <summary>
        /// 删除所有子物体
        /// </summary>
        public void Clear()
        {
            for (int i = layoutGroup.transform.childCount; i > 0; i--)
            {
                Destroy(layoutGroup.transform.GetChild(i - 1).gameObject);
            }
        }

        public class ItemChangeEvent : UnityEvent<int, UINode> { }
        
        
        public class ItemBindEvent : UnityEvent<int, BindableDataSet, UINode> { }

        public void OnBindArray(IBindableDataArray bindableDataArray)
        {
            SetItemCount(bindableDataArray.Count);
        }

        public void OnBindItem(BindableDataSet item, int index)
        {
            m_OnItemBind.Invoke(index, item, m_Items[index]);
        }

        public void SetOnItemBind(UnityAction<int, BindableDataSet, UINode> callback)
        {
            m_OnItemBind.RemoveAllListeners();
            m_OnItemBind.AddListener(callback);
        }
    }
}
