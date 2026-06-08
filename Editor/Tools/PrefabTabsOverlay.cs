using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    [Overlay(typeof(SceneView), "XFramework.PrefabTabsOverlay", "Prefab Tabs",
        defaultDockZone = DockZone.TopToolbar,
        defaultDockPosition = DockPosition.Top,
        defaultLayout = Layout.Panel,
        defaultDisplay = true)]
    internal sealed class PrefabTabsOverlay : Overlay
    {
        private PrefabTabsView m_View;

        public override VisualElement CreatePanelContent()
        {
            m_View = new PrefabTabsView(true);
            return m_View.Root;
        }

        public override void OnWillBeDestroyed()
        {
            m_View?.Dispose();
            m_View = null;
            base.OnWillBeDestroyed();
        }
    }
}
