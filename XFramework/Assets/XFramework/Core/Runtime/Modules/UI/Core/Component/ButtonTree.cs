using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace XFramework.UI
{
    public class ButtonTree : MonoBehaviour
    {
        public List<Node> nodes;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnGUI()
        {
            
        }
    }

    [System.Serializable]
    public class Node
    {
        public string text;
        public UnityEvent action;
        public List<Node> childNodes;
    }
}