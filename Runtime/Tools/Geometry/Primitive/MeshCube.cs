 using UnityEngine;

namespace XFramework
{
    public class MeshCube : CustomMesh<CubeDescription>
    {
        public bool useOld;

        private void Reset()
        {
            description = CubeDescription.identity;
        }

        private void OnValidate()
        {
            // 在编辑时实时加入限制，防止其触发 UUtility.Mesh.GenerateCube 的异常
            var des = description;
            des.x_length = Mathf.Max(0.01f, des.x_length);
            des.y_length = Mathf.Max(0.01f, des.y_length);
            des.z_length = Mathf.Max(0.01f, des.z_length);

            float minSide = Mathf.Min(des.x_length, des.y_length, des.z_length);
            des.chamfer_length = Mathf.Clamp(des.chamfer_length, 0, minSide * 0.5f);
            des.chamfer_section_count = Mathf.Max(0, des.chamfer_section_count);

            GenerateMesh();
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null)
            {
                m_MeshFilter = GetComponent<MeshFilter>();
            }

            if (m_MeshFilter != null)
            {
                // 实时生成并更新 Mesh
                Mesh generatedMesh;
                if (useOld)
                {
                    generatedMesh = UUtility.Model.GenerateCubeMeshOld(description);
                }
                else
                {
                    generatedMesh = UUtility.Model.GenerateCubeMesh(description);
                }
                generatedMesh.name = "Procedural Cube";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }

}
