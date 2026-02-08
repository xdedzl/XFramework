using System;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace XFramework.UI
{
    [RequireComponent(typeof(LayoutGroup))]
    [UnityEngine.AddComponentMenu("XFramework/XLayoutGroup")]
    public class XLayoutGroup : XUIBase, IBindArrayObject<BindableDataSet>
    {
        public LayoutGroup layoutGroup;
        
        private GameObject m_ItemTemplate;
        private ItemChangeEvent onItemChange;
        public int itemCount { get; private set; } = 0;

        private void Awake()
        {
            onItemChange = new ItemChangeEvent();

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
            int currentCount = itemCount;
            int differ = targetCount - currentCount;

            // 先处理数量变多的情况
            if (differ > 0)
            {
                // 先激活已有的inactive对象（从第2个开始）
                int activated = 0;
                for (int i = 1; i < transform.childCount && activated < differ; i++)
                {
                    var go = transform.GetChild(i).gameObject;
                    if (!go.activeSelf)
                    {
                        go.SetActive(true);
                        activated++;
                    }
                    
                    onItemChange.Invoke(i - 1, go.GetComponent<UINode>());
                }
                // 不够则创建
                for (int i = activated; i < differ; i++)
                {
                    var go = CreateItem();
                    go.SetActive(true);
                    onItemChange.Invoke(i, go.GetComponent<UINode>());
                }
            }
            // 数量变少时，仅将多余的active对象设为false（从后往前，跳过模板）
            else if (differ < 0)
            {
                int toDisable = -differ;
                for (int i = transform.childCount - 1; i > 0 && toDisable > 0; i--)
                {
                    var go = transform.GetChild(i).gameObject;
                    if (go.activeSelf)
                    {
                        go.SetActive(false);
                        toDisable--;
                    }
                }
            }
            
            itemCount = targetCount;
        }
        
        public void SetOnItemChange(UnityAction<int, UINode> callback)
        {
            onItemChange.RemoveAllListeners();
            onItemChange.AddListener(callback);
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
        public class ItemDataSetEvent : UnityEvent<int, BindableDataSet, GameObject> { }

        public void OnBindArray(IBindableDataArray bindableDataArray)
        {
            SetItemCount(bindableDataArray.Count);
            // throw new System.NotImplementedException();
        }

        public void OnBindItem(BindableDataSet item, int index)
        {
            // throw new System.NotImplementedException();
        }
    }
}