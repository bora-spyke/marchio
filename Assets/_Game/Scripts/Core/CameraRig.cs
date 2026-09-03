using UnityEngine;

namespace Marchio
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] float height = 500f;
        Camera cam;

        public Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();
        public Vector2 Center { get; private set; }
        public Vector2 HalfExtents => new Vector2(Cam.orthographicSize * Cam.aspect, Cam.orthographicSize);

        public void Configure(GameConfig cfg)
        {
            Cam.orthographic = true;
            Cam.orthographicSize = cfg.referenceHeightPx * 0.5f;
            Cam.backgroundColor = cfg.bg;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        public void Follow(Vector2 target, float shake)
        {
            Center = target;
            var offset = shake > 0f ? Random.insideUnitCircle * shake : Vector2.zero;
            transform.position = new Vector3(target.x + offset.x, height, target.y + offset.y);
        }

        public bool IsOutside(Vector2 p, float pad)
        {
            var h = HalfExtents;
            return p.x < Center.x - h.x - pad || p.x > Center.x + h.x + pad ||
                   p.y < Center.y - h.y - pad || p.y > Center.y + h.y + pad;
        }

        public Vector2 ScreenToPlane(Vector2 screen)
        {
            var w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, height));
            return new Vector2(w.x, w.z);
        }
    }
}
