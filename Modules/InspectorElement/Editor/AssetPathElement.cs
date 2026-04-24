using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    /// <summary>
    /// 存储资源路径信息的UI
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AssetPathElement<T> : InspectorElement where T : Object
    {
        protected ObjectField assetFiled;

        public AssetPathElement()
        {
            this.AddToClassList("asset-path-element");

            assetFiled = new ObjectField
            {
                objectType = typeof(T),
            };
            assetFiled.AddToClassList("inspector-input");
            this.Add(assetFiled);

            assetFiled.RegisterValueChangedCallback((v) =>
            {
                T asset = v.newValue as T;
                SetObjValue(asset);
            });
        }

        public override void Refresh()
        {
            base.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<T>(Value as string);
            assetFiled.value = asset;
        }

        private void SetObjValue(T asset)
        {
            if (asset != null)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                Value = path;
            }
            else
            {
                Value = null;
            }

            OnAssetChange(asset);
        }

        protected virtual void OnAssetChange(T asset)
        {

        }
    }
}