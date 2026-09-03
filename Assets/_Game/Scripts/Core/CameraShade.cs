using UnityEngine;

namespace Marchio
{
    public sealed class CameraShade : MonoBehaviour
    {
        [SerializeField] SpriteRenderer bottom;
        [SerializeField] SpriteRenderer top;
        [SerializeField] SpriteRenderer left;
        [SerializeField] SpriteRenderer right;
        [SerializeField] float depth = 784f;
        [SerializeField] float thickness = 150f;
        [SerializeField] float margin = 1.02f;

        Camera cam;
        int lastWidth;
        int lastHeight;

        void Awake()
        {
            cam = GetComponentInParent<Camera>();
            transform.localPosition = new Vector3(0f, 0f, depth);
            transform.localRotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (cam.pixelWidth == lastWidth && cam.pixelHeight == lastHeight) return;
            lastWidth = cam.pixelWidth;
            lastHeight = cam.pixelHeight;
            Layout();
        }

        void Layout()
        {
            float halfHeight = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * cam.aspect;
            float inset = thickness * 0.5f;
            Place(bottom, new Vector3(0f, -halfHeight + inset, 0f), 0f, halfWidth * 2f * margin);
            Place(top, new Vector3(0f, halfHeight - inset, 0f), 180f, halfWidth * 2f * margin);
            Place(left, new Vector3(-halfWidth + inset, 0f, 0f), 270f, halfHeight * 2f * margin);
            Place(right, new Vector3(halfWidth - inset, 0f, 0f), 90f, halfHeight * 2f * margin);
        }

        void Place(SpriteRenderer strip, Vector3 localPos, float rollDeg, float length)
        {
            if (strip == null) return;
            strip.transform.localPosition = localPos;
            strip.transform.localRotation = Quaternion.Euler(0f, 0f, rollDeg);
            strip.drawMode = SpriteDrawMode.Sliced;
            strip.size = new Vector2(length, thickness);
        }
    }
}
