using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class XBox : VisualElement
    {
        public XBox()
        {
            // style.flexGrow = 1;
            style.marginTop = 2;
            style.marginBottom = 2;
            style.borderLeftWidth = 2;
            style.borderRightWidth = 2;
            style.borderTopWidth = 2;
            style.borderBottomWidth = 2;
            style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f);
            style.borderRightColor = new Color(0.35f, 0.35f, 0.35f);
            style.borderTopColor = new Color(0.35f, 0.35f, 0.35f);
            style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;
        }
    }

    public class XItemBox : VisualElement
    {
        public XItemBox()
        {
            style.paddingTop = 2;
            style.paddingBottom = 2;
            style.marginLeft = 0;
            style.marginRight = 0;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopWidth = 0.5f;
            style.borderBottomWidth = 0.5f;
            style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            style.borderTopColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
            style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
        }
    }
}