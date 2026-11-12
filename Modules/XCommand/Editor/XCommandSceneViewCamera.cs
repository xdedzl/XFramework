using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using XFramework.Command;

namespace XFramework.Editor
{
    /// <summary>
    /// SceneView 相机控制（sceneview-camera）。读写 pivot / rotation / size / orthographic / in2DMode /
    /// isRotationLocked，支持六方向轴对齐吸附与按相机世界位置反推 pivot。不带选项时返回当前状态 JSON。
    /// </summary>
    internal static class XCommandSceneViewCamera
    {
        internal const string Usage = "sceneview-camera [--pivot X,Y,Z | --position X,Y,Z] [--rotation X,Y,Z | --snap +x|-x|+y|-y|+z|-z] [--size F] [--orthographic true|false] [--2d true|false] [--lock-rotation true|false] [--compact]";

        private static readonly string[] ValueOptions = { "pivot", "position", "rotation", "snap", "size", "orthographic", "2d", "lock-rotation" };
        private static readonly string[] FlagOptions = { "compact" };

        [Serializable]
        private sealed class CameraState
        {
            public Vector3 pivot;
            public Quaternion rotation;
            public Vector3 euler;
            public float size;
            public bool orthographic;
            public bool in2DMode;
            public bool isRotationLocked;
            public Vector3 cameraPosition;
        }

        internal static object Execute(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, ValueOptions, FlagOptions);
            args.RequireValueCount(0, $"usage: {Usage}");

            string pivotText = args.GetString("pivot");
            string positionText = args.GetString("position");
            string rotationText = args.GetString("rotation");
            string snapText = args.GetString("snap");
            string orthoText = args.GetString("orthographic");
            string twoDText = args.GetString("2d");
            string lockText = args.GetString("lock-rotation");
            bool hasSize = args.GetString("size") != null;
            float size = args.GetFloat("size", 0f);

            bool setOrtho = orthoText != null;
            bool ortho = setOrtho && ParseBool("orthographic", orthoText);
            bool setTwoD = twoDText != null;
            bool twoD = setTwoD && ParseBool("2d", twoDText);
            bool setLock = lockText != null;
            bool lockRotation = setLock && ParseBool("lock-rotation", lockText);

            if (rotationText != null && snapText != null)
                throw new ArgumentException("--rotation 与 --snap 不能同时指定。");
            if (pivotText != null && positionText != null)
                throw new ArgumentException("--pivot 与 --position 不能同时指定。");
            if (hasSize && (size <= 0f || !float.IsFinite(size)))
                throw new ArgumentException($"--size 必须为正有限数，收到：{size}");

            Vector3 pivot = pivotText != null ? ParseVector3("pivot", pivotText) : default;
            Vector3 position = positionText != null ? ParseVector3("position", positionText) : default;
            Quaternion rotation = rotationText != null ? Quaternion.Euler(ParseVector3("rotation", rotationText)) : default;
            Quaternion snapRotation = snapText != null ? ParseSnap(snapText) : default;

            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
                throw new InvalidOperationException("未找到打开的 SceneView 窗口，请先在 Unity 中打开 Scene 视图。");

            bool finalTwoD = setTwoD ? twoD : view.in2DMode;
            if (finalTwoD)
            {
                if (rotationText != null || snapText != null)
                    throw new ArgumentException("2D 模式下旋转固定为 identity，不能设置 --rotation/--snap；如需旋转请先 --2d false。");
                if (setOrtho && !ortho)
                    throw new ArgumentException("2D 模式强制正交，不能设置 --orthographic false；如需透视请先 --2d false。");
            }

            // 2d 切换会重置/恢复 rotation 与 ortho，必须最先；size 须在 position 之前（position 用最终 size 推距离）。
            if (setTwoD)
                view.in2DMode = twoD;
            if (setOrtho)
                view.orthographic = ortho;
            if (rotationText != null)
                view.rotation = rotation;
            else if (snapText != null)
                view.rotation = snapRotation;
            if (hasSize)
                view.size = size;
            if (pivotText != null)
                view.pivot = pivot;
            else if (positionText != null)
                view.pivot = position + view.rotation * Vector3.forward * GetCameraDistance(view);
            if (setLock)
                view.isRotationLocked = lockRotation;

            view.Repaint();

            var state = new CameraState {
                pivot = view.pivot,
                rotation = view.rotation,
                euler = view.rotation.eulerAngles,
                size = view.size,
                orthographic = view.orthographic,
                in2DMode = view.in2DMode,
                isRotationLocked = view.isRotationLocked,
                cameraPosition = view.pivot - view.rotation * Vector3.forward * GetCameraDistance(view),
            };
            return JsonUtility.ToJson(state, !args.HasFlag("compact"));
        }

        // 与 SceneView.GetTransformPosition 一致：正交距离 size*2，透视距离 size/sin(fov/2)。
        private static float GetCameraDistance(SceneView view)
        {
            if (view.orthographic)
                return view.size * 2f;
            float fov = view.cameraSettings.fieldOfView;
            return view.size / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad);
        }

        private static bool ParseBool(string name, string value)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                return false;
            throw new ArgumentException($"--{name} 只接受 true 或 false，收到：{value}");
        }

        private static Vector3 ParseVector3(string name, string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length != 3)
                throw new ArgumentException($"--{name} 需要 X,Y,Z 三个分量，收到：{value}");
            float[] components = new float[3];
            for (int i = 0; i < 3; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out components[i]))
                    throw new ArgumentException($"--{name} 的分量不是有效数字：{parts[i]}");
            }
            return new Vector3(components[0], components[1], components[2]);
        }

        private static Quaternion ParseSnap(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "+x": return Quaternion.Euler(0f, -90f, 0f);
                case "-x": return Quaternion.Euler(0f, 90f, 0f);
                case "+y": return Quaternion.Euler(90f, 0f, 0f);
                case "-y": return Quaternion.Euler(-90f, 0f, 0f);
                case "+z": return Quaternion.Euler(0f, 180f, 0f);
                case "-z": return Quaternion.Euler(0f, 0f, 0f);
                default: throw new ArgumentException($"--snap 只接受 +x/-x/+y/-y/+z/-z，收到：{value}");
            }
        }
    }
}