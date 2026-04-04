using System;
using System.Collections.Generic;
using UnityEngine;
using UMesh = UnityEngine.Mesh;

namespace XFramework
{
    # region model description
    public interface IMeshDescription
    {

    }

    // 立方体
    [Serializable]
    public struct CubeDescription : IMeshDescription
    {
        public float x_length;
        public float y_length;
        public float z_length;
        public float chamfer_length;
        public int chamfer_section_count;

        public static CubeDescription Default => new CubeDescription
        {
            x_length = 1.0f,
            y_length = 1.0f,
            z_length = 1.0f,
            chamfer_length = 0.0f,
            chamfer_section_count = 0
        };

        public string GenerateKey()
        {
            return $"CubeDescription{x_length:F2}{y_length:F2}{z_length:F2}{chamfer_length:F2}{chamfer_section_count}";
        }

        public static CubeDescription identity => new CubeDescription { x_length = 1.0f, y_length = 1.0f, z_length = 1.0f };
    }

    // 实心球
    [Serializable]
    public struct SphereDescription : IMeshDescription
    {
        public float radius;
        public float ratio;
        public int section_count;
        public static SphereDescription identity => new SphereDescription { radius = 0.5f, ratio = 1f, section_count = 20 };
    }

    // 空心球体
    [Serializable]
    public struct HollowSphereDescription : IMeshDescription
    {
        public float outer_radius;
        public float inner_radius;
        public float ratio;
        public int section_count;
        public static HollowSphereDescription identity => new HollowSphereDescription { outer_radius = 0.5f, inner_radius = 0.4f, ratio = 1f, section_count = 20 };
    }


    // 棱柱体
    [Serializable]
    public struct PrismDescription : IMeshDescription
    {
        public int section_count;           // 分段
        public float height;                // 高度
        public float top_radius;            // 顶面半径
        public float bottom_radius;         // 底面半径
        public float radian_offset;         // 旋转偏移角度
        public float chamfer_length;        // 倒角长度
        public int chamfer_section_count;   // 倒角分段数
        public bool side_vertex_combine;
        public static PrismDescription identity => new PrismDescription { section_count = 4, height = 1f, top_radius = 0.5f, bottom_radius = 0.5f, radian_offset = 0f, chamfer_length = 0f, chamfer_section_count = 0, side_vertex_combine = false };
    }

    // 圆柱体
    [Serializable]
    public struct CylinderDescription : IMeshDescription
    {
        public int section_count;           // 分段
        public float height;                // 高度
        public float top_radius;            // 顶面半径
        public float bottom_radius;         // 底面半径
        public float ratio;                 // 圆柱百分比
        public float chamfer_length;        // 倒角长度
        public int chamfer_section_count;   // 倒角分段数
        public static CylinderDescription identity => new CylinderDescription { section_count = 40, height = 1f, top_radius = 0.5f, bottom_radius = 0.5f, ratio = 1f, chamfer_length = 0f, chamfer_section_count = 0 };
    }

    // 棱锥体
    [Serializable]
    public struct PyramidDescription : IMeshDescription
    {
        public int section_count;
        public float height;
        public float bottom_radius;
        public bool side_vertex_combine;
        public float radian_offset;
        public static PyramidDescription identity => new PyramidDescription { section_count = 4, height = 1f, bottom_radius = 0.5f, side_vertex_combine = true, radian_offset = 0f };
    }
    
    // 圆环体
    [Serializable]
    public struct CircularRingDescription : IMeshDescription
    {
        public int section_count;           // 分段数
        public int circle_section_count;    // 圆截面分段数
        public float section_radius;        // 截面半径
        public float circle_radius;         // 圆环半径
        public float ratio;                 // 圆环百分比（360°为1）
        public static CircularRingDescription identity => new CircularRingDescription { section_count = 100, circle_section_count = 10, section_radius = 0.2f, circle_radius = 1f, ratio = 1f };
    }

    // 方圆体
    [Serializable]
    public struct SquareRingDescription : IMeshDescription
    {
        public int section_count;           // 分段数
        public float radius;                // 方环半径
        public float width;                 // 方环宽度
        public float height;                // 方环高度
        [Range(0, 1)]
        public float ratio;                 // 方环百分比（360°为1）
        public bool side_vertex_combine;    // 侧边面是否共用顶点
        [Range(0, 1)]
        public float chamfer_percent;        // 倒角百分比（0~1，基于 min(width, height) / 2）
        public float chamfer_section_length;// 倒角每段弧长
        public static SquareRingDescription identity => new SquareRingDescription { section_count = 100, radius = 1f, width = 0.2f, height = 0.2f, ratio = 1f, side_vertex_combine = true, chamfer_percent = 0f, chamfer_section_length = 0f };
    }
    
    // 多角星
    [Serializable]
    public struct StarDescription : IMeshDescription
    {
        public int corner_count;
        public float outer_radius;
        public float inner_radius;
        public float width;
        public static StarDescription identity => new StarDescription { corner_count = 5, outer_radius = 1f, inner_radius = 0.5f, width = 0.2f };
    }
    
    # endregion

    public partial class UUtility
    {
        /// <summary>
        /// Mesh相关工具
        /// </summary>
        public static class Model
        {
            private const float UVSize = 1.0f;
            
            #region create mesh
            // 立方体
            public static UMesh GenerateCubeMesh(CubeDescription description)
            {
                float x_length = description.x_length;
                float y_length = description.y_length;
                float z_length = description.z_length;
                float chamfer_length = description.chamfer_length;
                int chamfer_sections = description.chamfer_section_count;

                if (x_length <= 0 || y_length <= 0 || z_length <= 0)
                {
                    throw new ArgumentException("Cube length (x, y, z) must be greater than 0.");
                }

                chamfer_length = Mathf.Clamp(chamfer_length, 0f, Mathf.Min(x_length, y_length, z_length) * 0.5f);
                if (chamfer_sections < 0) chamfer_sections = 0;
                if (chamfer_length == 0) chamfer_sections = 0;

                int n = chamfer_sections; 
                int gridSize = 2 * n + 2; 

                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<Vector3> normals = new List<Vector3>();
                List<int> indices = new List<int>();

                float innerX = x_length * 0.5f - chamfer_length;
                float innerY = y_length * 0.5f - chamfer_length;
                float innerZ = z_length * 0.5f - chamfer_length;

                Vector3[] faceNormals = {
                    new Vector3(0, 0, 1),  // Front (+Z)
                    new Vector3(0, 0, -1), // Back (-Z)
                    new Vector3(-1, 0, 0), // Left (-X)
                    new Vector3(1, 0, 0),  // Right (+X)
                    new Vector3(0, 1, 0),  // Top (+Y)
                    new Vector3(0, -1, 0)  // Bottom (-Y)
                };

                Vector3[] faceUps = {
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, -1)
                };

                for (int f = 0; f < 6; f++)
                {
                    Vector3 normal = faceNormals[f];
                    Vector3 up = faceUps[f];
                    Vector3 right = Vector3.Cross(up, normal);

                    int vOffset = vertices.Count;

                    // 第一步：只生成顶点和法线，UV 暂时留空
                    for (int y = 0; y < gridSize; y++)
                    {
                        float vCoord, vInnerSign;
                        if (y <= n) {
                            vCoord = n == 0 ? 0 : -1f + (float)y / n;
                            vInnerSign = -1f;
                        } else {
                            vCoord = n == 0 ? 0 : (float)(y - n - 1) / n;
                            vInnerSign = 1f;
                        }

                        for (int x = 0; x < gridSize; x++)
                        {
                            float uCoord, uInnerSign;
                            if (x <= n) {
                                uCoord = n == 0 ? 0 : -1f + (float)x / n;
                                uInnerSign = -1f;
                            } else {
                                uCoord = n == 0 ? 0 : (float)(x - n - 1) / n;
                                uInnerSign = 1f;
                            }

                            Vector3 mappedDir = right * uCoord + up * vCoord + normal * 1f;

                            float dx2 = mappedDir.x * mappedDir.x;
                            float dy2 = mappedDir.y * mappedDir.y;
                            float dz2 = mappedDir.z * mappedDir.z;

                            // 生成完美球形法线
                            Vector3 N = new Vector3(
                                mappedDir.x * Mathf.Sqrt(1f - dy2 * 0.5f - dz2 * 0.5f + dy2 * dz2 / 3f),
                                mappedDir.y * Mathf.Sqrt(1f - dx2 * 0.5f - dz2 * 0.5f + dx2 * dz2 / 3f),
                                mappedDir.z * Mathf.Sqrt(1f - dx2 * 0.5f - dy2 * 0.5f + dx2 * dy2 / 3f)
                            );

                            Vector3 baseInner = right * (uInnerSign * (Mathf.Abs(right.x)*innerX + Mathf.Abs(right.y)*innerY + Mathf.Abs(right.z)*innerZ))
                                              + up * (vInnerSign * (Mathf.Abs(up.x)*innerX + Mathf.Abs(up.y)*innerY + Mathf.Abs(up.z)*innerZ))
                                              + normal * (1f * (Mathf.Abs(normal.x)*innerX + Mathf.Abs(normal.y)*innerY + Mathf.Abs(normal.z)*innerZ));

                            Vector3 finalPos = baseInner + N * chamfer_length;

                            // 修正世界原点：旧算法 Y 轴处于 0 到 y_length
                            finalPos.y += y_length * 0.5f;

                            vertices.Add(finalPos);
                            normals.Add(N);
                            uvs.Add(Vector2.zero); // 占位，下面回填
                        }
                    }

                    // 第二步：按实际 3D 顶点距离累积弧长，回填 UV（与 GenerateCubeOld 逻辑一致）
                    float[] u_dist = new float[gridSize];
                    u_dist[0] = 0;
                    for (int x = 1; x < gridSize; x++)
                    {
                        // 取中间行（y = gridSize/2）作为 u 方向模板，跟 Old 用 half_row_idx 一致
                        int midY = gridSize / 2;
                        Vector3 a = vertices[vOffset + midY * gridSize + (x - 1)];
                        Vector3 b = vertices[vOffset + midY * gridSize + x];
                        u_dist[x] = u_dist[x - 1] + Vector3.Distance(a, b);
                    }

                    float[] v_dist = new float[gridSize];
                    v_dist[0] = 0;
                    for (int y = 1; y < gridSize; y++)
                    {
                        // 取第一列（x = 0）作为 v 方向模板
                        Vector3 a = vertices[vOffset + (y - 1) * gridSize];
                        Vector3 b = vertices[vOffset + y * gridSize];
                        v_dist[y] = v_dist[y - 1] + Vector3.Distance(a, b);
                    }

                    for (int y = 0; y < gridSize; y++)
                        for (int x = 0; x < gridSize; x++)
                            uvs[vOffset + y * gridSize + x] = new Vector2(u_dist[x] / UVSize, v_dist[y] / UVSize);

                    // 三角面
                    for (int y = 0; y < gridSize - 1; y++)
                    {
                        for (int x = 0; x < gridSize - 1; x++)
                        {
                            int i0 = vOffset + y * gridSize + x;
                            int i1 = i0 + 1;
                            int i2 = i0 + gridSize;
                            int i3 = i2 + 1;

                            AddQuad(i0, i1, i3, i2, indices);
                        }
                    }
                }

                // 统一反转，适配旧框架的反转标准
                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.SetNormals(normals);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                
                return mesh;
            }
            
            // 立方体
            public static UMesh GenerateCubeMeshOld(CubeDescription description)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                float x_length = description.x_length;
                float y_length = description.y_length;
                float z_length = description.z_length;
                float chamfer_length = description.chamfer_length;
                int chamfer_section_count = description.chamfer_section_count;

                if (x_length <= 0 || y_length <= 0 || z_length <= 0)
                {
                    throw new ArgumentException("Cube length (x, y, z) must be greater than 0.");
                }

                if (chamfer_length < 0 || chamfer_length > Mathf.Min(x_length, y_length, z_length) * 0.5f)
                {
                    throw new ArgumentException("chamfer_length must be between 0 and half of the minimum side length of the cube.");
                }

                if (chamfer_section_count < 0)
                {
                    throw new ArgumentException("chamfer_section_count must be at least 0.");
                }

                float half_x_length = x_length / 2.0f;
                float half_z_length = z_length / 2.0f;

                Vector3[] key_positions = new Vector3[]
                {
                    new Vector3(half_x_length, 0, half_z_length),
                    new Vector3(half_x_length, 0, -half_z_length),
                    new Vector3(-half_x_length, 0, -half_z_length),
                    new Vector3(-half_x_length, 0, half_z_length)
                };

                List<List<Vector3>> row_position_lists = new List<List<Vector3>>();
                List<Vector3> uv_x_template_positions = new List<Vector3>();
                List<Vector3> bottom_row_positions = new List<Vector3>();
                List<Vector3> top_row_positions = new List<Vector3>();

                if (chamfer_length > 0)
                {
                    // bottom
                    List<Vector3> temp_bottom_row_positions = new List<Vector3>();
                    List<Vector3> raw_bottom_row_positions = new List<Vector3>();
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 position_pre = key_positions[i];
                        Vector3 position_next = key_positions[(i + 1) % 4];
                        Vector3 center_direction = (position_pre + position_next).normalized;
                        position_pre -= center_direction * chamfer_length;
                        position_next -= center_direction * chamfer_length;
                        Vector3 direction = (position_next - position_pre).normalized;
                        position_pre += direction * chamfer_length;
                        position_next -= direction * chamfer_length;
                        raw_bottom_row_positions.Add(position_pre);
                        raw_bottom_row_positions.Add(position_next);
                        temp_bottom_row_positions.Add(position_next);
                        bottom_row_positions.Add(position_pre);
                        bottom_row_positions.Add(position_next);
                    }

                    // top
                    List<Vector3> temp_top_row_positions = new List<Vector3>();
                    List<Vector3> raw_top_row_positions = new List<Vector3>();
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 position_pre = key_positions[i];
                        Vector3 position_next = key_positions[(i + 1) % 4];
                        Vector3 center_direction = (position_pre + position_next).normalized;
                        position_pre -= center_direction * chamfer_length;
                        position_pre += new Vector3(0, y_length, 0);
                        position_next -= center_direction * chamfer_length;
                        position_next += new Vector3(0, y_length, 0);
                        Vector3 direction = (position_next - position_pre).normalized;
                        position_pre += direction * chamfer_length;
                        position_next -= direction * chamfer_length;
                        raw_top_row_positions.Add(position_pre);
                        raw_top_row_positions.Add(position_next);
                        temp_top_row_positions.Add(position_next);
                        top_row_positions.Add(position_pre);
                        top_row_positions.Add(position_next);
                    }

                    // side
                    Vector3[] side_directions = new Vector3[raw_bottom_row_positions.Count];
                    for (int i = 0; i < raw_bottom_row_positions.Count; i++)
                    {
                        side_directions[i] = (raw_top_row_positions[i] - raw_bottom_row_positions[i]).normalized;
                    }

                    // side bottom
                    List<Vector3> raw_side_bottom_row_positions = new List<Vector3>();
                    for (int i = 0; i < 4; i++)
                    {
                        int pre_idx = i;
                        int next_idx = (i + 1) % 4;
                        Vector3 p_pre = key_positions[pre_idx] + side_directions[pre_idx * 2] * chamfer_length;
                        Vector3 p_next = key_positions[next_idx] + side_directions[pre_idx * 2 + 1] * chamfer_length;
                        Vector3 dir = (p_next - p_pre).normalized;
                        p_pre += dir * chamfer_length;
                        p_next -= dir * chamfer_length;
                        raw_side_bottom_row_positions.Add(p_pre);
                        raw_side_bottom_row_positions.Add(p_next);
                    }
                    List<Vector3> side_bottom_row_positions = RecalculateRowPositions(raw_side_bottom_row_positions, chamfer_section_count);

                    // side top
                    List<Vector3> raw_side_top_row_positions = new List<Vector3>();
                    for (int i = 0; i < 4; i++)
                    {
                        int pre_idx = i;
                        int next_idx = (i + 1) % 4;
                        Vector3 p_pre = key_positions[pre_idx] - side_directions[pre_idx * 2] * chamfer_length + new Vector3(0, y_length, 0);
                        Vector3 p_next = key_positions[next_idx] - side_directions[pre_idx * 2 + 1] * chamfer_length + new Vector3(0, y_length, 0);
                        Vector3 dir = (p_next - p_pre).normalized;
                        p_pre += dir * chamfer_length;
                        p_next -= dir * chamfer_length;
                        raw_side_top_row_positions.Add(p_pre);
                        raw_side_top_row_positions.Add(p_next);
                    }
                    List<Vector3> side_top_row_positions = RecalculateRowPositions(raw_side_top_row_positions, chamfer_section_count);

                    // vertical chamfer
                    int bezier_point_count = chamfer_section_count + 1;
                    for (int i = 0; i < bezier_point_count * 2; i++)
                    {
                        row_position_lists.Add(new List<Vector3>());
                    }

                    int row_position_lens = side_bottom_row_positions.Count;
                    for (int i = 0; i < row_position_lens; i++)
                    {
                        Vector3 bottom_pos = temp_bottom_row_positions[(i / bezier_point_count) % 4];
                        Vector3 bottom_side_pos = side_bottom_row_positions[i];
                        Vector3 pre_bottom_dir = bottom_pos - bottom_side_pos;
                        pre_bottom_dir.y = 0;
                        Vector3 bottom_pre_pos = bottom_pos + bottom_pos.magnitude * pre_bottom_dir.normalized;

                        Vector3 top_side_pos = side_top_row_positions[i];
                        Vector3 top_pos = temp_top_row_positions[(i / bezier_point_count) % 4];
                        Vector3 pre_top_dir = top_pos - top_side_pos;
                        pre_top_dir.y = 0;
                        Vector3 top_pre_pos = top_pos + top_pos.magnitude * pre_top_dir.normalized;

                        List<Vector3> curver_positions = new List<Vector3>();
                        curver_positions.AddRange(EvaluateBezierCurve(bottom_pre_pos, bottom_pos, bottom_side_pos, top_side_pos, chamfer_section_count, 0.3f));
                        curver_positions.AddRange(EvaluateBezierCurve(bottom_side_pos, top_side_pos, top_pos, top_pre_pos, chamfer_section_count, 0.3f));

                        for (int row_idx = 0; row_idx < bezier_point_count * 2; row_idx++)
                        {
                            row_position_lists[row_idx].Add(curver_positions[row_idx]);
                        }
                    }
                    foreach (var list in row_position_lists)
                    {
                        list.Add(list[0]);
                    }

                    // uv templete
                    int half_row_idx = row_position_lists.Count / 2;
                    for (int i = 0; i < row_position_lists[half_row_idx].Count; i++)
                    {
                        uv_x_template_positions.Add(row_position_lists[half_row_idx][i]);
                    }
                }
                else
                {
                    for (int i = 0; i < 4; i++)
                    {
                        bottom_row_positions.Add(key_positions[i]);
                        bottom_row_positions.Add(key_positions[(i + 1) % 4]);
                        top_row_positions.Add(key_positions[i] + new Vector3(0, y_length, 0));
                        top_row_positions.Add(key_positions[(i + 1) % 4] + new Vector3(0, y_length, 0));
                    }
                    row_position_lists.Add(new List<Vector3>(bottom_row_positions));
                    row_position_lists.Add(new List<Vector3>(top_row_positions));

                    for (int i = 0; i < bottom_row_positions.Count; i++)
                    {
                        uv_x_template_positions.Add((bottom_row_positions[i] + top_row_positions[i]) * 0.5f);
                    }
                }

                // bottom
                int bottom_offset = vertices.Count;
                foreach (var pos in bottom_row_positions)
                {
                    vertices.Add(pos);
                    uvs.Add(new Vector2(pos.x / UVSize, pos.z / UVSize));
                }
                AddQuad(bottom_offset + 2, bottom_offset, bottom_offset + 6, bottom_offset + 4, indices);

                // top
                int top_offset = vertices.Count;
                foreach (var pos in top_row_positions)
                {
                    vertices.Add(pos);
                    uvs.Add(new Vector2(pos.x / UVSize, pos.z / UVSize));
                }
                AddQuad(top_offset, top_offset + 2, top_offset + 4, top_offset + 6, indices);

                int row_point_count = row_position_lists[0].Count;
                // side
                int row_count = row_position_lists.Count;
                int side_offset = vertices.Count;

                List<float> uv_x_templates = new List<float> { 0.5f };
                for (int i = 1; i < row_point_count; i++)
                {
                    float dist = (uv_x_template_positions[i] - uv_x_template_positions[i - 1]).magnitude;
                    uv_x_templates.Add(uv_x_templates[uv_x_templates.Count - 1] - dist / UVSize);
                }

                List<float> uv_y_templates = new List<float> { 0.0f };
                for (int i = 1; i < row_count; i++)
                {
                    float dist = (row_position_lists[i][0] - row_position_lists[i - 1][0]).magnitude;
                    uv_y_templates.Add(uv_y_templates[uv_y_templates.Count - 1] + dist / UVSize);
                }

                for (int y = 0; y < row_count; y++)
                {
                    for (int x = 0; x < row_point_count; x++)
                    {
                        vertices.Add(row_position_lists[y][x]);
                        uvs.Add(new Vector2(uv_x_templates[x], uv_y_templates[y]));
                    }
                }

                for (int r = 0; r < row_count - 1; r++)
                {
                    AddSide(r * row_point_count + side_offset, (r + 1) * row_point_count + side_offset, row_point_count, indices, false);
                }

                // 修复坐标系/引擎导致的三角形绕排相反（反算/法线朝内）问题
                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                
                return mesh;
            }

            // 实心球
            public static UMesh GenerateSphereMesh(SphereDescription description)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                float radius = description.radius;
                float ratio = Mathf.Clamp01(description.ratio);
                int section_count = Mathf.Max(3, description.section_count);
                
                int latitude_count = section_count;
                int longitude_count = (int)(section_count * ratio);
                longitude_count = Mathf.Max(longitude_count, 1) * 2;
                
                float lat_section_radian = Mathf.PI / latitude_count;
                float lon_section_radian = 2 * Mathf.PI * ratio / longitude_count;
                float center_section_length = 2 * Mathf.PI * radius / section_count;

                for (int lat = 0; lat <= latitude_count; lat++)
                {
                    float theta = lat * lat_section_radian;
                    float sin_theta = Mathf.Sin(theta);
                    float cos_theta = Mathf.Cos(theta);

                    for (int lon = 0; lon <= longitude_count; lon++)
                    {
                        float phi = lon * lon_section_radian;
                        float sin_phi = Mathf.Sin(phi);
                        float cos_phi = Mathf.Cos(phi);

                        float x = cos_phi * sin_theta;
                        float y = cos_theta;
                        float z = sin_phi * sin_theta;
                        
                        Vector2 uv = new Vector2(-lon * center_section_length, lat * center_section_length);

                        vertices.Add(new Vector3(radius * x, radius * y, radius * z));
                        uvs.Add(uv);
                    }
                }

                for (int lat = 0; lat < latitude_count; lat++)
                {
                    for (int lon = 0; lon < longitude_count; lon++)
                    {
                        int first = (lat * (longitude_count + 1)) + lon;
                        int second = first + longitude_count + 1;
                        
                        AddTriangle(first, second, first + 1, indices);
                        AddTriangle(second, second + 1, first + 1, indices);
                    }
                }

                if (ratio < 1f)
                {
                    int front_side_offset = vertices.Count;
                    vertices.Add(Vector3.zero);
                    uvs.Add(Vector2.zero);
                    
                    for (int lat = 0; lat <= latitude_count; lat++)
                    {
                        Vector3 old_pos = vertices[lat * (longitude_count + 1)];
                        float radian = Mathf.PI / latitude_count * lat;
                        float x = -Mathf.Cos(radian) * radius;
                        float z = Mathf.Sin(radian) * radius;
                        
                        vertices.Add(old_pos);
                        uvs.Add(new Vector2(-x, -z));
                    }
                    
                    for (int lat = 0; lat < latitude_count; lat++)
                    {
                        AddTriangle(front_side_offset, front_side_offset + 1 + lat + 1, front_side_offset + 1 + lat, indices);
                    }

                    int back_side_offset = vertices.Count;
                    vertices.Add(Vector3.zero);
                    uvs.Add(Vector2.zero);

                    for (int lat = 0; lat <= latitude_count; lat++)
                    {
                        Vector3 old_pos = vertices[lat * (longitude_count + 1) + longitude_count];
                        float radian = Mathf.PI / latitude_count * lat;
                        float x = -Mathf.Cos(radian) * radius;
                        float z = Mathf.Sin(radian) * radius;
                        
                        vertices.Add(old_pos);
                        uvs.Add(new Vector2(x, -z));
                    }

                    for (int lat = 0; lat < latitude_count; lat++)
                    {
                        AddTriangle(back_side_offset, back_side_offset + 1 + lat, back_side_offset + 1 + lat + 1, indices);
                    }
                }

                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
            
            // 空心球体
            public static UMesh GenerateHollowSphereMesh(HollowSphereDescription description)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                float outer_radius = description.outer_radius;
                float inner_radius = description.inner_radius;
                float ratio = Mathf.Clamp01(description.ratio);
                int section_count = Mathf.Max(3, description.section_count);
                int latitude_count = section_count;
                int longitude_count = (int)(section_count * ratio);
                longitude_count = Mathf.Max(longitude_count, 1) * 2;
                float lat_section_radian = Mathf.PI / latitude_count;
                float lon_section_radian = 2 * Mathf.PI * ratio / longitude_count;
                float center_section_length = 2 * Mathf.PI * outer_radius / section_count;

                // outer
                int outer_offset = 0;
                for (int lat = 0; lat <= latitude_count; lat++)
                {
                    float theta = lat * lat_section_radian;
                    float sin_theta = Mathf.Sin(theta);
                    float cos_theta = Mathf.Cos(theta);

                    for (int lon = 0; lon <= longitude_count; lon++)
                    {
                        float phi = lon * lon_section_radian;
                        float sin_phi = Mathf.Sin(phi);
                        float cos_phi = Mathf.Cos(phi);

                        float x = cos_phi * sin_theta;
                        float y = cos_theta;
                        float z = sin_phi * sin_theta;
                        Vector2 uv = new Vector2(-lon * center_section_length, lat * center_section_length);

                        vertices.Add(new Vector3(outer_radius * x, outer_radius * y, outer_radius * z));
                        uvs.Add(uv);

                    }
                }

                for (int lat = 0; lat < latitude_count; lat++)
                {
                    for (int lon = 0; lon < longitude_count; lon++)
                    {
                        int first = (lat * (longitude_count + 1)) + lon;
                        int second = first + longitude_count + 1;
                        AddTriangle(first, second, first + 1, indices);
                        AddTriangle(second, second + 1, first + 1, indices);
                    }
                }

                if (ratio != 1f)
                {
                    int inner_offset = vertices.Count;
                    // inner
                    for (int lat = 0; lat <= latitude_count; lat++)
                    {
                        float theta = lat * lat_section_radian;
                        float sin_theta = Mathf.Sin(theta);
                        float cos_theta = Mathf.Cos(theta);

                        for (int lon = 0; lon <= longitude_count; lon++)
                        {
                            float phi = lon * lon_section_radian;
                            float sin_phi = Mathf.Sin(phi);
                            float cos_phi = Mathf.Cos(phi);

                            float x = cos_phi * sin_theta;
                            float y = cos_theta;
                            float z = sin_phi * sin_theta;
                            Vector2 uv = new Vector2(-lon * center_section_length, lat * center_section_length);

                            vertices.Add(new Vector3(inner_radius * x, inner_radius * y, inner_radius * z));
                            uvs.Add(uv);
                        }
                    }

                    for (int lat = 0; lat < latitude_count; lat++)
                    {
                        for (int lon = 0; lon < longitude_count; lon++)
                        {
                            int first = (lat * (longitude_count + 1)) + lon + inner_offset;
                            int second = first + longitude_count + 1;
                            AddTriangle(first, first + 1, second, indices);
                            AddTriangle(second, first + 1, second + 1, indices);
                        }
                    }

                    // front side
                    int front_side_offset = vertices.Count;
                    for (int lat = 0; lat <= latitude_count; lat++)
                    {
                        Vector3 old_pos = vertices[lat * (longitude_count + 1) + outer_offset];
                        vertices.Add(old_pos); uvs.Add(new Vector2(-Mathf.Cos(Mathf.PI / latitude_count * lat) * outer_radius, 0));
                    }
                    for (int lat = 0; lat <= latitude_count; lat++)
                    {
                        Vector3 old_pos = vertices[lat * (longitude_count + 1) + inner_offset];
                        vertices.Add(old_pos); uvs.Add(new Vector2(-Mathf.Cos(Mathf.PI / latitude_count * lat) * inner_radius, 0));
                    }
                    AddSide(front_side_offset, front_side_offset + latitude_count + 1, latitude_count + 1, indices, false);
                    
                    // back side
                    int back_side_offset = vertices.Count;
                    for (int lat = 0; lat <= latitude_count; lat++)
                    {
                        Vector3 old_pos = vertices[lat * (longitude_count + 1) + longitude_count + outer_offset];
                        vertices.Add(old_pos); uvs.Add(new Vector2(-Mathf.Cos(Mathf.PI / latitude_count * lat) * outer_radius, 0));
                    }
                    for (int lat = 0; lat <= latitude_count; lat++)
                    {
                        Vector3 old_pos = vertices[lat * (longitude_count + 1) + longitude_count + inner_offset];
                        vertices.Add(old_pos); uvs.Add(new Vector2(-Mathf.Cos(Mathf.PI / latitude_count * lat) * inner_radius, 0));
                    }
                    AddSide(back_side_offset + latitude_count + 1, back_side_offset, latitude_count + 1, indices, false);
                }
                
                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }

            // 棱锥体
            public static UMesh GeneratePyramidMesh(PyramidDescription description)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                int section_count = Mathf.Max(3, description.section_count);
                float single_radian = 2 * Mathf.PI / section_count;
                float radian_offset = description.radian_offset;
                float height = description.height;
                float bottom_radius = description.bottom_radius;
                bool side_vertex_combine = description.side_vertex_combine;
                int point_count = section_count + 1;

                int bottom_offset = 0;
                int side_offset = point_count + 1;
                float uv_section_length = 2 * Mathf.PI * bottom_radius / 2 / section_count;
                float bevel_edge_length = Mathf.Sqrt(bottom_radius * bottom_radius + height * height);

                Vector3 top_point = new Vector3(0, height, 0);

                // bottom
                vertices.Add(Vector3.zero);
                uvs.Add(Vector2.zero);
                for (int i = 0; i < point_count; i++)
                {
                    float radian = single_radian * i + radian_offset;
                    float x = -Mathf.Sin(radian) * bottom_radius;
                    float z = Mathf.Cos(radian) * bottom_radius;
                    vertices.Add(new Vector3(x, 0, z));
                    uvs.Add(new Vector2(x, -z));
                }

                // side
                if (side_vertex_combine)
                {
                    for (int i = 0; i < point_count; i++)
                    {
                        Vector3 pos = vertices[bottom_offset + 1 + i];
                        vertices.Add(pos);
                        uvs.Add(new Vector2(-uv_section_length * i, 0));
                    }
                    for (int i = 0; i < point_count; i++)
                    {
                        vertices.Add(top_point);
                        uvs.Add(new Vector2(-uv_section_length * (i - 0.5f), -bevel_edge_length));
                    }
                }
                else
                {
                    for (int i = 0; i < point_count - 1; i++)
                    {
                        int pre_index = i;
                        int next_index = i + 1;

                        Vector3 bottom_pre_pos = vertices[bottom_offset + 1 + pre_index];
                        Vector2 bottom_pre_uv = new Vector2(-uv_section_length * pre_index, 0);

                        Vector3 bottom_next_pos = vertices[bottom_offset + 1 + next_index];
                        Vector2 bottom_next_uv = new Vector2(-uv_section_length * next_index, 0);

                        Vector3 top_pos = top_point;
                        Vector2 top_uv = new Vector2((bottom_pre_uv.x + bottom_next_uv.x) / 2, -bevel_edge_length);

                        vertices.Add(bottom_pre_pos); uvs.Add(bottom_pre_uv);
                        vertices.Add(bottom_next_pos); uvs.Add(bottom_next_uv);
                        vertices.Add(top_pos); uvs.Add(top_uv);
                    }
                }

                // indices
                // bottom
                int offset = bottom_offset + 1;
                for (int i = 0; i < section_count; i++)
                {
                    int pre_index = i + offset;
                    int next_index = (i + 1) % section_count + offset;
                    AddTriangle(bottom_offset, next_index, pre_index, indices);
                }

                // side
                if (side_vertex_combine)
                {
                    AddSide(side_offset + point_count, side_offset, point_count, indices, true);
                }
                else
                {
                    for (int i = 0; i < section_count; i++)
                    {
                        int index = side_offset + i * 3;
                        AddTriangle(index, index + 1, index + 2, indices);
                    }
                }

                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }

            // 棱柱体
            public static UMesh GeneratePrismMesh(PrismDescription description)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                int sectionCount = Mathf.Max(3, description.section_count);
                float singleRadian = 2 * Mathf.PI / sectionCount;
                float radianOffset = description.radian_offset;
                float height = description.height;
                float bottomRadius = description.bottom_radius;
                float topRadius = description.top_radius;
                float chamfer_length = description.chamfer_length;
                int chamfer_section_count = description.chamfer_section_count;

                int raw_point_count = sectionCount;
                List<Vector3> raw_positions = new List<Vector3>();
                for (int i = 0; i < raw_point_count; i++)
                {
                    float radian = singleRadian * i + radianOffset;
                    raw_positions.Add(new Vector3(-Mathf.Sin(radian), 0, Mathf.Cos(radian)));
                }

                List<List<Vector3>> row_position_lists = new List<List<Vector3>>();
                List<Vector3> uv_x_templete_positions = new List<Vector3>();

                if (chamfer_length > 0)
                {
                    // bottom
                    List<Vector3> raw_bottom_row_positions = new List<Vector3>();
                    for (int i = 0; i < raw_point_count; i++)
                    {
                        Vector3 pos_pre = raw_positions[i] * (bottomRadius - chamfer_length);
                        Vector3 pos_next = raw_positions[(i + 1) % raw_point_count] * (bottomRadius - chamfer_length);
                        Vector3 dir = (pos_next - pos_pre).normalized;
                        raw_bottom_row_positions.Add(pos_pre + dir * chamfer_length);
                        raw_bottom_row_positions.Add(pos_next - dir * chamfer_length);
                    }
                    List<Vector3> bottom_row_positions = RecalculateRowPositions(raw_bottom_row_positions, chamfer_section_count);

                    // top
                    List<Vector3> raw_top_row_positions = new List<Vector3>();
                    for (int i = 0; i < raw_point_count; i++)
                    {
                        Vector3 pos_pre = raw_positions[i] * (topRadius - chamfer_length) + new Vector3(0, height, 0);
                        Vector3 pos_next = raw_positions[(i + 1) % raw_point_count] * (topRadius - chamfer_length) + new Vector3(0, height, 0);
                        Vector3 dir = (pos_next - pos_pre).normalized;
                        raw_top_row_positions.Add(pos_pre + dir * chamfer_length);
                        raw_top_row_positions.Add(pos_next - dir * chamfer_length);
                    }
                    List<Vector3> top_row_positions = RecalculateRowPositions(raw_top_row_positions, chamfer_section_count);

                    List<Vector3> side_directions = new List<Vector3>();
                    for (int i = 0; i < raw_bottom_row_positions.Count; i++)
                    {
                        side_directions.Add((raw_top_row_positions[i] - raw_bottom_row_positions[i]).normalized);
                    }

                    // side bottom
                    List<Vector3> raw_side_bottom_row_positions = new List<Vector3>();
                    for (int i = 0; i < raw_point_count; i++)
                    {
                        int pre_idx = i;
                        int next_idx = (i + 1) % raw_point_count;
                        Vector3 pos_pre = raw_positions[pre_idx] * bottomRadius + side_directions[pre_idx * 2] * chamfer_length;
                        Vector3 pos_next = raw_positions[next_idx] * bottomRadius + side_directions[pre_idx * 2 + 1] * chamfer_length;
                        Vector3 dir = (pos_next - pos_pre).normalized;
                        raw_side_bottom_row_positions.Add(pos_pre + dir * chamfer_length);
                        raw_side_bottom_row_positions.Add(pos_next - dir * chamfer_length);
                    }
                    List<Vector3> side_bottom_row_positions = RecalculateRowPositions(raw_side_bottom_row_positions, chamfer_section_count);

                    // side top
                    List<Vector3> raw_side_top_row_positions = new List<Vector3>();
                    for (int i = 0; i < raw_point_count; i++)
                    {
                        int pre_idx = i;
                        int next_idx = (i + 1) % raw_point_count;
                        Vector3 pos_pre = raw_positions[pre_idx] * topRadius - side_directions[pre_idx * 2] * chamfer_length + new Vector3(0, height, 0);
                        Vector3 pos_next = raw_positions[next_idx] * topRadius - side_directions[pre_idx * 2 + 1] * chamfer_length + new Vector3(0, height, 0);
                        Vector3 dir = (pos_next - pos_pre).normalized;
                        raw_side_top_row_positions.Add(pos_pre + dir * chamfer_length);
                        raw_side_top_row_positions.Add(pos_next - dir * chamfer_length);
                    }
                    List<Vector3> side_top_row_positions = RecalculateRowPositions(raw_side_top_row_positions, chamfer_section_count);

                    // vertical chamfer
                    int bezier_point_count = chamfer_section_count + 1;
                    for (int i = 0; i < bezier_point_count * 2; i++) row_position_lists.Add(new List<Vector3>());
                    
                    for (int i = 0; i < bottom_row_positions.Count; i++)
                    {
                        Vector3 p_bot = bottom_row_positions[i];
                        Vector3 p_side_bot = side_bottom_row_positions[i];
                        Vector3 p_side_top = side_top_row_positions[i];
                        Vector3 p_top = top_row_positions[i];

                        List<Vector3> curve0 = EvaluateBezierCurve(Vector3.zero, p_bot, p_side_bot, p_side_top, chamfer_section_count, 0.3f);
                        List<Vector3> curve1 = EvaluateBezierCurve(p_side_bot, p_side_top, p_top, new Vector3(0, height, 0), chamfer_section_count, 0.3f);
                        
                        List<Vector3> allCurves = new List<Vector3>(curve0);
                        allCurves.AddRange(curve1);

                        for (int r = 0; r < bezier_point_count * 2; r++)
                        {
                            row_position_lists[r].Add(allCurves[r]);
                        }
                    }

                    foreach (var lst in row_position_lists) lst.Add(lst[0]);

                    // uv templete
                    for (int i = 0; i < row_position_lists[0].Count; i++)
                    {
                        uv_x_templete_positions.Add((row_position_lists[0][i] + row_position_lists[row_position_lists.Count - 1][i]) * 0.5f);
                    }
                }
                else
                {
                    List<Vector3> bottom_row_positions = new List<Vector3>();
                    for (int i = 0; i < raw_point_count; i++)
                    {
                        bottom_row_positions.Add(raw_positions[i % raw_point_count] * bottomRadius);
                        bottom_row_positions.Add(raw_positions[(i + 1) % raw_point_count] * bottomRadius);
                    }
                    List<Vector3> top_row_positions = new List<Vector3>();
                    for (int i = 0; i < raw_point_count; i++)
                    {
                        top_row_positions.Add(raw_positions[i % raw_point_count] * topRadius + new Vector3(0, height, 0));
                        top_row_positions.Add(raw_positions[(i + 1) % raw_point_count] * topRadius + new Vector3(0, height, 0));
                    }
                    row_position_lists.Add(bottom_row_positions);
                    row_position_lists.Add(top_row_positions);

                    for (int i = 0; i < bottom_row_positions.Count; i++)
                    {
                        uv_x_templete_positions.Add((bottom_row_positions[i] + top_row_positions[i]) * 0.5f);
                    }
                }

                int row_point_count = row_position_lists[0].Count;

                // region bottom
                int bottom_offset = vertices.Count;
                vertices.Add(Vector3.zero); uvs.Add(Vector2.zero);
                int bottm_edge_offset = vertices.Count;
                for (int i = 0; i < row_point_count; i++)
                {
                    Vector3 pos = row_position_lists[0][i];
                    vertices.Add(pos); uvs.Add(new Vector2(pos.x, pos.z));
                }
                for (int i = 0; i < row_point_count - 1; i++)
                {
                    AddTriangle(bottom_offset, i + 1 + bottm_edge_offset, i + bottm_edge_offset, indices);
                }

                // region top
                int top_offset = vertices.Count;
                vertices.Add(new Vector3(0, height, 0)); uvs.Add(Vector2.zero);
                int top_edge_offset = vertices.Count;
                for (int i = 0; i < row_point_count; i++)
                {
                    Vector3 pos = row_position_lists[row_position_lists.Count - 1][i];
                    vertices.Add(pos); uvs.Add(new Vector2(pos.x, pos.z));
                }
                for (int i = 0; i < row_point_count - 1; i++)
                {
                    AddTriangle(top_offset, i + top_edge_offset, i + 1 + top_edge_offset, indices);
                }

                // region side
                int row_count = row_position_lists.Count;
                int side_offset = vertices.Count;

                List<float> uv_x_templates = new List<float> { 0.5f };
                for (int i = 1; i < row_point_count; i++)
                {
                    float dist = (uv_x_templete_positions[i - 1] - uv_x_templete_positions[i]).magnitude;
                    uv_x_templates.Add(uv_x_templates[uv_x_templates.Count - 1] - dist);
                }

                List<float> uv_y_templates = new List<float> { 0f };
                for (int r = 1; r < row_count; r++)
                {
                    float dist = (row_position_lists[r - 1][0] - row_position_lists[r][0]).magnitude;
                    uv_y_templates.Add(uv_y_templates[uv_y_templates.Count - 1] + dist);
                }

                for (int y = 0; y < row_count; y++)
                {
                    for (int x = 0; x < row_point_count; x++)
                    {
                        vertices.Add(row_position_lists[y][x]);
                        uvs.Add(new Vector2(uv_x_templates[x], uv_y_templates[y]));
                    }
                }

                for (int r = 0; r < row_count - 1; r++)
                {
                    AddSide((r + 1) * row_point_count + side_offset, r * row_point_count + side_offset, row_point_count, indices, true);
                }

                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }

            // 圆柱体
            public static UMesh GenerateCylinderMesh(CylinderDescription description)
            {
                PrismDescription pdesc = new PrismDescription {
                    section_count = description.section_count,
                    height = description.height,
                    top_radius = description.top_radius,
                    bottom_radius = description.bottom_radius,
                    radian_offset = 0f,
                    chamfer_length = description.chamfer_length,
                    chamfer_section_count = description.chamfer_section_count,
                    side_vertex_combine = false
                };
                return GeneratePrismMesh(pdesc);
            }

            // 圆环体
            public static UMesh GenerateCircularRingMesh(CircularRingDescription description)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                int section_count = Mathf.Max(3, description.section_count);
                int circle_count = section_count + 1;
                float ratio = Mathf.Clamp01(description.ratio);
                float circle_radius = description.circle_radius;
                float single_radian = 2 * Mathf.PI * ratio / section_count;

                // calculate key point
                List<Vector3> center_vertices = new List<Vector3>();
                for (int i = 0; i < circle_count; i++)
                {
                    float radian = single_radian * i;
                    float x = -Mathf.Sin(radian) * circle_radius;
                    float z = Mathf.Cos(radian) * circle_radius;
                    center_vertices.Add(new Vector3(x, 0, z));
                }

                int circle_section_count = description.circle_section_count;
                int circle_point_count = circle_section_count + 1;
                float single_circle_angle = 2 * Mathf.PI / circle_section_count;

                List<Vector3> circle_points = new List<Vector3>();
                for (int i = 0; i < circle_point_count; i++)
                {
                    float radian = single_circle_angle * i;
                    float x = -Mathf.Sin(radian);
                    float y = Mathf.Cos(radian);
                    circle_points.Add(new Vector3(x, y, 0));
                }

                int front_side_offset = circle_count * circle_point_count;
                int back_side_offset = 0;

                // surface
                float u = 0;
                for (int i = 0; i < circle_count; i++)
                {
                    int pre_index = Mathf.Max(i - 1, 0);
                    int next_index = Mathf.Min(i + 1, circle_count - 1);
                    Vector3 center_position = center_vertices[i];
                    float radius = description.section_radius;

                    Vector3 tangent = (center_vertices[next_index] - center_vertices[pre_index]).normalized;
                    Vector3 binormal = center_position.normalized;
                    Vector3 normal = Vector3.Cross(tangent, binormal);

                    Matrix4x4 m = Matrix4x4.identity;
                    m.SetColumn(0, new Vector4(binormal.x, binormal.y, binormal.z, 0));
                    m.SetColumn(1, new Vector4(normal.x, normal.y, normal.z, 0));
                    m.SetColumn(2, new Vector4(tangent.x, tangent.y, tangent.z, 0));
                    m.SetColumn(3, new Vector4(center_position.x, center_position.y, center_position.z, 1));

                    float circle_perimeter = radius * 2 * Mathf.PI;
                    float total_v = circle_perimeter; 
                    float segment_v = total_v / (circle_point_count - 1);
                    float start_v = -total_v / 2;

                    if (i > 0) u += (center_vertices[i] - center_vertices[i - 1]).magnitude;

                    for (int j = 0; j < circle_point_count; j++)
                    {
                        Vector3 position = m.MultiplyPoint3x4(circle_points[j] * radius);
                        vertices.Add(position);
                        uvs.Add(new Vector2(u, start_v + segment_v * j));
                    }
                }

                for (int i = 0; i < circle_count - 1; i++)
                {
                    int start_index = i * circle_point_count;
                    int next_start_index = ((i + 1) % circle_count) * circle_point_count;

                    for (int j = 0; j < circle_point_count; j++)
                    {
                        int next_j = (j + 1) % circle_point_count;
                        AddQuad(j + start_index, next_j + start_index, next_j + next_start_index, j + next_start_index, indices);
                    }
                }

                if (ratio < 1f)
                {
                    // front side
                    Vector3 centerF = center_vertices[0];
                    vertices.Add(centerF); uvs.Add(Vector2.zero);
                    for (int i = 0; i < circle_point_count; i++)
                    {
                        vertices.Add(vertices[i]);
                        uvs.Add(new Vector2(circle_points[i].x, circle_points[i].y));
                    }
                    for (int i = 0; i < circle_point_count - 1; i++)
                    {
                        AddTriangle(front_side_offset, front_side_offset + i + 1, front_side_offset + i + 2, indices);
                    }

                    // back side
                    int last_circle_offset = (circle_count - 1) * circle_point_count;
                    Vector3 centerB = center_vertices[circle_count - 1];
                    vertices.Add(centerB); uvs.Add(Vector2.zero);
                    back_side_offset = vertices.Count - 1;
                    
                    for (int i = 0; i < circle_point_count; i++)
                    {
                        vertices.Add(vertices[i + last_circle_offset]);
                        uvs.Add(new Vector2(circle_points[i].x, circle_points[i].y));
                    }
                    for (int i = 0; i < circle_point_count - 1; i++)
                    {
                        AddTriangle(back_side_offset, back_side_offset + i + 2, back_side_offset + i + 1, indices);
                    }
                }

                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }

            // 方圆体
            public static UMesh GenerateSquareRingMesh(SquareRingDescription description)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                int sectionCount = Mathf.Max(3, description.section_count);
                float radius = description.radius;
                float height = description.height;
                float half_height = height / 2;
                float width = description.width;
                float half_width = width / 2;
                float ratio = Mathf.Clamp01(description.ratio);
                float chamfer_percent = Mathf.Clamp01(description.chamfer_percent);
                float chamfer_length = chamfer_percent * Mathf.Min(width, height) * 0.5f;
                float chamfer_section_length = description.chamfer_section_length;
                int chamfer_section_count = chamfer_length > 0 && chamfer_section_length > 0
                    ? Mathf.Max(1, Mathf.RoundToInt(chamfer_length * Mathf.PI * 0.5f / chamfer_section_length))
                    : 0;

                float single_section_length = 2 * Mathf.PI * radius * ratio / sectionCount;
                int keyPointCount = ratio < 1f ? sectionCount + 1 : sectionCount + 1;

                // 横截面
                List<Vector3> raw_cross_positions = new List<Vector3>
                {
                    new Vector3(half_width - chamfer_length, height, 0),
                    new Vector3(-half_width + chamfer_length, height, 0),
                    new Vector3(-half_width, height - chamfer_length, 0),
                    new Vector3(-half_width, chamfer_length, 0),
                    new Vector3(-half_width + chamfer_length, 0, 0),
                    new Vector3(half_width - chamfer_length, 0, 0),
                    new Vector3(half_width, chamfer_length, 0),
                    new Vector3(half_width, height - chamfer_length, 0)
                };

                if (chamfer_length > 0)
                {
                    List<Vector3> new_raw = new List<Vector3>();
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 s0_p0 = raw_cross_positions[i * 2];
                        Vector3 s0_p1 = raw_cross_positions[i * 2 + 1];
                        Vector3 s1_p0 = raw_cross_positions[((i + 1) % 4) * 2];
                        Vector3 s1_p1 = raw_cross_positions[((i + 1) % 4) * 2 + 1];
                        new_raw.AddRange(EvaluateBezierCurve(s0_p0, s0_p1, s1_p0, s1_p1, chamfer_section_count, 0.3f));
                    }
                    raw_cross_positions = new_raw;
                    raw_cross_positions.Add(raw_cross_positions[0]);
                }

                List<Matrix4x4> raw_transforms = new List<Matrix4x4>();
                float singleRadian = 2 * Mathf.PI * ratio / sectionCount;
                for (int i = 0; i < keyPointCount; i++)
                {
                    float radian = singleRadian * i;
                    float x = -Mathf.Sin(radian) * radius;
                    float z = Mathf.Cos(radian) * radius;
                    Vector3 x_axis = new Vector3(x, 0, z).normalized;
                    Vector3 y_axis = new Vector3(0, 1, 0);
                    Vector3 z_axis = Vector3.Cross(x_axis, y_axis);
                    
                    Matrix4x4 m = Matrix4x4.identity;
                    m.SetColumn(0, new Vector4(x_axis.x, x_axis.y, x_axis.z, 0));
                    m.SetColumn(1, new Vector4(y_axis.x, y_axis.y, y_axis.z, 0));
                    m.SetColumn(2, new Vector4(z_axis.x, z_axis.y, z_axis.z, 0));
                    m.SetColumn(3, new Vector4(x, 0, z, 1));
                    raw_transforms.Add(m);
                }

                List<float> uv_y_templates = new List<float> { 0 };
                for (int idx = 1; idx < raw_cross_positions.Count; idx++)
                {
                    Vector3 pre_pos = raw_cross_positions[idx - 1];
                    Vector3 pos = raw_cross_positions[idx];
                    float dist = (pre_pos - pos).magnitude;
                    uv_y_templates.Add(uv_y_templates[uv_y_templates.Count - 1] + dist);
                }

                // main
                for (int i = 0; i < raw_transforms.Count; i++)
                {
                    Matrix4x4 t = raw_transforms[i];
                    for (int j = 0; j < raw_cross_positions.Count; j++)
                    {
                        Vector3 p = raw_cross_positions[j];
                        Vector3 new_p = t.MultiplyPoint3x4(p);
                        vertices.Add(new_p);
                        uvs.Add(new Vector2(-i * single_section_length, uv_y_templates[j]));
                    }
                }

                int raw_cross_count = raw_cross_positions.Count;
                for (int i = 0; i < raw_transforms.Count - 1; i++)
                {
                    AddSide(i * raw_cross_count, (i + 1) * raw_cross_count, raw_cross_count, indices, false);
                }

                // side
                if (ratio < 1f)
                {
                    Vector3 centerF = new Vector3(0, half_height, 0);
                    List<Vector3> side_raw = new List<Vector3> { centerF };
                    side_raw.AddRange(raw_cross_positions);

                    int center_offset = vertices.Count;
                    int side_offset = center_offset + 1;
                    Matrix4x4 t0 = raw_transforms[0];
                    foreach (var p in side_raw)
                    {
                        vertices.Add(t0.MultiplyPoint3x4(p));
                        uvs.Add(new Vector2(p.x, p.y));
                    }
                    for (int i = 0; i < raw_cross_positions.Count - 1; i++)
                    {
                        AddTriangle(center_offset, side_offset + i, side_offset + i + 1, indices);
                    }

                    center_offset = vertices.Count;
                    side_offset = center_offset + 1;
                    Matrix4x4 t1 = raw_transforms[raw_transforms.Count - 1];
                    foreach (var p in side_raw)
                    {
                        vertices.Add(t1.MultiplyPoint3x4(p));
                        uvs.Add(new Vector2(p.x, p.y));
                    }
                    for (int i = 0; i < raw_cross_positions.Count - 1; i++)
                    {
                        AddTriangle(center_offset, side_offset + i + 1, side_offset + i, indices);
                    }
                }

                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
            
            // 多角星
            public static UMesh GenerateStarMesh(StarDescription description)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                int corner_count = Mathf.Max(3, description.corner_count);
                float outer_radius = description.outer_radius;
                float inner_radius = description.inner_radius;
                float width = description.width;
                
                float single_radians = 2 * Mathf.PI / corner_count;
                float half_single_radians = single_radians / 2;

                // front
                for (int i = 0; i < corner_count; i++)
                {
                    float radian = i * single_radians;
                    float left_radian = radian - half_single_radians;
                    float right_radian = left_radian + single_radians;

                    Vector3 center_position = new Vector3(0, 0, -width);
                    Vector3 outer_position = new Vector3(-Mathf.Sin(radian) * outer_radius, Mathf.Cos(radian) * outer_radius, 0);
                    Vector3 left_inner_position = new Vector3(-Mathf.Sin(left_radian) * inner_radius, Mathf.Cos(left_radian) * inner_radius, 0);
                    Vector3 right_inner_position = new Vector3(-Mathf.Sin(right_radian) * inner_radius, Mathf.Cos(right_radian) * inner_radius, 0);

                    vertices.Add(center_position); uvs.Add(new Vector2(-center_position.x, -center_position.y));
                    vertices.Add(left_inner_position); uvs.Add(new Vector2(-left_inner_position.x, -left_inner_position.y));
                    vertices.Add(outer_position); uvs.Add(new Vector2(-outer_position.x, -outer_position.y));
                    
                    vertices.Add(center_position); uvs.Add(new Vector2(-center_position.x, -center_position.y));
                    vertices.Add(outer_position); uvs.Add(new Vector2(-outer_position.x, -outer_position.y));
                    vertices.Add(right_inner_position); uvs.Add(new Vector2(-right_inner_position.x, -right_inner_position.y));

                    int index_offset = i * 6;
                    indices.Add(index_offset);
                    indices.Add(index_offset + 1);
                    indices.Add(index_offset + 2);
                    indices.Add(index_offset + 3);
                    indices.Add(index_offset + 4);
                    indices.Add(index_offset + 5);
                }

                // back
                int back_offset = vertices.Count;
                int front_vertex_count = vertices.Count;
                for (int i = 0; i < front_vertex_count; i++)
                {
                    Vector3 old_pos = vertices[i];
                    Vector2 old_uv = uvs[i];
                    vertices.Add(new Vector3(old_pos.x, old_pos.y, -old_pos.z));
                    uvs.Add(new Vector2(-old_uv.x, old_uv.y));
                }

                for (int i = 0; i < corner_count; i++)
                {
                    int index_offset = i * 6 + back_offset;
                    indices.Add(index_offset);
                    indices.Add(index_offset + 2);
                    indices.Add(index_offset + 1);
                    indices.Add(index_offset + 3);
                    indices.Add(index_offset + 5);
                    indices.Add(index_offset + 4);
                }

                indices.Reverse();

                UMesh mesh = new UMesh();
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
            #endregion
            
            #region create go
            public static GameObject CreateCube(CubeDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshCube = go.AddComponent<MeshCube>();
                meshCube.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }

            public static GameObject CreateSphere(SphereDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshSphere = go.AddComponent<MeshSphere>();
                meshSphere.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }

            public static GameObject CreateHollowSphere(HollowSphereDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshHollowSphere = go.AddComponent<MeshHollowSphere>();
                meshHollowSphere.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }

            public static GameObject CreatePrism(PrismDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshPrism = go.AddComponent<MeshPrism>();
                meshPrism.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }

            public static GameObject CreateCylinder(CylinderDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshCylinder = go.AddComponent<MeshCylinder>();
                meshCylinder.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }

            public static GameObject CreatePyramid(PyramidDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshPyramid = go.AddComponent<MeshPyramid>();
                meshPyramid.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }

            public static GameObject CreateCircularRing(CircularRingDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshCircularRing = go.AddComponent<MeshCircularRing>();
                meshCircularRing.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }

            public static GameObject CreateSquareRing(SquareRingDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshSquareRing = go.AddComponent<MeshSquareRing>();
                meshSquareRing.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }

            public static GameObject CreateStar(StarDescription description)
            {
                GameObject go = new GameObject();
                MeshFilter mf = go.AddComponent<MeshFilter>();
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                var meshStar = go.AddComponent<MeshStar>();
                meshStar.description = description;
                var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                mr.sharedMaterial = urpAsset != null ? urpAsset.defaultMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return go;
            }
            
            #endregion
            
            private static List<Vector3> RecalculateRowPositions(List<Vector3> raw, int section_count)
            {
                List<Vector3> res = new List<Vector3>();
                for (int i = 0; i < 4; i++)
                {
                    int p0 = i * 2;
                    int p1 = i * 2 + 1;
                    int p2 = ((i + 1) % 4) * 2;
                    int p3 = ((i + 1) % 4) * 2 + 1;
                    res.AddRange(EvaluateBezierCurve(raw[p0], raw[p1], raw[p2], raw[p3], section_count, 0.3f));
                }
                return res;
            }

            private static List<Vector3> EvaluateBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int section_count, float ratio)
            {
                Vector3 start = p1;
                Vector3 end = p2;
                Vector3 c1 = p1 + (p1 - p0) * ratio;
                Vector3 c2 = p2 + (p2 - p3) * ratio;

                var curver = new BezierCurver(start, c1, c2, end);
                return curver.EvaluatePositions(Mathf.Max(1, section_count));
            }

            private static void AddQuad(int leftDown, int rightDown, int rightUp, int leftUp, List<int> indices)
            {
                indices.Add(rightDown);
                indices.Add(leftDown);
                indices.Add(leftUp);

                indices.Add(rightDown);
                indices.Add(leftUp);
                indices.Add(rightUp);
            }

            private static void AddTriangle(int index1, int index2, int index3, List<int> indices)
            {
                indices.Add(index1);
                indices.Add(index2);
                indices.Add(index3);
            }

            private static void AddSide(int downOffset, int upOffset, int count, List<int> indices, bool is_lu_rd = true)
            {
                for (int i = 0; i < count - 1; i++)
                {
                    if (is_lu_rd) AddQuad(downOffset + i, downOffset + 1 + i, upOffset + 1 + i, upOffset + i, indices);
                    else AddQuad1(downOffset + i, downOffset + 1 + i, upOffset + 1 + i, upOffset + i, indices);
                }
            }

            private static void AddQuad1(int ld, int rd, int ru, int lu, List<int> indices)
            {
                indices.Add(ld); indices.Add(lu); indices.Add(ru);
                indices.Add(rd); indices.Add(ld); indices.Add(ru);
            }
        }
    }
}
