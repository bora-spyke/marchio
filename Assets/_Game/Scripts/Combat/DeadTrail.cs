using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public sealed class DeadTrail : MonoBehaviour, IPoolable
    {
        [SerializeField] LineRenderer line;

        float t;
        float life;

        public void OnSpawn() { }
        public void OnDespawn() { line.positionCount = 0; }

        public void Init(List<Vector2> points)
        {
            t = 0f;
            life = GameManager.I.Config.deadTrailMs / 1000f;
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, PolygonMath.ToWorld(points[i], 0.8f));
            SetAlpha(0.6f);
        }

        public bool Tick(float dt)
        {
            t += dt;
            if (t >= life) return false;
            SetAlpha(0.6f * (1f - t / life));
            return true;
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
