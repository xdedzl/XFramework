using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace XFramework.Editor
{
    /// <summary>
    /// 字段检视器
    /// </summary>
    public sealed class FieldInspector
    {
        public FieldInfo Field;
        public SerializedProperty Property;
        public List<FieldDrawer> Drawers = new List<FieldDrawer>();
        public List<FieldSceneHandler> SceneHandlers = new List<FieldSceneHandler>();
        public MethodInfo EnableCondition;
        public MethodInfo DisplayCondition;
        public string Label;
        public Color UseColor = Color.white;
        public bool IsReadOnly = false;

        /// <summary>
        /// 是否处于激活状态
        /// </summary>
        public bool IsEnable
        {
            get
            {
                bool condition = true;
                if (EnableCondition != null)
                {
                    if (EnableCondition.IsStatic)
                    {
                        condition = (bool)EnableCondition.Invoke(null, null);
                    }
                    else
                    {
                        condition = (bool)EnableCondition.Invoke(Property.serializedObject.targetObject, null);
                    }
                }
                return !IsReadOnly && condition;
            }
        }

        /// <summary>
        /// 是否在Inspector面板上显示
        /// </summary>
        public bool IsDisplay
        {
            get
            {
                bool condition = true;
                if (DisplayCondition != null)
                {
                    if (DisplayCondition.IsStatic)
                    {
                        condition = (bool)DisplayCondition.Invoke(null, null);
                    }
                    else
                    {
                        condition = (bool)DisplayCondition.Invoke(Property.serializedObject.targetObject, null);
                    }
                }
                return condition;
            }
        }

        public FieldInspector(SerializedProperty property)
        {
            BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Field = property.serializedObject.targetObject.GetType().GetField(property.name, flags);
            Property = property;
            Label = property.displayName;

            if (Field != null)
            {
                InspectorAttribute[] iattributes = (InspectorAttribute[])Field.GetCustomAttributes(typeof(InspectorAttribute), true);
                for (int i = 0; i < iattributes.Length; i++)
                {
                    if (iattributes[i] is EnableAttribute)
                    {
                        EnableCondition = property.serializedObject.targetObject.GetType().GetMethod(iattributes[i].CastTo<EnableAttribute>().Condition, flags);
                        if (EnableCondition != null && EnableCondition.ReturnType != typeof(bool))
                        {
                            EnableCondition = null;
                        }
                    }
                    else if (iattributes[i] is DisplayAttribute)
                    {
                        DisplayCondition = property.serializedObject.targetObject.GetType().GetMethod(iattributes[i].CastTo<DisplayAttribute>().Condition, flags);
                        if (DisplayCondition != null && DisplayCondition.ReturnType != typeof(bool))
                        {
                            DisplayCondition = null;
                        }
                    }
                    else if (iattributes[i] is ColorAttribute)
                    {
                        ColorAttribute attribute = iattributes[i] as ColorAttribute;
                        UseColor = new Color(attribute.R, attribute.G, attribute.B, attribute.A);
                    }
                    else if (iattributes[i] is ReadOnlyAttribute)
                    {
                        IsReadOnly = true;
                    }
                    else
                    {
                        var attr = iattributes[i];
                        var drawerType = MonoComponentInspector.GetDrawerType(attr.GetType());
                        Drawers.Add(Utility.Reflection.CreateInstance<FieldDrawer>(drawerType, attr));
                    }
                }

                SceneHandlerAttribute[] sattributes = (SceneHandlerAttribute[])Field.GetCustomAttributes(typeof(SceneHandlerAttribute), true);
                for (int i = 0; i < sattributes.Length; i++)
                {
                    if (sattributes[i] is MoveHandlerAttribute)
                    {
                        SceneHandlers.Add(new MoveHandler(sattributes[i]));
                    }
                    else if (sattributes[i] is RadiusHandlerAttribute)
                    {
                        SceneHandlers.Add(new RadiusHandler(sattributes[i]));
                    }
                    else if (sattributes[i] is BoundsHandlerAttribute)
                    {
                        SceneHandlers.Add(new BoundsHandler(sattributes[i]));
                    }
                    else if (sattributes[i] is DirectionHandlerAttribute)
                    {
                        SceneHandlers.Add(new DirectionHandler(sattributes[i]));
                    }
                    else if (sattributes[i] is CircleAreaHandlerAttribute)
                    {
                        SceneHandlers.Add(new CircleAreaHandler(sattributes[i]));
                    }
                }
            }
        }

        public void Draw(MonoComponentInspector inspector)
        {
            if (IsDisplay)
            {
                GUI.color = UseColor;
                if (Drawers.Count > 0)
                {
                    GUI.enabled = IsEnable;
                    for (int i = 0; i < Drawers.Count; i++)
                    {
                        Drawers[i].Draw(inspector, this);
                    }
                    GUI.enabled = true;
                }
                else
                {
                    if (Property.name == "m_Script")
                    {
                        GUI.enabled = false;
                        EditorGUILayout.PropertyField(Property);
                        GUI.enabled = true;
                    }
                    else
                    {
                        GUI.enabled = IsEnable;
                        EditorGUILayout.PropertyField(Property, new GUIContent(Label), true);
                        GUI.enabled = true;
                    }
                }
                GUI.color = Color.white;
            }
        }

        public void SceneHandle(MonoComponentInspector inspector)
        {
            if (IsDisplay)
            {
                if (SceneHandlers.Count > 0)
                {
                    for (int i = 0; i < SceneHandlers.Count; i++)
                    {
                        SceneHandlers[i].SceneHandle(inspector, this);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 字段绘制器
    /// </summary>
    public abstract class FieldDrawer
    {
        public InspectorAttribute IAttribute;

        public FieldDrawer(InspectorAttribute attribute)
        {
            IAttribute = attribute;
        }

        public abstract void Draw(MonoComponentInspector inspector, FieldInspector fieldInspector);
    }

    /// <summary>
    /// 字段绘制器 - 超链接
    /// </summary>
    [CustomDrawer(typeof(HyperlinkAttribute))]
    public sealed class HyperlinkDrawer : FieldDrawer
    {
        public HyperlinkAttribute HAttribute;
        public MethodInfo LinkLabel;
        public object[] Parameter;

        public HyperlinkDrawer(InspectorAttribute attribute) : base(attribute)
        {
            HAttribute = attribute as HyperlinkAttribute;
            MethodInfo[] methods = typeof(EditorGUILayout).GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                if (method.Name == "LinkLabel")
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters != null && parameters.Length > 0 && parameters[0].ParameterType == typeof(string))
                    {
                        LinkLabel = method;
                        break;
                    }
                }
            }
            Parameter = new object[] { HAttribute.Name, new GUILayoutOption[0] };
        }

        public override void Draw(MonoComponentInspector inspector, FieldInspector fieldInspector)
        {
            if (fieldInspector.Field.FieldType == typeof(string))
            {
                GUILayout.BeginHorizontal();
                bool isClick = (bool)LinkLabel.Invoke(null, Parameter);
                if (isClick)
                {
                    Application.OpenURL((string)fieldInspector.Field.GetValue(inspector.target));
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("[" + fieldInspector.Field.Name + "] can't used Hyperlink! because the types don't match!", MessageType.Error);
                GUILayout.EndHorizontal();
            }
        }
    }

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

        public abstract void SceneHandle(MonoComponentInspector inspector, FieldInspector fieldInspector);
    }

    /// <summary>
    /// 字段场景处理器 - 移动手柄
    /// </summary>
    public sealed class MoveHandler : FieldSceneHandler
    {
        public MoveHandlerAttribute MAttribute;

        public MoveHandler(SceneHandlerAttribute attribute) : base(attribute)
        {
            MAttribute = attribute as MoveHandlerAttribute;
        }

        public override void SceneHandle(MonoComponentInspector inspector, FieldInspector fieldInspector)
        {
            if (fieldInspector.Field.FieldType == typeof(Vector3))
            {
                Vector3 value = (Vector3)fieldInspector.Field.GetValue(inspector.target);

                using (new Handles.DrawingScope(fieldInspector.UseColor))
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newValue = Handles.PositionHandle(value, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(inspector.target, "Move Handler");
                        fieldInspector.Field.SetValue(inspector.target, newValue);
                        inspector.HasChanged();
                    }
                    if (MAttribute.Display != null)
                    {
                        Handles.Label(newValue, MAttribute.Display);
                    }
                }
            }
            else if (fieldInspector.Field.FieldType == typeof(Vector2))
            {
                Vector2 value = (Vector2)fieldInspector.Field.GetValue(inspector.target);

                using (new Handles.DrawingScope(fieldInspector.UseColor))
                {
                    EditorGUI.BeginChangeCheck();
                    Vector2 newValue = Handles.PositionHandle(value, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(inspector.target, "Move Handler");
                        fieldInspector.Field.SetValue(inspector.target, newValue);
                        inspector.HasChanged();
                    }
                    if (MAttribute.Display != null)
                    {
                        Handles.Label(newValue, MAttribute.Display);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 字段场景处理器 - 半径手柄
    /// </summary>
    public sealed class RadiusHandler : FieldSceneHandler
    {
        public RadiusHandlerAttribute RAttribute;

        public RadiusHandler(SceneHandlerAttribute attribute) : base(attribute)
        {
            RAttribute = attribute as RadiusHandlerAttribute;
        }

        public override void SceneHandle(MonoComponentInspector inspector, FieldInspector fieldInspector)
        {
            if (fieldInspector.Field.FieldType == typeof(float))
            {
                Component component = inspector.target as Component;
                Vector3 center = component != null ? component.transform.position : Vector3.zero;
                float value = (float)fieldInspector.Field.GetValue(inspector.target);

                using (new Handles.DrawingScope(fieldInspector.UseColor))
                {
                    EditorGUI.BeginChangeCheck();
                    float newValue = Handles.RadiusHandle(Quaternion.identity, center, value);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(inspector.target, "Radius Handler");
                        fieldInspector.Field.SetValue(inspector.target, newValue);
                        inspector.HasChanged();
                    }
                    if (RAttribute.Display != null)
                    {
                        Handles.Label(center, RAttribute.Display);
                    }
                }
            }
            else if (fieldInspector.Field.FieldType == typeof(int))
            {
                Component component = inspector.target as Component;
                Vector3 center = component != null ? component.transform.position : Vector3.zero;
                int value = (int)fieldInspector.Field.GetValue(inspector.target);

                using (new Handles.DrawingScope(fieldInspector.UseColor))
                {
                    EditorGUI.BeginChangeCheck();
                    int newValue = (int)Handles.RadiusHandle(Quaternion.identity, center, value);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(inspector.target, "Radius Handler");
                        fieldInspector.Field.SetValue(inspector.target, newValue);
                        inspector.HasChanged();
                    }
                    if (RAttribute.Display != null)
                    {
                        Handles.Label(center, RAttribute.Display);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 字段场景处理器 - 包围盒
    /// </summary>
    public sealed class BoundsHandler : FieldSceneHandler
    {
        public BoundsHandlerAttribute BAttribute;
        public BoxBoundsHandle BoundsHandle;

        public BoundsHandler(SceneHandlerAttribute attribute) : base(attribute)
        {
            BAttribute = attribute as BoundsHandlerAttribute;
            BoundsHandle = new BoxBoundsHandle();
        }

        public override void SceneHandle(MonoComponentInspector inspector, FieldInspector fieldInspector)
        {
            if (fieldInspector.Field.FieldType == typeof(Bounds))
            {
                Bounds value = (Bounds)fieldInspector.Field.GetValue(inspector.target);
                BoundsHandle.center = value.center;
                BoundsHandle.size = value.size;

                using (new Handles.DrawingScope(fieldInspector.UseColor))
                {
                    EditorGUI.BeginChangeCheck();
                    BoundsHandle.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(inspector.target, "Bounds Handler");
                        value.center = BoundsHandle.center;
                        value.size = BoundsHandle.size;
                        fieldInspector.Field.SetValue(inspector.target, value);
                        inspector.HasChanged();
                    }
                    if (BAttribute.Display != null)
                    {
                        Handles.Label(BoundsHandle.center, BAttribute.Display);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 字段场景处理器 - 方向
    /// </summary>
    public sealed class DirectionHandler : FieldSceneHandler
    {
        public DirectionHandlerAttribute DAttribute;
        public Transform Target;
        public Vector3 Position;
        public float ExternalSize;
        public float InternalSize;
        public float DynamicMultiple;

        public DirectionHandler(SceneHandlerAttribute attribute) : base(attribute)
        {
            DAttribute = attribute as DirectionHandlerAttribute;
            DynamicMultiple = 1;
        }

        public override void SceneHandle(MonoComponentInspector inspector, FieldInspector fieldInspector)
        {
            if (fieldInspector.Field.FieldType == typeof(Vector3))
            {
                Vector3 value = (Vector3)fieldInspector.Field.GetValue(inspector.target);

                if (value != Vector3.zero)
                {
                    using (new Handles.DrawingScope(fieldInspector.UseColor))
                    {
                        ExternalSize = GetExternalSize(inspector.target);
                        InternalSize = GetInternalSize(inspector.target);
                        Handles.CircleHandleCap(0, Position, Quaternion.FromToRotation(Vector3.forward, value), ExternalSize, EventType.Repaint);
                        Handles.CircleHandleCap(0, Position, Quaternion.FromToRotation(Vector3.forward, value), InternalSize, EventType.Repaint);
                        Handles.Slider(Position, value);
                    }
                }
            }
            else if (fieldInspector.Field.FieldType == typeof(Vector2))
            {
                Vector2 value = (Vector2)fieldInspector.Field.GetValue(inspector.target);

                if (value != Vector2.zero)
                {
                    using (new Handles.DrawingScope(fieldInspector.UseColor))
                    {
                        ExternalSize = GetExternalSize(inspector.target);
                        InternalSize = GetInternalSize(inspector.target);
                        Handles.CircleHandleCap(0, Position, Quaternion.FromToRotation(Vector3.forward, value), ExternalSize, EventType.Repaint);
                        Handles.CircleHandleCap(0, Position, Quaternion.FromToRotation(Vector3.forward, value), InternalSize, EventType.Repaint);
                        Handles.Slider(Position, value);
                    }
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
    public sealed class CircleAreaHandler : FieldSceneHandler
    {
        public CircleAreaHandlerAttribute CAttribute;
        public Transform Target;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Size;
        public float DynamicMultiple;

        public CircleAreaHandler(SceneHandlerAttribute attribute) : base(attribute)
        {
            CAttribute = attribute as CircleAreaHandlerAttribute;
            Rotation = GetRotation();
            DynamicMultiple = 1;
        }

        public override void SceneHandle(MonoComponentInspector inspector, FieldInspector fieldInspector)
        {
            if (fieldInspector.Field.FieldType == typeof(float))
            {
                float value = (float)fieldInspector.Field.GetValue(inspector.target);

                using (new Handles.DrawingScope(fieldInspector.UseColor))
                {
                    Position = GetPosition(inspector.target);
                    Size = GetSize(inspector.target, value);
                    Handles.CircleHandleCap(0, Position, Rotation, Size, EventType.Repaint);
                    if (Target)
                    {
                        Handles.Slider(Position, Target.forward);
                    }
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
