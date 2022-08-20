using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(RawImage))]
    public class XRawImage : GUIBase
    {
        public RawImage image;

        private void Reset()
        {
            image = GetComponent<RawImage>();
        }
    }
}