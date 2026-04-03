using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshCapsule : CustomMesh<CapsuleDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }
        
        private void Reset()
        {
            description = CapsuleDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.radius = Mathf.Max(0.01f, desc.radius);
                desc.height = Mathf.Max(desc.radius * 2, desc.height);
                desc.section_count = Mathf.Max(3, desc.section_count);
                desc.cap_section_count = Mathf.Max(1, desc.cap_section_count);
                
                Mesh generatedMesh = UUtility.Model.GenerateCapsuleMesh(desc);
                generatedMesh.name = "Procedural Capsule";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}

