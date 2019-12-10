using UnityEngine;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(LineChart))]
    public class GULineChat : GUIBase
    {
        public LineChart lineChat;

        private void Reset()
        {
            lineChat = transform.GetComponent<LineChart>();
        }
    }
}