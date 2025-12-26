using System;
using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
#endif

namespace XReddot
{
    public interface IReddot
    {
        public bool IsActive { get; }
        public void SetActive(bool isActive);
    }
    

    /// <summary>
    /// UI红点组件
    /// </summary>
    public class Reddot : MonoBehaviour, IReddot
    {
        [SerializeField] private string key;
        [SerializeField] private string leafTag;
        [SerializeField] private GameObject target; 
        
        public bool IsActive => gameObject.activeSelf;

        public string LeafTag => string.IsNullOrEmpty(leafTag) ? ReddotManager.DEFAULT_RED_DOT_TAG : leafTag;
        public string Key => key;

        private void Awake()
        {
            RegisterReddot();
        }
        
        private void OnDestroy()
        {
            UnRegisterReddot();
        }

        public void SetActive(bool isActive)
        {
            var go = target == null ? gameObject : target;
            go.SetActive(isActive);
        }

        public void RegisterReddot()
        {
            ReddotManager.RegisterReddot(key, this);
        }

        public void UnRegisterReddot()
        {
            ReddotManager.UnRegisterReddot(key, this);
        }

        public void SetTag(string tag)
        {
            UnRegisterReddot();
            this.leafTag = tag;
            RegisterReddot();
        }
        
        #if UNITY_EDITOR
        private IEnumerable<string> KeyDropdown()
        {
            return ReddotManager.GetAllKeys();
        }
        #endif
    }
}