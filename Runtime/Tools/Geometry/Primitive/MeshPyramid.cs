using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshPyramid : CustomMesh<PyramidDescription>
    {
        private void Reset()
        {
            description = PyramidDescription.identity;
        }

        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.section_count = Mathf.Max(3, desc.section_count);
                desc.height = Mathf.Max(0.01f, desc.height);
                desc.bottom_radius = Mathf.Max(0.01f, desc.bottom_radius);

                Mesh generatedMesh = UUtility.Model.GeneratePyramidMesh(desc);
                generatedMesh.name = "Procedural Pyramid";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}
