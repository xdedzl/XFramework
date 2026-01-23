using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using XFramework;
using UnityEditor;
using System.IO;

namespace XFramework.World
{
    public static class XWorldEditorManager
    {
        [MenuItem("XFramework/XWorld/Generate Trunks for Current Scene")]
        public static void GenerateCurrentSceneTrunks()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GenerateTrunks(scene.path);
        }
        

        [MenuItem("XFramework/XWorld/Generate Trunks for Current Scene")]
        public static void GenerateTrunks(string scenePath)
        {
            var scene = EditorSceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"场景无效或未加载：{scenePath} ");
                return;
            }
            
            var rootGameObjects = scene.GetRootGameObjects();

            List<GameObject> allGameObjects = new List<GameObject>();

            foreach (var rootGameObject in rootGameObjects)
            {
                DeepSplitRecursive(rootGameObject.transform);
            }



            GenerateTrunks(scenePath, allGameObjects);
            return;
            

            // 返回true表示该节点或子树有WorldSplitter，false表示没有
            bool DeepSplitRecursive(Transform node)
            {
                var spliter = node.GetComponent<WorldSplitter>();
                if (spliter != null)
                {
                    allGameObjects.AddRange(spliter.GetGameObjects());
                    return true;
                }
                bool anyChildHasSplitter = false;
                foreach (Transform child in node)
                {
                    if (DeepSplitRecursive(child))
                    {
                        anyChildHasSplitter = true;
                    }
                }
                // 如果所有子节点都没有WorldSplitter，且自己也没有WorldSplitter，则加自己
                if (!anyChildHasSplitter)
                {
                    allGameObjects.Add(node.gameObject);
                    return true; // 标记本节点已处理
                }
                return false;
            }
        }

        /// <summary>
        /// 分层网格划分：
        /// - mip0 单元大小 = baseCellSize（如 16），mip1=32，依此类推（每层 *2）。
        /// - 对每个 GameObject 的 AABB，若跨多个格子，则将 mip+1 继续判断，直至落入单一格或达到最大层。
        /// - 计算得到 (mip, x, z)；x/z 采用向下取整（支持负坐标）。
        /// </summary>
        private static void GenerateTrunks(string scenePath, IReadOnlyList<GameObject> gameObjects)
        {
            string sceneDir = Path.GetDirectoryName(scenePath).Replace("\\", "/");
            string sceneNameNoExt = Path.GetFileNameWithoutExtension(scenePath);
            string targetFolder = Path.Combine(sceneDir, sceneNameNoExt).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                string parent = sceneDir.Replace("\\", "/");
                AssetDatabase.CreateFolder(parent, sceneNameNoExt);
            }
            
            int _maxMipLevel = XWorldManager.Setting.maxMipLevel;
            float _baseCellSize = XWorldManager.Setting.baseCellSize;

            var tile2Gos = new Dictionary<TileCoord, List<GameObject>>();

            // 小的偏移量，避免边界点恰好落在分界线上产生抖动
            const float epsilon = 1e-4f;

            foreach (var go in gameObjects)
            {
                if (go == null) continue;

                // 计算 GameObject 的世界空间 AABB（剔除粒子）
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                Bounds bounds;
                if (renderers.Any())
                {
                    // 以第一个 Renderer 的 bounds 初始化，再逐个合并（对粒子渲染器跳过）
                    bounds = renderers.First().bounds;
                    foreach (var renderer in renderers.Skip(1))
                    {
                        if (renderer.GetComponent<ParticleSystem>()) continue;
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
                else
                {
                    // 无 Renderer 时，以 Transform 位置作为一个点的 AABB
                    var pos = go.transform.position;
                    bounds = new Bounds(pos, Vector3.zero);
                }

                int mip; // 在 for 语句中初始化
                int xIndex = 0, zIndex = 0;

                // 从 mip0 开始，逐层扩大单元，直到 AABB 落入单个格子
                for (mip = 0; mip <= _maxMipLevel; mip++)
                {
                    float cellSize = _baseCellSize * (1 << mip);

                    // 使用 FloorToInt 支持负坐标；max 使用一个微小的向内偏移，避免边界粘连
                    int minX = Mathf.FloorToInt((bounds.min.x) / cellSize);
                    int maxX = Mathf.FloorToInt(((bounds.max.x - epsilon)) / cellSize);
                    int minZ = Mathf.FloorToInt((bounds.min.z) / cellSize);
                    int maxZ = Mathf.FloorToInt(((bounds.max.z - epsilon)) / cellSize);

                    if (minX == maxX && minZ == maxZ)
                    {
                        xIndex = minX;
                        zIndex = minZ;
                        break;
                    }
                }

                // 若超过最大层仍未收敛，使用最后一层的近似（以中心点定位）
                if (mip > _maxMipLevel)
                {
                    mip = _maxMipLevel;
                    float cellSize = _baseCellSize * (1 << mip);
                    Vector3 center = bounds.center;
                    xIndex = Mathf.FloorToInt((center.x) / cellSize);
                    zIndex = Mathf.FloorToInt((center.z) / cellSize);
                }
                
                var coord = new TileCoord
                {
                    mip = mip,
                    x = xIndex,
                    z = zIndex,
                };

                if (!tile2Gos.ContainsKey(coord))
                {
                    var gos = new List<GameObject>();
                    tile2Gos.Add(coord, gos);
                }
                tile2Gos[coord].Add(go);
            }
            


            var trunks = tile2Gos.Select((a) =>
            {
                var coord = a.Key;
                var goArray = a.Value.ToArray();
                var prefabList = new List<GameObject>();
                for (int i = 0; i < goArray.Length; ++i)
                {
                    var go = goArray[i];
                    if (go == null) continue;
                    string prefabName = $"mip{coord.mip}_{coord.x}_{coord.z}_{go.name}";
                    string prefabPath = $"{targetFolder}/{prefabName}.prefab";
                    prefabPath = prefabPath.Replace("\\", "/");
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                    {
                        AssetDatabase.DeleteAsset(prefabPath);
                    }
                    var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
                    prefabList.Add(prefab);
                }
                
                
                var xWorldTrunk = new XWorldTrunk(a.Key, prefabList.ToArray());
                return xWorldTrunk;
            });

            var mip2Trunks = new Dictionary<int, List<XWorldTrunk>>();
            foreach (var trunk in trunks)
            {
                if (!mip2Trunks.ContainsKey(trunk.coord.mip))
                {
                    mip2Trunks.Add(trunk.coord.mip, new List<XWorldTrunk>());
                }

                var trunkList = mip2Trunks[trunk.coord.mip];
                trunkList.Add(trunk);
            }
            var mipScenes = mip2Trunks.Select((a) =>
            {
                var mipScene = new XWorldMipScene(a.Key, a.Value.ToArray());
                return mipScene;
            });

            var xWorldScene = ScriptableObject.CreateInstance<XWorldScene>();
            xWorldScene.mipScenes = mipScenes.ToArray();
            xWorldScene.scenePath = scenePath;
            
            string assetPath = Path.Combine(targetFolder, "XWorldScene.asset").Replace("\\", "/");
            AssetDatabase.CreateAsset(xWorldScene, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}

