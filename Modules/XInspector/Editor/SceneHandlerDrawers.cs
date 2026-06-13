using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace XFramework.Editor
{
    /// <summary>
    /// 字段场景处理器
    /// </summary>
    public abstract class FieldSceneHandler
    {
        public SceneHandlerAttribute SAttribute;

        public FieldSceneHandler(SceneHandlerAttribute attribute)
        {
            SAttribute = attribute;
        }

        public abstract void SceneHandle(XMonoBehaviourInspector inspector, FieldInfo field);
    }

    /// <summary>
    /// 字段场景处理器工厂
    /// </summary>
    public static class SceneHandlerDrawerFactory
    {
        private static readonly Dictionary<Type, Type> s_Attribute2Drawer = new Dictionary<Type, Type>();
        private static bool s_IsInitialized;

        public static void Initialize()
        {
            if (s_IsInitialized)
            {
                return;
            }

            s_IsInitialized = true;
            MoveHandlerDrawer.Register();
            RadiusHandlerDrawer.Register();
            BoundsHandlerDrawer.Register();
            DirectionHandlerDrawer.Register();
            CircleAreaHandlerDrawer.Register();
        }

        public static void Register<TAttribute, TDrawer>()
            where TAttribute : SceneHandlerAttribute
            where TDrawer : FieldSceneHandler
        {
            s_Attribute2Drawer[typeof(TAttribute)] = typeof(TDrawer);
        }

        public static List<FieldSceneHandler> Create(FieldInfo field)
        {
            Initialize();

            List<FieldSceneHandler> sceneHandlers = new List<FieldSceneHandler>();
            SceneHandlerAttribute[] attributes = (SceneHandlerAttribute[])field.GetCustomAttributes(typeof(SceneHandlerAttribute), true);
            for (int i = 0; i < attributes.Length; i++)
            {
                var attr = attributes[i];
                Type drawerType = GetSceneHandlerDrawerType(attr.GetType());
                var handler = Activator.CreateInstance(drawerType, attr) as FieldSceneHandler;
                sceneHandlers.Add(handler);
            }
            return sceneHandlers;
        }

        private static Type GetSceneHandlerDrawerType(Type type)
        {
            if (s_Attribute2Drawer.TryGetValue(type, out Type drawerType))
            {
                return drawerType;
            }

            throw new Exception($"{type.Name}没有设置对应的scene handler drawer，请补充");
        }
    }

    /// <summary>
    /// 字段场景处理器 - 移动手柄
    /// </summary>
    public sealed class MoveHandlerDrawer : FieldSceneHandler
    {
        public MoveHandlerAttribute MAttribute;

        public static void Register()
        {
            SceneHandlerDrawerFactory.Register<MoveHandlerAttribute, MoveHandlerDrawer>();
        }

        public MoveHandlerDrawer(SceneHandlerAttribute attribute) : base(attribute)
        {
            MAttribute = attribute as MoveHandlerAttribute;
        }

        public override void SceneHandle(XMonoBehaviourInspector inspector, FieldInfo field)
        {
            if (field.FieldType == typeof(Vector3))
            {
                Vector3 value = (Vector3)field.GetValue(inspector.target);

                EditorGUI.BeginChangeCheck();
                Vector3 newValue = Handles.PositionHandle(value, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspector.target, "Move Handler");
                    field.SetValue(inspector.target, newValue);
                    inspector.HasChanged();
                }
                if (MAttribute.Display != null)
                {
                    Handles.Label(newValue, MAttribute.Display);
                }
            }
            else if (field.FieldType == typeof(Vector2))
            {
                Vector2 value = (Vector2)field.GetValue(inspector.target);

                EditorGUI.BeginChangeCheck();
                Vector2 newValue = Handles.PositionHandle(value, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspector.target, "Move Handler");
                    field.SetValue(inspector.target, newValue);
                    inspector.HasChanged();
                }
                if (MAttribute.Display != null)
                {
                    Handles.Label(newValue, MAttribute.Display);
                }
            }
        }
    }

    /// <summary>
    /// 字段场景处理器 - 半径手柄
    /// </summary>
    public sealed class RadiusHandlerDrawer : FieldSceneHandler
    {
        public RadiusHandlerAttribute RAttribute;

        public static void Register()
        {
            SceneHandlerDrawerFactory.Register<RadiusHandlerAttribute, RadiusHandlerDrawer>();
        }

        public RadiusHandlerDrawer(SceneHandlerAttribute attribute) : base(attribute)
        {
            RAttribute = attribute as RadiusHandlerAttribute;
        }

        public override void SceneHandle(XMonoBehaviourInspector inspector, FieldInfo field)
        {
            if (field.FieldType == typeof(float))
            {
                Component component = inspector.target as Component;
                Vector3 center = component != null ? component.transform.position : Vector3.zero;
                float value = (float)field.GetValue(inspector.target);

                EditorGUI.BeginChangeCheck();
                float newValue = Handles.RadiusHandle(Quaternion.identity, center, value);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspector.target, "Radius Handler");
                    field.SetValue(inspector.target, newValue);
                    inspector.HasChanged();
                }
                if (RAttribute.Display != null)
                {
                    Handles.Label(center, RAttribute.Display);
                }
            }
            else if (field.FieldType == typeof(int))
            {
                Component component = inspector.target as Component;
                Vector3 center = component != null ? component.transform.position : Vector3.zero;
                int value = (int)field.GetValue(inspector.target);

                EditorGUI.BeginChangeCheck();
                int newValue = (int)Handles.RadiusHandle(Quaternion.identity, center, value);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspector.target, "Radius Handler");
                    field.SetValue(inspector.target, newValue);
                    inspector.HasChanged();
                }
                if (RAttribute.Display != null)
                {
                    Handles.Label(center, RAttribute.Display);
                }
            }
        }
    }

    /// <summary>
    /// 字段场景处理器 - 包围盒
    /// </summary>
    public sealed class BoundsHandlerDrawer : FieldSceneHandler
    {
        public BoundsHandlerAttribute BAttribute;
        public BoxBoundsHandle BoundsHandle;

        public static void Register()
        {
            SceneHandlerDrawerFactory.Register<BoundsHandlerAttribute, BoundsHandlerDrawer>();
        }

        public BoundsHandlerDrawer(SceneHandlerAttribute attribute) : base(attribute)
        {
            BAttribute = attribute as BoundsHandlerAttribute;
            BoundsHandle = new BoxBoundsHandle();
        }

        public override void SceneHandle(XMonoBehaviourInspector inspector, FieldInfo field)
        {
            if (field.FieldType == typeof(Bounds))
            {
                Bounds value = (Bounds)field.GetValue(inspector.target);
                BoundsHandle.center = value.center;
                BoundsHandle.size = value.size;

                EditorGUI.BeginChangeCheck();
                BoundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspector.target, "Bounds Handler");
                    value.center = BoundsHandle.center;
                    value.size = BoundsHandle.size;
                    field.SetValue(inspector.target, value);
                    inspector.HasChanged();
                }
                if (BAttribute.Display != null)
                {
                    Handles.Label(BoundsHandle.center, BAttribute.Display);
                }
            }
        }
    }

    /// <summary>
    /// 字段场景处理器 - 方向
    /// </summary>
    public sealed class DirectionHandlerDrawer : FieldSceneHandler
    {
        public DirectionHandlerAttribute DAttribute;
        public Transform Target;
        public Vector3 Position;
        public float ExternalSize;
        public float InternalSize;
        public float DynamicMultiple;

        public static void Register()
        {
            SceneHandlerDrawerFactory.Register<DirectionHandlerAttribute, DirectionHandlerDrawer>();
        }

        public DirectionHandlerDrawer(SceneHandlerAttribute attribute) : base(attribute)
        {
            DAttribute = attribute as DirectionHandlerAttribute;
            DynamicMultiple = 1;
        }

        public override void SceneHandle(XMonoBehaviourInspector inspector, FieldInfo field)
        {
            if (field.FieldType == typeof(Vector3))
            {
                Vector3 value = (Vector3)field.GetValue(inspector.target);

                if (value != Vector3.zero)
                {
                    ExternalSize = GetExternalSize(inspector.target);
                    InternalSize = GetInternalSize(inspector.target);
                    Handles.CircleHandleCap(0, Position, Quaternion.FromToRotation(Vector3.forward, value), ExternalSize, EventType.Repaint);
                    Handles.CircleHandleCap(0, Position, Quaternion.FromToRotation(Vector3.forward, value), InternalSize, EventType.Repaint);
                    Handles.Slider(Position, value);
                }
            }
            else if (field.FieldType == typeof(Vector2))
            {
                Vector2 value = (Vector2)field.GetValue(inspector.target);

                if (value != Vector2.zero)
                {
                    ExternalSize = GetExternalSize(inspector.target);
                    InternalSize = GetInternalSize(inspector.target);
                    Handles.CircleHandleCap(0, Position, Quaternion.FromToRotation(Vector3.forward, value), ExternalSize, EventType.Repaint);
                    Handles.CircleHandleCap(0, Position, Quaternion.FromToRotation(Vector3.forward, value), InternalSize, EventType.Repaint);
                    Handles.Slider(Position, value);
                }
            }
        }

        public float GetExternalSize(UObject target)
        {
            if (Target == null)
            {
                Component component = target as Component;
                if (component)
                {
                    Target = component.transform;
                }
            }

            if (Target != null)
            {
                Position = Target.position;
                return HandleUtility.GetHandleSize(Target.TransformPoint(Target.position)) * 1;
            }
            else
            {
                return 1;
            }
        }

        public float GetInternalSize(UObject target)
        {
            if (Target == null)
            {
                Component component = target as Component;
                if (component)
                {
                    Target = component.transform;
                }
            }

            if (DAttribute.IsDynamic)
            {
                if (DynamicMultiple < 2)
                {
                    DynamicMultiple += 0.005f;
                }
                else
                {
                    DynamicMultiple = 0;
                }
                GUI.changed = true;
            }

            if (Target != null)
            {
                Position = Target.position;
                return HandleUtility.GetHandleSize(Target.TransformPoint(Target.position)) * 0.5f * DynamicMultiple;
            }
            else
            {
                return 0.5f * DynamicMultiple;
            }
        }
    }

    /// <summary>
    /// 字段场景处理器 - 圆形区域
    /// </summary>
    public sealed class CircleAreaHandlerDrawer : FieldSceneHandler
    {
        public CircleAreaHandlerAttribute CAttribute;
        public Transform Target;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Size;
        public float DynamicMultiple;

        public static void Register()
        {
            SceneHandlerDrawerFactory.Register<CircleAreaHandlerAttribute, CircleAreaHandlerDrawer>();
        }

        public CircleAreaHandlerDrawer(SceneHandlerAttribute attribute) : base(attribute)
        {
            CAttribute = attribute as CircleAreaHandlerAttribute;
            Rotation = GetRotation();
            DynamicMultiple = 1;
        }

        public override void SceneHandle(XMonoBehaviourInspector inspector, FieldInfo field)
        {
            if (field.FieldType == typeof(float))
            {
                float value = (float)field.GetValue(inspector.target);

                Position = GetPosition(inspector.target);
                Size = GetSize(inspector.target, value);
                Handles.CircleHandleCap(0, Position, Rotation, Size, EventType.Repaint);
                if (Target)
                {
                    Handles.Slider(Position, Target.forward);
                }
            }
        }

        public Vector3 GetPosition(UObject target)
        {
            if (Target == null)
            {
                Component component = target as Component;
                if (component)
                {
                    Target = component.transform;
                }
            }

            return Target != null ? Target.position : Vector3.zero;
        }

        public Quaternion GetRotation()
        {
            if (CAttribute.Direction == CircleAreaHandlerAttribute.Axis.X)
            {
                return Quaternion.FromToRotation(Vector3.forward, Vector3.right);
            }
            else if (CAttribute.Direction == CircleAreaHandlerAttribute.Axis.Y)
            {
                return Quaternion.FromToRotation(Vector3.forward, Vector3.up);
            }
            else
            {
                return Quaternion.identity;
            }
        }

        public float GetSize(UObject target, float value)
        {
            if (CAttribute.IsDynamic)
            {
                if (DynamicMultiple < 1)
                {
                    DynamicMultiple += 0.0025f;
                }
                else
                {
                    DynamicMultiple = 0;
                }
                GUI.changed = true;
            }

            return value * DynamicMultiple;
        }
    }
}
