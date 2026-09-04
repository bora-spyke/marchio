using UnityEngine;

namespace Marchio
{
    public sealed class SoulStone : MonoBehaviour, IPoolable
    {
        [SerializeField] Transform visualRoot;
        [SerializeField] float spinDegPerSec = 90f;
        [SerializeField] float bobAmplitude = 3f;
        [SerializeField] float bobSpeed = 2f;

        float t;
        Vector3 visualBasePos;

        public Vector2 Pos { get; private set; }

        public void OnSpawn() { }
        public void OnDespawn() { }

        public void Init(Vector2 pos)
        {
            Pos = pos;
            t = 0f;
            transform.position = PolygonMath.ToWorld(pos, 1f);
            if (visualRoot != null) visualBasePos = visualRoot.localPosition;
        }

        public void Tick(float dt)
        {
            t += dt;
            if (visualRoot == null) return;
            visualRoot.Rotate(Vector3.up, spinDegPerSec * dt, Space.World);
            var p = visualBasePos;
            p.y += Mathf.Sin(t * bobSpeed) * bobAmplitude;
            visualRoot.localPosition = p;
        }
    }
}
