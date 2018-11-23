using UnityEngine;
using System.Collections.Generic;

namespace Splines
{

    public class SplineIndex
    {
        public Vector3[] linearPoints;
        SplineComponent spline;

        public int ControlPointCount => spline.ControlPointCount;

        public SplineIndex(SplineComponent spline)
        {
            this.spline = spline;
            ReIndex();
        }

        public void ReIndex()
        {
            var searchStepSize = 0.00001f;
            var length = spline.GetLength(searchStepSize);
            var indexSize = Mathf.FloorToInt(length * 2);
            var _linearPoints = new List<Vector3>(indexSize);
            var t = 0f;

            var linearDistanceStep = length / 1024;
            var linearDistanceStep2 = Mathf.Pow(linearDistanceStep, 2);

            var start = spline.GetNonUniformPoint(0);
            _linearPoints.Add(start);
            while (t <= 1f)
            {
                var current = spline.GetNonUniformPoint(t);
                while ((current - start).sqrMagnitude <= linearDistanceStep2)
                {
                    t += searchStepSize;
                    current = spline.GetNonUniformPoint(t);
                }
                start = current;
                _linearPoints.Add(current);
            }
            linearPoints = _linearPoints.ToArray();
        }

        public Vector3 GetPoint(float t)
        {
            var sections = linearPoints.Length - (spline.closed ? 0 : 3);
            var i = Mathf.Min(Mathf.FloorToInt(t * (float)sections), sections - 1);
            var count = linearPoints.Length;
            if (i < 0) i += count;
            var u = t * (float)sections - (float)i;
            var a = linearPoints[(i + 0) % count];
            var b = linearPoints[(i + 1) % count];
            var c = linearPoints[(i + 2) % count];
            var d = linearPoints[(i + 3) % count];
            return spline.Interpolate(a, b, c, d, u);
        }

    }
}