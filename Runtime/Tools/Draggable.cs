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

        public UnityEvent onBeginDrag = new UnityEvent();
        public UnityEvent onDrag = new UnityEvent();
        public UnityEvent onEndDrag = new UnityEvent();

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

            differ = Camera.main.ScreenToWorldPoint(Input.mousePosition) - target.position;
            differ.z = 0;
            onBeginDrag.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!is3D)
            {
                target.transform.position = - differ + new Vector3(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, Camera.main.ScreenToWorldPoint(Input.mousePosition).y, 0f);
            }
            else
            {

            }
            onDrag.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (target != transform)
            {
                Destroy(target.gameObject);
            }
            onEndDrag.Invoke();
        }
    }
}