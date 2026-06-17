using UnityEngine;

using System;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("XFramework/XButton")]
    public class XButton : XUIBase, IUIEventSource
    {
        public Button button;
        [UIClickSound]
        public string clickSoundKey;

        public Type ListenerType => typeof(UnityAction);

        private void Start()
        {
            AddListener(OnClickPlaySound);
        }

        private void Reset()
        {
            button = transform.GetComponent<Button>();
        }

        public void AddListener(UnityAction call)
        {
            button.onClick.AddListener(call);
        }

        void IUIEventSource.AddListener(Delegate listener)
        {
            AddListener((UnityAction)listener);
        }

        private void OnClickPlaySound()
        {
            if(XApplication.Setting.TryGetUIClickSoundPath(clickSoundKey, out string clickSoundPath))
            {
                SoundManager.Instance.PlaySound(clickSoundPath);
            }
        }
    }
}
