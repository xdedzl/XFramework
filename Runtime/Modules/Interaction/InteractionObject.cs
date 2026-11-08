using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace XFramework.Interaction
{
    /// <summary>
    /// 可交互对象
    /// </summary>
    public interface IInteractable
    {
    }
    
    /// <summary>
    /// 交互者
    /// </summary>
    public interface IInterlocutor
    {
        
    }
    
    
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class InteractionObject : MonoBehaviour
    {
        // 悬浮提示（在需要时延迟创建）
        private GameObject _hintRoot;
        public IInteractable interactable { get; private set; }
        
        public bool valid { get; private set; }

        private void Reset()
        {
            // 确保触发器已启用（新增组件时自动设置）
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            InitInteractable();
        }

        private void InitInteractable()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogError($"InteractionObject requires a Collider component. {gameObject.name}");
                valid = false;
                return;
            }

            if (!col.isTrigger)
            {
                Debug.LogWarning($"Collider is not set as trigger. Setting isTrigger to true. {gameObject.name}");
                valid = false;
                return;
            }

            var _interactable = GetComponent<IInteractable>();
            if (_interactable == null)
            {
                Debug.LogError($"InteractionObject requires a Interactable component. {gameObject.name}");
                valid = false;
                return;
            }

            interactable = _interactable;
            valid = true;
        }
        
        private void OnDisable()
        {
            if (InteractionManager.IsValid)
            {
                InteractionManager.Instance.Unregister(this);
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (enabled && other.CompareTag("Player"))
            {
                InteractionManager.Instance.Register(this);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (enabled && other.CompareTag("Player"))
            {
                InteractionManager.Instance.Register(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                InteractionManager.Instance.Unregister(this);
            }
        }
    }

    public class BillboardToCamera : MonoBehaviour
    {
        private Camera _cam;

        private void Start()
        {
            _cam = Camera.main;
        }

        private void LateUpdate()
        {
            if (_cam)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);
            }
        }
    }
}
