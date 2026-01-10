using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XFramework.Farm;

// 自定义场景编辑器：
// - 显示多边形的边和关键点
// - 拖动关键点
// - 双击边在中间插入关键点
// - 选中态（高亮）与删除（Delete），但当点数<=3时禁止删除
// - 使用本地坐标存储，场景中以Transform进行换算

[CustomEditor(typeof(PolygonArea))]
public class PolygonAreaEditor : Editor
{
    private PolygonArea land;

    // 可调整的可视化参数
    private const float HandleSize = 0.08f;            // 点的大小（相对于场景尺度）
    private const float PickSize = 0.12f;              // 点的拾取大小
    private readonly Color EdgeColor = new Color(0.2f, 0.8f, 1f, 1f);
    private readonly Color PointColor = new Color(1f, 0.6f, 0.2f, 1f);
    private readonly Color SelectedPointColor = new Color(1f, 0.2f, 0.2f, 1f);
    private readonly Color SelectedEdgeColor = new Color(1f, 0.9f, 0.2f, 1f);

    // 边的选中与悬停索引
    private int selectedEdgeIndex = -1;
    private int hoverEdgeIndex = -1;

    private void OnEnable()
    {
        land = (PolygonArea)target;
        if (land.points == null || land.points.Count < 3)
        {
            // 保证有默认矩形
            Undo.RecordObject(land, "Init Default Polygon");
            land.SendMessage("EnsureDefaultRectangle", SendMessageOptions.DontRequireReceiver);
            EditorUtility.SetDirty(land);
        }
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("在 Scene 中可编辑多边形：拖动点、双击边或点添加中点、Delete 删除选中点（最少3个）。边支持选中并高亮。", MessageType.Info);

        // 合并：删除选中元素（点或边）
        using (new EditorGUILayout.HorizontalScope())
        {
            bool canDeletePoint = land.points != null && land.points.Count > 3 && land.selectedIndex >= 0 && land.selectedIndex < land.points.Count;
            bool canDeleteEdge = land.points != null && land.points.Count > 4 && selectedEdgeIndex >= 0 && selectedEdgeIndex < land.points.Count;
            GUI.enabled = canDeletePoint || canDeleteEdge;
            if (GUILayout.Button("删除选中元素", GUILayout.Height(24)))
            {
                DeleteSelectedElement();
            }
            GUI.enabled = true;
        }
    }

    private void DeleteSelectedElement()
    {
        var pts = land.points;
        if (pts == null || pts.Count == 0)
        {
            ShowNotificationSafe("没有可删除的元素");
            return;
        }
        // 优先删除选中点
        if (land.selectedIndex >= 0 && land.selectedIndex < pts.Count)
        {
            if (pts.Count <= 3)
            {
                ShowNotificationSafe("无法删除点：至少需要3个点");
                return;
            }
            Undo.RecordObject(land, "Delete Vertex");
            pts.RemoveAt(land.selectedIndex);
            land.selectedIndex = -1;
            selectedEdgeIndex = -1;
            EditorUtility.SetDirty(land);
            return;
        }
        // 其次删除选中边
        if (selectedEdgeIndex >= 0 && selectedEdgeIndex < pts.Count)
        {
            if (pts.Count <= 4)
            {
                ShowNotificationSafe("无法删除边：至少需要保留4个关键点");
                return;
            }
            int removeIndex = (selectedEdgeIndex + 1) % pts.Count;
            Undo.RecordObject(land, "Delete Edge Vertex");
            pts.RemoveAt(removeIndex);
            selectedEdgeIndex = -1;
            land.selectedIndex = -1;
            EditorUtility.SetDirty(land);
            return;
        }
        ShowNotificationSafe("未选中点或边");
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (land == null || land.points == null) return;

        Transform t = land.transform;
        List<Vector3> points = land.points;
        int count = points.Count;
        if (count < 2) return;

        Event e = Event.current;

        // 单击时先选中最近点（GUI空间阈值），避免必须先拖拽才能选中
        if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1)
        {
            int nearestIdx = -1;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector2 guiPos = HandleUtility.WorldToGUIPoint(t.TransformPoint(points[i]));
                float guiDist = Vector2.Distance(e.mousePosition, guiPos);
                if (guiDist < nearestDist)
                {
                    nearestDist = guiDist;
                    nearestIdx = i;
                }
            }
            if (nearestIdx >= 0 && nearestDist < 10f)
            {
                land.selectedIndex = nearestIdx;
                selectedEdgeIndex = -1;
            }
        }

        // 先处理点：允许直接拖动（FreeMoveHandle），并在拖动时自动选中该点
        for (int i = 0; i < count; i++)
        {
            Vector3 worldPos = t.TransformPoint(points[i]);
            float size = HandleUtility.GetHandleSize(worldPos) * HandleSize;

            // 拖动前设置颜色（句柄Cap会使用当前Handles.color）
            Handles.color = (i == land.selectedIndex) ? SelectedPointColor : PointColor;

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(land, "Move Vertex");
                Vector3 localPos = t.InverseTransformPoint(newWorldPos);
                points[i] = ClampToPlane(localPos);
                // 拖动过程中自动切换选中态
                land.selectedIndex = i;
                selectedEdgeIndex = -1;
                EditorUtility.SetDirty(land);
            }
        }

        // 计算悬停边（用于高亮），不处理选择
        UpdateEdgeHoverOnly(points, t, e.mousePosition);

        // 画边（选中边高亮）
        for (int i = 0; i < count; i++)
        {
            Vector3 p0World = t.TransformPoint(points[i]);
            Vector3 p1World = t.TransformPoint(points[(i + 1) % count]);
            bool isSelected = (i == selectedEdgeIndex);
            bool isHover = (i == hoverEdgeIndex);
            Color c = isSelected ? SelectedEdgeColor : (isHover ? EdgeColor * 1.15f : EdgeColor);
            Handles.color = c;
            float thickness = isSelected ? 4f : 2f;
            Handles.DrawAAPolyLine(thickness, new[] { p0World, p1World });
        }

        // 双击边：在中点插入一个点（世界坐标->本地坐标）
        HandleDoubleClickOnEdge(points, t);

        // 最后处理边的单击选择（若点未接管事件）
        UpdateEdgeHoverAndSelection(points, t, e);
    }

    private void UpdateEdgeHoverOnly(List<Vector3> pts, Transform t, Vector2 mousePos)
    {
        hoverEdgeIndex = -1;
        float closestDist = float.MaxValue;
        int closestIdx = -1;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = t.TransformPoint(pts[i]);
            Vector3 b = t.TransformPoint(pts[(i + 1) % pts.Count]);
            Vector2 a2 = HandleUtility.WorldToGUIPoint(a);
            Vector2 b2 = HandleUtility.WorldToGUIPoint(b);
            float dist = DistancePointToSegment(mousePos, a2, b2);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIdx = i;
            }
        }
        if (closestIdx >= 0 && closestDist < 8f)
        {
            hoverEdgeIndex = closestIdx;
        }
    }

    private void UpdateEdgeHoverAndSelection(List<Vector3> pts, Transform t, Event e)
    {
        // 计算离鼠标最近的边
        hoverEdgeIndex = -1;
        float closestDist = float.MaxValue;
        int closestIdx = -1;
        Vector2 mousePos = e.mousePosition;

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = t.TransformPoint(pts[i]);
            Vector3 b = t.TransformPoint(pts[(i + 1) % pts.Count]);
            Vector2 a2 = HandleUtility.WorldToGUIPoint(a);
            Vector2 b2 = HandleUtility.WorldToGUIPoint(b);
            float dist = DistancePointToSegment(mousePos, a2, b2);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIdx = i;
            }
        }

        // 悬停阈值
        if (closestIdx >= 0 && closestDist < 8f)
        {
            hoverEdgeIndex = closestIdx;
        }

        // 单击选择边（点优先）
        if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1)
        {
            if (hoverEdgeIndex >= 0)
            {
                // 如果鼠标靠近任意点，则优先让点处理，不选择边
                float nearestPointDist = ClosestPointGuiDistance(pts, t, mousePos, out _);
                const float pointPriorityThreshold = 6f; // 更紧的像素阈值，减少边选择被点抢占
                if (nearestPointDist > pointPriorityThreshold)
                {
                    selectedEdgeIndex = hoverEdgeIndex;
                    land.selectedIndex = -1; // 选择边时取消点选中
                    e.Use(); // 消耗事件，避免后续句柄按钮抢占，从而保证边能被选中
                }
            }
            else
            {
                // 点击空白取消边选中
                selectedEdgeIndex = -1;
            }
        }
    }

    private float ClosestPointGuiDistance(List<Vector3> pts, Transform t, Vector2 mousePos, out int closestPoint)
    {
        closestPoint = -1;
        float closestDist = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = t.TransformPoint(pts[i]);
            Vector2 p2 = HandleUtility.WorldToGUIPoint(p);
            float dist = Vector2.Distance(mousePos, p2);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestPoint = i;
            }
        }
        return closestDist;
    }

    private void HandleDeleteKey(List<Vector3> pts)
    {
        
    }

    private void HandleDoubleClickOnEdge(List<Vector3> pts, Transform t)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.clickCount == 2 && e.button == 0)
        {
            // 在鼠标附近找到最近的边
            int closestEdgeIndex = -1;
            float closestDist = float.MaxValue;
            Vector2 mousePos = e.mousePosition;

            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 a = t.TransformPoint(pts[i]);
                Vector3 b = t.TransformPoint(pts[(i + 1) % pts.Count]);
                Vector2 a2 = HandleUtility.WorldToGUIPoint(a);
                Vector2 b2 = HandleUtility.WorldToGUIPoint(b);
                float dist = DistancePointToSegment(mousePos, a2, b2);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestEdgeIndex = i;
                }
            }

            // 阈值：若在边附近则插入
            if (closestEdgeIndex >= 0 && closestDist < 10f) // 10 像素阈值
            {
                Vector3 a = t.TransformPoint(pts[closestEdgeIndex]);
                Vector3 b = t.TransformPoint(pts[(closestEdgeIndex + 1) % pts.Count]);
                Vector3 midWorld = (a + b) * 0.5f;
                Vector3 midLocal = t.InverseTransformPoint(midWorld);

                Undo.RecordObject(land, "Insert Vertex");
                pts.Insert(closestEdgeIndex + 1, ClampToPlane(midLocal));
                land.selectedIndex = closestEdgeIndex + 1;
                selectedEdgeIndex = -1; // 插入后选中新点
                EditorUtility.SetDirty(land);
                e.Use();
            }
        }
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return Vector2.Distance(p, a + ab * t);
    }

    private static Vector3 ClampToPlane(Vector3 v)
    {
        // 锁定到对象的本地Y=0平面（大多数地面编辑符合预期）。如需3D可移除此逻辑或改为项目需要的约束。
        v.y = 0f;
        return v;
    }

    private void ShowNotificationSafe(string msg)
    {
        // 在Inspector上弹提示，或在SceneView上显示通知
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.ShowNotification(new GUIContent(msg));
        }
        else
        {
            Debug.LogWarning(msg);
        }
    }
    
    private void OnSceneGUI()
    {
        // 获取当前事件
        Event currentEvent = Event.current;

        // 检查是否按下了Delete键，并且有对象被选中
        if (currentEvent.type == EventType.KeyDown && 
            currentEvent.keyCode == KeyCode.Delete && 
            Selection.activeGameObject != null)
        {
            var pts = land.points;
             // 优先删除选中点
            if (land.selectedIndex >= 0 && land.selectedIndex < pts.Count)
            {
                if (pts.Count > 3)
                {
                    Undo.RecordObject(land, "Delete Vertex");
                    pts.RemoveAt(land.selectedIndex);
                    land.selectedIndex = -1;
                    EditorUtility.SetDirty(land);
                    // 防止默认删除 GameObject：暂时清空选择并稍后恢复
                    GameObject toRestore = land.gameObject;
                    Selection.activeGameObject = null;
                    EditorApplication.delayCall += () => Selection.activeGameObject = toRestore;
                    currentEvent.Use();
                }
                else
                {
                    ShowNotificationSafe("无法删除：至少需要3个点");
                    GameObject toRestore = land.gameObject;
                    Selection.activeGameObject = null;
                    EditorApplication.delayCall += () => Selection.activeGameObject = toRestore;
                }
                return;
            }

            // 尝试删除选中边（当关键点数量>4）
            if (selectedEdgeIndex >= 0 && selectedEdgeIndex < pts.Count)
            {
                if (pts.Count > 4)
                {
                    int removeIndex = (selectedEdgeIndex + 1) % pts.Count;
                    Undo.RecordObject(land, "Delete Edge Vertex");
                    pts.RemoveAt(removeIndex);
                    selectedEdgeIndex = -1;
                    land.selectedIndex = -1;
                    EditorUtility.SetDirty(land);
                    GameObject toRestore = land.gameObject;
                    Selection.activeGameObject = null;
                    EditorApplication.delayCall += () => Selection.activeGameObject = toRestore;
                    currentEvent.Use();
                }
                else
                {
                    ShowNotificationSafe("无法删除边：至少需要保留4个关键点");
                    GameObject toRestore = land.gameObject;
                    Selection.activeGameObject = null;
                    EditorApplication.delayCall += () => Selection.activeGameObject = toRestore;
                }
            }
        }
    }
}
