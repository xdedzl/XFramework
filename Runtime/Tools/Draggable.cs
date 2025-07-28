using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace XFramework
{
    /// <summary>
    /// 2D/3D物体拖拽（需在相机上挂对应的Physics Raycaster）
    /// </summary>
    public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>
        /// 空间为3D还是2D
        /// </summary>
        public bool is3D = false;
        /// <summary>
        /// 为true的话会复制一个GameObject作为被拖到的对象
        /// </summary>
        public bool cloneDrag = false;
        /// <summary>
        /// 拖动目标
        /// </summary>
        private Transform target;
        /// <summary>
        /// 鼠标落在面板上的位置和面板位置差
        /// </summary>
        private Vector3 differ;
        /// <summary>
        /// 拖拽前的位置
        /// </summary>
        public Vector3 oldPosition { get; private set; }

        public UnityEvent<PointerEventData, Draggable> onBeginDrag = new();
        public UnityEvent<PointerEventData, Draggable> onDrag = new();
        public UnityEvent<PointerEventData, Draggable> onEndDrag = new();

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (cloneDrag)
            {
                target = Instantiate(gameObject).transform;
            }
            else
            {
                target = transform;
            }
            oldPosition = transform.position;
            differ = Camera.main.ScreenToWorldPoint(eventData.position) - target.position;
            differ.z = 0;
            onBeginDrag.Invoke(eventData, this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!is3D)
            {
                target.transform.position = - differ + Camera.main.ScreenToWorldPoint(eventData.position).WithZ(target.position.z);
            }
            else
            {

            }
            onDrag.Invoke(eventData, this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (target != transform)
            {
                Destroy(target.gameObject);
            }
            onEndDrag.Invoke(eventData, this);
        }
    }
}