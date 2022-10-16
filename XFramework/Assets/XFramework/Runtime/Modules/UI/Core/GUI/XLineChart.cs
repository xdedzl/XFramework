using UnityEngine;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(LineChart))]
    public class XLineChat : XUIBase
    {
        public LineChart lineChat;

        private void Reset()
        {
            lineChat = transform.GetComponent<LineChart>();
        }
    }
}