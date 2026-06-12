using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 挂载到GameObject上，注册到UObjectFinder中，方便快速查找
    /// 不填key时，通过name查找；填key时，通过key查找
    /// </summary>
    public class UObjectReference : MonoBehaviour
    {
        public enum RegistrationMode
        {
            Single,
            List,
        }

        [SerializeField, Label("注册模式")] private RegistrationMode registrationMode;
        [SerializeField, Label("使用自定义Key"), Display(nameof(CheckShowUseKey))] private bool useKey;
        [SerializeField, Display(nameof(CheckShowKey))] private string key;

        private bool CheckShowUseKey() => registrationMode != RegistrationMode.List;
        private bool CheckShowKey() => UseKey;

        /// <summary>
         /// 注册到UObjectFinder中的实际路径
         /// 不填key时为gameObject.name，填key时为key
         /// </summary>
        public bool UseKey => registrationMode == RegistrationMode.List || useKey;
        public string Path => UseKey ? key : gameObject.name;
        public RegistrationMode Mode => registrationMode;

        private void OnValidate()
        {
            if (UseKey && string.IsNullOrWhiteSpace(key))
            {
                key = gameObject != null ? gameObject.name : string.Empty;
            }
        }

        private void Awake()
        {
            UObjectFinder.Register(this);
        }

        private void OnDestroy()
        {
            UObjectFinder.Unregister(this);
        }
    }
}
