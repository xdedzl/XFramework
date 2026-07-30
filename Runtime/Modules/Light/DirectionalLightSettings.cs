using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 方向光的可序列化参数集合。用于 <see cref="AreaLightVolume"/> 配置区域全局光，
    /// 以及 <see cref="LightVolumeManager"/> 快照/恢复全局光原始状态。
    /// </summary>
    [System.Serializable]
    public class DirectionalLightSettings
    {
        [SerializeField]
        [Tooltip("是否启用全局光。关闭时进入该区域会把全局光整体关掉。")]
        private bool enabled = true;

        [SerializeField]
        [Tooltip("光的颜色。")]
        private Color color = Color.white;

        [SerializeField]
        [Tooltip("光的强度。")]
        private float intensity = 1f;

        [SerializeField]
        [Tooltip("光的旋转角度（欧拉角），决定光照方向。")]
        private Vector3 eulerAngles = new Vector3(50f, -30f, 0f);

        [SerializeField]
        [Tooltip("阴影类型。")]
        private LightShadows shadowType = LightShadows.Soft;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("阴影强度。")]
        private float shadowStrength = 1f;

        [SerializeField]
        [Tooltip("阴影投射距离。<=0 表示不覆盖（保留全局光原值）。")]
        private float shadowDistance = 0f;

        public bool Enabled => enabled;
        public Color Color => color;
        public float Intensity => intensity;
        public Vector3 EulerAngles => eulerAngles;
        public LightShadows ShadowType => shadowType;
        public float ShadowStrength => shadowStrength;
        public float ShadowDistance => shadowDistance;

        /// <summary>
        /// 是否为有效配置（始终为 true，除非被显式置空）。
        /// 用作 <see cref="AreaLightVolume.HasLightSettings"/> 的判据。
        /// </summary>
        public bool IsValid => true;

        /// <summary>
        /// 默认配置（与 Unity 新建 Directional Light 的默认参数一致）。
        /// </summary>
        public static DirectionalLightSettings Default => new();

        /// <summary>
        /// 从一个 Light 组件采集当前参数。
        /// </summary>
        public static DirectionalLightSettings CaptureFrom(Light light)
        {
            if (light == null)
            {
                return null;
            }

            return new DirectionalLightSettings
            {
                enabled = light.enabled,
                color = light.color,
                intensity = light.intensity,
                eulerAngles = light.transform.eulerAngles,
                shadowType = light.shadows,
                shadowStrength = light.shadowStrength,
            };
        }

        /// <summary>
        /// 把参数应用到一个 Light 组件（以及 QualitySettings.shadowDistance）。
        /// </summary>
        public void ApplyTo(Light light)
        {
            if (light == null)
            {
                return;
            }

            light.enabled = enabled;
            light.color = color;
            light.intensity = intensity;
            light.transform.eulerAngles = eulerAngles;
            light.shadows = shadowType;
            light.shadowStrength = shadowStrength;

            if (shadowDistance > 0f)
            {
                QualitySettings.shadowDistance = shadowDistance;
            }
        }
    }
}
