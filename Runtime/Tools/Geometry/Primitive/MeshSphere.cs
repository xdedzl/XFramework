using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshSphere : CustomMesh<SphereDescription>
    {
        private void Reset()
        {
            description = SphereDescription.identity;
        }

        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var des = description;
                des.radius = Mathf.Max(0.01f, description.radius);
                des.ratio = Mathf.Clamp(description.ratio, 0.01f, 1f);
                des.section_count = Mathf.Max(3, description.section_count);
                Mesh generatedMesh = UUtility.Model.GenerateSphereMesh(des);
                generatedMesh.name = "Procedural Sphere";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}
