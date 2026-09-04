using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public enum LoopCancelReason { Release, Limit, Hit }

    public sealed class LoopTrail : MonoBehaviour
    {
        [SerializeField] LineRenderer line;
        [SerializeField] LineRenderer flashLine;
        [SerializeField] Transform emitter;

        static readonly int DangerTId = Shader.PropertyToID("_DangerT");

        readonly List<Vector2> points = new List<Vector2>(256);
        readonly List<float> lengths = new List<float>(256);
        readonly List<Vector2> polyBuffer = new List<Vector2>(256);
        Vector2 last;
        float flashT;
        float flashLife;

        public bool Drawing { get; private set; }
        public float PathLength { get; private set; }
        public IReadOnlyList<Vector2> Points => points;

        GameManager Gm => GameManager.I;

        Vector2 Head => emitter != null ? PolygonMath.ToPlane(emitter.position) : Gm.Player.Pos;
        float LineHeight => emitter != null ? emitter.position.y : 1f;

        public bool Touches(Vector2 pos, float radius)
        {
            if (!Drawing || points.Count < 3) return false;
            float r2 = radius * radius;
            int lastSegment = points.Count - 3;
            for (int i = 0; i < lastSegment; i++)
            {
                var cp = PolygonMath.ClosestPointOnSegment(pos, points[i], points[i + 1]);
                if ((pos - cp).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        public void ResetState()
        {
            Drawing = false;
            points.Clear();
            lengths.Clear();
            PathLength = 0f;
            flashLife = 0f;
            line.positionCount = 0;
            flashLine.positionCount = 0;
        }

        public void Tick(float dt, bool draw)
        {
            var cfg = Gm.Config;
            var pos = Head;

            if (draw && !Drawing)
            {
                Drawing = true;
                points.Clear();
                lengths.Clear();
                points.Add(pos);
                lengths.Add(0f);
                PathLength = 0f;
                last = pos;
            }
            if (!Drawing) return;

            float d = Vector2.Distance(pos, last);
            if (d > cfg.trailMinDist)
            {
                PathLength += d;
                points.Add(pos);
                lengths.Add(PathLength);
                last = pos;
            }

            if (PathLength >= Gm.EffectiveMaxLoopLength()) { Cancel(LoopCancelReason.Limit); return; }
            if (!draw) { Cancel(LoopCancelReason.Release); return; }

            float minLen = cfg.MinLoopLength;
            if (PathLength >= minLen)
            {
                int hit = FindSelfTouchIndex(pos, minLen, cfg.CloseRadius);
                if (hit >= 0) { Close(hit); return; }
            }
            RenderLine(pos);
        }

        int FindSelfTouchIndex(Vector2 pos, float minLen, float radius)
        {
            float r2 = radius * radius;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (PathLength - lengths[i] < minLen) continue;
                if ((pos - points[i]).sqrMagnitude <= r2) return i;
            }
            return -1;
        }

        public void Cancel(LoopCancelReason reason)
        {
            if (!Drawing) return;
            Gm.Fx.Burst(Gm.Player.Pos, Gm.Config.trail, 6);
            Drawing = false;
            line.positionCount = 0;
        }

        void Close(int hitIndex)
        {
            polyBuffer.Clear();
            for (int i = hitIndex; i < points.Count; i++) polyBuffer.Add(points[i]);
            Drawing = false;
            line.positionCount = 0;
            if (polyBuffer.Count < 3) return;
            LoopDamage.Resolve(polyBuffer);
            ShowFlash(polyBuffer);
        }

        void ShowFlash(List<Vector2> poly)
        {
            flashT = 0f;
            flashLife = Gm.Config.loopFlashMs / 1000f;
            flashLine.positionCount = poly.Count;
            for (int i = 0; i < poly.Count; i++) flashLine.SetPosition(i, PolygonMath.ToWorld(poly[i], 1.5f));
            SetLineAlpha(flashLine, 1f);
        }

        public void TickFlash(float dt)
        {
            if (flashLife <= 0f) return;
            flashT += dt;
            if (flashT >= flashLife)
            {
                flashLife = 0f;
                flashLine.positionCount = 0;
                return;
            }
            SetLineAlpha(flashLine, 1f - flashT / flashLife);
        }

        void RenderLine(Vector2 head)
        {
            float y = LineHeight;
            line.positionCount = points.Count + 1;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, PolygonMath.ToWorld(points[i], y));
            line.SetPosition(points.Count, PolygonMath.ToWorld(head, y));

            float lifeT = Mathf.Clamp01(PathLength / Gm.EffectiveMaxLoopLength());
            line.material.SetFloat(DangerTId, lifeT);
        }

        static void SetLineAlpha(LineRenderer lr, float a)
        {
            var c = lr.startColor;
            c.a = a;
            lr.startColor = c;
            lr.endColor = c;
        }
    }
}
