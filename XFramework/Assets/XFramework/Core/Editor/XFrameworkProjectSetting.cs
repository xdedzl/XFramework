using UnityEditor;

namespace XFramework.Editor
{
    static class XFrameworkProjectSetting
    {
        [SettingsProvider]
        public static SettingsProvider XFrameworkSetting()
        {
            var provider = new SettingsProvider("Project/XFramework", SettingsScope.Project)
            {
                label = "XFramwework",

            };
            return provider;
        }
    }
}