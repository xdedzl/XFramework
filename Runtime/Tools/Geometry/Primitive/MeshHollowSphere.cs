using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshHollowSphere : CustomMesh<HollowSphereDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }

        private void Reset()
        {
            description = HollowSphereDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var des = description;
                des.outer_radius = Mathf.Max(0.01f, des.outer_radius);
                des.inner_radius = Mathf.Clamp(des.inner_radius, 0.001f, des.outer_radius - 0.001f);
                des.ratio = Mathf.Clamp(des.ratio, 0.01f, 1f);
                des.section_count = Mathf.Max(3, des.section_count);
                
                UnityEngine.Mesh generatedMesh = UUtility.Model.GenerateHollowSphereMesh(des);
                generatedMesh.name = "Procedural Hollow Sphere";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}
