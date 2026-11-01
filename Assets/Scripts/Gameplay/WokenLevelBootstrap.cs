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
            BuildStorageRoom();
            BuildMaintenanceRoom();
            BuildSecondFloorStage();
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

        private void BuildSecondFloorStage()
        {
            // Same footprint and coordinates as floor one; kept inactive until floor switching is implemented.
            var secondFloor = new GameObject("Second Floor - staged (inactive)");
            secondFloor.SetActive(false);
            Transform root = secondFloor.transform;

            RectPiece("Floor 2 - entrance", new Vector2(-10, 0), new Vector2(8, 16), new Color32(191, 190, 178, 255), -10, false, root);
            RectPiece("Floor 2 - route hall", new Vector2(0, 0), new Vector2(12, 16), new Color32(166, 174, 173, 255), -10, false, root);
            RectPiece("Floor 2 - storage", new Vector2(10, 4), new Vector2(8, 8), new Color32(185, 178, 158, 255), -10, false, root);
            RectPiece("Floor 2 - maintenance", new Vector2(10, -4), new Vector2(8, 8), new Color32(151, 164, 165, 255), -10, false, root);
            for (int x = -13; x <= 13; x++)
                for (int y = -7; y <= 7; y++)
                    if ((x + y) % 2 == 0)
                        RectPiece("Floor 2 tile", new Vector2(x, y), Vector2.one, new Color(1f, 1f, 1f, .035f), -9, false, root);

            // Elevator copy matches floor one's position, size, style, and closed-door colliders.
            Color steel = new Color32(119, 132, 135, 255);
            Color lightSteel = new Color32(165, 175, 175, 255);
            Color dark = new Color32(49, 60, 64, 255);
            RectPiece("Floor 2 - elevator wall mounted faceplate", new Vector2(13.82f, -4f), new Vector2(.25f, 2f), steel, 1, true, root);
            RectPiece("Floor 2 - elevator jamb north", new Vector2(13.78f, -2.95f), new Vector2(.42f, .12f), lightSteel, 3, true, root);
            RectPiece("Floor 2 - elevator jamb south", new Vector2(13.78f, -5.05f), new Vector2(.42f, .12f), lightSteel, 3, true, root);
            RectPiece("Floor 2 - elevator door leaf north", new Vector2(13.86f, -3.5f), new Vector2(.3f, .94f), new Color32(113, 126, 130, 255), 4, true, root);
            RectPiece("Floor 2 - elevator door leaf south", new Vector2(13.86f, -4.5f), new Vector2(.3f, .94f), new Color32(128, 140, 143, 255), 4, true, root);
            RectPiece("Floor 2 - elevator door inset north", new Vector2(13.84f, -3.5f), new Vector2(.045f, .78f), new Color32(82, 94, 98, 255), 5, false, root);
            RectPiece("Floor 2 - elevator door inset south", new Vector2(13.84f, -4.5f), new Vector2(.045f, .78f), new Color32(94, 106, 109, 255), 5, false, root);
            RectPiece("Floor 2 - elevator center seam", new Vector2(13.86f, -4f), new Vector2(.28f, .035f), new Color32(38, 47, 51, 255), 6, false, root);
            RectPiece("Floor 2 - elevator top indicator housing", new Vector2(13.78f, -2.78f), new Vector2(.3f, .18f), dark, 3, true, root);
            RectPiece("Floor 2 - elevator top indicator", new Vector2(13.77f, -2.78f), new Vector2(.12f, .07f), new Color32(99, 190, 171, 255), 4, false, root);
            RectPiece("Floor 2 - elevator warning strip north", new Vector2(13.68f, -3.5f), new Vector2(.045f, .72f), new Color32(202, 177, 106, 255), 4, false, root);
            RectPiece("Floor 2 - elevator warning strip south", new Vector2(13.68f, -4.5f), new Vector2(.045f, .72f), new Color32(202, 177, 106, 255), 4, false, root);
            RectPiece("Floor 2 - elevator threshold", new Vector2(13.52f, -4f), new Vector2(.1f, 1.9f), lightSteel, 2, false, root);
        }

        private void BuildWalls()
        {
            Color wall = new Color32(64, 76, 80, 255);
            // Outer 28 x 16 frame.
            Wall("Outer north", new Vector2(0, 8), new Vector2(28, .35f), wall);
            Wall("Outer south", new Vector2(0, -8), new Vector2(28, .35f), wall);
            Wall("Outer west", new Vector2(-14, 0), new Vector2(.35f, 16), wall);
            Wall("Outer east upper", new Vector2(14, 3.75f), new Vector2(.35f, 8.5f), wall);
            // Leave an elevator doorway on the east wall.
            Wall("Outer east lower south", new Vector2(14, -6.5f), new Vector2(.35f, 3f), wall);
            Wall("Outer east lower north", new Vector2(14, -1.75f), new Vector2(.35f, 2.5f), wall);
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

        private void BuildStorageRoom()
        {
            Color shelf = new Color32(112, 127, 130, 255);
            Color shelfEdge = new Color32(191, 199, 194, 255);
            Color bench = new Color32(139, 151, 148, 255);
            Color benchTop = new Color32(209, 211, 199, 255);

            // Wall storage stays against the edges, leaving a broad route from the north-west door.
            RectPiece("Depot north shelving", new Vector2(10.7f, 7.55f), new Vector2(4.8f, .55f), shelf, 0, true);
            RectPiece("Depot north shelf lip", new Vector2(10.7f, 7.22f), new Vector2(4.8f, .1f), shelfEdge, 1, false);
            RectPiece("Depot east shelving", new Vector2(13.55f, 4.15f), new Vector2(.55f, 4.8f), shelf, 0, true);
            RectPiece("Depot east shelf lip", new Vector2(13.22f, 4.15f), new Vector2(.1f, 4.8f), shelfEdge, 1, false);
            RectPiece("Depot south shelving", new Vector2(10.65f, .48f), new Vector2(5.1f, .55f), shelf, 0, true);
            RectPiece("Depot south shelf lip", new Vector2(10.65f, .81f), new Vector2(5.1f, .1f), shelfEdge, 1, false);

            Bench("Depot material counter", new Vector2(12.05f, 6.25f), new Vector2(1.8f, .95f), bench, benchTop);
            Bench("Depot check counter", new Vector2(12.05f, 1.75f), new Vector2(1.8f, .9f), bench, benchTop);
            RectPiece("Depot closed cabinet", new Vector2(7.25f, 1.55f), new Vector2(1.05f, 1.8f), new Color32(91, 108, 111, 255), 0, true);
            RectPiece("Depot cabinet door", new Vector2(7.25f, 1.55f), new Vector2(.78f, 1.5f), new Color32(143, 157, 157, 255), 1, false);

            CreateBatteryCrate(new Vector2(10.1f, 4.15f), new Vector2(1.25f, .95f));
            CreateStorageBox("Depot parts box A", new Vector2(8.45f, 1.35f));
            CreateStorageBox("Depot parts box B", new Vector2(9.25f, 1.35f));
            CreateStorageBox("Depot parts box C", new Vector2(10.05f, 1.35f));
        }

        private void BuildMaintenanceRoom()
        {
            Color steel = new Color32(105, 119, 123, 255);
            Color lightSteel = new Color32(165, 175, 175, 255);
            Color dark = new Color32(49, 60, 64, 255);

            // Compact service bench tucked into the south-west corner.
            RectPiece("Maintenance workbench body", new Vector2(8.17f, -7.38f), new Vector2(2.55f, .9f), steel, 0, true);
            RectPiece("Maintenance workbench top", new Vector2(8.05f, -7.08f), new Vector2(2.55f, .66f), new Color32(205, 210, 202, 255), 1, false);
            RectPiece("Maintenance workbench front edge", new Vector2(8.05f, -7.53f), new Vector2(2.55f, .12f), new Color32(89, 102, 106, 255), 2, false);
            for (int x = 0; x < 3; x++)
            {
                float drawerX = 7.18f + x * .82f;
                RectPiece("Workbench drawer face", new Vector2(drawerX, -7.48f), new Vector2(.62f, .2f), new Color32(124, 138, 140, 255), 3, false);
                RectPiece("Workbench drawer pull", new Vector2(drawerX, -7.48f), new Vector2(.16f, .035f), lightSteel, 4, false);
            }
            RectPiece("Workbench left leg", new Vector2(6.98f, -7.61f), new Vector2(.12f, .34f), dark, 1, false);
            RectPiece("Workbench right leg", new Vector2(9.22f, -7.61f), new Vector2(.12f, .34f), dark, 1, false);
            RectPiece("Workbench parts tray", new Vector2(7.55f, -7.02f), new Vector2(.42f, .22f), new Color32(132, 148, 149, 255), 2, false);
            RectPiece("Workbench tool", new Vector2(8.35f, -7.02f), new Vector2(.34f, .07f), dark, 2, false);
            RectPiece("Workbench tool grip", new Vector2(8.35f, -7.02f), new Vector2(.08f, .2f), lightSteel, 3, false);
            RectPiece("Maintenance tool board", new Vector2(7.35f, -1.05f), new Vector2(1.25f, .6f), steel, 0, true);
            RectPiece("Tool board wrench", new Vector2(7.12f, -1.05f), new Vector2(.08f, .38f), lightSteel, 2, false);
            RectPiece("Tool board driver", new Vector2(7.48f, -1.05f), new Vector2(.08f, .42f), new Color32(132, 148, 149, 255), 2, false);
            RectPiece("Tool board shelf", new Vector2(7.35f, -1.36f), new Vector2(1.25f, .1f), dark, 2, false);
            RectPiece("Tool board fastener left", new Vector2(6.84f, -1.05f), new Vector2(.07f, .07f), lightSteel, 3, false);
            RectPiece("Tool board fastener right", new Vector2(7.86f, -1.05f), new Vector2(.07f, .07f), lightSteel, 3, false);

            // Surface-mounted elevator assembly sits on the east wall; its two leaves remain separate for future animation.
            RectPiece("Elevator wall mounted faceplate", new Vector2(13.82f, -4f), new Vector2(.25f, 2f), new Color32(119, 132, 135, 255), 1, true);
            RectPiece("Elevator jamb north", new Vector2(13.78f, -2.95f), new Vector2(.42f, .12f), lightSteel, 3, true);
            RectPiece("Elevator jamb south", new Vector2(13.78f, -5.05f), new Vector2(.42f, .12f), lightSteel, 3, true);
            RectPiece("Elevator door leaf north", new Vector2(13.86f, -3.5f), new Vector2(.3f, .94f), new Color32(113, 126, 130, 255), 4, true);
            RectPiece("Elevator door leaf south", new Vector2(13.86f, -4.5f), new Vector2(.3f, .94f), new Color32(128, 140, 143, 255), 4, true);
            RectPiece("Elevator door inset north", new Vector2(13.84f, -3.5f), new Vector2(.045f, .78f), new Color32(82, 94, 98, 255), 5, false);
            RectPiece("Elevator door inset south", new Vector2(13.84f, -4.5f), new Vector2(.045f, .78f), new Color32(94, 106, 109, 255), 5, false);
            RectPiece("Elevator center seam", new Vector2(13.86f, -4f), new Vector2(.28f, .035f), new Color32(38, 47, 51, 255), 6, false);
            RectPiece("Elevator top indicator housing", new Vector2(13.78f, -2.78f), new Vector2(.3f, .18f), dark, 3, true);
            RectPiece("Elevator top indicator", new Vector2(13.77f, -2.78f), new Vector2(.12f, .07f), new Color32(99, 190, 171, 255), 4, false);
            RectPiece("Elevator door warning strip north", new Vector2(13.68f, -3.5f), new Vector2(.045f, .72f), new Color32(202, 177, 106, 255), 4, false);
            RectPiece("Elevator door warning strip south", new Vector2(13.68f, -4.5f), new Vector2(.045f, .72f), new Color32(202, 177, 106, 255), 4, false);

            // Battery bay is mounted against the east wall directly below the elevator.
            RectPiece("Battery bay backing", new Vector2(13.48f, -5.72f), new Vector2(.42f, 1.16f), dark, 0, true);
            RectPiece("Battery bay upper rail", new Vector2(13.12f, -5.13f), new Vector2(1.15f, .08f), lightSteel, 2, true);
            RectPiece("Battery bay lower rail", new Vector2(13.12f, -6.31f), new Vector2(1.15f, .08f), lightSteel, 2, true);
            RectPiece("Battery bay outer rail", new Vector2(12.56f, -5.72f), new Vector2(.08f, .9f), steel, 2, true);
            RectPiece("Battery bay inner rail", new Vector2(13.68f, -5.72f), new Vector2(.08f, .9f), steel, 2, true);
            RectPiece("Battery bay contacts", new Vector2(13.72f, -5.72f), new Vector2(.12f, .42f), lightSteel, 3, true);
            RectPiece("Battery bay contact slot", new Vector2(13.66f, -5.72f), new Vector2(.045f, .2f), dark, 4, false);
            RectPiece("Battery bay status light", new Vector2(12.67f, -5.72f), new Vector2(.09f, .09f), new Color32(105, 190, 151, 255), 4, false);
            RectPiece("Battery bay caution stripe", new Vector2(13.12f, -6.43f), new Vector2(.82f, .045f), new Color32(202, 177, 106, 255), 3, false);

            // Subtle wall-mounted service details keep the room readable and uncluttered.
            RectPiece("Maintenance north conduit", new Vector2(10.2f, -.32f), new Vector2(6.8f, .12f), steel, 1, false);
            RectPiece("Maintenance east conduit upper", new Vector2(13.72f, -1.62f), new Vector2(.12f, 2.5f), steel, 1, false);
            RectPiece("Maintenance east conduit lower", new Vector2(13.72f, -6.95f), new Vector2(.12f, 1.75f), steel, 1, false);
            RectPiece("Elevator service panel", new Vector2(12.72f, -2.2f), new Vector2(.72f, .9f), steel, 1, true);
            RectPiece("Elevator service panel screen", new Vector2(12.72f, -2.12f), new Vector2(.42f, .16f), new Color32(96, 155, 151, 255), 2, false);
            RectPiece("Service panel lower switch", new Vector2(12.72f, -2.42f), new Vector2(.18f, .1f), lightSteel, 3, false);
            RectPiece("Service panel indicator left", new Vector2(12.51f, -1.92f), new Vector2(.07f, .07f), new Color32(100, 185, 150, 255), 3, false);
            RectPiece("Service panel indicator right", new Vector2(12.91f, -1.92f), new Vector2(.07f, .07f), new Color32(205, 174, 100, 255), 3, false);
            RectPiece("Elevator threshold", new Vector2(13.52f, -4f), new Vector2(.1f, 1.9f), lightSteel, 2, false);
        }

        private void CreateStorageBox(string name, Vector2 position)
        {
            RectPiece(name, position, new Vector2(.58f, .48f), new Color32(190, 157, 111, 255), 1, true);
            RectPiece(name + " seam", position, new Vector2(.06f, .48f), new Color32(133, 103, 72, 255), 2, false);
        }

        private void CreateBatteryCrate(Vector2 position, Vector2 size)
        {
            var crate = new GameObject("Battery crate");
            crate.transform.position = position;
            crate.transform.localScale = new Vector3(size.x, size.y, 1f);
            var body = crate.AddComponent<SpriteRenderer>();
            body.sprite = GetPixel();
            body.color = new Color32(57, 65, 70, 255);
            body.sortingOrder = 2;
            crate.AddComponent<BoxCollider2D>();

            // Layered metal panels, ribs, corner lugs, and a visible connector; decoration only.
            AddBatteryDetail(crate.transform, "Inset metal lid", Vector2.zero, new Vector2(.84f, .78f), new Color32(105, 115, 119, 255), 3);
            AddBatteryDetail(crate.transform, "Recessed graphite panel", Vector2.zero, new Vector2(.76f, .69f), new Color32(67, 76, 81, 255), 4);
            AddBatteryDetail(crate.transform, "Upper raised rail", new Vector2(0f, .39f), new Vector2(.92f, .09f), new Color32(163, 172, 174, 255), 4);
            AddBatteryDetail(crate.transform, "Lower raised rail", new Vector2(0f, -.39f), new Vector2(.92f, .09f), new Color32(142, 152, 155, 255), 4);
            for (int i = -2; i <= 2; i++)
            {
                float y = i * .115f;
                AddBatteryDetail(crate.transform, "Lid groove", new Vector2(-.015f, y), new Vector2(.67f, .035f), new Color32(42, 49, 53, 255), 5);
                AddBatteryDetail(crate.transform, "Groove highlight", new Vector2(-.015f, y + .028f), new Vector2(.67f, .012f), new Color32(125, 135, 138, 255), 6);
            }

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                AddBatteryDetail(crate.transform, "Raised corner latch", new Vector2(x * .48f, y * .31f), new Vector2(.14f, .12f), new Color32(184, 191, 190, 255), 6, 45f);

            AddBatteryDetail(crate.transform, "Connector rim", new Vector2(.405f, 0f), new Vector2(.19f, .43f), new Color32(184, 191, 190, 255), 7);
            AddBatteryDetail(crate.transform, "Recessed connector", new Vector2(.405f, 0f), new Vector2(.12f, .34f), new Color32(36, 43, 47, 255), 8);
            AddBatteryDetail(crate.transform, "Connector upper contact", new Vector2(.405f, .095f), new Vector2(.055f, .045f), new Color32(205, 211, 208, 255), 9);
            AddBatteryDetail(crate.transform, "Connector lower contact", new Vector2(.405f, -.095f), new Vector2(.055f, .045f), new Color32(205, 211, 208, 255), 9);
        }

        private void AddBatteryDetail(Transform parent, string name, Vector2 localPosition, Vector2 localSize, Color color, int sortingOrder, float rotation = 0f)
        {
            var detail = new GameObject(name);
            detail.transform.SetParent(parent, false);
            detail.transform.localPosition = new Vector3(localPosition.x, localPosition.y, -.01f);
            detail.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            detail.transform.localScale = new Vector3(localSize.x, localSize.y, 1f);
            var renderer = detail.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPixel();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
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

        private void RectPiece(string name, Vector2 center, Vector2 size, Color color, int sorting, bool collider, Transform parent = null)
        {
            var piece = new GameObject(name);
            piece.transform.position = center;
            piece.transform.localScale = new Vector3(size.x, size.y, 1f);
            if (parent != null) piece.transform.SetParent(parent, true);
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
            GUI.Label(new Rect(24, 42, 245, 22), "Hedef seç; WASD ile ilerle.");
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
