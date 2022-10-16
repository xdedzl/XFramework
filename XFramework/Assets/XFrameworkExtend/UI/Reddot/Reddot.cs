using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace XFramework.UI
{
    public enum ReddotType
    {
        Common,
        Number,
        Custom,
    }

    delegate void ReddotSetter(GameObject gameObject, bool isActive);

    /// <summary>
    /// UI红点组件
    /// </summary>
    public class Reddot : MonoBehaviour
    {
        public ReddotType reddotType;
        public Vector2 offset;
        public string key;
        public GameObject target;
        private ReddotSetter reddotSetter;
        private bool m_isActive;

        public void Start()
        {
            if(target is null)
            {
                throw new System.Exception("请设置target");
            }
            switch (reddotType)
            {
                case ReddotType.Number:
                    break;
                case ReddotType.Common:
                    reddotSetter = new CommonController().SetActive;
                    break;
            }

            RegisterReddot();
        }

        public void SetActive(bool isActive)
        {
            if(m_isActive != isActive)
            {
                m_isActive = isActive;

                reddotSetter.Invoke(target, isActive);
            }
        }

        public void RegisterReddot()
        {
            ReddotManager.Instance.RegisterReddot(key, this);
        }

        public void UnRegisterRedot()
        {
            ReddotManager.Instance.UnRegisterReddot(key, this);
        }
    }

    public interface IReddotController
    {
        void SetActive(GameObject target, bool isActive);
    }

    public class CommonController : IReddotController
    {
        public void SetActive(GameObject target, bool isActive)
        {
            target.SetActive(isActive);
        }
    }
}