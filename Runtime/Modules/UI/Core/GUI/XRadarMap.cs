using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(RadarMap))]
    public class XRadarMap : XUIBase
    {
        public RadarMap radarMap;

        private void Reset()
        {
            radarMap = transform.GetComponent<RadarMap>();
        }
    }
}