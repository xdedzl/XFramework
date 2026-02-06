using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XFramework.UI
{
    public enum ScrollPosType
    {
        Top,
        Center,
        Bottom,
    }
    
    [RequireComponent(typeof(RectTransform))]
    public abstract class ListViewItemBase : UINode
    {
        
    }
    
    [RequireComponent(typeof(ScrollRect))]
    public class ListView : MonoBehaviour
    {
        [Tooltip("间隔"), SerializeField]
        private float m_Padding = 15f;
        [Tooltip("事先预留的最小列表高度")]
        public float PreAllocHeight = 0;
        
        
        private RectTransform m_ItemTemplate;
        private int m_ItemCount;
        private ScrollRect m_ScrollRect;
        private ListViewItemBase[] childItems;
        
        /// <summary>
        /// 循环列表中，第一个item的索引，最开始每个item都有一个原始索引，最顶部的item的原始索引就是childBufferStart
        /// 由于列表是循环复用的，所以往下滑动时，childBufferStart会从0开始到n，然后又从0开始，以此往复
        /// 如果是往上滑动，则是从0到-n，再从0开始，以此往复
        /// </summary>
        private int childBufferStart = 0;
        /// <summary>
        /// 列表中最顶部的item的真实数据索引，比如有一百条数据，复用10个item，当前最顶部是第60条数据，那么sourceDataRowStart就是59（注意索引从0开始）
        /// </summary>
        private int sourceDataRowStart;

        private bool m_IgnoreScrollChange = false;
        private float previousBuildHeight = 0;
        private const int rowsAboveBelow = 1;
        
        public int ItemCount => m_ItemCount;
        
        private ItemChangeEvent onItemChange;
        
        /// <summary>
        /// 行高
        /// </summary>
        public float RowHeight =>  m_Padding + m_ItemTemplate.GetComponent<RectTransform>().rect.height;
        /// <summary>
        /// 视图高度
        /// </summary>
        public float ViewportHeight => m_ScrollRect.viewport.rect.height;

        
        protected virtual void Awake()
        {
            onItemChange = new ItemChangeEvent();
            m_ScrollRect = GetComponent<ScrollRect>();
            m_ItemTemplate = m_ScrollRect.content.GetChild(0).GetComponent<RectTransform>();
            m_ItemTemplate.gameObject.SetActive(false);
            if (m_ItemTemplate == null)
            {
                throw new System.Exception("ListView需要一个实体模板，请在Content中添加一个子物体");
            }
            
            var node = m_ItemTemplate.GetComponent<UINode>();
            if (node == null)
            {
                m_ItemTemplate.gameObject.AddComponent<ListViewItem>();
            }
        }


        protected virtual void OnEnable()
        {
            m_ScrollRect.onValueChanged.AddListener(OnScrollChanged);
            m_IgnoreScrollChange = false;
        }

        protected virtual void OnDisable()
        {
            m_ScrollRect.onValueChanged.RemoveListener(OnScrollChanged);
        }

        
        public void SetItemType<T>() where T : ListViewItemBase
        {
            UINode.GetOrAddNode<T>(m_ItemTemplate);
        }
        
        public void SetOnItemChange(UnityAction<int, ListViewItemBase> call)
        {
            onItemChange.RemoveAllListeners();
            onItemChange.AddListener(call);
        }

        public void SetItemCount(int count)
        {
            if (ItemCount != count)
            {
                m_ItemCount = count;
                // 先禁用滚动变化
                m_IgnoreScrollChange = true;
                // 更新高度
                UpdateContentHeight();
                // 重新启用滚动变化
                m_IgnoreScrollChange = false;
                // 重新计算item
                ReorganiseContent(true);
            }
        }
        
        /// <summary>
        /// 强制刷新整个列表
        /// </summary>
        public void Refresh()
        {
            ReorganiseContent(true);
        }

        /// <summary>
        /// 刷新局部item
        /// </summary>
        public void Refresh(int startIndex, int count)
        {
            int sourceDataLimit = sourceDataRowStart + childItems.Length;
            for (int i = 0; i < count; ++i)
            {
                int row = startIndex + i;
                if (row < sourceDataRowStart || row >= sourceDataLimit)
                    continue;

                int bufIdx = WrapChildIndex(childBufferStart + row - sourceDataRowStart);
                if (childItems[bufIdx] != null)
                {
                    UpdateChild(childItems[bufIdx], row);
                }
            }
        }

        /// <summary>
        /// 强制刷新某一个item
        /// </summary>
        public void Refresh(ListViewItemBase item)
        {
            for (int i = 0; i < childItems.Length; ++i)
            {
                int idx = WrapChildIndex(childBufferStart + i);
                if (childItems[idx] != null && childItems[idx] == item)
                {
                    UpdateChild(childItems[i], sourceDataRowStart + i);
                    break;
                }
            }
        }

        /// <summary>
        /// 列表定位到某一个item
        /// </summary>
        public void ScrollToItem(int idx, ScrollPosType posType)
        {
            m_ScrollRect.verticalNormalizedPosition = GetRowScrollPosition(idx, posType);
        }

        /// <summary>
        /// 获得归一化的滚动位置，该位置将给定的行在视图中居中
        /// </summary>
        /// <param name="row">行号</param>
        /// <returns></returns>
        private float GetRowScrollPosition(int row, ScrollPosType posType)
        {
            // 视图高
            float viewportHeight = ViewportHeight;
            float rowHeight = RowHeight;
            // 将目标行滚动到列表目标位置时，列表顶部的位置
            float vpTop = 0;
            switch (posType)
            {
                case ScrollPosType.Top:
                    {
                        vpTop = row * rowHeight;
                    }
                    break;
                case ScrollPosType.Center:
                    {
                        // 目标行的中心位置与列表顶部的距离
                        float rowCentre = (row + 0.5f) * rowHeight;
                        // 视口中心位置
                        float halfVpHeight = viewportHeight * 0.5f;

                        vpTop = Mathf.Max(0, rowCentre - halfVpHeight);
                    }
                    break;
                case ScrollPosType.Bottom:
                    {
                        vpTop = (row+1) * rowHeight - viewportHeight;
                    }
                    break;
            }


            // 滚动后，列表底部的位置
            float vpBottom = vpTop + viewportHeight;
            // 列表内容总高度
            float contentHeight = m_ScrollRect.content.sizeDelta.y;
            // 如果滚动后，列表底部的位置已经超过了列表总高度，则调整列表顶部的位置
            if (vpBottom > contentHeight)
                vpTop = Mathf.Max(0, vpTop - (vpBottom - contentHeight));

            // 反插值，计算两个值之间的Lerp参数。也就是value在from和to之间的比例值
            return Mathf.InverseLerp(contentHeight - viewportHeight, 0, vpTop);
        }

        /// <summary>
        /// 根据行号获取复用的item对象
        /// </summary>
        /// <param name="row">行号</param>
        protected ListViewItemBase GetRowItem(int row)
        {
            if (childItems != null &&
                row >= sourceDataRowStart && row < sourceDataRowStart + childItems.Length &&
                row < m_ItemCount)
            {
                // 注意这里要根据行号计算复用的item原始索引
                return childItems[WrapChildIndex(childBufferStart + row - sourceDataRowStart)];
            }

            return null;
        }

        protected virtual bool CheckChildItems()
        {
            // 列表视口高度
            float viewportHeight = ViewportHeight;
            float buildHeight = Mathf.Max(viewportHeight, PreAllocHeight);
            bool rebuild = childItems == null || buildHeight > previousBuildHeight;
            if (rebuild)
            {

                int childCount = Mathf.RoundToInt(0.5f + buildHeight / RowHeight);
                childCount += rowsAboveBelow * 2;

                if (childItems == null)
                    childItems = new ListViewItemBase[childCount];
                else if (childCount > childItems.Length)
                    Array.Resize(ref childItems, childCount);

                // 创建item
                for (int i = 0; i < childItems.Length; ++i)
                {
                    if (childItems[i] == null)
                    {
                        var item = Instantiate(m_ItemTemplate);
                        childItems[i] = item.GetComponent<ListViewItemBase>();
                    }
                    childItems[i].transform.SetParent(m_ScrollRect.content, false);
                    childItems[i].gameObject.SetActive(false);
                }

                previousBuildHeight = buildHeight;
            }

            return rebuild;
        }


        /// <summary>
        /// 列表滚动时，会回调此函数
        /// </summary>
        /// <param name="normalisedPos">归一化的位置</param>
        protected virtual void OnScrollChanged(Vector2 normalisedPos)
        {
            if (!m_IgnoreScrollChange)
            {
                ReorganiseContent(false);
            }
        }

        /// <summary>
        /// 重新计算列表内容
        /// </summary>
        /// <param name="clearContents">是否要清空列表重新计算</param>
        protected virtual void ReorganiseContent(bool clearContents)
        {

            if (clearContents)
            {
                m_ScrollRect.StopMovement();
                m_ScrollRect.verticalNormalizedPosition = 1;
            }

            bool childrenChanged = CheckChildItems();
            // 是否要更新整个列表
            bool populateAll = childrenChanged || clearContents;


            float ymin = m_ScrollRect.content.localPosition.y;

            // 第一个可见item的索引
            int firstVisibleIndex = (int)(ymin / RowHeight);


            int newRowStart = firstVisibleIndex - rowsAboveBelow;

            // 滚动变化量
            int diff = newRowStart - sourceDataRowStart;
            if (populateAll || Mathf.Abs(diff) >= childItems.Length)
            {

                sourceDataRowStart = newRowStart;
                childBufferStart = 0;
                int rowIdx = newRowStart;
                foreach (var item in childItems)
                {
                    UpdateChild(item, rowIdx++);
                }

            }
            else if (diff != 0)
            {
                int newBufferStart = (childBufferStart + diff) % childItems.Length;

                if (diff < 0)
                {
                    // 向前滑动
                    for (int i = 1; i <= -diff; ++i)
                    {
                        // 得到复用item的索引
                        int wrapIndex = WrapChildIndex(childBufferStart - i);
                        int rowIdx = sourceDataRowStart - i;
                        UpdateChild(childItems[wrapIndex], rowIdx);
                    }
                }
                else
                {
                    // 向后滑动
                    int prevLastBufIdx = childBufferStart + childItems.Length - 1;
                    int prevLastRowIdx = sourceDataRowStart + childItems.Length - 1;
                    for (int i = 1; i <= diff; ++i)
                    {
                        int wrapIndex = WrapChildIndex(prevLastBufIdx + i);
                        int rowIdx = prevLastRowIdx + i;
                        UpdateChild(childItems[wrapIndex], rowIdx);
                    }
                }

                sourceDataRowStart = newRowStart;

                childBufferStart = newBufferStart;
            }
        }
        
        private int WrapChildIndex(int idx)
        {
            while (idx < 0)
                idx += childItems.Length;

            return idx % childItems.Length;
        }
        
        protected virtual void UpdateChild(ListViewItemBase child, int rowIdx)
        {
            if (rowIdx < 0 || rowIdx >= m_ItemCount)
            {
                child.gameObject.SetActive(false);
            }
            else
            {
                // 移动到正确的位置
                var childRect = m_ItemTemplate.rect;
                Vector2 pivot = m_ItemTemplate.pivot;
                float yTopPos = RowHeight * rowIdx;
                float yPos = yTopPos + (1f - pivot.y) * childRect.height;
                float xPos = 0 + pivot.x * childRect.width;
                child.RectTransform().anchoredPosition = new Vector2(xPos, -yPos);

                // 更新数据
                onItemChange.Invoke(rowIdx, child);

                child.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 更新content的高度
        /// </summary>
        private void UpdateContentHeight()
        {
            // 列表高度
            float height = m_ItemTemplate.rect.height * m_ItemCount + (m_ItemCount - 1) * m_Padding;
            // 更新content的高度
            var sz = m_ScrollRect.content.sizeDelta;
            m_ScrollRect.content.sizeDelta = new Vector2(sz.x, height);
        }
        
        public class ItemChangeEvent : UnityEvent<int, ListViewItemBase> { }
        
        public class ListViewItem : ListViewItemBase
        {
        
        }
    }
}

