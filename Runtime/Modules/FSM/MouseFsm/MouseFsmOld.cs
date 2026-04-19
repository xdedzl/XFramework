using UnityEngine;
using UnityEngine.EventSystems;

namespace XFramework.FsmOld
{
    /// <summary>
    /// 鼠标事件
    /// </summary>
    public sealed class MouseFsmOld : FsmOld<MouseStateOld>
    {
        /// <summary>
        /// 当前鼠标状态
        /// </summary>
        public MouseStateOld CurrentMouseStateOld
        {
            get
            {
                return (MouseStateOld)CurrentState;
            }
        }
        /// <summary>
        /// 鼠标在上一帧的位置
        /// </summary>
        private Vector3 lastPosition;
        /// <summary>
        /// 鼠标是否移动
        /// </summary>
        public bool MouseMove { get; private set; }

        /// <summary>
        /// 每帧调用
        /// </summary>
        public override void OnUpdate()
        {
            //处理鼠标事件 当点击UI面板时不处理
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (Input.GetMouseButtonDown(0))
                {
                    CurrentMouseStateOld.OnLeftButtonDown();
                }
                else if (Input.GetMouseButton(0))
                {
                    CurrentMouseStateOld.OnLeftButtonHold();
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    CurrentMouseStateOld.OnLeftButtonUp();
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    CurrentMouseStateOld.OnRightButtonDown();
                }
                else if (Input.GetMouseButton(1))
                {
                    CurrentMouseStateOld.OnRightButtonHold();
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    CurrentMouseStateOld.OnRightButtonUp();
                }
                else if (Input.GetMouseButtonDown(2))
                {
                    CurrentMouseStateOld.OnCenterButtonDown();
                }
                else if (Input.GetMouseButton(2))
                {
                    CurrentMouseStateOld.OnCenterButtonHold();
                }
                else if (Input.GetMouseButtonUp(2))
                {
                    CurrentMouseStateOld.OnCenterButtonUp();
                }
            }

            CurrentMouseStateOld.OnUpdate();

            if (Input.mousePosition != lastPosition)
            {
                lastPosition = Input.mousePosition;
                MouseMove = true;
            }
            else
            {
                MouseMove = false;
            }
        }
    }
}