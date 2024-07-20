using UnityEngine;
using System.Collections.Generic;
using XFramework;
using XFramework.Mathematics;

public class RoadTest : ProcedureBase
{
    private List<Vector3> positions;
    RaycastHit hit;
    SplineCurve outCurve;
    SplineCurve inCurve;
    SplineCurve centerCurve;
    private float c = 0;

    private MeshFilter meshFilter;

    private List<Vector3> path = new List<Vector3>();

    private List<GameObject> objs = new List<GameObject>();


    public override void Init()
    {
        outCurve = new SplineCurve();
        inCurve = new SplineCurve();
        centerCurve = new SplineCurve();
        positions = new List<Vector3>();

        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        meshFilter = obj.GetComponent<MeshFilter>();
        obj.GetComponent<MeshRenderer>().material.color = Color.cyan;

        GameObject.CreatePrimitive(PrimitiveType.Plane).transform.localScale = new Vector3(50, 1, 50);
        Camera.main.transform.position = new Vector3(-118, 179, -115);
        Camera.main.transform.localEulerAngles = new Vector3(60, 45, 0);
    }

    public override void OnUpdate()
    {
        MouseLeft();

        // 路面创建测试
        if (Input.GetKeyDown(KeyCode.K))
        {
            List<Vector3> points_out = new List<Vector3>();
            List<Vector3> points_in = new List<Vector3>();
            Vector3 dirr;

            dirr = Math2d.GetHorizontalDir(positions[1] - positions[0]);
            points_in.Add(positions[0] + dirr * 4);
            points_out.Add(positions[0] - dirr * 4);

            for (int i = 1; i < positions.Count - 1; i++)
            {
                dirr = Math2d.GetHorizontalDir(positions[i + 1] - positions[i - 1]);
                points_in.Add(positions[i] + dirr * 4);
                points_out.Add(positions[i] - dirr * 4);
            }

            dirr = Math2d.GetHorizontalDir(positions[positions.Count - 1] - positions[positions.Count - 2]);
            points_in.Add(positions[positions.Count - 1] + dirr * 4);
            points_out.Add(positions[positions.Count - 1] - dirr * 4);

            for (int i = 0; i < positions.Count; i++)
            {
                outCurve.AddNode(points_out[i], c);
                inCurve.AddNode(points_in[i], c);
                centerCurve.AddNode(positions[i], c);
            }
            outCurve.AddCatmull_RomControl();
            inCurve.AddCatmull_RomControl();
            centerCurve.AddCatmull_RomControl();


            for (int i = 0; i < outCurve.segmentList.Count; i++)
            {
                float add = 1f / 20;
                for (float j = 0; j < 1; j += add)
                {
                    Vector3 point = centerCurve.segmentList[i].GetPoint(j);
                    path.Add(point);
                    objs.Add(UUtility.CreatPrimitiveType(PrimitiveType.Sphere, Color.red, point, 1));

                    //point = outCurve.segmentList[i].GetPoint(j);
                    //path.Add(point);
                    //objs.Add(Utility.CreatPrimitiveType(PrimitiveType.Sphere, point, 1, Color.red));

                    //point = inCurve.segmentList[i].GetPoint(j);
                    //path.Add(point);
                    //objs.Add(Utility.CreatPrimitiveType(PrimitiveType.Sphere, point, 1, Color.red));
                }
            }

            CreateRoads(Terrain.activeTerrain, meshFilter, path, 6);
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            meshFilter.mesh.Clear();
            objs.ForEach((a) => { Object.Destroy(a); });
            objs.Clear();
            outCurve = new SplineCurve();
            inCurve = new SplineCurve();
            centerCurve = new SplineCurve();
            positions.Clear();
            path.Clear();
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            UUtility.SendRay(out hit, LayerMask.GetMask("Terrain"));
        }


        // 刷新地图
        if (Input.GetKeyDown(KeyCode.L))
        {
            //TerrainUtility.Refresh();
        }
    }

    public override void OnEnter(params object[] parms)
    {
        MonoEvent.Instance.ONGUI += OnGUI;
    }

    public override void OnExit()
    {
        MonoEvent.Instance.ONGUI -= OnGUI;
    }

    public void OnGUI()
    {
        GUIStyle style = new GUIStyle
        {
            padding = new RectOffset(10, 10, 10, 10),
            fontSize = 15,
            fontStyle = FontStyle.Normal,
        };
        GUI.Label(new Rect(0, 0, 200, 80), 
            "1.鼠标左键点击设置路径点\n" +
            "2.C:清空之前的操作\n" +
            "3.K 创建路面Mesh\n" +
            "4.白色Cube为设置的关键点\n" +
            "5.红色球为曲线算法得出的路径点", style);
    }

    private void MouseLeft()
    {
        if (Input.GetKeyUp(KeyCode.Mouse0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                Vector3 worldHitPos = hit.point + Vector3.up * 5;
                positions.Add(worldHitPos);
                GameObject gameObject = UUtility.CreatPrimitiveType(PrimitiveType.Cube, Color.white, worldHitPos, 1f);
                objs.Add(gameObject);
            }
        }
    }

    /// <summary>
    /// 根据传参 路径点信息 创建路面：
    /// 0 --- 2
    /// |     |
    /// 1 --- 3
    /// </summary>
    /// <param name="meshFilter">路面网格</param>
    /// <param name="_roadPoints">路点</param>
    /// <param name="_width">路面宽度</param>
    public static void CreateRoads(Terrain terrain, MeshFilter meshFilter, List<Vector3> wayPoints, float _width = 5)
    {
        if (wayPoints.Count < 2) return;    // 路点数量不能低于2个

        List<Vector3> _roadPoints = wayPoints;   // 取出路径点

        List<Vector3> vertice = new List<Vector3>();    // 顶点
        List<int> triangles = new List<int>();          // 三角形排序
        List<Vector2> uv = new List<Vector2>();         // uv排序

        Vector3 dir = Math2d.GetHorizontalDir(_roadPoints[1], _roadPoints[0]);   // 获取两点间的垂直向量
        vertice.Add(_roadPoints[0] + dir * _width);     // 添加初始顶点
        vertice.Add(_roadPoints[0] - dir * _width);

        uv.Add(Vector2.zero);                           // 添加初始顶点对应uv
        uv.Add(Vector2.right);

        for (int i = 1, count = _roadPoints.Count; i < count; i++)
        {
            // 添加由 路径点 生成的路面点集
            dir = Math2d.GetHorizontalDir(_roadPoints[i], _roadPoints[i - 1]);
            vertice.Add(_roadPoints[i] + dir * _width);
            vertice.Add(_roadPoints[i] - dir * _width);

            // 添加三jio形排序
            triangles.Add(2 * i - 2);
            triangles.Add(2 * i);
            triangles.Add(2 * i - 1);

            triangles.Add(2 * i);
            triangles.Add(2 * i + 1);
            triangles.Add(2 * i - 1);

            // 添加uv排序
            if (i % 2 == 1)
            {
                uv.Add(Vector2.up);
                uv.Add(Vector2.one);
            }
            else
            {
                uv.Add(Vector2.zero);
                uv.Add(Vector2.right);
            }
        }

        List<float> roadHeights = PointsFitToTerrain(terrain, ref vertice);                     // 路面高度适配地形

        //TerrainUtility.ChangeHeights(terrain, _roadPoints.ToArray(), roadHeights.ToArray());    // 将道路整平

        meshFilter.mesh.Clear();
        meshFilter.mesh.vertices = vertice.ToArray();
        meshFilter.mesh.triangles = triangles.ToArray();
        meshFilter.mesh.uv = uv.ToArray();

        meshFilter.mesh.RecalculateBounds();     // 重置范围
        meshFilter.mesh.RecalculateNormals();    // 重置法线
        meshFilter.mesh.RecalculateTangents();   // 重置切线
    }

    /// <summary>
    /// 使得路面点集适配(贴合)地形
    /// </summary>
    /// <param name="terrain">给定地形</param>
    /// <param name="points">路面点集</param>
    public static List<float> PointsFitToTerrain(Terrain terrain, ref List<Vector3> points)
    {
        List<Vector3> roadPointDoub = new List<Vector3>();  // 两侧路面高度
        List<float> roadHeightSing = new List<float>();     // 两侧路面高度min
        RaycastHit hit;
        for (int i = 0, length = points.Count; i < length; i++)
        {
            if (Physics.Raycast(points[i] + Vector3.up * 111, Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask("Terrain")))
            {
                points[i] = hit.point + Vector3.up * 0.2f;
            }
            roadPointDoub.Add(points[i]);
        }
        for (int i = 0, length = roadPointDoub.Count / 2; i < length; i++)
        {
            roadHeightSing.Add(Mathf.Min(roadPointDoub[2 * i].y, roadPointDoub[2 * i + 1].y));
        }
        return roadHeightSing;
    }
}