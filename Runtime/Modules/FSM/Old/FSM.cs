using UnityEngine;

namespace XFramework.OldFsm
{
    public class FSM : MonoBehaviour
    {
        /// <summary>
        /// 攻击目标
        /// </summary>
        public Transform enemy;
        /// <summary>
        /// 攻击目标点
        /// </summary>
        public Vector3 attackPos;
        /// <summary>
        /// 移动目标点
        /// </summary>
        protected Vector3 targetPos;
        /// <summary>
        /// 攻击时间间隔
        /// </summary>
        protected float shootRate;
        /// <summary>
        /// 时间
        /// </summary>
        protected float time;
        /// <summary>
        /// 时间控制
        /// </summary>
        protected float timer;


        protected virtual void Initialize() { }
        protected virtual void FSMUpdate() { }
        protected virtual void FSMFixedUpdate() { }

        //Use this for initialization
        void Start()
        {
            Initialize();
        }

        // Update is called once per frame
        void Update()
        {
            FSMUpdate();
        }

        void FixedUpdate()
        {
            FSMFixedUpdate();
        }
    }
}