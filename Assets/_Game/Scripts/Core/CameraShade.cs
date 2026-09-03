using UnityEngine;

namespace Marchio
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CameraShade : MonoBehaviour
    {
        [SerializeField] float widthMargin = 1.2f;

        SpriteRenderer sprite;
        Camera cam;
        Vector2 authoredSize;

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            cam = GetComponentInParent<Camera>();
            authoredSize = sprite.size;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float depth = cam.transform.InverseTransformPoint(transform.position).z;
            float halfHeight = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = halfHeight * 2f * cam.aspect * widthMargin;
            sprite.size = new Vector2(Mathf.Max(authoredSize.x, width), authoredSize.y);
        }
    }
}
