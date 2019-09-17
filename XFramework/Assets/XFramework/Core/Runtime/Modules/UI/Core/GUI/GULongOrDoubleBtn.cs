using UnityEngine;
using UnityEngine.Events;

namespace XFramework.UI
{
    [RequireComponent(typeof(LongOrDoubleBtn))]
    [UnityEngine.AddComponentMenu("XFramework/GULongOrDoubleBtn")]
    public class GULongOrDoubleBtn : GUIBase
    {
        public LongOrDoubleBtn longOrDoubleBtn;

        private void Reset()
        {
            longOrDoubleBtn = GetComponent<LongOrDoubleBtn>();
        }

        public void AddOnLongClick(UnityAction<float> call)
        {
            longOrDoubleBtn.onLongClick.AddListener(call);
        }

        public void AddOnDoubleClick(UnityAction call)
        {
            longOrDoubleBtn.onDoubleClick.AddListener(call);
        }
    }
}