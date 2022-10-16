using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Geometry
{
    public class Polygon : IPlane
    {
        private List<Vector3> keyPoints;

        public float Area => throw new System.NotImplementedException();

        public Vector3 normal => throw new System.NotImplementedException();

        public SR R(Point point)
        {
            throw new System.NotImplementedException();
        }

        public SR R(MulitLineSegment point)
        {
            throw new System.NotImplementedException();
        }
    }
}