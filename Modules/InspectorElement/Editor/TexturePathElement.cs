using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    public class TexturePathElement : AssetPathElement<Texture>
    {
        private Image preview;

        public TexturePathElement()
        {
            this.AddToClassList("texture-path-element");
            this.Remove(variableNameText);
            this.Remove(assetFiled);

            VisualElement title = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                }
            };

            preview = new Image
            {
                scaleMode = ScaleMode.StretchToFill,
            };
            title.Add(variableNameText);
            title.Add(assetFiled);
            this.Add(title);

            variableNameText.RegisterCallback<MouseDownEvent>((v) =>
            {
                if (v.button == 0 && v.clickCount == 2)
                {
                    if (Contains(preview))
                    {
                        Remove(preview);
                    }
                    else
                    {
                        Add(preview);
                        RefreshPreview();
                    }
                }
            });
        }

        protected override void OnAssetChange(Texture texture)
        {
            base.OnAssetChange(texture);

            preview.image = texture;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            Texture texture = preview.image;
            if (texture != null)
            {
                float ratio = (float)texture.height / texture.width;
                preview.style.height = preview.layout.width * ratio;
            }
            else
            {
                preview.style.height = 0;
            }
        }
    }
}