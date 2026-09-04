using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public sealed class GroundTiler : MonoBehaviour
    {
        [SerializeField] Material material;

        const int MaxTilesPerAxis = 40;

        readonly List<Transform> tiles = new List<Transform>();
        int columns;
        int rows;

        void Start()
        {
            EnsureGrid();
            Snap();
        }

        void LateUpdate()
        {
            EnsureGrid();
            Snap();
        }

        void EnsureGrid()
        {
            var gm = GameManager.I;
            float tile = gm.Config.groundTilePx;
            var half = gm.Cam.HalfExtents;
            int needCols = Mathf.Min(MaxTilesPerAxis, Mathf.CeilToInt(half.x * 2f / tile) + 3);
            int needRows = Mathf.Min(MaxTilesPerAxis, Mathf.CeilToInt(half.y * 2f / tile) + 3);
            if (needCols <= columns && needRows <= rows) return;
            columns = Mathf.Max(columns, needCols);
            rows = Mathf.Max(rows, needRows);
            var mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            while (tiles.Count < columns * rows)
            {
                var go = new GameObject("Tile");
                go.transform.SetParent(transform, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(tile, tile, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                tiles.Add(go.transform);
            }
        }

        void Snap()
        {
            var gm = GameManager.I;
            if (gm == null || tiles.Count == 0) return;
            float tile = gm.Config.groundTilePx;
            var c = gm.Cam.Center;
            float originX = (Mathf.Round(c.x / tile) - columns / 2) * tile;
            float originZ = (Mathf.Round(c.y / tile) - rows / 2) * tile;
            for (int i = 0; i < tiles.Count; i++)
            {
                int cx = i % columns, cz = i / columns;
                tiles[i].position = new Vector3(originX + cx * tile, -2f, originZ + cz * tile);
            }
        }
    }
}
