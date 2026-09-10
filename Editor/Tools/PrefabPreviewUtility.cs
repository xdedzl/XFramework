using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace XFramework.Editor
{
    public static class PrefabPreviewUtility
    {
        private const int PreviewLayer = 31;

        public static void Render(GameObject prefab, string outputPath, int width, int height, Color background, bool isUI)
        {
            Scene originalScene = SceneManager.GetActiveScene();
            // UGUI 在普通临时场景中构建 Canvas 网格；PreviewScene 无法可靠输出 UI。
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            RenderTexture target = null;
            Texture2D screenshot = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.SetActive(true);
                foreach (Transform transform in instance.GetComponentsInChildren<Transform>(true))
                    transform.gameObject.layer = PreviewLayer;
                foreach (Camera embeddedCamera in instance.GetComponentsInChildren<Camera>(true))
                    embeddedCamera.enabled = false;

                var cameraObject = new GameObject("PrefabPreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.cameraType = CameraType.Game;
                camera.orthographic = true;
                camera.aspect = (float)width / height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.cullingMask = 1 << PreviewLayer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                foreach (Canvas canvas in instance.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = camera;
                }
                foreach (CanvasScaler scaler in instance.GetComponentsInChildren<CanvasScaler>(true))
                    scaler.enabled = false;

                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                if (!target.Create())
                    throw new InvalidOperationException($"无法创建 {width}x{height} 预览渲染纹理。");
                camera.targetTexture = target;
                if (isUI)
                    PrepareUI(prefab, instance, scene, camera, width, height);
                else
                    PrepareModel(instance, scene, camera);

                camera.Render();
                RenderTexture.active = target;
                screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                SceneManager.SetActiveScene(originalScene);
                EditorSceneManager.CloseScene(scene, true);
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                if (screenshot != null)
                    Object.DestroyImmediate(screenshot);
            }
        }

        private static void PrepareUI(GameObject prefab, GameObject instance, Scene scene, Camera camera, int width, int height)
        {
            Vector2 referenceSize = GetUIReferenceSize(prefab, width, height);
            var canvasObject = new GameObject("PrefabPreviewCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = PreviewLayer;
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = referenceSize;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;

            var root = (RectTransform)instance.transform;
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition3D = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            root.sizeDelta = referenceSize;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographicSize = Mathf.Max(referenceSize.y, referenceSize.x / camera.aspect) * 0.5f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
            foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>())
            {
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
        }

        private static Vector2 GetUIReferenceSize(GameObject prefab, int width, int height)
        {
            CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                return scaler.referenceResolution;
            Vector2 size = ((RectTransform)prefab.transform).rect.size;
            // Stretch 类型面板在资源中没有实际尺寸，使用本次输出尺寸作为画布。
            return new Vector2(size.x > 0 ? size.x : width, size.y > 0 ? size.y : height);
        }

        private static void PrepareModel(GameObject instance, Scene scene, Camera camera)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0)
                throw new InvalidOperationException("模型 Prefab 中没有启用的 Renderer，无法生成预览。");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            float radius = bounds.extents.magnitude;
            if (radius <= 0)
                throw new InvalidOperationException("模型 Prefab 的渲染包围盒为空，无法确定预览范围。");
            camera.transform.rotation = Quaternion.Euler(20, -30, 0);
            camera.transform.position = bounds.center - camera.transform.forward * (radius * 3f + 1f);
            camera.orthographicSize = radius * Mathf.Max(1f, 1f / camera.aspect) * 1.1f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = radius * 6f + 2f;
            AddPreviewLight(scene, "PrefabPreviewKeyLight", Quaternion.Euler(40, -35, 0), 1.2f);
            AddPreviewLight(scene, "PrefabPreviewFillLight", Quaternion.Euler(20, 145, 0), 0.5f);
        }

        private static void AddPreviewLight(Scene scene, string name, Quaternion rotation, float intensity)
        {
            var lightObject = new GameObject(name, typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = rotation;
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }
    }
}
