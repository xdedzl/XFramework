using System;
using UnityEngine.Events;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.UI.Button))]
    [UnityEngine.AddComponentMenu("XFramework/XProgressBar")]
    public class XProgressBar : XUIBase, IUIEventSource
    {
        public ProgressBar progressBar;

        public Type ListenerType => typeof(UnityAction<float>);

        private void Reset()
        {
            progressBar = transform.GetComponent<ProgressBar>();
        }

        public void AddListener(UnityAction<float> call)
        {
            progressBar.onValueChange.AddListener(call);
        }

        void IUIEventSource.AddListener(Delegate listener)
        {
            AddListener((UnityAction<float>)listener);
        }

        public float value
        {
            get
            {
                return progressBar.value;
            }
            set
            {
                progressBar.value = value;
            }
        }
    }
}
