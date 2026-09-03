using UnityEngine;

namespace Marchio
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] float height = 500f;
        Camera cam;

        public Camera Cam => cam;
        public Vector2 Center { get; private set; }
        public Vector2 HalfExtents => new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        public void Configure(GameConfig cfg)
        {
            cam.orthographicSize = cfg.referenceHeightPx * 0.5f;
            cam.backgroundColor = cfg.bg;
            cam.clearFlags = CameraClearFlags.SolidColor;
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
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, height));
            return new Vector2(w.x, w.z);
        }
    }
}
