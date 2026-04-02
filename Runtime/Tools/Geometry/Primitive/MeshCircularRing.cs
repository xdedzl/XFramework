using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshCircularRing : CustomMesh<CircularRingDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }

        private void Reset()
        {
            description = CircularRingDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var des = description;
                des.section_count = Mathf.Max(3, des.section_count);
                des.circle_section_count = Mathf.Max(3, des.circle_section_count);
                des.circle_radius = Mathf.Max(0.01f, des.circle_radius);
                des.section_radius = Mathf.Clamp(des.section_radius, 0.01f, des.circle_radius);
                des.ratio = Mathf.Clamp(des.ratio, 0.01f, 1f);

                Mesh generatedMesh = UUtility.Model.GenerateCircularRingMesh(des);
                generatedMesh.name = "Procedural Circular Ring";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}
