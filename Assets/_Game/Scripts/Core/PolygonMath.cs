using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public static class PolygonMath
    {
        public static float Area(List<Vector2> poly)
        {
            float a = 0f;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                a += (poly[j].x + poly[i].x) * (poly[j].y - poly[i].y);
            return Mathf.Abs(a * 0.5f);
        }

        public static bool PointInPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var pi = poly[i];
                var pj = poly[j];
                bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                                 (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        public static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return a + ab * t;
        }

        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            return Vector2.Distance(p, ClosestPointOnSegment(p, a, b));
        }

        public static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 hit)
        {
            hit = default;
            float d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
            if (Mathf.Abs(d) < 1e-9f) return false;
            float t = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
            float u = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            hit = p1 + (p2 - p1) * t;
            return true;
        }

        public static float DistToBoundary(Vector2 p, List<Vector2> poly)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < poly.Count; i++)
            {
                float d = DistToSegment(p, poly[i], poly[(i + 1) % poly.Count]);
                if (d < best) best = d;
            }
            return best;
        }

        public static Vector2 Centroid(List<Vector2> poly)
        {
            var sum = Vector2.zero;
            for (int i = 0; i < poly.Count; i++) sum += poly[i];
            return poly.Count > 0 ? sum / poly.Count : Vector2.zero;
        }

        public static Vector3 ToWorld(Vector2 p, float y = 0f) => new Vector3(p.x, y, p.y);
        public static Vector2 ToPlane(Vector3 w) => new Vector2(w.x, w.z);
    }
}
