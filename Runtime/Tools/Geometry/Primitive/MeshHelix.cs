using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshHelix : CustomMesh<HelixDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }
        
        private void Reset()
        {
            description = HelixDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.coil_radius = Mathf.Max(0.01f, desc.coil_radius);
                desc.tube_radius = Mathf.Clamp(desc.tube_radius, 0.01f, desc.coil_radius);
                desc.section_count = Mathf.Max(3, desc.section_count);
                desc.tube_section_count = Mathf.Max(3, desc.tube_section_count);
                
                Mesh generatedMesh = UUtility.Model.GenerateHelixMesh(desc);
                generatedMesh.name = "Procedural Helix";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}

