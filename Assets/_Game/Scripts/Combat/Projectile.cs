using UnityEngine;

namespace Marchio
{
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] Transform visualRoot;
        [SerializeField] Renderer visualRenderer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock mpb;

        public Vector2 Pos { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float Radius { get; private set; }
        public float Damage { get; private set; }
        public bool FromBoss { get; private set; }
        public bool Homing { get; private set; }
        public float Life { get; private set; }

        public void OnSpawn() { }
        public void OnDespawn() { }

        public void Init(Vector2 pos, Vector2 velocity, float radius, float damage, bool fromBoss, bool homing, float life, Color color)
        {
            Pos = pos;
            Velocity = velocity;
            Radius = radius;
            Damage = damage;
            FromBoss = fromBoss;
            Homing = homing;
            Life = life;
            if (visualRoot != null) visualRoot.localScale = Vector3.one * radius * 2f;
            if (visualRenderer != null)
            {
                mpb ??= new MaterialPropertyBlock();
                mpb.SetColor(BaseColorId, color);
                visualRenderer.SetPropertyBlock(mpb);
            }
            Apply();
        }

        public void Advance(float dt, Vector2 target)
        {
            if (Homing)
            {
                var toTarget = target - Pos;
                float desired = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                float current = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
                float maxTurn = GameManager.I.Config.bossHomingTurnRate * Mathf.Rad2Deg * dt;
                float next = Mathf.MoveTowardsAngle(current, desired, maxTurn) * Mathf.Deg2Rad;
                float speed = Velocity.magnitude;
                Velocity = new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * speed;
                Life -= dt;
            }
            Pos += Velocity * dt;
            Apply();
        }

        void Apply()
        {
            transform.position = PolygonMath.ToWorld(Pos, 2f);
        }
    }
}
