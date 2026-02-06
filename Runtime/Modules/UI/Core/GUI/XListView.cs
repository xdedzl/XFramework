using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(ListView))]
    public class XListView : XUIBase
    {
        public ListView listView;

        private void Reset()
        {
            listView = transform.GetComponent<ListView>();
        }
    }
}