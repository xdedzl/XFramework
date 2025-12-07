using System.IO;
using UnityEditor;
using UnityEngine;
using XFramework;
using System.Collections.Generic;
using TMPro;

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

        [MenuItem("XFramework/GenerateScriptsGUIDFile")]
        public static void GenerateScriptsGUIDFile()
        {

            string fullPath = Path.Combine(XApplication.dataPath, "Assets", "XFramework");

            Dictionary<string, string> infos = new Dictionary<string, string>();
            foreach (var item in Utility.IO.Foreach(fullPath))
            {
                if (item.FullName.EndsWith(".cs"))
                {
                    string metaPath = item.FullName + ".meta";
                    foreach (string line in File.ReadAllLines(metaPath))
                    {
                        var values = line.Split(':');
                        if (values.Length == 2 && values[0].Trim() == "guid")
                        {
                            infos[item.Name] = values[1].Trim();
                            break;
                        }
                    } 
                }
            }
            List<string> file2guid = new List<string>();
            foreach (var item in infos)
            {
                file2guid.Add(item.Key + ":" + item.Value);
            }
            string savePath = Path.Combine(XApplication.dataPath, "ProjectSettings", "ScriptsGuid.txt");
            File.WriteAllLines(savePath, file2guid);
        }

        [MenuItem("XFramework/UpdatePrefabScriptGUID")]
        public static void UpdatePrefabScriptGUID()
        {
            Canvas canvas = GameObject.Find("Canvas").GetComponent<Canvas>();

            var root = new GameObject();
            root.transform.SetParent(canvas.transform);
            var tmp = root.AddComponent<TextMeshProUGUI>();
            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 100);

            tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            tmp.alignment = TextAlignmentOptions.Center;
        }
        
        
        private static Component[] copiedComponents;
        [MenuItem("GameObject/Copy Current Components #&C")]
        static void CopyComponents()
        {
            copiedComponents = Selection.activeGameObject.GetComponents<Component>();
            foreach (var component in copiedComponents)
            {
                if (!component) continue;
            }
            Debug.Log("已复制组件");
        }

        [MenuItem("GameObject/Paste Components If Not Present #&P")]
        static void PasteComponents()
        {
            if (copiedComponents == null)
            {
                Debug.LogWarning("没有复制的组件可粘贴。请先复制组件。");
                return;
            }

            foreach (var targetGameObject in Selection.gameObjects)
            {
                if (!targetGameObject) continue;

                bool isComponentPasted = false;
                foreach (var copiedComponent in copiedComponents)
                {
                    if (!copiedComponent) continue;
                    // Check if the targetGameObject already has the component
                    if (targetGameObject.GetComponent(copiedComponent.GetType()) == null)
                    {
                        UnityEditorInternal.ComponentUtility.CopyComponent(copiedComponent);
                        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(targetGameObject);
                        isComponentPasted = true;
                    }
                }

                if (isComponentPasted)
                {
                    Debug.Log(targetGameObject.name + "上成功粘贴了新组件。");
                }
                else
                {
                    Debug.Log(targetGameObject.name + "上已存在所有组件，没有新组件被粘贴。");
                }
            }
        }
    }


    public static class BoxCollider2DEditor
    {
        [MenuItem("GameObject/Adjust BoxCollider2D to Sprites")]
        private static void AdjustBoxColliderToSprites()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Debug.LogError("请先选择一个GameObject");
                return;
            }

            BoxCollider2D boxCollider = selectedObject.GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                Debug.LogError("选中的GameObject没有BoxCollider2D组件");
                return;
            }

            SpriteRenderer[] spriteRenderers = selectedObject.GetComponentsInChildren<SpriteRenderer>(true);
            if (spriteRenderers.Length == 0)
            {
                Debug.LogWarning("没有找到子物体的SpriteRenderer组件");
                return;
            }

            if(UUtility.Physics2D.TryUpdateBoxColliderBySprites(selectedObject, false))
            {
                Debug.Log("成功调整BoxCollider2D大小以匹配所有子物体SpriteRenderer");
            }

        }

        [MenuItem("GameObject/Adjust BoxCollider2D to Sprites", true)]
        private static bool ValidateAdjustBoxColliderToSprites()
        {
            return Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<BoxCollider2D>() != null;
        }
    }


    public static class FBXAnimationRenamer
    {
        private static bool RenameSingleFBXAnimations(Object fbxObject)
        {
            string assetPath = AssetDatabase.GetAssetPath(fbxObject);
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (modelImporter == null)
            {
                Debug.LogWarning($"无法获取ModelImporter: {assetPath}");
                return false;
            }

            // 获取FBX文件名（不包含扩展名）
            string fbxName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            // 获取动画剪辑信息
            ModelImporterClipAnimation[] clipAnimations = modelImporter.clipAnimations;
            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                clipAnimations = modelImporter.defaultClipAnimations;
            }

            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                Debug.LogWarning($"FBX文件中没有找到动画剪辑: {assetPath}");
                return false;
            }

            bool hasChanges = false;

            // 重命名所有动画剪辑
            foreach (ModelImporterClipAnimation clipAnimation in clipAnimations)
            {
                if (clipAnimation.name != fbxName)
                {
                    clipAnimation.name = fbxName;
                    hasChanges = true;
                    Debug.Log($"重命名动画: {assetPath} -> {fbxName}");
                }
            }

            if (hasChanges)
            {
                modelImporter.clipAnimations = clipAnimations;
                modelImporter.SaveAndReimport();
                return true;
            }

            return false;
        }

        // 添加右键菜单到GameObject菜单（可选）
        [MenuItem("GameObject/Rename FBX Animations", false, 30)]
        private static void RenameFBXAnimationsFromGameObject()
        {
            // 获取选中的所有对象
            Object[] selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("请先选择一个或多个FBX文件");
                return;
            }

            List<Object> fbxObjects = new List<Object>();

            // 筛选出FBX文件
            foreach (Object obj in selectedObjects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (assetPath.ToLower().EndsWith(".fbx"))
                {
                    fbxObjects.Add(obj);
                }
            }

            if (fbxObjects.Count == 0)
            {
                Debug.LogWarning("选中的文件中没有FBX格式的文件");
                return;
            }

            // 批量重命名动画
            int renameCount = 0;
            foreach (Object fbxObject in fbxObjects)
            {
                if (RenameSingleFBXAnimations(fbxObject))
                {
                    renameCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"成功处理 {renameCount} 个FBX文件中的动画重命名");
        }

        [MenuItem("GameObject/Rename FBX Animations", true)] 
        private static bool ValidateRenameFBXAnimationsFromGameObject()
        {
            // 只在选中FBX文件时启用菜单项
            Object[] selectedObjects = Selection.objects;
            if (selectedObjects == null) return false;

            foreach (Object obj in selectedObjects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (assetPath.ToLower().EndsWith(".fbx"))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
