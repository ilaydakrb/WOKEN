using UnityEngine;
using UnityEngine.Tilemaps;

namespace Woken.Gameplay
{
    /// <summary>Hides unexplored rooms in the play view and permanently reveals nearby cells.</summary>
    public sealed class WorldExplorationFog : MonoBehaviour
    {
        private const float CellSize = .5f;
        private const float MinX = -6f;
        private const float MinY = -8f;
        private const int Columns = 24;
        private const int Rows = 32;
        private const float RevealRadius = 2.4f;
        private Tilemap fog;
        private Transform player;
        private bool[,] explored;
        private Vector2Int lastCell = new Vector2Int(-999, -999);

        private void Start()
        {
            var gridObject = new GameObject("Exploration fog grid");
            var grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(CellSize, CellSize, 1f);
            var mapObject = new GameObject("Unexplored areas");
            mapObject.transform.SetParent(gridObject.transform, false);
            fog = mapObject.AddComponent<Tilemap>();
            var renderer = mapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = 2;
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "Unexplored shadow";
            tile.sprite = MakeSprite();
            tile.color = new Color32(23, 31, 37, 232);
            explored = new bool[Columns, Rows];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++) fog.SetTile(new Vector3Int(x, y, 0), tile);
            // Fog is limited to the 12 x 16 central maze hall; side rooms stay bright.
            gridObject.transform.position = new Vector3(MinX, MinY, -2f);
            var playerObject = GameObject.Find("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        private void Update()
        {
            if (player == null || fog == null) return;
            Vector2Int center = CellAt(player.position);
            if (center == lastCell) return;
            lastCell = center;
            int range = Mathf.CeilToInt(RevealRadius / CellSize);
            for (int x = Mathf.Max(0, center.x - range); x <= Mathf.Min(Columns - 1, center.x + range); x++)
                for (int y = Mathf.Max(0, center.y - range); y <= Mathf.Min(Rows - 1, center.y + range); y++)
                {
                    if (explored[x, y]) continue;
                    Vector2 point = new Vector2(MinX + (x + .5f) * CellSize, MinY + (y + .5f) * CellSize);
                    if ((point - (Vector2)player.position).sqrMagnitude > RevealRadius * RevealRadius) continue;
                    Vector2 direction = point - (Vector2)player.position;
                    Vector2 origin = (Vector2)player.position + direction.normalized * .3f;
                    if (Physics2D.Linecast(origin, point).collider != null) continue;
                    explored[x, y] = true;
                    fog.SetTile(new Vector3Int(x, y, 0), null);
                }
        }

        private static Vector2Int CellAt(Vector2 point) => new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt((point.x - MinX) / CellSize), 0, Columns - 1),
            Mathf.Clamp(Mathf.FloorToInt((point.y - MinY) / CellSize), 0, Rows - 1));

        private static Sprite MakeSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Fog tile" };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 2f);
        }
    }
}
