using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XFramework.UI
{
    /// <summary>
    /// 对Unity UI的扩展方法
    /// </summary>
    public static class UIExtFun
    {
        #region 对EventTrigger的扩展  但尽量不要用EventTrigger，它会循环遍历所有的接口方法

        /// <summary>
        /// 鼠标进入
        /// </summary>
        public static void AddOnPointerEnter(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.PointerEnter);
        }
        public static void AddOnPointerEnter(this EventTrigger eventTrigger, UnityAction<BaseEventData> call)
        {
            AddCall(eventTrigger, call, EventTriggerType.PointerEnter);
        }

        /// <summary>
        /// 鼠标移出
        /// </summary>
        public static void AddOnPointerExit(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.PointerExit);
        }

        /// <summary>
        /// 鼠标按下
        /// </summary>
        public static void AddOnPointerDown(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.PointerDown);
        }

        /// <summary>
        /// 鼠标抬起
        /// </summary>
        public static void AddOnPointerUp(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.PointerUp);
        }

        /// <summary>
        /// 鼠标点击（鼠标抬起时已不在原UI上时不会触发，在PointerUp之后调用）
        /// </summary>
        public static void AddOnPointerClick(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.PointerClick);
        }

        /// <summary>
        /// 鼠标拖拽时（鼠标按下不移动不会触发）
        /// </summary>
        public static void AddOnDrag(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Drag);
        }

        /// <summary>
        /// 拖拽结束时鼠标不在被拖拽UI上并且在宁外一个UI上时触发（在PointerUp之后）
        /// </summary>
        public static void AddOnDrop(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Drop);
        }

        /// <summary>
        /// 滑轮滚动时
        /// </summary>
        public static void AddOnScroll(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Scroll);
        }

        #region 物体被选中时并满足相应条件触发（用EventSystem.current.SetSelectedGameObject(gameObject)选中物体）

        /// <summary>
        /// 在被选中时
        /// </summary>
        public static void AddOnSelect(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Select);
        }

        /// <summary>
        /// 被选中后的每一帧
        /// </summary>
        public static void AddOnUpdateSelect(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.UpdateSelected);
        }

        /// <summary>
        /// 结束选中时
        /// </summary>
        public static void AddOnDeselect(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Deselect);
        }

        /// <summary>
        /// 按方向键时
        /// </summary>
        public static void AddOnMove(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Move);
        }
        public static void AddOnMove(this EventTrigger eventTrigger, UnityAction<BaseEventData> call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Move);
        }

        /// <summary>
        /// 默认为Enter键
        /// </summary>
        public static void AddOnSubmit(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Submit);
        }

        /// <summary>
        /// 默认为Esc键
        /// </summary>
        public static void AddOnCancel(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.Cancel);
        }

        #endregion

        /// <summary>
        /// 初始化拖拽（在PointerDown之后，PoinerUp之前调用，点击就会调用）
        /// </summary>
        public static void AddOnInitializePotentialDrag(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.InitializePotentialDrag);
        }

        /// <summary>
        /// 拖拽开始（鼠标按下不移动不会触发）
        /// </summary>
        public static void AddOnBeginDrag(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.BeginDrag);
        }

        /// <summary>
        /// 拖拽结束（鼠标按下不移动不会触发，在Drop之后）
        /// </summary>
        public static void AddOnEndDrag(this EventTrigger eventTrigger, UnityAction call)
        {
            AddCall(eventTrigger, call, EventTriggerType.EndDrag);
        }

        /// <summary>
        /// 给EventTrigger添加Entry
        /// </summary>
        private static void AddCall(EventTrigger eventTrigger, UnityAction call, EventTriggerType type)
        {
            EventTrigger.Entry myclick = new EventTrigger.Entry
            {
                eventID = type,
            };
            myclick.callback.AddListener((BaseEventData data) => { call?.Invoke(); });
            eventTrigger.triggers.Add(myclick);
        }

        /// <summary>
        /// 提供一种开放BaseEventData的重载
        /// 需要BaseEventData的事件也要写重载
        /// </summary>
        private static void AddCall(EventTrigger eventTrigger, UnityAction<BaseEventData> call, EventTriggerType type)
        {
            EventTrigger.Entry myclick = new EventTrigger.Entry
            {
                eventID = type,
            };
            myclick.callback.AddListener((BaseEventData data) => { call?.Invoke(data); });
            eventTrigger.triggers.Add(myclick);
        }

        #endregion
    }

}