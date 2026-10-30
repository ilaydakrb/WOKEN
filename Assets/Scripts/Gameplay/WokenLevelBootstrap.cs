using System.Collections.Generic;
using UnityEngine;
using Woken.Characters;

namespace Woken.Gameplay
{
    /// <summary>
    /// Builds the playable Woken blockout from simple, collidable level pieces.
    /// </summary>
    public sealed class WokenLevelBootstrap : MonoBehaviour
    {
        private const float MoveSpeed = 3.4f;

        private static Sprite pixel;

        private Transform player;
        private string destination = "Depo";

        private List<RouteOption> routes = new List<RouteOption>();
        private int selectedRoute = -1;

        private readonly List<LineRenderer> routeLines =
            new List<LineRenderer>();

        private Camera sceneCamera;

        private void Awake()
        {
            sceneCamera = Camera.main != null
                ? Camera.main
                : GetComponent<Camera>();

            if (sceneCamera == null)
            {
                sceneCamera = gameObject.AddComponent<Camera>();
            }

            sceneCamera.tag = "MainCamera";
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 5f;
            sceneCamera.backgroundColor =
                new Color32(29, 37, 42, 255);
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
            if (player == null)
            {
                return;
            }

            Vector3 target =
                player.position + new Vector3(0f, 0.2f, -10f);

            sceneCamera.transform.position = Vector3.Lerp(
                sceneCamera.transform.position,
                target,
                8f * Time.deltaTime
            );
        }

        private void BuildFloor()
        {
            RectPiece(
                "Floor - entrance",
                new Vector2(-10f, 0f),
                new Vector2(8f, 16f),
                new Color32(191, 190, 178, 255),
                -10,
                false
            );

            RectPiece(
                "Floor - route hall",
                new Vector2(0f, 0f),
                new Vector2(12f, 16f),
                new Color32(166, 174, 173, 255),
                -10,
                false
            );

            RectPiece(
                "Floor - storage",
                new Vector2(10f, 4f),
                new Vector2(8f, 8f),
                new Color32(185, 178, 158, 255),
                -10,
                false
            );

            RectPiece(
                "Floor - maintenance",
                new Vector2(10f, -4f),
                new Vector2(8f, 8f),
                new Color32(151, 164, 165, 255),
                -10,
                false
            );

            for (int x = -13; x <= 13; x++)
            {
                for (int y = -7; y <= 7; y++)
                {
                    if ((x + y) % 2 == 0)
                    {
                        RectPiece(
                            "Floor tile",
                            new Vector2(x, y),
                            Vector2.one,
                            new Color(1f, 1f, 1f, 0.035f),
                            -9,
                            false
                        );
                    }
                }
            }
        }

        private void BuildWalls()
        {
            Color wall = new Color32(64, 76, 80, 255);

            // Outer 28 x 16 frame.
            Wall(
                "Outer north",
                new Vector2(0f, 8f),
                new Vector2(28f, 0.35f),
                wall
            );

            Wall(
                "Outer south",
                new Vector2(0f, -8f),
                new Vector2(28f, 0.35f),
                wall
            );

            Wall(
                "Outer west",
                new Vector2(-14f, 0f),
                new Vector2(0.35f, 16f),
                wall
            );

            Wall(
                "Outer east upper",
                new Vector2(14f, 3.75f),
                new Vector2(0.35f, 8.5f),
                wall
            );

            Wall(
                "Outer east lower",
                new Vector2(14f, -4.25f),
                new Vector2(0.35f, 7.5f),
                wall
            );

            // Entrance into route hall.
            Wall(
                "Entrance divider north",
                new Vector2(-6f, 4.5f),
                new Vector2(0.35f, 7f),
                wall
            );

            Wall(
                "Entrance divider south",
                new Vector2(-6f, -4.5f),
                new Vector2(0.35f, 7f),
                wall
            );

            // Storage and maintenance entrances.
            Wall(
                "Storage divider lower",
                new Vector2(6f, 3.15f),
                new Vector2(0.35f, 6.3f),
                wall
            );

            Wall(
                "Storage divider middle",
                new Vector2(6f, -1.5f),
                new Vector2(0.35f, 3f),
                wall
            );

            Wall(
                "Storage divider south",
                new Vector2(6f, -6.5f),
                new Vector2(0.35f, 3f),
                wall
            );

            Wall(
                "Wing split west",
                new Vector2(10f, 0f),
                new Vector2(8f, 0.35f),
                wall
            );

            // Storage vestibule.
            Wall(
                "Storage approach elbow",
                new Vector2(4.05f, 4.9f),
                new Vector2(0.34f, 2.8f),
                wall
            );

            Wall(
                "Storage approach south",
                new Vector2(5.02f, 3.4f),
                new Vector2(1.64f, 0.34f),
                wall
            );
        }

        private void BuildLaboratory()
        {
            Color steel = new Color32(105, 119, 123, 255);
            Color top = new Color32(215, 218, 210, 255);

            // Full west wall shelving.
            RectPiece(
                "Full metal file shelving",
                new Vector2(-13.35f, 0f),
                new Vector2(0.75f, 14.8f),
                steel,
                0,
                true
            );

            for (int y = -6; y <= 6; y += 2)
            {
                RectPiece(
                    "Shelf lip",
                    new Vector2(-12.92f, y),
                    new Vector2(0.12f, 1.5f),
                    top,
                    1,
                    false
                );
            }

            // Laboratory benches.
            Bench(
                "North laboratory bench",
                new Vector2(-9.15f, 6.5f),
                new Vector2(5.2f, 1.5f),
                steel,
                top
            );

            Bench(
                "South laboratory bench",
                new Vector2(-9.15f, -6.5f),
                new Vector2(5.2f, 1.5f),
                steel,
                top
            );

            Bench(
                "Central island laboratory bench",
                new Vector2(-9.4f, 0f),
                new Vector2(3.2f, 4.2f),
                steel,
                top
            );

            // Laboratory props.
            RectPiece(
                "Lab instrument",
                new Vector2(-10.2f, 6.35f),
                new Vector2(0.7f, 0.45f),
                new Color32(93, 154, 158, 255),
                2,
                false
            );

            RectPiece(
                "Lab instrument",
                new Vector2(-8.7f, -6.25f),
                new Vector2(0.6f, 0.4f),
                new Color32(93, 154, 158, 255),
                2,
                false
            );

            RectPiece(
                "Island tray",
                new Vector2(-9.4f, 0.35f),
                new Vector2(1.25f, 0.65f),
                new Color32(170, 190, 181, 255),
                2,
                false
            );
        }

        private void Bench(
            string name,
            Vector2 center,
            Vector2 size,
            Color baseColor,
            Color topColor)
        {
            RectPiece(
                name + " body",
                center + new Vector2(0.12f, -0.18f),
                size,
                baseColor,
                0,
                true
            );

            RectPiece(
                name + " worktop",
                center + new Vector2(0f, 0.12f),
                size * new Vector2(1f, 0.73f),
                topColor,
                1,
                false
            );

            RectPiece(
                name + " front edge",
                center + new Vector2(0f, -0.43f),
                new Vector2(size.x, 0.12f),
                new Color32(89, 102, 106, 255),
                2,
                false
            );
        }

        private void BuildMaze()
        {
            Color barrier = new Color32(83, 98, 102, 255);

            Wall(
                "Hall west north spur",
                new Vector2(-3.65f, 5.15f),
                new Vector2(4.7f, 0.35f),
                barrier
            );

            Wall(
                "Hall west spur return",
                new Vector2(-1.5f, 3.55f),
                new Vector2(0.35f, 3.2f),
                barrier
            );

            Wall(
                "Hall center north barrier",
                new Vector2(1.55f, 5.8f),
                new Vector2(0.35f, 3.8f),
                barrier
            );

            Wall(
                "Hall center block",
                new Vector2(0.1f, 1.35f),
                new Vector2(3.8f, 0.35f),
                barrier
            );

            Wall(
                "Hall center block leg",
                new Vector2(1.85f, -0.05f),
                new Vector2(0.35f, 2.8f),
                barrier
            );

            Wall(
                "Hall south barrier",
                new Vector2(-2.3f, -2.2f),
                new Vector2(5f, 0.35f),
                barrier
            );

            Wall(
                "Hall south short spur",
                new Vector2(-4.65f, -4.1f),
                new Vector2(0.35f, 3.8f),
                barrier
            );

            Wall(
                "Hall east short spur",
                new Vector2(3.85f, -0.55f),
                new Vector2(0.35f, 3.5f),
                barrier
            );

            Wall(
                "Hall east lower spur",
                new Vector2(3.8f, -4.75f),
                new Vector2(3.8f, 0.35f),
                barrier
            );
        }

        private void BuildCharacters()
        {
            // Oyuncuyu oluştur.
            player = CreateCharacter(
                "Player",
                new Vector2(-10.7f, -3.2f),
                false
            ).transform;

            sceneCamera.transform.position =
                player.position + new Vector3(0f, 0.2f, -10f);

            // Robot oyuncunun solunda, aynı yatay hizada başlar.
            GameObject robot = CreateCharacter(
                "Companion Robot",
                new Vector2(-12.7f, -3.2f),
                true
            );

            // Robotun başlangıçta aşağıya doğru baköasını sağla.
            CharacterAnimator robotAnimator =
                robot.GetComponent<CharacterAnimator>();

            if (robotAnimator != null)
            {
                robotAnimator.SetMotion(Vector2.down);
            }

            // Mevcut takip sistemini koru.
            RobotMovementController robotMovement =
                robot.AddComponent<RobotMovementController>();

            robotMovement.SetPlayer(player);
        }

        private GameObject CreateCharacter(
            string objectName,
            Vector2 position,
            bool robot)
        {
            GameObject actor = new GameObject(objectName);
            actor.transform.position = position;

            SpriteRenderer renderer =
                actor.AddComponent<SpriteRenderer>();

            renderer.sortingOrder = 5;

            renderer.sprite = robot
                ? CharacterArtFactory.GetRobot(
                    CharacterArtFactory.Facing.Down,
                    0
                )
                : CharacterArtFactory.GetPlayer(
                    CharacterArtFactory.Facing.Down,
                    0
                );

            CharacterAnimator animator =
                actor.AddComponent<CharacterAnimator>();

            animator.Initialize(robot);

            Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider =
                actor.AddComponent<CircleCollider2D>();

            collider.radius = 0.27f;

            if (!robot)
            {
                actor.AddComponent<PlayerWASDController>();
            }

            return actor;
        }

        private void Wall(
            string name,
            Vector2 center,
            Vector2 size,
            Color color)
        {
            RectPiece(name, center, size, color, 0, true);
        }

        private void RectPiece(
            string name,
            Vector2 center,
            Vector2 size,
            Color color,
            int sorting,
            bool collider)
        {
            GameObject piece = new GameObject(name);
            piece.transform.position = center;
            piece.transform.localScale =
                new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer =
                piece.AddComponent<SpriteRenderer>();

            renderer.sprite = GetPixel();
            renderer.color = color;
            renderer.sortingOrder = sorting;

            if (collider)
            {
                piece.AddComponent<BoxCollider2D>();
            }
        }

        private static Sprite GetPixel()
        {
            if (pixel != null)
            {
                return pixel;
            }

            Texture2D texture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false
            )
            {
                filterMode = FilterMode.Point,
                name = "Woken blockout pixel"
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            pixel = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f
            );

            return pixel;
        }

        private void OnGUI()
        {
            GUI.Box(
                new Rect(12, 12, 270, 110),
                "WOKEN | Kat 1"
            );

            GUI.Label(
                new Rect(24, 42, 245, 22),
                "Hedef seç; hareket için WASD kullan."
            );

            if (GUI.Button(
                new Rect(24, 69, 112, 34),
                "Depo"))
            {
                Choose("Depo", new Vector2(8f, 5.5f));
            }

            if (GUI.Button(
                new Rect(148, 69, 112, 34),
                "Bakım / asansör"))
            {
                Choose("Bakım / asansör", new Vector2(8f, -4f));
            }

            if (routes.Count == 0)
            {
                return;
            }

            GUI.Box(
                new Rect(12, 130, 270, 56 + routes.Count * 34),
                destination + " rotaları"
            );

            for (int i = 0; i < routes.Count; i++)
            {
                string buttonText =
                    (selectedRoute == i ? "✓ " : "") +
                    "Rota " + (i + 1) +
                    " — yaklaşık " +
                    routes[i].EstimatedSeconds +
                    " sn";

                if (GUI.Button(
                    new Rect(24, 158 + i * 34, 245, 28),
                    buttonText))
                {
                    SelectRoute(i);
                }
            }
        }

        private void Choose(string name, Vector2 target)
        {
            destination = name;

            routes = GridRoutePlanner.FindTwoRoutes(
                player.position,
                target,
                MoveSpeed
            );

            selectedRoute = -1;
            ClearRouteLines();

            for (int i = 0; i < routes.Count; i++)
            {
                Color color = i == 0
                    ? new Color32(239, 174, 63, 210)
                    : new Color32(65, 184, 193, 180);

                float width = i == 0 ? 0.14f : 0.09f;

                DrawRoute(routes[i], color, width);
            }
        }

        private void SelectRoute(int index)
        {
            if (index < 0 || index >= routes.Count)
            {
                return;
            }

            selectedRoute = index;
            ClearRouteLines();

            DrawRoute(
                routes[index],
                new Color32(255, 194, 76, 235),
                0.17f
            );
        }

        private void DrawRoute(
            RouteOption route,
            Color color,
            float width)
        {
            GameObject lineObject =
                new GameObject("Suggested route");

            LineRenderer line =
                lineObject.AddComponent<LineRenderer>();

            line.positionCount = route.Points.Count;
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;

            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                line.material = new Material(shader);
            }

            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 3;

            for (int i = 0; i < route.Points.Count; i++)
            {
                line.SetPosition(
                    i,
                    route.Points[i] + Vector2.up * 0.02f
                );
            }

            routeLines.Add(line);
        }

        private void ClearRouteLines()
        {
            for (int i = 0; i < routeLines.Count; i++)
            {
                if (routeLines[i] != null)
                {
                    Destroy(routeLines[i].gameObject);
                }
            }

            routeLines.Clear();
        }
    }
}