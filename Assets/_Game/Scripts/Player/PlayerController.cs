using UnityEngine;

namespace Marchio
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] Transform visualRoot;
        [SerializeField] Renderer visualRenderer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock mpb;

        public Vector2 Pos { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float Hp { get; private set; }
        public float Invuln { get; private set; }
        public float LastMoveAngle { get; private set; }

        GameConfig Cfg => GameManager.I.Config;

        public void ResetState()
        {
            Pos = Vector2.zero;
            Velocity = Vector2.zero;
            Hp = Cfg.playerMaxHP;
            Invuln = 0f;
            LastMoveAngle = 0f;
            ApplyTransform();
        }

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
            Pos = GameManager.I.ClampToBossArena(Pos + Velocity * dt);
            if (Velocity.sqrMagnitude > 1f) LastMoveAngle = Mathf.Atan2(Velocity.y, Velocity.x);
            ApplyTransform();
        }

        public void TakeDamage(float dmg)
        {
            var gm = GameManager.I;
            var cfg = Cfg;
            Hp -= dmg;
            Invuln = cfg.playerInvulnMs;
            gm.ResetCombo();
            gm.Trail.Cancel(LoopCancelReason.Hit);
            gm.Fx.Burst(Pos, cfg.player, 10);
            gm.AddJuice(cfg.hitstopBaseMs, cfg.shakeBase * 1.5f);
            if (Hp <= 0f) gm.EndRun();
        }

        public void Heal(float amount)
        {
            Hp = Mathf.Min(Cfg.playerMaxHP, Hp + amount);
        }

        void ApplyTransform()
        {
            transform.position = PolygonMath.ToWorld(Pos);
            if (visualRoot != null)
                visualRoot.rotation = Quaternion.Euler(0f, -LastMoveAngle * Mathf.Rad2Deg, 0f);
            if (visualRenderer != null)
            {
                mpb ??= new MaterialPropertyBlock();
                bool blink = Invuln > 0f && Mathf.FloorToInt(Invuln / 60f) % 2 == 0;
                mpb.SetColor(BaseColorId, blink ? Color.white : Cfg.player);
                visualRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
