using UnityEngine;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.UI.Button))]
    [UnityEngine.AddComponentMenu("XFramework/XButton")]
    public class XButton : XUIBase
    {
        public UnityEngine.UI.Button button;
        [AssetPath]
        public string soundPath;

        private void Start()
        {
            AddListener(OnClickPlaySound);
        }

        private void Reset()
        {
            button = transform.GetComponent<UnityEngine.UI.Button>();
        }

        public void AddListener(UnityEngine.Events.UnityAction call)
        {
            button.onClick.AddListener(call);
        }

        private void OnClickPlaySound()
        {
            if(!string.IsNullOrEmpty(soundPath))
            {
                SoundManager.Instance.PlaySound(soundPath);
            }
        }
    }
}