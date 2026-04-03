using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshDisc : CustomMesh<DiscDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }
        
        private void Reset()
        {
            description = DiscDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.outer_radius = Mathf.Max(0.01f, desc.outer_radius);
                desc.inner_radius = Mathf.Clamp(desc.inner_radius, 0f, desc.outer_radius - 0.01f);
                desc.section_count = Mathf.Max(3, desc.section_count);
                desc.ratio = Mathf.Clamp01(desc.ratio);
                
                Mesh generatedMesh = UUtility.Model.GenerateDiscMesh(desc);
                generatedMesh.name = "Procedural Disc";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}

