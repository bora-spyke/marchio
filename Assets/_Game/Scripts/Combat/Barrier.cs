using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public sealed class Barrier : MonoBehaviour, IPoolable
    {
        [SerializeField] LineRenderer line;

        readonly List<Vector2> poly = new List<Vector2>(128);
        float t;
        float life;

        public IReadOnlyList<Vector2> Poly => poly;

        public void OnSpawn() { }
        public void OnDespawn() { line.positionCount = 0; }

        public void Init(List<Vector2> source)
        {
            poly.Clear();
            poly.AddRange(source);
            t = 0f;
            life = GameManager.I.Config.barrierDurationMs / 1000f;
            line.loop = true;
            line.positionCount = poly.Count;
            for (int i = 0; i < poly.Count; i++) line.SetPosition(i, PolygonMath.ToWorld(poly[i], 0.5f));
            SetAlpha(1f);
        }

        public bool Tick(float dt)
        {
            t += dt;
            if (t >= life) return false;
            SetAlpha(Mathf.Clamp01(1f - t / life) * 0.8f + 0.2f);
            return true;
        }

        public bool PushOut(Enemy en, float dt)
        {
            bool touching = false;
            var p = en.Pos;
            for (int i = 0; i < poly.Count; i++)
            {
                var cp = PolygonMath.ClosestPointOnSegment(p, poly[i], poly[(i + 1) % poly.Count]);
                var d = p - cp;
                float dist = d.magnitude;
                if (dist < en.Radius)
                {
                    touching = true;
                    float push = (en.Radius - dist) + 0.5f;
                    p += dist > 1e-4f ? d / dist * push : new Vector2(push, 0f);
                }
            }
            if (touching) en.SetPos(p);
            return touching;
        }

        public bool BlocksSegment(Vector2 a, Vector2 b, out Vector2 hit)
        {
            for (int i = 0; i < poly.Count; i++)
                if (PolygonMath.SegmentsIntersect(a, b, poly[i], poly[(i + 1) % poly.Count], out hit)) return true;
            hit = default;
            return false;
        }

        void SetAlpha(float a)
        {
            var c = line.startColor;
            c.a = a;
            line.startColor = c;
            line.endColor = c;
        }
    }
}
