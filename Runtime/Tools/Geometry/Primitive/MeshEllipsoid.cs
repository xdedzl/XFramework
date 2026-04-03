using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshEllipsoid : CustomMesh<EllipsoidDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }
        
        private void Reset()
        {
            description = EllipsoidDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.radius_x = Mathf.Max(0.01f, desc.radius_x);
                desc.radius_y = Mathf.Max(0.01f, desc.radius_y);
                desc.radius_z = Mathf.Max(0.01f, desc.radius_z);
                desc.section_count = Mathf.Max(3, desc.section_count);
                desc.ratio = Mathf.Clamp01(desc.ratio);
                
                Mesh generatedMesh = UUtility.Model.GenerateEllipsoidMesh(desc);
                generatedMesh.name = "Procedural Ellipsoid";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}

