using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    [DisallowMultipleComponent]
    public sealed class AttachmentHost : XMonoBehaviour
    {
        [Serializable]
        private struct DynamicPointData
        {
            public string pointName;
            public Transform parent;
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public Vector3 localScale;
        }

        [SerializeField]
        private List<DynamicPointData> dynamicPoints = new List<DynamicPointData>();

        private Dictionary<string, Transform> m_Points;

        private void Awake()
        {
            for (int i = 0; i < dynamicPoints.Count; i++)
            {
                CreatePoint(dynamicPoints[i]);
            }

            BuildPointMap();
        }

        private void BuildPointMap()
        {
            AttachPoint[] points = GetComponentsInChildren<AttachPoint>(true);
            m_Points = new Dictionary<string, Transform>(points.Length, StringComparer.Ordinal);

            for (int i = 0; i < points.Length; i++)
            {
                AttachPoint point = points[i];
                if (point.GetComponentInParent<AttachmentHost>(true) != this)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(point.PointName))
                {
                    throw new InvalidOperationException($"AttachPoint {point.name} under {name} has no point name");
                }

                if (m_Points.ContainsKey(point.PointName))
                {
                    throw new InvalidOperationException($"AttachmentHost {name} contains duplicate point {point.PointName}");
                }

                m_Points.Add(point.PointName, point.transform);
            }
        }

        public Transform GetPoint(string pointName)
        {
            if (m_Points == null)
            {
                BuildPointMap();
            }

            if (!m_Points.TryGetValue(pointName, out Transform point))
            {
                throw new InvalidOperationException($"AttachmentHost {name} is missing point {pointName}");
            }

            return point;
        }

        public void Attach(Transform target, string pointName)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                for (int i = 0; i < dynamicPoints.Count; i++)
                {
                    DynamicPointData data = dynamicPoints[i];
                    if (data.pointName != pointName)
                    {
                        continue;
                    }

                    target.SetParent(data.parent, false);
                    target.localPosition = data.localPosition;
                    target.localRotation = Quaternion.Euler(data.localEulerAngles);
                    target.localScale = data.localScale;
                    return;
                }
            }
#endif

            target.SetParent(GetPoint(pointName), false);
            target.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            target.localScale = Vector3.one;
        }

        [Button("Dynamic2Static", ButtonAttribute.EnableMode.Editor)]
        private void Dynamic2Static()
        {
            for (int i = 0; i < dynamicPoints.Count; i++)
            {
                CreatePoint(dynamicPoints[i]);
            }

            dynamicPoints.Clear();
            BuildPointMap();
        }

        [Button("Static2Dynamic", ButtonAttribute.EnableMode.Editor)]
        private void Static2Dynamic()
        {
            AttachPoint[] points = GetComponentsInChildren<AttachPoint>(true);
            dynamicPoints.Clear();

            for (int i = 0; i < points.Length; i++)
            {
                AttachPoint point = points[i];
                if (point.GetComponentInParent<AttachmentHost>(true) != this)
                {
                    continue;
                }

                Transform pointTransform = point.transform;
                dynamicPoints.Add(new DynamicPointData
                {
                    pointName = point.PointName,
                    parent = pointTransform.parent,
                    localPosition = pointTransform.localPosition,
                    localEulerAngles = pointTransform.localEulerAngles,
                    localScale = pointTransform.localScale
                });
            }

            for (int i = 0; i < points.Length; i++)
            {
                AttachPoint point = points[i];
                if (point.GetComponentInParent<AttachmentHost>(true) == this)
                {
                    DestroyImmediate(point.gameObject);
                }
            }

            m_Points = null;
        }

        private static void CreatePoint(DynamicPointData data)
        {
            var pointObject = new GameObject(data.pointName);
            Transform pointTransform = pointObject.transform;
            pointTransform.SetParent(data.parent, false);
            pointTransform.localPosition = data.localPosition;
            pointTransform.localRotation = Quaternion.Euler(data.localEulerAngles);
            pointTransform.localScale = data.localScale;

            pointObject.AddComponent<AttachPoint>();
        }
    }
}
