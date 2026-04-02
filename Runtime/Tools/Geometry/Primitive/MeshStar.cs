using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshStar : CustomMesh<StarDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }
        
        private void Reset()
        {
            description = StarDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        public void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.corner_count = Mathf.Max(3, desc.corner_count);
                desc.outer_radius = Mathf.Max(0.01f, desc.outer_radius);
                desc.inner_radius = Mathf.Clamp(desc.inner_radius, 0.01f, desc.outer_radius - 0.01f);
                
                Mesh generatedMesh = UUtility.Model.GenerateStarMesh(desc);
                generatedMesh.name = "Procedural Star";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}
