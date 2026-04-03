using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshTetrahedron : CustomMesh<TetrahedronDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }
        
        private void Reset()
        {
            description = TetrahedronDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.radius = Mathf.Max(0.01f, desc.radius);
                
                Mesh generatedMesh = UUtility.Model.GenerateTetrahedronMesh(desc);
                generatedMesh.name = "Procedural Tetrahedron";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}

