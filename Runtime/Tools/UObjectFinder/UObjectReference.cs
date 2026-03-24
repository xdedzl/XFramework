using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 挂载到GameObject上，注册到UObjectFinder中，方便快速查找
    /// 不填key时，通过name查找；填key时，通过key/name查找
    /// </summary>
    public class UObjectReference : MonoBehaviour
    {
        [SerializeField, Label("使用自定义Key")] private bool useKey;
        [SerializeField, Display(nameof(CheckShowKey))] private string key;

        private bool CheckShowKey() => useKey;

        /// <summary>
        /// 注册到UObjectFinder中的实际路径
        /// 不填key时为gameObject.name，填key时为key/gameObject.name
        /// </summary>
        public string Path { get; private set; }

        private void Awake()
        {
            if (useKey && !string.IsNullOrEmpty(key))
            {
                Path = $"{key}/{gameObject.name}";
            }
            else
            {
                Path = gameObject.name;
            }
            UObjectFinder.Register(this);
        }

        private void OnDestroy()
        {
            UObjectFinder.Unregister(this);
        }
    }
}

