using UnityEngine;
using UnityEngine.Events;

namespace XFramework.UI
{
    [RequireComponent(typeof(LongOrDoubleBtn))]
    public class GULongOrDoubleBtn : BaseGUI
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