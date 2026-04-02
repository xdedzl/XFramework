using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public class BezierCurver
    {
        public Vector3 p0;
        public Vector3 p1;
        public Vector3 p2;
        public Vector3 p3;

        private List<float> _t_key;
        private List<float> _length_val;

        public BezierCurver(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            this.p0 = p0;
            this.p1 = p1;
            this.p2 = p2;
            this.p3 = p3;
            this._t_key = new List<float>();
            this._length_val = new List<float>();
        }

        public Vector3 EvaluatePosition(float t)
        {
            return UUtility.Bezier.CubicBezierPoint(p0, p1, p2, p3, t);
        }

        public Vector3 EvaluateTangent(float t)
        {
            return UUtility.Bezier.CubicBezierTangent(p0, p1, p2, p3, t);
        }

        public List<Vector3> EvaluatePositions(int sectionCount)
        {
            InitFastBezierLength(sectionCount);
            var positions = new List<Vector3>();
            for (int idx = 0; idx <= sectionCount; idx++)
            {
                float t = 1.0f / sectionCount * idx;
                t = FastGetTByRatio(t);
                positions.Add(EvaluatePosition(t));
            }
            return positions;
        }

        public List<Vector3> EvaluatePositionsBySectionLength(float sectionLength, int maxSectionCount = int.MaxValue)
        {
            InitFastBezierLength(10);
            int sectionCount = GetSectionCountByLength(sectionLength, maxSectionCount);
            var positions = new List<Vector3>();
            for (int idx = 0; idx <= sectionCount; idx++)
            {
                float t = 1.0f / sectionCount * idx;
                t = FastGetTByRatio(t);
                positions.Add(EvaluatePosition(t));
            }
            return positions;
        }

        public List<Vector3> EvaluateTangents(int sectionCount)
        {
            InitFastBezierLength(sectionCount);
            var tangents = new List<Vector3>();
            for (int idx = 0; idx <= sectionCount; idx++)
            {
                float t = 1.0f / sectionCount * idx;
                t = FastGetTByRatio(t);
                tangents.Add(EvaluateTangent(t));
            }
            return tangents;
        }

        public List<Vector3> EvaluateTangentsBySectionLength(float sectionLength, int maxSectionCount = int.MaxValue)
        {
            InitFastBezierLength(10);
            int sectionCount = GetSectionCountByLength(sectionLength, maxSectionCount);
            var tangents = new List<Vector3>();
            for (int idx = 0; idx <= sectionCount; idx++)
            {
                float t = 1.0f / sectionCount * idx;
                t = FastGetTByRatio(t);
                tangents.Add(EvaluateTangent(t));
            }
            return tangents;
        }

        public void EvaluatePositionsAndTangentsBySectionLength(float sectionLength, out List<Vector3> positions, out List<Vector3> tangents, out List<float> ts, int maxSectionCount = int.MaxValue)
        {
            InitFastBezierLength(10);
            int sectionCount = GetSectionCountByLength(sectionLength, maxSectionCount);
            positions = new List<Vector3>();
            tangents = new List<Vector3>();
            ts = new List<float>();

            float interval = 1.0f / sectionCount;
            float t = 0;
            for (int idx = 0; idx <= sectionCount; idx++)
            {
                float fixedT = FastGetTByRatio(t);
                Vector3 position = EvaluatePosition(fixedT);
                Vector3 tangent = EvaluateTangent(fixedT);

                positions.Add(position);
                tangents.Add(tangent);
                ts.Add(t);
                t += interval;
            }
        }

        public float CalculateLength(int sectionCount)
        {
            float length = 0;
            Vector3 prev = EvaluatePosition(0);
            for (int idx = 1; idx <= sectionCount; idx++)
            {
                float t = 1.0f / sectionCount * idx;
                Vector3 position = EvaluatePosition(t);
                Vector3 direction = position - prev;
                length += direction.magnitude;
                prev = position;
            }
            return length;
        }

        public void InitFastBezierLength(int sectionCount, bool force = false)
        {
            if (_t_key.Count > 0 && !force)
            {
                return;
            }
            float length = 0;
            _t_key.Clear();
            _length_val.Clear();
            _t_key.Add(0);
            _length_val.Add(0);
            Vector3 prev = EvaluatePosition(0);
            for (int idx = 1; idx <= sectionCount; idx++)
            {
                float t = 1.0f / sectionCount * idx;
                Vector3 position = EvaluatePosition(t);
                Vector3 direction = position - prev;
                if (direction.sqrMagnitude == 0)
                {
                    Debug.LogError($"Direction length is 0 for p0:{p0}, p1:{p1}, p2:{p2}, p3:{p3}, section_count:{sectionCount}");
                }
                length += direction.magnitude;
                prev = position;
                _t_key.Add(t);
                _length_val.Add(length);
            }
        }

        public float FastGetTByRatio(float ratio)
        {
            float targetLength = Mathf.Clamp(_length_val[_length_val.Count - 1] * ratio, 0, _length_val[_length_val.Count - 1]);
            int index = _length_val.BinarySearch(targetLength);
            if (index < 0)
            {
                index = ~index;
            }

            int leftIndex, rightIndex;
            if (index == 0)
            {
                leftIndex = 0; rightIndex = 1;
            }
            else if (index >= _t_key.Count)
            {
                leftIndex = _t_key.Count - 2; rightIndex = _t_key.Count - 1;
            }
            else
            {
                leftIndex = index - 1; rightIndex = index;
            }

            if (_length_val[rightIndex] - _length_val[leftIndex] == 0)
                return _t_key[leftIndex];

            float coeffLeftIndex = (_length_val[rightIndex] - targetLength) / (_length_val[rightIndex] - _length_val[leftIndex]);
            float coeffRightIndex = (targetLength - _length_val[leftIndex]) / (_length_val[rightIndex] - _length_val[leftIndex]);
            float t = _t_key[leftIndex] * coeffLeftIndex + _t_key[rightIndex] * coeffRightIndex;
            return t;
        }

        private int GetSectionCountByLength(float sectionLength, int maxSectionCount)
        {
            int sectionCount = Mathf.FloorToInt(_length_val[_length_val.Count - 1] / sectionLength);
            sectionCount = Mathf.Min(Mathf.Max(1, sectionCount), maxSectionCount);
            return sectionCount;
        }

        private int GetMinSectionCount()
        {
            float angle = Vector3.Angle(p1 - p0, p3 - p2);
            return Mathf.FloorToInt(angle) * 5;
        }
    }

    
    public partial class UUtility
    {
        public static class Bezier
        {

            public static Vector3 CubicBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
            {
                float t2 = t * t;
                float t3 = t2 * t;
                Vector3 position = p0 * (-1 * t3 + 3 * t2 - 3 * t + 1) +
                                   p1 * (3 * t3 - 6 * t2 + 3 * t) +
                                   p2 * (-3 * t3 + 3 * t2) +
                                   p3 * t3;
                return position;
            }

            public static Vector3 CubicBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
            {
                float t2 = t * t;
                Vector3 tangent = p0 * (-3 * t2 + 6 * t - 3) +
                                  p1 * (9 * t2 - 12 * t + 3) +
                                  p2 * (-9 * t2 + 6 * t) +
                                  p3 * (3 * t2);
                return tangent.normalized;
            }

            public static BezierCurver CreateBezierCurveBetweenSegment(Vector3 s0_p0, Vector3 s0_p1, Vector3 s1_p0, Vector3 s1_p1, float arkRatio = 0.5f)
            {
                Vector3 p0 = s0_p1;
                Vector3 p3 = s1_p0;
                float tangentLength = (p3 - p0).magnitude * arkRatio;
                Vector3 p1 = p0 + (s0_p1 - s0_p0).normalized * tangentLength;
                Vector3 p2 = p3 + (s1_p0 - s1_p1).normalized * tangentLength;
                return new BezierCurver(p0, p1, p2, p3);
            }
        }
    }
}