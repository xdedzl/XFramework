using System;
using UnityEngine;

namespace XFramework
{
    [ExecuteAlways]
    public class MeshPrism : CustomMesh<PrismDescription>
    {
        private void Awake() { GenerateMesh(); }
        private void OnValidate() { GenerateMesh(); }

        private void Reset()
        {
            description = PrismDescription.identity;
        }

        [ContextMenu("Regenerate Mesh")]
        protected override void GenerateMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter != null)
            {
                var desc = description;
                desc.section_count = Mathf.Max(3, desc.section_count);
                desc.height = Mathf.Max(0.01f, desc.height);
                desc.top_radius = Mathf.Max(0.0f, desc.top_radius);
                desc.bottom_radius = Mathf.Max(0.01f, desc.bottom_radius);
                
                float minRadius = Mathf.Min(desc.top_radius, desc.bottom_radius);
                float minExt = Mathf.Min(minRadius, desc.height);
                desc.chamfer_length = Mathf.Clamp(desc.chamfer_length, 0, minExt * 0.49f);
                desc.chamfer_section_count = Mathf.Max(0, desc.chamfer_section_count);

                UnityEngine.Mesh generatedMesh = UUtility.Model.GeneratePrismMesh(desc);
                generatedMesh.name = "Procedural Prism";
                m_MeshFilter.sharedMesh = generatedMesh;
            }
        }
    }
}
