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
        private const string kSnapToGroundMenuPath = "GameObject/XFramework/Snap To Ground %#G";
        private const string kTransformSnapToGroundMenuPath = "CONTEXT/Transform/XFramework/Snap To Ground";
        private const float kSnapRayStartOffset = 1000f;
        private const float kSnapRayDistance = 20000f;

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

        [MenuItem("GameObject/Group To Bottom Center Parent", priority = 0)]
        public static void GroupToBottomCenterParent()
        {
            if (Selection.transforms.Length == 0)
            {
                Debug.LogWarning("请先选择至少一个GameObject");
                return;
            }

            // 计算所有选中物体的包围盒（合并所有Renderer的bounds）
            Bounds totalBounds = new Bounds();
            bool boundsInitialized = false;

            foreach (var trans in Selection.transforms)
            {
                Renderer[] renderers = trans.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    if (!boundsInitialized)
                    {
                        totalBounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        totalBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            Vector3 parentPos;
            if (boundsInitialized)
            {
                // 底面中心：XZ取包围盒中心，Y取包围盒最低点
                parentPos = new Vector3(totalBounds.center.x, totalBounds.min.y, totalBounds.center.z);
            }
            else
            {
                // 没有Renderer时，退化为所有物体position的平均值，Y取最低
                Vector3 avg = Vector3.zero;
                float minY = float.MaxValue;
                foreach (var trans in Selection.transforms)
                {
                    avg += trans.position;
                    if (trans.position.y < minY) minY = trans.position.y;
                }
                avg /= Selection.transforms.Length;
                parentPos = new Vector3(avg.x, minY, avg.z);
            }

            // 记录Undo
            Undo.SetCurrentGroupName("Group To Bottom Center Parent");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject parentGo = new GameObject("new parent");
            Undo.RegisterCreatedObjectUndo(parentGo, "Create Parent");
            parentGo.transform.position = parentPos;

            foreach (var trans in Selection.transforms)
            {
                Undo.SetTransformParent(trans, parentGo.transform, "Reparent " + trans.name);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = parentGo;
        }

        [MenuItem(kSnapToGroundMenuPath, false, 20)]
        public static void SnapSelectedToGround()
        {
            SnapTransformsToGround(GetTopLevelSelectedTransforms());
        }

        [MenuItem(kSnapToGroundMenuPath, true)]
        public static bool ValidateSnapSelectedToGround()
        {
            foreach (var transform in Selection.transforms)
            {
                if (transform != null && !EditorUtility.IsPersistent(transform))
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem(kTransformSnapToGroundMenuPath)]
        public static void SnapContextTransformToGround(MenuCommand command)
        {
            if (command.context is Transform transform)
            {
                SnapTransformsToGround(new[] { transform });
            }
        }

        [MenuItem(kTransformSnapToGroundMenuPath, true)]
        public static bool ValidateSnapContextTransformToGround(MenuCommand command)
        {
            return command.context is Transform transform && !EditorUtility.IsPersistent(transform);
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

        private static Transform[] GetTopLevelSelectedTransforms()
        {
            var selectedTransforms = Selection.transforms;
            var selectedSet = new HashSet<Transform>(selectedTransforms);
            var result = new List<Transform>();

            foreach (var transform in selectedTransforms)
            {
                if (transform == null || EditorUtility.IsPersistent(transform))
                {
                    continue;
                }

                bool hasSelectedAncestor = false;
                Transform parent = transform.parent;
                while (parent != null)
                {
                    if (selectedSet.Contains(parent))
                    {
                        hasSelectedAncestor = true;
                        break;
                    }

                    parent = parent.parent;
                }

                if (!hasSelectedAncestor)
                {
                    result.Add(transform);
                }
            }

            return result.ToArray();
        }

        private static void SnapTransformsToGround(IReadOnlyList<Transform> transforms)
        {
            if (transforms == null || transforms.Count == 0)
            {
                Debug.LogWarning("请先选择至少一个场景中的GameObject。");
                return;
            }

            Undo.SetCurrentGroupName("Snap To Ground");
            int undoGroup = Undo.GetCurrentGroup();
            int snappedCount = 0;

            foreach (var transform in transforms)
            {
                if (transform == null || EditorUtility.IsPersistent(transform))
                {
                    continue;
                }

                if (TrySnapTransformToGround(transform))
                {
                    snappedCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (snappedCount == 0)
            {
                Debug.LogWarning("没有找到可用于贴地的地面碰撞体。");
            }
            else
            {
                Debug.Log($"贴地完成，共处理 {snappedCount} 个 GameObject。");
            }
        }

        private static bool TrySnapTransformToGround(Transform transform)
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = TryGetWorldBounds(transform, out bounds);

            float currentBottomY = hasBounds ? bounds.min.y : transform.position.y;
            Vector3 rayOrigin = transform.position;

            if (!TryGetGroundHeight(transform, rayOrigin, out float groundY))
            {
                return false;
            }

            float deltaY = groundY - currentBottomY;
            if (Mathf.Abs(deltaY) < 0.0001f)
            {
                return true;
            }

            Undo.RecordObject(transform, "Snap To Ground");

            Vector3 position = transform.position;
            position.y += deltaY;
            transform.position = position;

            PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
            return true;
        }

        private static bool TryGetWorldBounds(Transform transform, out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds(transform.position, Vector3.zero);

            foreach (var collider in transform.GetComponentsInChildren<Collider>(true))
            {
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            foreach (var collider2D in transform.GetComponentsInChildren<Collider2D>(true))
            {
                if (!hasBounds)
                {
                    bounds = collider2D.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider2D.bounds);
                }
            }

            foreach (var renderer in transform.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetGroundHeight(Transform transform, Vector3 rayOrigin, out float groundY)
        {
            bool foundGround = false;
            groundY = 0f;

            RaycastHit[] hits3D = Physics.RaycastAll(rayOrigin, Vector3.down, kSnapRayDistance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits3D, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits3D)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                groundY = hit.point.y;
                foundGround = true;
                break;
            }

            RaycastHit2D[] hits2D = Physics2D.RaycastAll(rayOrigin, Vector2.down, kSnapRayDistance);
            System.Array.Sort(hits2D, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits2D)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!foundGround || hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                    foundGround = true;
                }

                break;
            }

            return foundGround;
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
