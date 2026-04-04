using UnityEngine;

namespace XFramework
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CustomMesh<T> : MonoBehaviour where T : IMeshDescription
    {
        [SerializeField]
        public T _description;
        public T description
        {
            get { return _description; }
            set 
            {
                _description = value;
                GenerateMesh();
            }
        }
        
        protected MeshFilter m_MeshFilter;

        protected virtual void GenerateMesh()
        {
            
        }
        
        private void Awake()
        {
            GenerateMesh();
        }
    }
}