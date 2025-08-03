using System;
using UnityEngine.Events;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.UI.Button))]
    [UnityEngine.AddComponentMenu("XFramework/XProgressBar")]
    public class XProgressBar : XUIBase
    {
        public ProgressBar progressBar;

        private void Reset()
        {
            progressBar = transform.GetComponent<ProgressBar>();
        }

        public void AddListener(UnityAction<float> call)
        {
            progressBar.onValueChange.AddListener(call);
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