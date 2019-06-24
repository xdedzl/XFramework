﻿using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 挂在相机上
    /// </summary>
    public class RuntimeHandle : MonoBehaviour
    {
        private float m_HandleScale = 1;
        private float m_QuadScale = 0.2f;    // 方块长度和轴长度的比例
        private float m_ArrowScale = 1f;
        private float m_CubeScale = 0.15f;
        private float m_CircleRadius = 0.6f;

        public static Vector3 m_QuadDir = Vector3.one;

        private static bool m_LockX = false;
        private static bool m_LockY = false;
        private static bool m_LockZ = false;
        private bool m_MouseDonw = false;    // 鼠标左键是否按下

        private RuntimeHandleAxis m_SelectedAxis = RuntimeHandleAxis.None; // 当前有碰撞的轴
        private TransformMode m_TransformMode = TransformMode.Position;    // 当前控制类型
        private BaseHandle m_CurrentHandle;

        private readonly PositionHandle m_PositionHandle = new PositionHandle();
        private readonly RotationHandle m_RotationHandle = new RotationHandle();
        private readonly ScaleHandle m_ScaleHandle = new ScaleHandle();

        private Color m_SelectedColor = Color.yellow;
        private Color m_SelectedColorA = new Color(1, 0.92f, 0.016f, 0.2f);
        private Color m_RedA = new Color(1, 0, 0, 0.2f);
        private Color m_GreenA = new Color(0, 1, 0, 0.2f);
        private Color m_GlueA = new Color(0, 0, 1, 0.2f);

        private Material m_LineMaterial;
        private Material m_QuadeMaterial;
        private Material m_ShapesMaterial;

        public static Matrix4x4 m_LocalToWorld { get; private set; }
        public static float m_ScreenScale { get; private set; }
        public static Transform m_Target { get; private set; }
        public Transform testTarget;
        public static new Camera camera { get; private set; }

        public static Vector3[] circlePosX;
        public static Vector3[] circlePosY;
        public static Vector3[] circlePosZ;

        private void Awake()
        {

            m_LineMaterial = new Material(Shader.Find("RunTimeHandles/VertexColor"));
            m_LineMaterial.color = Color.white;
            m_QuadeMaterial = new Material(Shader.Find("RunTimeHandles/VertexColor"));
            m_QuadeMaterial.color = Color.white;
            m_ShapesMaterial = new Material(Shader.Find("RunTimeHandles/Shape"));
            m_ShapesMaterial.color = Color.white;

            camera = GetComponent<Camera>();
            m_CurrentHandle = m_PositionHandle;

            // 测试用
            if (testTarget)
            {
                SetTarget(testTarget);
            }
        }

        private void Update()
        {
            if (m_Target)
            {
                if (Input.GetKeyDown(KeyCode.W))
                {
                    m_CurrentHandle = m_PositionHandle;
                    m_TransformMode = TransformMode.Position;
                }
                else if (Input.GetKeyDown(KeyCode.E))
                {
                    m_CurrentHandle = m_RotationHandle;
                    m_TransformMode = TransformMode.Rotation;
                }
                else if (Input.GetKeyDown(KeyCode.R))
                {
                    m_CurrentHandle = m_ScaleHandle;
                    m_TransformMode = TransformMode.Scale;
                }

                if (!m_MouseDonw)
                    m_SelectedAxis = m_CurrentHandle.SelectedAxis();

                ControlTarget();
            }
        }

        void OnPostRender()
        {
            if (m_Target)
            {
                m_ScreenScale = GetScreenScale(m_Target.position, camera);
                m_LocalToWorld = Matrix4x4.TRS(m_Target.position, m_Target.rotation, Vector3.one * m_ScreenScale);
                DrawHandle(m_Target);
            }
        }

        /// <summary>
        /// 根据变换模式绘制不同的手柄
        /// </summary>
        private void DrawHandle(Transform target)
        {
            switch (m_TransformMode)
            {
                case TransformMode.Position:
                    DoPosition(target);
                    break;
                case TransformMode.Rotation:
                    DoRotation(target);
                    break;
                case TransformMode.Scale:
                    DoSacle(target);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 绘制位移手柄
        /// </summary>
        private void DoPosition(Transform target)
        {
            DrawCoordinate(target, true);
            DrawCoordinateArrow(target);
        }

        /// <summary>
        /// 绘制旋转手柄
        /// </summary>
        /// <param name="target"></param>
        private void DoRotation(Transform target)
        {
            Matrix4x4 transform = Matrix4x4.TRS(target.position, target.rotation, Vector3.one * m_ScreenScale);

            m_LineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(transform);
            GL.Begin(GL.LINES);

            DrawCircle(Vector3.right, m_CircleRadius, m_SelectedAxis == RuntimeHandleAxis.X ? m_SelectedColor : Color.red);
            DrawCircle(Vector3.up, m_CircleRadius, m_SelectedAxis == RuntimeHandleAxis.Y ? m_SelectedColor : Color.green);
            DrawCircle(Vector3.forward, m_CircleRadius, m_SelectedAxis == RuntimeHandleAxis.Z ? m_SelectedColor : Color.blue);

            GL.End();
            GL.PopMatrix();

            DrawScreenCircle(Color.white, target.position, 60);
        }

        /// <summary>
        /// 绘制比例手柄
        /// </summary>
        private void DoSacle(Transform target)
        {
            DrawCoordinate(target, false);
            DrawCoordinateCube(target);
        }

        /// <summary>
        /// 绘制坐标系
        /// </summary>
        private void DrawCoordinate(Transform target, bool hasQuad)
        {
            Vector3 position = target.position;
            Matrix4x4 transform = Matrix4x4.TRS(target.position, target.rotation, Vector3.one * m_ScreenScale);

            m_LineMaterial.SetPass(0);
            Vector3 x = Vector3.right * m_HandleScale;
            Vector3 y = Vector3.up * m_HandleScale;
            Vector3 z = Vector3.forward * m_HandleScale;
            Vector3 xy = x + y;
            Vector3 xz = x + z;
            Vector3 yz = y + z;
            Vector3 o = Vector3.zero;

            GL.PushMatrix();
            GL.MultMatrix(transform);   // 在绘制的时候GL会用这个矩阵转换坐标

            // 画三个坐标轴线段
            GL.Begin(GL.LINES);
            GL.Color(m_SelectedAxis == RuntimeHandleAxis.X ? m_SelectedColor : Color.red);
            GL.Vertex(o);
            GL.Vertex(x);
            GL.Color(m_SelectedAxis == RuntimeHandleAxis.Y ? m_SelectedColor : Color.green);
            GL.Vertex(o);
            GL.Vertex(y);
            GL.Color(m_SelectedAxis == RuntimeHandleAxis.Z ? m_SelectedColor : Color.blue);
            GL.Vertex(o);
            GL.Vertex(z);
            GL.End();

            Vector3 dir = position - camera.transform.position;
            float angleX = Vector3.Angle(target.right, dir);
            float angleY = Vector3.Angle(target.up, dir);
            float angleZ = Vector3.Angle(target.forward, dir);

            bool signX = angleX >= 90 && angleX < 270;
            bool signY = angleY >= 90 && angleY < 270;
            bool signZ = angleZ >= 90 && angleZ < 270;

            m_QuadDir = Vector3.one;
            if (!signX)
            {
                x = -x;
                m_QuadDir.x = -1;
            }
            if (!signY)
            {
                y = -y;
                m_QuadDir.y = -1;
            }
            if (!signZ)
            {
                z = -z;
                m_QuadDir.z = -1;
            }

            // 画方块的边框线
            if (hasQuad)
            {
                GL.Begin(GL.LINES);
                GL.Color(Color.red);
                GL.Vertex(y * m_QuadScale);
                GL.Vertex((y + z) * m_QuadScale);
                GL.Vertex((y + z) * m_QuadScale);
                GL.Vertex(z * m_QuadScale);
                GL.Color(Color.green);
                GL.Vertex(x * m_QuadScale);
                GL.Vertex((x + z) * m_QuadScale);
                GL.Vertex((x + z) * m_QuadScale);
                GL.Vertex(z * m_QuadScale);
                GL.Color(Color.blue);
                GL.Vertex(x * m_QuadScale);
                GL.Vertex((x + y) * m_QuadScale);
                GL.Vertex((x + y) * m_QuadScale);
                GL.Vertex(y * m_QuadScale);
                GL.End();

                // 画三个小方块
                GL.Begin(GL.QUADS);
                GL.Color(m_SelectedAxis == RuntimeHandleAxis.YZ ? m_SelectedColorA : m_RedA);
                GL.Vertex(o * m_QuadScale);
                GL.Vertex(y * m_QuadScale);
                GL.Vertex((y + z) * m_QuadScale);
                GL.Vertex(z * m_QuadScale);
                GL.Color(m_SelectedAxis == RuntimeHandleAxis.XZ ? m_SelectedColorA : m_GreenA);
                GL.Vertex(o * m_QuadScale);
                GL.Vertex(x * m_QuadScale);
                GL.Vertex((x + z) * m_QuadScale);
                GL.Vertex(z * m_QuadScale);
                GL.Color(m_SelectedAxis == RuntimeHandleAxis.XY ? m_SelectedColorA : m_GlueA);
                GL.Vertex(o * m_QuadScale);
                GL.Vertex(x * m_QuadScale);
                GL.Vertex((x + y) * m_QuadScale);
                GL.Vertex(y * m_QuadScale);
                GL.End();
            }

            GL.PopMatrix();
        }

        /// <summary>
        /// 画一个空心圆
        /// </summary>
        private void DrawCircle(Vector3 axis, float radius, Color color)
        {
            int detlaAngle = 10;
            float x;
            float y;
            GL.Color(color);

            Vector3 start;
            if (axis.x == 1)
                start = Vector3.up * radius;
            else
                start = Vector3.right * radius;
            Vector3[] circlePos;

            circlePos = new Vector3[360 / detlaAngle];

            GL.Vertex(start);
            circlePos[0] = start;
            for (int i = 1; i < 360 / detlaAngle; i++)
            {
                x = Mathf.Cos(i * detlaAngle * Mathf.Deg2Rad) * radius;
                y = Mathf.Sin(i * detlaAngle * Mathf.Deg2Rad) * radius;

                Vector3 temp;
                if (axis.x == 1)
                    temp = new Vector3(0, x, y);
                else if (axis.y == 1)
                    temp = new Vector3(x, 0, y);
                else
                    temp = new Vector3(x, y, 0);
                GL.Vertex(temp);
                GL.Vertex(temp);
                circlePos[i] = temp;
            }
            GL.Vertex(start);

            if (axis.x == 1)
                circlePosX = circlePos;
            else if (axis.y == 1)
                circlePosY = circlePos;
            else
                circlePosZ = circlePos;
        }

        /// <summary>
        /// 绘制坐标系箭头
        /// </summary>
        private void DrawCoordinateArrow(Transform target)
        {
            Vector3 position = target.position;
            Vector3 euler = target.eulerAngles;
            // 画坐标轴的箭头 (箭头的锥顶不是它自身坐标的forword)
            Mesh meshX = GLDraw.CreateArrow(m_SelectedAxis == RuntimeHandleAxis.X ? m_SelectedColor : Color.red, m_ArrowScale * m_ScreenScale);
            Graphics.DrawMeshNow(meshX, position + target.right * m_HandleScale * m_ScreenScale, target.rotation * Quaternion.Euler(0, 0, -90));
            Mesh meshY = GLDraw.CreateArrow(m_SelectedAxis == RuntimeHandleAxis.Y ? m_SelectedColor : Color.green, m_ArrowScale * m_ScreenScale);
            Graphics.DrawMeshNow(meshY, position + target.up * m_HandleScale * m_ScreenScale, target.rotation);
            Mesh meshZ = GLDraw.CreateArrow(m_SelectedAxis == RuntimeHandleAxis.Z ? m_SelectedColor : Color.blue, m_ArrowScale * m_ScreenScale);
            Graphics.DrawMeshNow(meshZ, position + target.forward * m_HandleScale * m_ScreenScale, target.rotation * Quaternion.Euler(90, 0, 0));
        }

        /// <summary>
        /// 绘制坐标系小正方体
        /// </summary>
        private void DrawCoordinateCube(Transform target)
        {
            Vector3 position = target.position;
            Vector3 euler = target.eulerAngles;
            // 画坐标轴的小方块
            m_ShapesMaterial.SetPass(0);
            Mesh meshX = GLDraw.CreateCube(m_SelectedAxis == RuntimeHandleAxis.X ? m_SelectedColor : Color.red, Vector3.zero, m_CubeScale * m_ScreenScale);
            Graphics.DrawMeshNow(meshX, position + target.right * m_HandleScale * m_ScreenScale, target.rotation * Quaternion.Euler(0, 0, -90));
            Mesh meshY = GLDraw.CreateCube(m_SelectedAxis == RuntimeHandleAxis.Y ? m_SelectedColor : Color.green, Vector3.zero, m_CubeScale * m_ScreenScale);
            Graphics.DrawMeshNow(meshY, position + target.up * m_HandleScale * m_ScreenScale, target.rotation);
            Mesh meshZ = GLDraw.CreateCube(m_SelectedAxis == RuntimeHandleAxis.Z ? m_SelectedColor : Color.blue, Vector3.zero, m_CubeScale * m_ScreenScale);
            Graphics.DrawMeshNow(meshZ, position + target.forward * m_HandleScale * m_ScreenScale, target.rotation * Quaternion.Euler(90, 0, 0));

            Mesh meshO = GLDraw.CreateCube(m_SelectedAxis == RuntimeHandleAxis.XYZ ? m_SelectedColor : Color.white, Vector3.zero, m_CubeScale * m_ScreenScale);
            Graphics.DrawMeshNow(meshO, position, target.rotation);
        }

        /// <summary>
        /// 控制目标
        /// </summary>
        private void ControlTarget()
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                m_MouseDonw = true;
            }
            if (Input.GetKey(KeyCode.Mouse0))
            {
                float inputX = Input.GetAxis("Mouse X");
                float inputY = Input.GetAxis("Mouse Y");

                float x = 0;
                float y = 0;
                float z = 0;

                switch (m_SelectedAxis)
                {
                    case RuntimeHandleAxis.None:
                        break;
                    case RuntimeHandleAxis.X:
                        if (!m_LockX)
                            x = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.right);
                        break;
                    case RuntimeHandleAxis.Y:
                        if (!m_LockY)
                            y = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.up);
                        break;
                    case RuntimeHandleAxis.Z:
                        if (!m_LockZ)
                            z = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.forward);
                        break;
                    case RuntimeHandleAxis.XY:
                        if (!m_LockX)
                            x = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.right);
                        if (!m_LockY)
                            y = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.up);
                        break;
                    case RuntimeHandleAxis.XZ:
                        if (!m_LockX)
                            x = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.right);
                        if (!m_LockZ)
                            z = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.forward);
                        break;
                    case RuntimeHandleAxis.YZ:
                        if (!m_LockY)
                            y = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.up);
                        if (!m_LockZ)
                            z = m_CurrentHandle.GetTransformAxis(new Vector2(inputX, inputY), m_Target.forward);
                        break;
                    case RuntimeHandleAxis.XYZ:
                        x = y = z = inputX;
                        if (m_LockX)
                            x = 0;
                        if (m_LockY)
                            y = 0;
                        if (m_LockZ)
                            z = 0;
                        break;
                    default:
                        break;
                }

                m_CurrentHandle.Transform(new Vector3(x, y, z) * m_ScreenScale);
            }
            if (Input.GetKeyUp(KeyCode.Mouse0))
            {
                m_MouseDonw = false;
            }
        }

        private void DrawScreenCircle(Color color, Vector3 position, int pixel)
        {
            Vector2 temp = camera.WorldToScreenPoint(position);
            Vector3 offset = new Vector3(temp.x, temp.y, 0);

            m_LineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            GL.Color(color);

            int detlaAngle = 10;
            float x;
            float y;

            GL.Vertex(new Vector3(1, 0, 0) * pixel + offset);
            for (int i = 1; i < 360 / detlaAngle; i++)
            {
                x = Mathf.Cos(i * detlaAngle * Mathf.Deg2Rad) * pixel;
                y = Mathf.Sin(i * detlaAngle * Mathf.Deg2Rad) * pixel;

                GL.Vertex(new Vector3(x, y, 0) + offset);
                GL.Vertex(new Vector3(x, y, 0) + offset);
            }
            GL.Vertex(new Vector3(1, 0, 0) * pixel + offset);
            GL.End();
            GL.PopMatrix();

        }

        private void DoRotationNew()
        {
            Quaternion rotation = m_Target.rotation;
            Vector3 scale = new Vector3(m_ScreenScale, m_ScreenScale, m_ScreenScale);
            Matrix4x4 xTranform = Matrix4x4.TRS(Vector3.zero, rotation * Quaternion.AngleAxis(-90, Vector3.up), Vector3.one);
            Matrix4x4 yTranform = Matrix4x4.TRS(Vector3.zero, rotation * Quaternion.AngleAxis(90, Vector3.right), Vector3.one);
            Matrix4x4 zTranform = Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);
            Matrix4x4 objToWorld = Matrix4x4.TRS(m_Target.position, Quaternion.identity, m_ScreenScale * Vector3.one);

            m_LineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(objToWorld);
            GL.Begin(GL.LINES);

            DrawCircleNew(xTranform, Color.red, m_CircleRadius);

            GL.End();
            GL.PopMatrix();
        }

        private void DrawCircleNew(Matrix4x4 transform, Color color, float radius)
        {
            int detlaAngle = 10;
            float x;
            float z;
            GL.Color(color);

            Vector3 start;
            start = transform.MultiplyPoint(Vector3.right * radius);

            GL.Vertex(start);
            for (int i = 1; i < 180 / detlaAngle; i++)
            {
                x = Mathf.Cos(i * detlaAngle * Mathf.Deg2Rad) * radius;
                z = Mathf.Sin(i * detlaAngle * Mathf.Deg2Rad) * radius;
                Vector3 temp = transform.MultiplyPoint(new Vector3(x, 0, z));
                GL.Vertex(temp);
                GL.Vertex(temp);
            }

            GL.Vertex(transform.MultiplyPoint(Vector3.left * radius));
        }


        // ------------- Tools -------------- //


        /// <summary>
        /// 通过一个世界坐标和相机获取比例
        /// </summary>
        private float GetScreenScale(Vector3 position, Camera camera)
        {
            float h = camera.pixelHeight;
            if (camera.orthographic)
            {
                return camera.orthographicSize * 2f / h * 90;
            }

            Transform transform = camera.transform;
            float distance = Vector3.Dot(position - transform.position, transform.forward);       // Position位置的深度距离
            float scale = 2.0f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad); // 在Position的深度上，每个像素点对应的y轴距离
            return scale / h * 90; // 90为自定义系数
        }


        // ---------------- 外部调用 ------------------- //

        public static void SetTarget(Transform _target)
        {
            m_Target = _target;
        }

        public static void ConfigFreeze(bool _lockX = false, bool _lockY = false, bool _locKZ = false)
        {
            m_LockX = _lockX;
            m_LockY = _lockY;
            m_LockZ = _locKZ;
        }

        public static void Disable()
        {
            m_Target = null;
        }
    }

    /// <summary>
    /// 鼠标选择轴的类型
    /// </summary>
    public enum RuntimeHandleAxis
    {
        None,
        X,
        Y,
        Z,
        XY,
        XZ,
        YZ,
        XYZ,
    }

    /// <summary>
    /// 控制模式
    /// </summary>
    public enum TransformMode
    {
        Position,
        Rotation,
        Scale,
    }
}