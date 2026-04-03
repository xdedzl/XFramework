using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshArrow : CustomMesh<ArrowDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }
        
        private void Reset()
        {
            description = ArrowDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.shaft_radius = Mathf.Max(0.01f, desc.shaft_radius);
                desc.head_radius = Mathf.Max(desc.shaft_radius, desc.head_radius);
                desc.section_count = Mathf.Max(3, desc.section_count);
                
                Mesh generatedMesh = UUtility.Model.GenerateArrowMesh(desc);
                generatedMesh.name = "Procedural Arrow";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}

