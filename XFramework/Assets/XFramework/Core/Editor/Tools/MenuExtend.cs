using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    /// <summary>
    /// 菜单栏扩展
    /// </summary>
    public static class MenuExtend
    {
        [MenuItem("GameObject/CreateParent", priority = 0)]
        public static void CreateParent()
        {
            Vector3 avg_pos = Vector3.zero;
            foreach (var trans in Selection.transforms)
            {
                avg_pos += trans.position;
            }
            avg_pos /= Selection.transforms.Length;

            Transform parent = new GameObject("new parent").transform;
            parent.position = avg_pos;

            foreach (var trans in Selection.transforms)
            {
                trans.SetParent(parent, true);
            }
        }
    }
}
