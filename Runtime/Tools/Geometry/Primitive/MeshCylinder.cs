using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshCylinder : CustomMesh<CylinderDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }

        private void Reset()
        {
            description = CylinderDescription.identity;
        }
        
        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var des = description;
                des.section_count = Mathf.Max(3, des.section_count);
                des.height = Mathf.Max(0.01f, des.height);
                des.top_radius = Mathf.Max(0.0f, des.top_radius);
                des.bottom_radius = Mathf.Max(0.01f, des.bottom_radius);
                des.ratio = Mathf.Clamp(des.ratio, 0.01f, 1f);

                float minRadius = Mathf.Min(des.top_radius, des.bottom_radius);
                float minExt = Mathf.Min(minRadius, des.height);
                des.chamfer_length = Mathf.Clamp(des.chamfer_length, 0, minExt * 0.49f);
                des.chamfer_section_count = Mathf.Max(0, des.chamfer_section_count);

                UnityEngine.Mesh generatedMesh = UUtility.Model.GenerateCylinderMesh(des);
                generatedMesh.name = "Procedural Cylinder";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}
