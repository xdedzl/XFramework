using UnityEngine;
using UnityEngine.EventSystems;

namespace XFramework.Fsm
{
    /// <summary>
    /// 鼠标事件
    /// </summary>
    public sealed class MouseFsm : Fsm<MouseState>
    {
        /// <summary>
        /// 当前鼠标状态
        /// </summary>
        public MouseState CurrentMouseState
        {
            get
            {
                return (MouseState)CurrentState;
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
                    CurrentMouseState.OnLeftButtonDown();
                }
                else if (Input.GetMouseButton(0))
                {
                    CurrentMouseState.OnLeftButtonHold();
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    CurrentMouseState.OnLeftButtonUp();
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    CurrentMouseState.OnRightButtonDown();
                }
                else if (Input.GetMouseButton(1))
                {
                    CurrentMouseState.OnRightButtonHold();
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    CurrentMouseState.OnRightButtonUp();
                }
                else if (Input.GetMouseButtonDown(2))
                {
                    CurrentMouseState.OnCenterButtonDown();
                }
                else if (Input.GetMouseButton(2))
                {
                    CurrentMouseState.OnCenterButtonHold();
                }
                else if (Input.GetMouseButtonUp(2))
                {
                    CurrentMouseState.OnCenterButtonUp();
                }
            }

            CurrentMouseState.OnUpdate();

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