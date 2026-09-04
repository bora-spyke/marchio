using UnityEngine;

namespace Marchio
{
    public sealed class SoulStone : MonoBehaviour, IPoolable
    {
        [SerializeField] float hoverHeight = 10f;
        [SerializeField] float bobAmplitude = 4f;
        [SerializeField] float bobSpeed = 3f;
        [SerializeField] float wobbleDeg = 10f;
        [SerializeField] float spawnPopS = 0.35f;

        float age;
        float collectT = -1f;
        Vector2 collectFrom;
        Vector3 baseScale = Vector3.one;

        public Vector2 Pos { get; private set; }
        public bool Collecting => collectT >= 0f;

        public void OnSpawn() { }
        public void OnDespawn() { }

        public void Init(Vector2 pos)
        {
            Pos = pos;
            age = 0f;
            collectT = -1f;
            var sr = GetComponentInChildren<SpriteRenderer>();
            float spriteSize = sr != null && sr.sprite != null ? Mathf.Max(0.01f, sr.sprite.bounds.size.y) : 1f;
            baseScale = Vector3.one * (GameManager.I.Config.soulstoneSizePx / spriteSize);
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Play(true);
            Apply(0f, 0f);
        }

        public void BeginCollect()
        {
            if (Collecting) return;
            collectT = 0f;
            collectFrom = Pos;
        }

        public bool Tick(float dt, Vector2 playerPos)
        {
            age += dt;
            var cfg = GameManager.I.Config;
            if (!Collecting)
            {
                float pop = spawnPopS <= 0f ? 1f : Mathf.Clamp01(age / spawnPopS);
                float overshoot = 1f + 0.35f * Mathf.Sin(pop * Mathf.PI) * (1f - pop);
                Apply(pop * overshoot, 0f);
                return false;
            }
            collectT += dt;
            float u = Mathf.Clamp01(collectT / cfg.soulstoneCollectS);
            float ease = u * u * u;
            Pos = Vector2.Lerp(collectFrom, playerPos, ease);
            float arc = Mathf.Sin(u * Mathf.PI) * cfg.soulstoneCollectArcPx;
            Apply(1f + 0.3f * Mathf.Sin(u * Mathf.PI) - 0.9f * u * u, arc);
            return u >= 1f;
        }

        void Apply(float scaleMult, float extraHeight)
        {
            float bob = Mathf.Sin(age * bobSpeed) * bobAmplitude;
            transform.position = PolygonMath.ToWorld(Pos, hoverHeight + bob + extraHeight);
            transform.localScale = baseScale * Mathf.Max(0f, scaleMult);
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(age * bobSpeed * 0.7f) * wobbleDeg);
        }
    }
}
