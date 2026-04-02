using System;
using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshSquareRing : CustomMesh<SquareRingDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }

        private void Reset()
        {
            description = SquareRingDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.section_count = Mathf.Max(3, desc.section_count);
                desc.radius = Mathf.Max(0.01f, desc.radius);
                desc.width = Mathf.Max(0.01f, desc.width);
                desc.height = Mathf.Max(0.01f, desc.height);
                desc.ratio = Mathf.Clamp(desc.ratio, 0.01f, 1f);

                Mesh generatedMesh = UUtility.Model.GenerateSquareRingMesh(desc);
                generatedMesh.name = "Procedural Square Ring";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}
