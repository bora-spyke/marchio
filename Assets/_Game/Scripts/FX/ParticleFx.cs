using UnityEngine;

namespace Marchio
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticleFx : MonoBehaviour
    {
        ParticleSystem ps;
        ParticleSystem.EmitParams emit;

        void Awake()
        {
            ps = GetComponent<ParticleSystem>();
        }

        public void Burst(Vector2 pos, Color color, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float sp = 50f + Random.value * 160f;
                Emit(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, color, 2f + Random.value * 3f);
            }
        }

        public void BurstToward(Vector2 pos, Vector2 target, Color color, int n)
        {
            var d = target - pos;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
            d.Normalize();
            for (int i = 0; i < n; i++)
            {
                float sp = 80f + Random.value * 100f;
                Emit(pos, d * sp, color, 2f + Random.value * 2f);
            }
        }

        void Emit(Vector2 pos, Vector2 vel, Color color, float radius)
        {
            emit.position = PolygonMath.ToWorld(pos, 3f);
            emit.velocity = new Vector3(vel.x, 0f, vel.y);
            emit.startColor = color;
            emit.startSize = radius * 2f;
            emit.startLifetime = 1f / 2.2f;
            ps.Emit(emit, 1);
        }
    }
}
