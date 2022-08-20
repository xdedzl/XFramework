using UnityEngine;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(LineChart))]
    public class XLineChat : GUIBase
    {
        public LineChart lineChat;

        private void Reset()
        {
            lineChat = transform.GetComponent<LineChart>();
        }
    }
}