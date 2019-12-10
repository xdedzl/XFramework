using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(RadarMap))]
    public class GURadarMap : GUIBase
    {
        public RadarMap radarMap;

        private void Reset()
        {
            radarMap = transform.GetComponent<RadarMap>();
        }
    }
}