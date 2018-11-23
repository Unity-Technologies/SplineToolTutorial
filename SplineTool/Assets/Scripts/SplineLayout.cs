using UnityEngine;

namespace Splines
{
    [ExecuteInEditMode]
    public class SplineLayout : MonoBehaviour
    {
        public SplineComponent spline;
        public int count = 0;

        void Reset()
        {
            spline = GetComponent<SplineComponent>();
        }

        void OnDrawGizmos()
        {
            for (var i = 0; i < count; i++)
            {
                var n = i * 1f / count;
                var p = spline.GetPoint(n);
                var normal = spline.GetRight(n);
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(p, p + normal);
            }
        }

    }
}