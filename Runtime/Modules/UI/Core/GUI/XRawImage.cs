using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(RawImage))]
    public class XRawImage : XUIBase
    {
        public RawImage image;

        private void Reset()
        {
            image = GetComponent<RawImage>();
        }
    }
}