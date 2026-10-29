using System.Collections.Generic;
using UnityEngine;
using Woken.Characters;

namespace Woken.Gameplay
{
    /// <summary>Builds the playable Woken blockout from simple, collidable level pieces.</summary>
    public sealed class WokenLevelBootstrap : MonoBehaviour
    {
        private const float MoveSpeed = 3.4f;
        private static Sprite pixel;
        private Transform player;
        private string destination = "Depo";
        private List<RouteOption> routes = new List<RouteOption>();
        private int selectedRoute = -1;
        private readonly List<LineRenderer> routeLines = new List<LineRenderer>();
        private Camera sceneCamera;

        private void Awake()
        {
            sceneCamera = Camera.main != null ? Camera.main : GetComponent<Camera>();
            if (sceneCamera == null) sceneCamera = gameObject.AddComponent<Camera>();
            sceneCamera.tag = "MainCamera";
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 5f;
            sceneCamera.backgroundColor = new Color32(29, 37, 42, 255);
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            BuildFloor();
            BuildWalls();
            BuildLaboratory();
            BuildMaze();
            BuildCharacters();
            gameObject.AddComponent<WorldExplorationFog>();
        }

        private void LateUpdate()
        {
            if (player == null) return;
            Vector3 target = player.position + new Vector3(0, .2f, -10);
            sceneCamera.transform.position = Vector3.Lerp(sceneCamera.transform.position, target, 8f * Time.deltaTime);
        }

        private void BuildFloor()
        {
            RectPiece("Floor - entrance", new Vector2(-10, 0), new Vector2(8, 16), new Color32(191, 190, 178, 255), -10, false);
            RectPiece("Floor - route hall", new Vector2(0, 0), new Vector2(12, 16), new Color32(166, 174, 173, 255), -10, false);
            RectPiece("Floor - storage", new Vector2(10, 4), new Vector2(8, 8), new Color32(185, 178, 158, 255), -10, false);
            RectPiece("Floor - maintenance", new Vector2(10, -4), new Vector2(8, 8), new Color32(151, 164, 165, 255), -10, false);
            for (int x = -13; x <= 13; x++)
                for (int y = -7; y <= 7; y++)
                    if ((x + y) % 2 == 0) RectPiece("Floor tile", new Vector2(x, y), Vector2.one, new Color(1f, 1f, 1f, .035f), -9, false);
        }

        private void BuildWalls()
        {
            Color wall = new Color32(64, 76, 80, 255);
            // Outer 28 x 16 frame.
            Wall("Outer north", new Vector2(0, 8), new Vector2(28, .35f), wall);
            Wall("Outer south", new Vector2(0, -8), new Vector2(28, .35f), wall);
            Wall("Outer west", new Vector2(-14, 0), new Vector2(.35f, 16), wall);
            Wall("Outer east upper", new Vector2(14, 3.75f), new Vector2(.35f, 8.5f), wall);
            Wall("Outer east lower", new Vector2(14, -4.25f), new Vector2(.35f, 7.5f), wall);
            // Entrance into route hall.
            Wall("Entrance divider north", new Vector2(-6, 4.5f), new Vector2(.35f, 7f), wall);
            Wall("Entrance divider south", new Vector2(-6, -4.5f), new Vector2(.35f, 7f), wall);
            // Only a northern approach enters the storage wing; the southern door serves maintenance.
            Wall("Storage divider lower", new Vector2(6, 3.15f), new Vector2(.35f, 6.3f), wall);
            Wall("Storage divider middle", new Vector2(6, -1.5f), new Vector2(.35f, 3f), wall);
            Wall("Storage divider south", new Vector2(6, -6.5f), new Vector2(.35f, 3f), wall);
            Wall("Wing split west", new Vector2(10, 0), new Vector2(8, .35f), wall);
            // Storage vestibule closes direct side/southern entries. The remaining route comes down from the north.
            Wall("Storage approach elbow", new Vector2(4.05f, 4.9f), new Vector2(.34f, 2.8f), wall);
            Wall("Storage approach south", new Vector2(5.02f, 3.4f), new Vector2(1.64f, .34f), wall);
        }

        private void BuildLaboratory()
        {
            Color steel = new Color32(105, 119, 123, 255);
            Color top = new Color32(215, 218, 210, 255);
            // Full west wall shelving for folders and archive boxes.
            RectPiece("Full metal file shelving", new Vector2(-13.35f, 0), new Vector2(.75f, 14.8f), steel, 0, true);
            for (int y = -6; y <= 6; y += 2)
                RectPiece("Shelf lip", new Vector2(-12.92f, y), new Vector2(.12f, 1.5f), top, 1, false);
            // Two long perimeter work benches and a central island; clear walking lanes remain between them.
            Bench("North laboratory bench", new Vector2(-9.15f, 6.5f), new Vector2(5.2f, 1.5f), steel, top);
            Bench("South laboratory bench", new Vector2(-9.15f, -6.5f), new Vector2(5.2f, 1.5f), steel, top);
            Bench("Central island laboratory bench", new Vector2(-9.4f, 0), new Vector2(3.2f, 4.2f), steel, top);
            // A few restrained lab props.
            RectPiece("Lab instrument", new Vector2(-10.2f, 6.35f), new Vector2(.7f, .45f), new Color32(93, 154, 158, 255), 2, false);
            RectPiece("Lab instrument", new Vector2(-8.7f, -6.25f), new Vector2(.6f, .4f), new Color32(93, 154, 158, 255), 2, false);
            RectPiece("Island tray", new Vector2(-9.4f, .35f), new Vector2(1.25f, .65f), new Color32(170, 190, 181, 255), 2, false);
        }

        private void Bench(string name, Vector2 center, Vector2 size, Color baseColor, Color topColor)
        {
            RectPiece(name + " body", center + new Vector2(.12f, -.18f), size, baseColor, 0, true);
            RectPiece(name + " worktop", center + new Vector2(0, .12f), size * new Vector2(1f, .73f), topColor, 1, false);
            RectPiece(name + " front edge", center + new Vector2(0, -.43f), new Vector2(size.x, .12f), new Color32(89, 102, 106, 255), 2, false);
        }

        private void BuildMaze()
        {
            Color barrier = new Color32(83, 98, 102, 255);
            // Short wall runs create choices without forming a dense, opaque maze.
            Wall("Hall west north spur", new Vector2(-3.65f, 5.15f), new Vector2(4.7f, .35f), barrier);
            Wall("Hall west spur return", new Vector2(-1.5f, 3.55f), new Vector2(.35f, 3.2f), barrier);
            Wall("Hall center north barrier", new Vector2(1.55f, 5.8f), new Vector2(.35f, 3.8f), barrier);
            Wall("Hall center block", new Vector2(.1f, 1.35f), new Vector2(3.8f, .35f), barrier);
            Wall("Hall center block leg", new Vector2(1.85f, -.05f), new Vector2(.35f, 2.8f), barrier);
            Wall("Hall south barrier", new Vector2(-2.3f, -2.2f), new Vector2(5f, .35f), barrier);
            Wall("Hall south short spur", new Vector2(-4.65f, -4.1f), new Vector2(.35f, 3.8f), barrier);
            Wall("Hall east short spur", new Vector2(3.85f, -.55f), new Vector2(.35f, 3.5f), barrier);
            Wall("Hall east lower spur", new Vector2(3.8f, -4.75f), new Vector2(3.8f, .35f), barrier);
        }

        private void BuildCharacters()
        {
            player = CreateCharacter("Player", new Vector2(-10.7f, -3.2f), false).transform;
            sceneCamera.transform.position = player.position + new Vector3(0, .2f, -10);
            CreateCharacter("Companion Robot", new Vector2(-11.9f, -3.6f), true);
        }

        private GameObject CreateCharacter(string objectName, Vector2 position, bool robot)
        {
            var actor = new GameObject(objectName);
            actor.transform.position = position;
            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 5;
            renderer.sprite = robot ? CharacterArtFactory.GetRobot(CharacterArtFactory.Facing.Down, 0) : CharacterArtFactory.GetPlayer(CharacterArtFactory.Facing.Down, 0);
            var animator = actor.AddComponent<CharacterAnimator>();
            animator.Initialize(robot);
            if (!robot)
            {
                var body = actor.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                var collider = actor.AddComponent<CircleCollider2D>();
                collider.radius = .27f;
                actor.AddComponent<PlayerWASDController>();
            }
            return actor;
        }

        private void Wall(string name, Vector2 center, Vector2 size, Color color) => RectPiece(name, center, size, color, 0, true);

        private void RectPiece(string name, Vector2 center, Vector2 size, Color color, int sorting, bool collider)
        {
            var piece = new GameObject(name);
            piece.transform.position = center;
            piece.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPixel();
            renderer.color = color;
            renderer.sortingOrder = sorting;
            if (collider) piece.AddComponent<BoxCollider2D>();
        }

        private static Sprite GetPixel()
        {
            if (pixel != null) return pixel;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Woken blockout pixel" };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
            return pixel;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 270, 110), "WOKEN | Kat 1");
            GUI.Label(new Rect(24, 42, 245, 22), "Hedef seç; hareket için WASD kullan.");
            if (GUI.Button(new Rect(24, 69, 112, 34), "Depo")) Choose("Depo", new Vector2(8f, 5.5f));
            if (GUI.Button(new Rect(148, 69, 112, 34), "Bakım / asansör")) Choose("Bakım / asansör", new Vector2(8f, -4f));
            if (routes.Count == 0) return;
            GUI.Box(new Rect(12, 130, 270, 56 + routes.Count * 34), destination + " rotaları");
            for (int i = 0; i < routes.Count; i++)
                if (GUI.Button(new Rect(24, 158 + i * 34, 245, 28), (selectedRoute == i ? "✓ " : "") + "Rota " + (i + 1) + " — yaklaşık " + routes[i].EstimatedSeconds + " sn")) SelectRoute(i);
        }

        private void Choose(string name, Vector2 target)
        {
            destination = name;
            routes = GridRoutePlanner.FindTwoRoutes(player.position, target, MoveSpeed);
            selectedRoute = -1;
            ClearRouteLines();
            for (int i = 0; i < routes.Count; i++) DrawRoute(routes[i], i == 0 ? new Color32(239, 174, 63, 210) : new Color32(65, 184, 193, 180), i == 0 ? .14f : .09f);
        }

        private void SelectRoute(int index)
        {
            if (index < 0 || index >= routes.Count) return;
            selectedRoute = index;
            ClearRouteLines();
            DrawRoute(routes[index], new Color32(255, 194, 76, 235), .17f);
        }

        private void DrawRoute(RouteOption route, Color color, float width)
        {
            var lineObject = new GameObject("Suggested route");
            var line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = route.Points.Count;
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 3;
            for (int i = 0; i < route.Points.Count; i++) line.SetPosition(i, route.Points[i] + Vector2.up * .02f);
            routeLines.Add(line);
        }

        private void ClearRouteLines()
        {
            for (int i = 0; i < routeLines.Count; i++) if (routeLines[i] != null) Destroy(routeLines[i].gameObject);
            routeLines.Clear();
        }
    }
}
