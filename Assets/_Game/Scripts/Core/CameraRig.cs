using UnityEngine;

namespace Marchio
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        Camera cam;
        Vector3 offset;
        Quaternion startRotation;
        Vector2 halfExtents;
        bool cached;

        public Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();
        public Vector2 Center { get; private set; }
        public Vector2 HalfExtents => halfExtents;

        public void Configure(GameConfig cfg)
        {
            if (!cached)
            {
                offset = transform.position;
                startRotation = transform.rotation;
                cached = true;
            }
            Cam.backgroundColor = cfg.bg;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            transform.rotation = startRotation;
            Follow(Vector2.zero, 0f);
        }

        public void Follow(Vector2 target, float shake)
        {
            Center = target;
            var jitter = shake > 0f ? Random.insideUnitCircle * shake : Vector2.zero;
            transform.position = new Vector3(target.x + jitter.x, 0f, target.y + jitter.y) + offset;
            halfExtents = MeasureHalfExtents();
        }

        Vector2 MeasureHalfExtents()
        {
            float w = Cam.pixelWidth, h = Cam.pixelHeight;
            var half = Vector2.zero;
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(0f, h), new Vector2(w, h) })
            {
                var p = ScreenToPlane(corner) - Center;
                half.x = Mathf.Max(half.x, Mathf.Abs(p.x));
                half.y = Mathf.Max(half.y, Mathf.Abs(p.y));
            }
            return half;
        }

        public bool IsOutside(Vector2 p, float pad)
        {
            var h = HalfExtents;
            return p.x < Center.x - h.x - pad || p.x > Center.x + h.x + pad ||
                   p.y < Center.y - h.y - pad || p.y > Center.y + h.y + pad;
        }

        public Vector2 ScreenToPlane(Vector2 screen)
        {
            var ray = Cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (!Ground.Raycast(ray, out float enter)) enter = Cam.farClipPlane;
            var w = ray.GetPoint(enter);
            return new Vector2(w.x, w.z);
        }
    }
}
