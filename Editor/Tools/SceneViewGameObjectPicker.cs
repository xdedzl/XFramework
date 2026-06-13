using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    [InitializeOnLoad]
    internal static class SceneViewGameObjectPicker
    {
        private const int MaxPickCount = 512;
        private const float DragThreshold = 4f;
        private static bool s_RightMousePressed;
        private static Vector2 s_RightMouseDownPosition;

        static SceneViewGameObjectPicker()
        {
            SceneView.beforeSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var currentEvent = global::UnityEngine.Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
            {
                s_RightMousePressed = true;
                s_RightMouseDownPosition = currentEvent.mousePosition;
                return;
            }

            if (currentEvent.type != EventType.MouseUp || currentEvent.button != 1 || !s_RightMousePressed)
            {
                return;
            }

            s_RightMousePressed = false;
            if (Vector2.Distance(s_RightMouseDownPosition, currentEvent.mousePosition) > DragThreshold)
            {
                return;
            }

            List<GameObject> pickedObjects = PickAll(currentEvent.mousePosition);
            ShowMenu(pickedObjects);
            currentEvent.Use();
        }

        private static List<GameObject> PickAll(Vector2 mousePosition)
        {
            var result = new List<GameObject>();
            var pickedIds = new HashSet<int>();

            while (result.Count < MaxPickCount)
            {
                GameObject pickedObject = HandleUtility.PickGameObject(
                    mousePosition,
                    false,
                    result.ToArray());

                if (pickedObject == null || !pickedIds.Add(pickedObject.GetInstanceID()))
                {
                    break;
                }

                result.Add(pickedObject);
            }

            return result;
        }

        private static void ShowMenu(IReadOnlyList<GameObject> pickedObjects)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent($"鼠标位置对象 ({pickedObjects.Count})"));
            menu.AddSeparator(string.Empty);

            if (pickedObjects.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("未拾取到 GameObject"));
            }
            else
            {
                for (int i = 0; i < pickedObjects.Count; i++)
                {
                    GameObject pickedObject = pickedObjects[i];
                    string label = $"{pickedObject.name}({GetHierarchyPath(pickedObject.transform)})";
                    Texture icon = EditorGUIUtility.ObjectContent(pickedObject, typeof(GameObject)).image;

                    menu.AddItem(
                        new GUIContent(label, icon),
                        false,
                        () => SelectGameObject(pickedObject));
                }
            }

            menu.ShowAsContext();
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new List<string>();
            while (transform != null)
            {
                names.Add(EscapeMenuText(transform.name));
                transform = transform.parent;
            }

            names.Reverse();
            return string.Join(" > ", names);
        }

        private static string EscapeMenuText(string text)
        {
            return text.Replace("/", "\u2215");
        }

        private static void SelectGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            Selection.activeGameObject = gameObject;
            EditorGUIUtility.PingObject(gameObject);
        }
    }
}
