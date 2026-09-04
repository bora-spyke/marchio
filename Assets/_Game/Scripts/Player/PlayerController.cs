using UnityEngine;

namespace Marchio
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] Transform visualRoot;
        [SerializeField] Renderer visualRenderer;
        [SerializeField] Transform introCameraPose;

        public Transform IntroCameraPose => introCameraPose;
        [SerializeField] ParticleSystem hitParticle;
        [SerializeField] ParticleSystem explosionParticle;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock mpb;

        void Awake()
        {
            if (hitParticle == null)
                foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
                    if (ps.name.Contains("Hit")) { hitParticle = ps; break; }
            if (explosionParticle == null)
                foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
                    if (ps.name.Contains("Explosion")) { explosionParticle = ps; break; }
        }

        public Vector2 Pos { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float Hp { get; private set; }
        public float Invuln { get; private set; }
        public float LastMoveAngle { get; private set; }
        float visualYaw;

        GameConfig Cfg => GameManager.I.Config;

        public void ResetState()
        {
            Pos = Vector2.zero;
            Velocity = Vector2.zero;
            Hp = GameManager.I.PlayerMaxHp;
            Invuln = 0f;
            LastMoveAngle = 0f;
            visualYaw = 0f;
            ApplyTransform(float.PositiveInfinity);
        }

        public void SetHp(float hp) => Hp = Mathf.Clamp(hp, 0f, GameManager.I.PlayerMaxHp);

        public void ClampHp() => Hp = Mathf.Min(Hp, GameManager.I.PlayerMaxHp);

        public void Tick(float dt, in InputFrame inp)
        {
            var cfg = Cfg;
            if (Invuln > 0f) Invuln -= dt * 1000f;
            var target = inp.Move * cfg.playerSpeed;
            bool idle = inp.Move.x == 0f && inp.Move.y == 0f;
            float accel = (idle ? cfg.playerDecel : cfg.playerAccel) * dt;
            Velocity = new Vector2(
                Mathf.MoveTowards(Velocity.x, target.x, accel),
                Mathf.MoveTowards(Velocity.y, target.y, accel));
            Pos += Velocity * dt;
            if (Velocity.sqrMagnitude > 1f) LastMoveAngle = Mathf.Atan2(Velocity.y, Velocity.x);
            ApplyTransform(dt);
        }

        public void TakeDamage(float dmg)
        {
            var gm = GameManager.I;
            var cfg = Cfg;
            Hp -= dmg;
            Invuln = cfg.playerInvulnMs;
            gm.ResetCombo();
            gm.Run.Streak = 0;
            gm.Trail.Cancel(LoopCancelReason.Hit);
            if (hitParticle != null) hitParticle.Play(true);
            gm.Fx.Burst(Pos, cfg.player, 10);
            gm.AddJuice(cfg.hitstopBaseMs, cfg.shakeBase * 1.5f);
            if (Hp <= 0f)
            {
                if (explosionParticle != null) explosionParticle.Play(true);
                gm.Fail();
            }
        }

        public void Heal(float amount)
        {
            Hp = Mathf.Min(GameManager.I.PlayerMaxHp, Hp + amount);
        }

        void ApplyTransform(float dt)
        {
            transform.position = PolygonMath.ToWorld(Pos);
            if (visualRoot != null)
            {
                float targetYaw = -LastMoveAngle * Mathf.Rad2Deg;
                visualYaw = Mathf.MoveTowardsAngle(visualYaw, targetYaw, Cfg.carTurnDegPerS * dt);
                visualRoot.rotation = Quaternion.Euler(0f, visualYaw, 0f);
            }
            if (visualRenderer != null)
            {
                bool blink = Invuln > 0f && Mathf.FloorToInt(Invuln / 60f) % 2 == 0;
                if (blink)
                {
                    mpb ??= new MaterialPropertyBlock();
                    mpb.SetColor(BaseColorId, Color.white);
                    visualRenderer.SetPropertyBlock(mpb);
                }
                else visualRenderer.SetPropertyBlock(null);
            }
        }
    }
}
