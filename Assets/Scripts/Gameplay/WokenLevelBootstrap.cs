using System.Collections;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using UnityEngine;
using UnityEngine.UI;
using Woken.Characters;
using Woken.UI;

namespace Woken.Gameplay
{
    /// <summary>
    /// Builds the playable Woken blockout from simple, collidable level pieces.
    /// </summary>
    public sealed class WokenLevelBootstrap : MonoBehaviour
    {
        private static Sprite pixel;
        private const float SecondFloorOffsetX = 32f;

        // Temporary testing switch: set false to restore the battery requirement.
        private const bool BypassElevatorBatteryForFloorTesting = true;

        private Transform player;
        private Transform robot;
        private Transform firstFloorRoot;
        private Transform secondFloorRoot;
        private Transform firstFloorNorthDoor;
        private Transform firstFloorSouthDoor;
        private Rigidbody2D playerBody;
        private Rigidbody2D robotBody;
        private PlayerWASDController playerController;
        private RobotMovementController robotController;
        private CharacterAnimator playerAnimator;
        private CharacterAnimator robotAnimator;
        private Vector2 lastPlayerFacing = Vector2.down;
        private bool elevatorTransitioning;
        private int activeFloor = 1;
        private Camera sceneCamera;
        private GameObject elevatorPrompt;
        private RectTransform elevatorPromptRect;
        private RectTransform elevatorPromptCanvasRect;
        private bool wasNearElevator;
        private float elevatorPromptHideAt;
        private Transform elevatorServicePanel;
        private WorldObjectNameUI worldObjectNameUI;
        private float elevatorPanelNearSince = -1f;
        private bool elevatorPanelMessageShown;
        private BatteryBayInteractionUI batteryBayInteraction;

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

            firstFloorRoot = new GameObject("First Floor - active").transform;
            BuildFloor();
            BuildWalls();
            BuildLaboratory();
            BuildMaze();
            BuildStorageRoom();
            BuildMaintenanceRoom();
            BuildSecondFloorStage();
        }

        private IEnumerator Start()
        {
            BuildNavigationSurface();
            yield return null;

            BuildCharacters();
            BuildElevatorPrompt();
            elevatorServicePanel = firstFloorRoot.Find("Elevator service panel");
            worldObjectNameUI = GetComponent<WorldObjectNameUI>();
            Transform batterySource = firstFloorRoot.Find("Battery crate");
            Transform batteryBay = firstFloorRoot.Find("Battery bay backing");
            Transform batteryStatusLight = firstFloorRoot.Find("Battery bay status light");
            Transform materialTable = firstFloorRoot.Find("Depot material counter body");
            Transform wrenchTarget = firstFloorRoot.Find("Wrench click target");
            Transform pliersTarget = firstFloorRoot.Find("Pliers click target");
            Transform sparePartsTarget = firstFloorRoot.Find("Spare parts click target");
            Transform[] wrenchVisuals = { wrenchTarget, firstFloorRoot.Find("Depot wrench handle"), firstFloorRoot.Find("Depot wrench head") };
            Transform[] pliersVisuals = { pliersTarget, firstFloorRoot.Find("Depot pliers handle"), firstFloorRoot.Find("Depot pliers jaw"), firstFloorRoot.Find("Depot plier grip left"), firstFloorRoot.Find("Depot plier grip right") };
            Transform[] sparePartsVisuals = { sparePartsTarget, firstFloorRoot.Find("Depot spare parts tray"), firstFloorRoot.Find("Depot spare part one"), firstFloorRoot.Find("Depot spare part two") };

            batteryBayInteraction = gameObject.AddComponent<BatteryBayInteractionUI>();
            batteryBayInteraction.Initialize(player, robot, batterySource != null ? batterySource.gameObject : null,
                batteryBay, batteryStatusLight, sceneCamera);

            RobotRepairInteractionUI robotRepairInteraction = gameObject.AddComponent<RobotRepairInteractionUI>();
            robotRepairInteraction.Initialize(player, robot, materialTable,
                wrenchTarget, pliersTarget, sparePartsTarget,
                wrenchVisuals, pliersVisuals, sparePartsVisuals, sceneCamera);
            Transform northLaptop = firstFloorRoot.Find("North bench laptop casing");
            Transform northLaptopScreen = firstFloorRoot.Find("North bench laptop screen");
            LaptopArchiveUI laptopArchive = gameObject.AddComponent<LaptopArchiveUI>();
            laptopArchive.Initialize(player, northLaptop, northLaptopScreen, sceneCamera, worldObjectNameUI);
            gameObject.AddComponent<WorldExplorationFog>();

            Transform keypad = firstFloorRoot.Find("Depot keypad housing");
            Transform door = firstFloorRoot.Find("Depot security door");
            DepotKeypadUI keypadUI = gameObject.AddComponent<DepotKeypadUI>();
            keypadUI.Initialize(player, robot, keypad, door);
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

        private void Update()
        {
            if (player == null)
                return;

            UpdateElevatorPanelProximityMessage();

            Vector2 input = playerController != null ? playerController.MoveInput : Vector2.zero;
            if (input.sqrMagnitude > 0.05f)
                lastPlayerFacing = input.normalized;

            bool closeEnough = player.position.x >= 12f
                && player.position.x <= 13.45f
                && Mathf.Abs(player.position.y + 4f) <= 0.5f;
            bool facingElevator = lastPlayerFacing.x > 0.7f
                && Mathf.Abs(lastPlayerFacing.y) < 0.7f;
            bool batteryReady = BypassElevatorBatteryForFloorTesting
                || batteryBayInteraction != null && batteryBayInteraction.BatteryInstalled;
            bool canInteract = activeFloor == 1 && !elevatorTransitioning
                && playerController != null && closeEnough && facingElevator && batteryReady
                && (batteryBayInteraction == null || !batteryBayInteraction.IsNearBatteryBay);
            UpdateElevatorPrompt(canInteract);

            if (activeFloor != 1 || elevatorTransitioning || playerController == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
                return;

            if (!closeEnough || !facingElevator || batteryBayInteraction != null && batteryBayInteraction.IsNearBatteryBay)
                return;

            if (!BypassElevatorBatteryForFloorTesting
                && (batteryBayInteraction == null || !batteryBayInteraction.BatteryInstalled))
            {
                if (worldObjectNameUI != null)
                    worldObjectNameUI.ShowMessage(
                        "Batarya yuvası boş, asansör çalışamıyor.",
                        new Vector3(13.82f, -2.65f, 0f),
                        2f);
                return;
            }

            StartCoroutine(CrossSecurityGate());
        }

        private void UpdateElevatorPanelProximityMessage()
        {
            bool isNearPanel = activeFloor == 1 && !elevatorTransitioning
                && !BypassElevatorBatteryForFloorTesting
                && elevatorServicePanel != null
                && batteryBayInteraction != null && !batteryBayInteraction.BatteryInstalled
                && Vector2.Distance(player.position, elevatorServicePanel.position) <= 1.35f;
            if (!isNearPanel)
            {
                elevatorPanelNearSince = -1f;
                elevatorPanelMessageShown = false;
                return;
            }

            if (elevatorPanelNearSince < 0f)
                elevatorPanelNearSince = Time.unscaledTime;
            if (elevatorPanelMessageShown || Time.unscaledTime - elevatorPanelNearSince < 1f)
                return;

            if (worldObjectNameUI != null)
                worldObjectNameUI.ShowMessage(
                    "Batarya yuvası boş, asansör çalışamıyor.",
                    elevatorServicePanel.position + Vector3.up * .7f,
                    2f);
            elevatorPanelMessageShown = true;
        }
        private void BuildElevatorPrompt()
        {
            GameObject canvasObject = new GameObject("Elevator instruction canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            elevatorPromptCanvasRect = canvasObject.GetComponent<RectTransform>();
            elevatorPromptCanvasRect.anchorMin = Vector2.zero;
            elevatorPromptCanvasRect.anchorMax = Vector2.one;
            elevatorPromptCanvasRect.offsetMin = Vector2.zero;
            elevatorPromptCanvasRect.offsetMax = Vector2.zero;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject bubble = new GameObject("Elevator instruction bubble",
                typeof(RectTransform), typeof(Image), typeof(Outline));
            elevatorPromptRect = bubble.GetComponent<RectTransform>();
            elevatorPromptRect.SetParent(elevatorPromptCanvasRect, false);
            elevatorPromptRect.sizeDelta = new Vector2(430f, 76f);
            Image background = bubble.GetComponent<Image>();
            background.sprite = MakePromptBubbleSprite();
            background.type = Image.Type.Sliced;
            background.color = new Color32(245, 245, 242, 250);
            background.raycastTarget = false;
            Outline outline = bubble.GetComponent<Outline>();
            outline.effectColor = new Color32(30, 35, 37, 220);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject textObject = new GameObject("Elevator instruction text",
                typeof(RectTransform), typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(elevatorPromptRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 5f);
            textRect.offsetMax = new Vector2(-8f, -5f);
            Text label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "Güvenlik kapısını açmak için F’ye bas.";
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color32(20, 20, 20, 255);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;

            elevatorPrompt = bubble;
            elevatorPrompt.SetActive(false);
        }

        private void UpdateElevatorPrompt(bool canInteract)
        {
            if (elevatorPrompt == null)
                return;

            if (canInteract && !wasNearElevator)
                elevatorPromptHideAt = Time.unscaledTime + 1.3f;
            wasNearElevator = canInteract;

            bool shouldShow = canInteract && Time.unscaledTime < elevatorPromptHideAt;
            elevatorPrompt.SetActive(shouldShow);
            if (!shouldShow || sceneCamera == null)
                return;

            Vector3 screenPoint = sceneCamera.WorldToScreenPoint(new Vector3(13.82f, -2.65f, 0f));
            if (screenPoint.z <= 0f)
            {
                elevatorPrompt.SetActive(false);
                return;
            }

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                elevatorPromptCanvasRect, screenPoint, null, out localPoint))
                elevatorPromptRect.anchoredPosition = localPoint;
        }

        private static Sprite MakePromptBubbleSprite()
        {
            const int size = 32;
            const float radius = 7f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Elevator instruction bubble sprite"
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nearestX = Mathf.Clamp(x + .5f, radius, size - radius);
                float nearestY = Mathf.Clamp(y + .5f, radius, size - radius);
                float dx = x + .5f - nearestX;
                float dy = y + .5f - nearestY;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
            }
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(10f, 10f, 10f, 10f));
            sprite.name = "Elevator instruction bubble sprite";
            return sprite;
        }
        private IEnumerator CrossSecurityGate()
        {
            if (!BypassElevatorBatteryForFloorTesting
                && (batteryBayInteraction == null || !batteryBayInteraction.BatteryInstalled))
                yield break;

            if (elevatorPrompt != null) elevatorPrompt.SetActive(false);
            elevatorTransitioning = true;
            playerController.enabled = false;
            if (robotController != null)
            {
                robotController.ResumeFollowingPlayer();
                robotController.enabled = false;
            }

            NavMeshAgent robotAgent = robot != null
                ? robot.GetComponent<NavMeshAgent>()
                : null;
            if (robotAgent != null && robotAgent.enabled)
            {
                if (robotAgent.isOnNavMesh)
                {
                    robotAgent.isStopped = true;
                    robotAgent.ResetPath();
                }
                robotAgent.enabled = false;
            }

            yield return StartCoroutine(AnimateElevatorDoors(
                firstFloorNorthDoor, firstFloorSouthDoor, true, 0.9f));

            GameObject fog = GameObject.Find("Unexplored areas");
            if (fog != null)
                fog.SetActive(false);

            GameObject routeCanvas = GameObject.Find("Robot Route Guide Canvas");
            if (routeCanvas != null)
                routeCanvas.SetActive(false);

            Coroutine playerCrossing = StartCoroutine(MoveActorTo(
                playerBody, playerAnimator, new Vector2(19.25f, -4f), 3.3f));
            Coroutine robotCrossing = StartCoroutine(MoveActorTo(
                robotBody, robotAnimator, new Vector2(18.45f, -4f), 3.3f));
            yield return playerCrossing;
            yield return robotCrossing;

            yield return StartCoroutine(AnimateElevatorDoors(
                firstFloorNorthDoor, firstFloorSouthDoor, false, 0.8f));
            activeFloor = 2;
            Physics2D.SyncTransforms();

            if (robotAgent != null)
            {
                robotAgent.enabled = true;
                if (!robotAgent.Warp(robotBody.position))
                    Debug.LogWarning("Robot could not attach to the continuous NavMesh after crossing the security gate.");
            }
            robotController.ResumeFollowingPlayer();

            playerController.enabled = true;
            if (robotController != null)
                robotController.enabled = true;
            elevatorTransitioning = false;
        }

        private IEnumerator AnimateElevatorDoors(
            Transform northDoor,
            Transform southDoor,
            bool open,
            float duration)
        {
            if (northDoor == null || southDoor == null)
                yield break;

            Vector3 northStart = northDoor.position;
            Vector3 southStart = southDoor.position;
            Vector3 northClosed = new Vector3(13.86f, -3.5f, northStart.z);
            Vector3 southClosed = new Vector3(13.86f, -4.5f, southStart.z);
            Vector3 northTarget = open
                ? northClosed + Vector3.up * 0.68f
                : northClosed;
            Vector3 southTarget = open
                ? southClosed + Vector3.down * 0.68f
                : southClosed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                northDoor.position = Vector3.Lerp(northStart, northTarget, t);
                southDoor.position = Vector3.Lerp(southStart, southTarget, t);
                yield return null;
            }

            northDoor.position = northTarget;
            southDoor.position = southTarget;
            Physics2D.SyncTransforms();
        }

        private IEnumerator MoveActorTo(
            Rigidbody2D body,
            CharacterAnimator animator,
            Vector2 target,
            float speed)
        {
            while (body != null && Vector2.Distance(body.position, target) > 0.025f)
            {
                Vector2 direction = (target - body.position).normalized;
                body.MovePosition(Vector2.MoveTowards(
                    body.position, target, speed * Time.deltaTime));
                if (animator != null)
                    animator.SetMotion(direction);
                yield return null;
            }

            if (body != null)
                body.position = target;
            if (animator != null)
                animator.SetMotion(Vector2.zero);
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

        private void BuildSecondFloorStage()
        {
            // Keep the measured 28 x 16 second-floor footprint and replace its former
            // internal layout with five simple rooms. Root offset is +32 on X.
            var secondFloor = new GameObject("Second Floor - east wing");
            Transform root = secondFloor.transform;
            secondFloorRoot = root;

            Color black = new Color32(0, 0, 0, 255);
            Color floor = new Color32(164, 171, 168, 255);
            Color wall = new Color32(48, 59, 63, 255);
            Color wallFace = new Color32(76, 88, 91, 255);
            Color wallTrim = new Color32(117, 130, 132, 255);

            RectPiece("Floor 2 black exterior", Vector2.zero,
                new Vector2(36f, 22f), black, -20, false, root);
            RectPiece("Floor 2 five-room floor", Vector2.zero,
                new Vector2(28f, 16f), floor, -11, false, root);

            // The west opening aligns with the elevator arrival; all other exterior edges stay closed.
            RectPiece("Floor 2 west boundary upper", new Vector2(-14f, 2.6f),
                new Vector2(.4f, 10.8f), wall, 0, true, root);
            RectPiece("Floor 2 west boundary lower", new Vector2(-14f, -6.6f),
                new Vector2(.4f, 2.8f), wall, 0, true, root);
            RectPiece("Floor 2 north boundary", new Vector2(0f, 7.8f),
                new Vector2(28.4f, .4f), wall, 0, true, root);
            RectPiece("Floor 2 south boundary", new Vector2(0f, -7.8f),
                new Vector2(28.4f, .4f), wall, 0, true, root);
            RectPiece("Floor 2 east boundary", new Vector2(14f, 0f),
                new Vector2(.4f, 16f), wall, 0, true, root);

            // Room 1 is 1.5 times its original width (7.5 units). Door openings are all 1.4 units.
            // Room 1 -> room 3: lower-right opening.
            RectPiece("Floor 2 partition west upper", new Vector2(-6.5f, 2.45f),
                new Vector2(.36f, 10.3f), wall, 1, true, root);
            RectPiece("Floor 2 partition west lower", new Vector2(-6.5f, -5.95f),
                new Vector2(.36f, 3.7f), wall, 1, true, root);

            // Room 3 -> room 2: opening in their shared horizontal wall.
            RectPiece("Floor 2 middle divider west", new Vector2(-3.6f, 0f),
                new Vector2(5.8f, .36f), wall, 1, true, root);
            RectPiece("Floor 2 middle divider east", new Vector2(2.85f, 0f),
                new Vector2(4.3f, .36f), wall, 1, true, root);

            // Room 2 -> room 4: upper opening in the central/east partition.
            RectPiece("Floor 2 partition east upper", new Vector2(5f, 6.65f),
                new Vector2(.36f, 2.3f), wall, 1, true, root);
            RectPiece("Floor 2 partition east lower", new Vector2(5f, -1.85f),
                new Vector2(.36f, 11.9f), wall, 1, true, root);

            // Room 4 -> room 5: opening in their shared horizontal wall.
            RectPiece("Floor 2 east divider left", new Vector2(6.9f, 0f),
                new Vector2(3.8f, .36f), wall, 1, true, root);
            RectPiece("Floor 2 east divider right before window", new Vector2(10.75f, 0f),
                new Vector2(1.1f, .36f), wall, 1, true, root);
            RectPiece("Floor 2 room 4 to 5 observation window frame", new Vector2(12.1f, 0f),
                new Vector2(1.6f, .36f), new Color32(49, 71, 77, 255), 2, true, root);
            RectPiece("Floor 2 room 4 to 5 observation glass", new Vector2(12.1f, 0f),
                new Vector2(1.34f, .13f), new Color32(110, 161, 174, 220), 3, true, root);
            RectPiece("Floor 2 observation glass reflection", new Vector2(12.1f, .025f),
                new Vector2(1.1f, .035f), new Color32(196, 219, 219, 255), 4, false, root);
            RectPiece("Floor 2 east divider right after window", new Vector2(13.45f, 0f),
                new Vector2(1.1f, .36f), wall, 1, true, root);
            // Room 4: materials storage. Leave the 2-to-4 and 4-to-5 openings clear,
            // and keep the observation glass visible from both rooms.
            Color storageSteel = new Color32(74, 88, 91, 255);
            Color shelfEdge = new Color32(135, 148, 145, 255);
            Color carton = new Color32(157, 126, 91, 255);
            Color cartonLight = new Color32(190, 159, 117, 255);
            Color crate = new Color32(104, 116, 111, 255);

            // North storage rack with visible shelves and a few supplies.
            RectPiece("Floor 2 room 4 north shelving frame", new Vector2(7.65f, 7.12f),
                new Vector2(4.1f, .72f), storageSteel, 0, true, root);
            RectPiece("Floor 2 room 4 north shelf upper", new Vector2(7.65f, 6.94f),
                new Vector2(4.15f, .09f), shelfEdge, 1, false, root);
            RectPiece("Floor 2 room 4 north shelf lower", new Vector2(7.65f, 7.32f),
                new Vector2(4.15f, .09f), shelfEdge, 1, false, root);
            RectPiece("Floor 2 room 4 rack carton A", new Vector2(6.45f, 6.78f),
                new Vector2(.72f, .42f), carton, 2, true, root);
            RectPiece("Floor 2 room 4 rack carton A seam", new Vector2(6.45f, 6.78f),
                new Vector2(.06f, .42f), cartonLight, 3, false, root);
            RectPiece("Floor 2 room 4 rack crate", new Vector2(8.05f, 6.76f),
                new Vector2(.82f, .48f), crate, 2, true, root);
            RectPiece("Floor 2 room 4 rack small supply bin", new Vector2(9.1f, 6.78f),
                new Vector2(.5f, .38f), new Color32(115, 130, 130, 255), 2, true, root);

            // Packing table, kept west of the direct route to the glass and doorway.
            RectPiece("Floor 2 room 4 packing table body", new Vector2(7.25f, 1.2f),
                new Vector2(2.5f, 1.12f), storageSteel, 0, true, root);
            RectPiece("Floor 2 room 4 packing table top", new Vector2(7.25f, 1.53f),
                new Vector2(2.58f, .62f), new Color32(180, 184, 176, 255), 1, false, root);
            RectPiece("Floor 2 room 4 packing tape dispenser", new Vector2(6.55f, 1.51f),
                new Vector2(.42f, .25f), new Color32(58, 67, 69, 255), 2, false, root);
            RectPiece("Floor 2 room 4 packing tape roll", new Vector2(6.55f, 1.64f),
                new Vector2(.22f, .12f), new Color32(189, 174, 125, 255), 3, false, root);
            RectPiece("Floor 2 room 4 packing paper", new Vector2(7.55f, 1.59f),
                new Vector2(.48f, .29f), new Color32(221, 218, 203, 255), 2, false, root);
            RectPiece("Floor 2 room 4 table scissors", new Vector2(8.15f, 1.55f),
                new Vector2(.34f, .08f), new Color32(70, 80, 80, 255), 3, false, root);

            // Parked hand cart and two crates.
            RectPiece("Floor 2 room 4 hand cart platform", new Vector2(11.35f, 4.55f),
                new Vector2(1.3f, .52f), new Color32(82, 96, 98, 255), 1, true, root);
            RectPiece("Floor 2 room 4 hand cart handle", new Vector2(11.82f, 5.06f),
                new Vector2(.12f, .62f), shelfEdge, 2, true, root);
            RectPiece("Floor 2 room 4 hand cart grip", new Vector2(11.82f, 5.38f),
                new Vector2(.34f, .1f), new Color32(60, 70, 71, 255), 3, false, root);
            RectPiece("Floor 2 room 4 cart crate", new Vector2(11.32f, 4.91f),
                new Vector2(.72f, .54f), crate, 2, true, root);
            RectPiece("Floor 2 room 4 corner carton", new Vector2(12.0f, 7.0f),
                new Vector2(1.15f, .92f), carton, 1, true, root);
            RectPiece("Floor 2 room 4 corner carton lid", new Vector2(12.0f, 7.0f),
                new Vector2(1.04f, .1f), cartonLight, 2, false, root);
            RectPiece("Floor 2 room 4 corner crate", new Vector2(13.25f, 7.0f),
                new Vector2(.92f, .76f), crate, 1, true, root);

            // PPE wall board and recycling bins, away from the shared-wall window.
            RectPiece("Floor 2 room 4 PPE board", new Vector2(12.95f, 2.9f),
                new Vector2(1.45f, .18f), storageSteel, 1, true, root);
            RectPiece("Floor 2 room 4 PPE helmet", new Vector2(12.55f, 2.57f),
                new Vector2(.42f, .32f), cartonLight, 2, false, root);
            RectPiece("Floor 2 room 4 PPE gloves", new Vector2(13.25f, 2.56f),
                new Vector2(.42f, .25f), new Color32(126, 145, 130, 255), 2, false, root);
            RectPiece("Floor 2 room 4 recycling bin cardboard", new Vector2(6.35f, 2.75f),
                new Vector2(.72f, .92f), new Color32(132, 113, 88, 255), 1, true, root);
            RectPiece("Floor 2 room 4 recycling bin packaging", new Vector2(7.2f, 2.75f),
                new Vector2(.72f, .92f), new Color32(94, 119, 119, 255), 1, true, root);
            // Room 5 contains the final exit; the exit itself remains closed for now.
            RectPiece("Floor 2 exit door", new Vector2(14f, -4.25f),
                new Vector2(.4f, 2.5f), wallFace, 2, true, root);
            RectPiece("Floor 2 exit door face", new Vector2(13.96f, -4.25f),
                new Vector2(.08f, 2.1f), wallTrim, 3, false, root);
            // Room 1: compact arrival lounge and coffee counter. Keep the elevator-to-room-3 route clear.
            Color cabinet = new Color32(83, 94, 96, 255);
            Color counter = new Color32(194, 197, 187, 255);
            Color upholstery = new Color32(103, 116, 116, 255);
            Color cushion = new Color32(150, 157, 150, 255);
            Color wood = new Color32(117, 91, 68, 255);
            Color leaf = new Color32(78, 112, 78, 255);

            // Coffee counter along the north wall.
            RectPiece("Floor 2 room 1 coffee counter body", new Vector2(-10.55f, 6.78f),
                new Vector2(4.9f, .9f), cabinet, 0, true, root);
            RectPiece("Floor 2 room 1 coffee counter top", new Vector2(-10.55f, 6.27f),
                new Vector2(5.05f, .18f), counter, 1, false, root);
            RectPiece("Floor 2 room 1 counter left cabinet", new Vector2(-12.25f, 6.75f),
                new Vector2(1.35f, .72f), new Color32(105, 115, 114, 255), 2, false, root);
            RectPiece("Floor 2 room 1 counter right cabinet", new Vector2(-10.55f, 6.75f),
                new Vector2(1.65f, .72f), new Color32(96, 108, 108, 255), 2, false, root);
            RectPiece("Floor 2 room 1 coffee machine base", new Vector2(-9.1f, 6.13f),
                new Vector2(.62f, .34f), new Color32(49, 59, 61, 255), 3, false, root);
            RectPiece("Floor 2 room 1 coffee machine", new Vector2(-9.1f, 6.42f),
                new Vector2(.45f, .42f), new Color32(62, 72, 74, 255), 4, false, root);
            RectPiece("Floor 2 room 1 coffee machine highlight", new Vector2(-9.1f, 6.48f),
                new Vector2(.24f, .08f), new Color32(118, 160, 157, 255), 5, false, root);
            RectPiece("Floor 2 room 1 cup tray", new Vector2(-11.45f, 6.13f),
                new Vector2(.92f, .28f), new Color32(139, 130, 112, 255), 3, false, root);
            for (int i = 0; i < 3; i++)
            {
                RectPiece("Floor 2 room 1 coffee cup " + i, new Vector2(-11.72f + i * .27f, 6.08f),
                    new Vector2(.16f, .18f), new Color32(222, 221, 207, 255), 4, false, root);
            }

            // Small seating nook set above the clear walk lane from the elevator to room 3.
            RectPiece("Floor 2 room 1 rug", new Vector2(-10.7f, 2.2f),
                new Vector2(4.6f, 2.8f), new Color32(132, 137, 130, 255), -8, false, root);
            RectPiece("Floor 2 room 1 sofa base", new Vector2(-11.65f, 2.25f),
                new Vector2(2.05f, 1.18f), new Color32(71, 82, 83, 255), 0, true, root);
            RectPiece("Floor 2 room 1 sofa seat", new Vector2(-11.65f, 2.12f),
                new Vector2(1.76f, .78f), upholstery, 1, false, root);
            RectPiece("Floor 2 room 1 sofa back", new Vector2(-11.65f, 2.72f),
                new Vector2(1.88f, .32f), cushion, 2, false, root);
            RectPiece("Floor 2 room 1 sofa cushion left", new Vector2(-12.08f, 2.12f),
                new Vector2(.72f, .58f), cushion, 2, false, root);
            RectPiece("Floor 2 room 1 sofa cushion right", new Vector2(-11.22f, 2.12f),
                new Vector2(.72f, .58f), cushion, 2, false, root);
            RectPiece("Floor 2 room 1 coffee table top", new Vector2(-9.55f, 2.18f),
                new Vector2(1.1f, .78f), wood, 1, true, root);
            RectPiece("Floor 2 room 1 coffee table inset", new Vector2(-9.55f, 2.18f),
                new Vector2(.91f, .59f), new Color32(145, 112, 81, 255), 2, false, root);
            RectPiece("Floor 2 room 1 magazine", new Vector2(-9.55f, 2.18f),
                new Vector2(.34f, .24f), new Color32(190, 190, 173, 255), 3, false, root);

            // One modest potted plant in the upper-west corner.
            RectPiece("Floor 2 room 1 plant pot", new Vector2(-13.05f, 4.55f),
                new Vector2(.55f, .5f), new Color32(128, 100, 77, 255), 1, true, root);
            RectPiece("Floor 2 room 1 plant leaves", new Vector2(-13.05f, 4.88f),
                new Vector2(.78f, .64f), leaf, 2, false, root);
            RectPiece("Floor 2 room 1 plant leaf highlight", new Vector2(-13.05f, 4.96f),
                new Vector2(.24f, .33f), new Color32(112, 144, 91, 255), 3, false, root);
            // Lower-wall rest details; centered below the walk lane so the elevator route stays open.
            RectPiece("Floor 2 room 1 lower bench base", new Vector2(-11.4f, -6.78f),
                new Vector2(2.15f, .64f), new Color32(67, 79, 81, 255), 0, true, root);
            RectPiece("Floor 2 room 1 lower bench seat", new Vector2(-11.4f, -6.66f),
                new Vector2(1.88f, .43f), new Color32(130, 142, 136, 255), 1, false, root);
            RectPiece("Floor 2 room 1 lower bench cushion left", new Vector2(-11.84f, -6.65f),
                new Vector2(.72f, .33f), new Color32(158, 164, 152, 255), 2, false, root);
            RectPiece("Floor 2 room 1 lower bench cushion right", new Vector2(-10.96f, -6.65f),
                new Vector2(.72f, .33f), new Color32(158, 164, 152, 255), 2, false, root);
            RectPiece("Floor 2 room 1 lower side table", new Vector2(-8.9f, -6.8f),
                new Vector2(.72f, .62f), new Color32(112, 87, 65, 255), 1, true, root);
            RectPiece("Floor 2 room 1 lower side table top", new Vector2(-8.9f, -6.74f),
                new Vector2(.62f, .5f), new Color32(151, 117, 84, 255), 2, false, root);
            RectPiece("Floor 2 room 1 lower waste bin", new Vector2(-7.45f, -6.83f),
                new Vector2(.48f, .62f), new Color32(91, 105, 105, 255), 1, true, root);
            RectPiece("Floor 2 room 1 lower waste bin rim", new Vector2(-7.45f, -6.55f),
                new Vector2(.5f, .1f), new Color32(126, 140, 138, 255), 2, false, root);
            // Preserve the existing connection between the first-floor gate and second-floor arrival.
            RectPiece(
                "Security gate connector floor",
                new Vector2(16f, -4f),
                new Vector2(5f, 2.4f),
                new Color32(166, 174, 173, 255),
                -10,
                false,
                firstFloorRoot
            );
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
                "Outer east lower south",
                new Vector2(14f, -6.5f),
                new Vector2(0.35f, 3f),
                wall
            );

            // Keep the east-wall elevator opening clear.
            Wall(
                "Outer east lower north",
                new Vector2(14f, -1.75f),
                new Vector2(0.35f, 2.5f),
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

            // Security door fills the north opening into the depot.
            RectPiece("Depot security door", new Vector2(6f, 7.05f),
                new Vector2(0.32f, 1.5f), new Color32(56, 67, 71, 255), 3, true);
            RectPiece("Depot security door inset", new Vector2(6f, 7.05f),
                new Vector2(0.12f, 1.28f), new Color32(82, 94, 97, 255), 4, false);
            RectPiece("Depot security door indicator", new Vector2(6f, 7.48f),
                new Vector2(0.08f, 0.08f), new Color32(191, 80, 70, 255), 5, false);

            // Gray keypad housing on the corridor side of the locked door.
            RectPiece("Depot keypad housing", new Vector2(5.58f, 7.62f),
                new Vector2(0.28f, 0.48f), new Color32(128, 139, 139, 255), 4, false);
            RectPiece("Depot keypad screen", new Vector2(5.58f, 7.77f),
                new Vector2(0.18f, 0.07f), new Color32(52, 73, 75, 255), 5, false);
            RectPiece("Depot keypad button one", new Vector2(5.53f, 7.65f),
                new Vector2(0.035f, 0.035f), new Color32(208, 210, 199, 255), 5, false);
            RectPiece("Depot keypad button two", new Vector2(5.63f, 7.65f),
                new Vector2(0.035f, 0.035f), new Color32(208, 210, 199, 255), 5, false);
            RectPiece("Depot keypad button three", new Vector2(5.53f, 7.58f),
                new Vector2(0.035f, 0.035f), new Color32(208, 210, 199, 255), 5, false);
            RectPiece("Depot keypad button four", new Vector2(5.63f, 7.58f),
                new Vector2(0.035f, 0.035f), new Color32(208, 210, 199, 255), 5, false);

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

            // Clicking the north bench displays its name above the tabletop.
            Bench(
                "North laboratory bench",
                new Vector2(-9.15f, 6.5f),
                new Vector2(5.2f, 1.5f),
                steel,
                top
            );
            BoxCollider2D northBenchCollider = firstFloorRoot
                .Find("North laboratory bench body")
                .GetComponent<BoxCollider2D>();
            WorldObjectNameUI nameUI = gameObject.AddComponent<WorldObjectNameUI>();
            nameUI.Initialize(sceneCamera, northBenchCollider,
                "Kuzey Laboratuvar Tezgâhı", new Vector3(-9.15f, 7.62f, 0f));

            // Small black laptop on the north bench's upper-right corner.
            RectPiece("North bench laptop casing", new Vector2(-7.15f, 6.78f),
                new Vector2(0.9f, 0.62f), new Color32(35, 39, 41, 255), 3, false);
            RectPiece("North bench laptop screen", new Vector2(-7.15f, 6.92f),
                new Vector2(0.72f, 0.3f), new Color32(13, 17, 19, 255), 4, false);
            RectPiece("North bench laptop keyboard", new Vector2(-7.15f, 6.61f),
                new Vector2(0.72f, 0.15f), new Color32(54, 58, 60, 255), 4, false);
            RectPiece("North bench laptop touchpad", new Vector2(-7.15f, 6.59f),
                new Vector2(0.18f, 0.045f), new Color32(82, 86, 87, 255), 5, false);
            BoxCollider2D laptopCollider = firstFloorRoot
                .Find("North bench laptop casing")
                .gameObject.AddComponent<BoxCollider2D>();
            laptopCollider.isTrigger = true;
            nameUI.Register(laptopCollider, "Laptop", new Vector3(-7.15f, 7.34f, 0f));

            // One book and one file on the north bench, away from its laptop.
            RectPiece("North bench loose notebook", new Vector2(-9.55f, 6.78f),
                new Vector2(0.48f, 0.56f), new Color32(72, 82, 91, 255), 3, false);
            RectPiece("North bench loose notebook spine", new Vector2(-9.73f, 6.78f),
                new Vector2(0.06f, 0.52f), new Color32(135, 146, 148, 255), 4, false);
            RotateRoomPiece("North bench loose notebook", -9f);
            RotateRoomPiece("North bench loose notebook spine", -9f);
            RectPiece("North bench loose file", new Vector2(-8.48f, 6.34f),
                new Vector2(0.72f, 0.42f), new Color32(119, 103, 76, 255), 3, false);
            RectPiece("North bench loose file paper", new Vector2(-8.48f, 6.35f),
                new Vector2(0.58f, 0.3f), new Color32(215, 212, 198, 255), 4, false);
            RectPiece("North bench loose file tab", new Vector2(-8.7f, 6.55f),
                new Vector2(0.18f, 0.08f), new Color32(154, 135, 99, 255), 5, false);
            RotateRoomPiece("North bench loose file", 7f);
            RotateRoomPiece("North bench loose file paper", 7f);
            RotateRoomPiece("North bench loose file tab", 7f);

            Bench(
                "South laboratory bench",
                new Vector2(-9.15f, -6.5f),
                new Vector2(5.2f, 1.5f),
                steel,
                top
            );
            BoxCollider2D southBenchCollider = firstFloorRoot
                .Find("South laboratory bench body")
                .GetComponent<BoxCollider2D>();
            nameUI.Register(southBenchCollider, "Güney Laboratuvar Tezgâhı",
                new Vector3(-9.15f, -5.38f, 0f));

            // A black laptop with visible cracks in its screen on the south bench.
            RectPiece("South bench broken laptop casing", new Vector2(-10.78f, -6.38f),
                new Vector2(0.88f, 0.6f), new Color32(31, 35, 37, 255), 3, false);
            RectPiece("South bench broken laptop screen", new Vector2(-10.78f, -6.48f),
                new Vector2(0.72f, 0.32f), new Color32(12, 16, 18, 255), 4, false);
            RectPiece("South bench broken laptop keyboard", new Vector2(-10.78f, -6.19f),
                new Vector2(0.72f, 0.12f), new Color32(52, 57, 59, 255), 4, false);
            RectPiece("South bench broken laptop crack main", new Vector2(-10.78f, -6.48f),
                new Vector2(0.38f, 0.022f), new Color32(139, 151, 151, 255), 5, false);
            RectPiece("South bench broken laptop crack branch A", new Vector2(-10.85f, -6.48f),
                new Vector2(0.18f, 0.018f), new Color32(139, 151, 151, 255), 5, false);
            RectPiece("South bench broken laptop crack branch B", new Vector2(-10.68f, -6.52f),
                new Vector2(0.16f, 0.018f), new Color32(139, 151, 151, 255), 5, false);
            BoxCollider2D brokenLaptopCollider = firstFloorRoot
                .Find("South bench broken laptop casing")
                .gameObject.AddComponent<BoxCollider2D>();
            brokenLaptopCollider.isTrigger = true;
            nameUI.Register(brokenLaptopCollider, "Kırık Laptop", new Vector3(-10.78f, -5.98f, 0f));
            RotateRoomPiece("South bench broken laptop crack main", -28f);
            RotateRoomPiece("South bench broken laptop crack branch A", 34f);
            RotateRoomPiece("South bench broken laptop crack branch B", 45f);

            // Two books and one file scattered across the south bench.
            RectPiece("South bench blue book", new Vector2(-9.72f, -6.42f),
                new Vector2(0.46f, 0.56f), new Color32(55, 70, 77, 255), 3, false);
            RectPiece("South bench blue book spine", new Vector2(-9.88f, -6.42f),
                new Vector2(0.06f, 0.52f), new Color32(127, 143, 146, 255), 4, false);
            RotateRoomPiece("South bench blue book", 12f);
            RotateRoomPiece("South bench blue book spine", 12f);

            RectPiece("South bench red book", new Vector2(-7.18f, -6.43f),
                new Vector2(0.48f, 0.58f), new Color32(89, 57, 58, 255), 3, false);
            RectPiece("South bench red book label", new Vector2(-7.18f, -6.31f),
                new Vector2(0.24f, 0.08f), new Color32(194, 180, 154, 255), 4, false);
            RotateRoomPiece("South bench red book", -11f);
            RotateRoomPiece("South bench red book label", -11f);

            RectPiece("South bench loose file", new Vector2(-8.1f, -6.68f),
                new Vector2(0.72f, 0.4f), new Color32(113, 101, 76, 255), 3, false);
            RectPiece("South bench loose file papers", new Vector2(-8.1f, -6.68f),
                new Vector2(0.58f, 0.28f), new Color32(218, 215, 199, 255), 4, false);
            RectPiece("South bench loose file tab", new Vector2(-8.32f, -6.47f),
                new Vector2(0.18f, 0.08f), new Color32(155, 137, 103, 255), 5, false);
            RotateRoomPiece("South bench loose file", -8f);
            RotateRoomPiece("South bench loose file papers", -8f);
            RotateRoomPiece("South bench loose file tab", -8f);

            Bench(
                "Central island laboratory bench",
                new Vector2(-9.4f, 0f),
                new Vector2(3.2f, 4.2f),
                steel,
                top
            );
            BoxCollider2D centerBenchCollider = firstFloorRoot
                .Find("Central island laboratory bench body")
                .GetComponent<BoxCollider2D>();
            nameUI.Register(centerBenchCollider, "Orta Laboratuvar Tezgâhı",
                new Vector3(-9.4f, 2.05f, 0f));

            // Scattered books, folders, and loose papers on the central worktop.
            RectPiece("Center bench blue notebook", new Vector2(-10.42f, 0.86f),
                new Vector2(0.55f, 0.72f), new Color32(48, 64, 72, 255), 3, false);
            RectPiece("Center bench blue notebook spine", new Vector2(-10.62f, 0.86f),
                new Vector2(0.07f, 0.68f), new Color32(112, 132, 135, 255), 4, false);
            RotateRoomPiece("Center bench blue notebook", -13f);
            RotateRoomPiece("Center bench blue notebook spine", -13f);

            RectPiece("Center bench red notebook", new Vector2(-9.62f, 0.3f),
                new Vector2(0.58f, 0.76f), new Color32(91, 58, 58, 255), 3, false);
            RectPiece("Center bench red notebook label", new Vector2(-9.62f, 0.46f),
                new Vector2(0.28f, 0.1f), new Color32(188, 177, 153, 255), 4, false);
            RotateRoomPiece("Center bench red notebook", 11f);
            RotateRoomPiece("Center bench red notebook label", 11f);

            RectPiece("Center bench green notebook", new Vector2(-8.42f, 0.94f),
                new Vector2(0.52f, 0.68f), new Color32(57, 77, 67, 255), 3, false);
            RectPiece("Center bench green notebook band", new Vector2(-8.42f, 0.94f),
                new Vector2(0.07f, 0.68f), new Color32(137, 151, 132, 255), 4, false);
            RotateRoomPiece("Center bench green notebook", -8f);
            RotateRoomPiece("Center bench green notebook band", -8f);

            RectPiece("Center bench tan folder", new Vector2(-10.25f, -0.48f),
                new Vector2(0.82f, 0.55f), new Color32(139, 119, 83, 255), 3, false);
            RectPiece("Center bench tan folder papers", new Vector2(-10.25f, -0.44f),
                new Vector2(0.68f, 0.39f), new Color32(210, 207, 190, 255), 4, false);
            RectPiece("Center bench tan folder tab", new Vector2(-10.48f, -0.16f),
                new Vector2(0.22f, 0.1f), new Color32(160, 140, 101, 255), 5, false);
            RotateRoomPiece("Center bench tan folder", 9f);
            RotateRoomPiece("Center bench tan folder papers", 9f);
            RotateRoomPiece("Center bench tan folder tab", 9f);

            RectPiece("Center bench gray file", new Vector2(-8.78f, -0.62f),
                new Vector2(0.86f, 0.58f), new Color32(78, 88, 90, 255), 3, false);
            RectPiece("Center bench gray file papers", new Vector2(-8.78f, -0.62f),
                new Vector2(0.7f, 0.42f), new Color32(218, 216, 200, 255), 4, false);
            RectPiece("Center bench gray file clip", new Vector2(-8.78f, -0.36f),
                new Vector2(0.2f, 0.07f), new Color32(151, 159, 155, 255), 5, false);
            RotateRoomPiece("Center bench gray file", -12f);
            RotateRoomPiece("Center bench gray file papers", -12f);
            RotateRoomPiece("Center bench gray file clip", -12f);

            RectPiece("Center bench loose paper A", new Vector2(-9.48f, 1.17f),
                new Vector2(0.4f, 0.52f), new Color32(221, 219, 205, 255), 3, false);
            RectPiece("Center bench loose paper B", new Vector2(-9.23f, -0.88f),
                new Vector2(0.42f, 0.5f), new Color32(210, 212, 202, 255), 3, false);
            RectPiece("Center bench loose paper C", new Vector2(-8.05f, -0.12f),
                new Vector2(0.38f, 0.48f), new Color32(225, 222, 207, 255), 3, false);
            RotateRoomPiece("Center bench loose paper A", 16f);
            RotateRoomPiece("Center bench loose paper B", -18f);
            RotateRoomPiece("Center bench loose paper C", 12f);

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

            // Small chair collision footprints keep the surrounding path open.
            CreateChair(nameUI, "North bench chair left", new Vector2(-10.7f, 5.28f), 1f);
            CreateChair(nameUI, "North bench chair center", new Vector2(-9.15f, 5.28f), 1f);
            CreateChair(nameUI, "North bench chair right", new Vector2(-7.6f, 5.28f), 1f);
            CreateChair(nameUI, "South bench chair left", new Vector2(-10.1f, -5.42f), -1f);
            CreateChair(nameUI, "South bench chair right", new Vector2(-8.0f, -5.42f), -1f);

            RegisterBookFileTarget(nameUI, "North bench loose notebook");
            RegisterBookFileTarget(nameUI, "North bench loose file");
            RegisterBookFileTarget(nameUI, "Center bench blue notebook");
            RegisterBookFileTarget(nameUI, "Center bench red notebook");
            RegisterBookFileTarget(nameUI, "Center bench green notebook");
            RegisterBookFileTarget(nameUI, "Center bench tan folder");
            RegisterBookFileTarget(nameUI, "Center bench gray file");
            RegisterBookFileTarget(nameUI, "South bench blue book");
            RegisterBookFileTarget(nameUI, "South bench red book");
            RegisterBookFileTarget(nameUI, "South bench loose file");
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

        private void RegisterDepotItemName(
            WorldObjectNameUI nameUI,
            string objectName,
            string displayName,
            Vector2 center,
            Vector2 size)
        {
            GameObject clickTarget = new GameObject(objectName + " click target");
            clickTarget.transform.SetParent(firstFloorRoot, true);
            clickTarget.transform.position = center;
            clickTarget.transform.localScale = new Vector3(size.x, size.y, 1f);
            BoxCollider2D clickArea = clickTarget.AddComponent<BoxCollider2D>();
            clickArea.isTrigger = true;
            nameUI.Register(clickArea, displayName,
                new Vector3(center.x, center.y + 0.55f, 0f));
        }

        private void DepotDecoration(
            string name,
            Vector2 center,
            Vector2 size,
            Color color,
            int sorting,
            float rotation = 0f)
        {
            RectPiece(name, center, size, color, sorting, false);
            Transform piece = firstFloorRoot.Find(name);
            if (piece == null) return;

            NavMeshModifier modifier = piece.GetComponent<NavMeshModifier>();
            if (modifier != null)
                modifier.ignoreFromBuild = true;
            piece.rotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void RegisterRoomObjectName(
            WorldObjectNameUI nameUI,
            string objectName,
            string displayName,
            Vector3 bubbleOffset)
        {
            if (nameUI == null) return;
            Transform target = firstFloorRoot.Find(objectName);
            if (target == null) return;

            Collider2D targetCollider = target.GetComponent<Collider2D>();
            if (targetCollider != null)
                nameUI.Register(targetCollider, displayName, target.position + bubbleOffset);
        }

        private void RegisterBookFileTarget(WorldObjectNameUI nameUI, string objectName)
        {
            Transform item = firstFloorRoot.Find(objectName);
            if (item == null)
            {
                Debug.LogWarning($"Book or file object '{objectName}' was not found.", this);
                return;
            }

            BoxCollider2D clickArea = item.gameObject.AddComponent<BoxCollider2D>();
            clickArea.isTrigger = true;
            nameUI.Register(clickArea, "Kitaplar ve Dosyalar", item.position + Vector3.up * 0.55f);
        }

        private void CreateChair(WorldObjectNameUI nameUI, string chairName, Vector2 seatCenter, float tableDirection)
        {
            Color seatColor = new Color32(91, 111, 115, 255);
            Color backColor = new Color32(47, 60, 64, 255);
            Color legColor = new Color32(75, 87, 89, 255);

            CreateChairPiece(chairName + " seat", seatCenter,
                new Vector2(0.62f, 0.44f), seatColor, 2, true);
            CreateChairPiece(chairName + " backrest",
                seatCenter - Vector2.up * tableDirection * 0.31f,
                new Vector2(0.7f, 0.14f), backColor, 3, false);
            Vector2 frontLegCenter = seatCenter + Vector2.up * tableDirection * 0.14f;
            CreateChairPiece(chairName + " front leg left",
                frontLegCenter + Vector2.left * 0.23f,
                new Vector2(0.08f, 0.12f), legColor, 1, false);
            CreateChairPiece(chairName + " front leg right",
                frontLegCenter + Vector2.right * 0.23f,
                new Vector2(0.08f, 0.12f), legColor, 1, false);

            BoxCollider2D seatCollider = firstFloorRoot.Find(chairName + " seat")
                .GetComponent<BoxCollider2D>();
            nameUI.Register(seatCollider, "Sandalye",
                new Vector3(seatCenter.x, seatCenter.y + 0.7f, 0f));
        }

        private void CreateChairPiece(
            string name,
            Vector2 center,
            Vector2 size,
            Color color,
            int sorting,
            bool blocksPath)
        {
            RectPiece(name, center, size, color, sorting, blocksPath);
            NavMeshModifier modifier = firstFloorRoot.Find(name).GetComponent<NavMeshModifier>();
            if (modifier != null)
                modifier.ignoreFromBuild = !blocksPath;
        }

        private void RotateRoomPiece(string objectName, float angle)
        {
            Transform piece = firstFloorRoot.Find(objectName);
            if (piece != null)
                piece.rotation = Quaternion.Euler(0f, 0f, angle);
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

        private void BuildNavigationSurface()
        {
            Physics2D.SyncTransforms();
            NavMeshSurface surface = CreateNavigationSurface("Woken Continuous Navigation Surface");
            Transform blackExterior = secondFloorRoot != null
                ? secondFloorRoot.Find("Floor 2 black exterior")
                : null;
            SpriteRenderer backgroundRenderer = blackExterior != null
                ? blackExterior.GetComponent<SpriteRenderer>()
                : null;
            bool restoreBackground = backgroundRenderer != null && backgroundRenderer.enabled;
            if (backgroundRenderer != null)
                backgroundRenderer.enabled = false;

            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                if (backgroundRenderer != null)
                    backgroundRenderer.enabled = restoreBackground;
            }
        }

        private NavMeshSurface CreateNavigationSurface(string surfaceName)
        {
            var surfaceObject = new GameObject(surfaceName);
            surfaceObject.transform.rotation = Quaternion.Euler(270f, 0f, 0f);

            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.hideEditorLogs = true;

            surfaceObject.AddComponent<CollectSources2d>();
            return surface;
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
            WorldObjectNameUI nameUI = GetComponent<WorldObjectNameUI>();
            RegisterRoomObjectName(nameUI, "Depot north shelving", "Raf", new Vector3(0f, .4f, 0f));
            RegisterRoomObjectName(nameUI, "Depot east shelving", "Raf", new Vector3(0f, .4f, 0f));
            RegisterRoomObjectName(nameUI, "Depot south shelving", "Raf", new Vector3(0f, .4f, 0f));
            RegisterRoomObjectName(nameUI, "Depot security door", "Kapı", new Vector3(0f, .85f, 0f));
            Transform keypad = firstFloorRoot.Find("Depot keypad housing");
            if (keypad != null)
            {
                BoxCollider2D keypadCollider = keypad.gameObject.AddComponent<BoxCollider2D>();
                keypadCollider.isTrigger = true;
                nameUI.Register(keypadCollider, "Şifre Paneli", keypad.position + Vector3.up * .5f);
            }
            RectPiece("Depot south shelf lip", new Vector2(10.65f, .81f), new Vector2(5.1f, .1f), shelfEdge, 1, false);

            Bench("Depot material counter", new Vector2(12.05f, 6.25f), new Vector2(1.8f, .95f), bench, benchTop);
            RegisterRoomObjectName(nameUI, "Depot material counter body", "Masa", new Vector3(0f, .75f, 0f));
            Bench("Depot check counter", new Vector2(12.05f, 1.75f), new Vector2(1.8f, .9f), bench, benchTop);
            RegisterRoomObjectName(nameUI, "Depot check counter body", "Masa", new Vector3(0f, .75f, 0f));

            // Hand tools and spare parts on the upper material counter.
            DepotDecoration("Depot pliers handle", new Vector2(11.52f, 6.36f),
                new Vector2(.1f, .34f), new Color32(74, 83, 84, 255), 3);
            DepotDecoration("Depot pliers jaw", new Vector2(11.52f, 6.53f),
                new Vector2(.24f, .07f), new Color32(130, 140, 139, 255), 4);
            DepotDecoration("Depot plier grip left", new Vector2(11.47f, 6.23f),
                new Vector2(.07f, .16f), new Color32(135, 91, 65, 255), 4);
            DepotDecoration("Depot plier grip right", new Vector2(11.57f, 6.23f),
                new Vector2(.07f, .16f), new Color32(135, 91, 65, 255), 4);
            DepotDecoration("Depot wrench handle", new Vector2(12.02f, 6.36f),
                new Vector2(.38f, .07f), new Color32(105, 117, 119, 255), 3, -12f);
            DepotDecoration("Depot wrench head", new Vector2(12.2f, 6.4f),
                new Vector2(.14f, .12f), new Color32(130, 140, 139, 255), 4, -12f);
            DepotDecoration("Depot spare parts tray", new Vector2(12.59f, 6.39f),
                new Vector2(.42f, .25f), new Color32(72, 83, 85, 255), 3);
            DepotDecoration("Depot spare part one", new Vector2(12.5f, 6.4f),
                new Vector2(.11f, .08f), new Color32(171, 157, 120, 255), 4);
            DepotDecoration("Depot spare part two", new Vector2(12.65f, 6.36f),
                new Vector2(.1f, .07f), new Color32(132, 145, 145, 255), 4);

            // Clipboard, pencil, and flashlight on the lower check counter.
            DepotDecoration("Depot checklist clipboard", new Vector2(11.62f, 1.91f),
                new Vector2(.65f, .47f), new Color32(65, 75, 78, 255), 3, -8f);
            DepotDecoration("Depot checklist paper", new Vector2(11.62f, 1.92f),
                new Vector2(.53f, .36f), new Color32(222, 220, 205, 255), 4, -8f);
            DepotDecoration("Depot checklist clip", new Vector2(11.62f, 2.1f),
                new Vector2(.18f, .055f), new Color32(125, 136, 137, 255), 5, -8f);
            DepotDecoration("Depot checklist line one", new Vector2(11.62f, 1.99f),
                new Vector2(.34f, .018f), new Color32(143, 151, 144, 255), 5, -8f);
            DepotDecoration("Depot checklist line two", new Vector2(11.62f, 1.92f),
                new Vector2(.34f, .018f), new Color32(143, 151, 144, 255), 5, -8f);
            DepotDecoration("Depot checklist line three", new Vector2(11.62f, 1.85f),
                new Vector2(.34f, .018f), new Color32(143, 151, 144, 255), 5, -8f);
            DepotDecoration("Depot pencil", new Vector2(12.23f, 1.83f),
                new Vector2(.37f, .045f), new Color32(183, 151, 88, 255), 3, -32f);
            DepotDecoration("Depot pencil tip", new Vector2(12.39f, 1.73f),
                new Vector2(.08f, .045f), new Color32(59, 64, 64, 255), 4, -32f);
            DepotDecoration("Depot flashlight body", new Vector2(12.54f, 2.02f),
                new Vector2(.32f, .12f), new Color32(49, 58, 60, 255), 3, 18f);
            DepotDecoration("Depot flashlight lens", new Vector2(12.69f, 2.07f),
                new Vector2(.09f, .12f), new Color32(195, 182, 128, 255), 4, 18f);

            RegisterDepotItemName(nameUI, "Pliers", "Pense",
                new Vector2(11.52f, 6.39f), new Vector2(.32f, .44f));
            RegisterDepotItemName(nameUI, "Wrench", "Anahtar",
                new Vector2(12.02f, 6.38f), new Vector2(.48f, .17f));
            RegisterDepotItemName(nameUI, "Spare parts", "Yedek Parçalar",
                new Vector2(12.59f, 6.39f), new Vector2(.5f, .3f));
            RegisterDepotItemName(nameUI, "Clipboard", "Kontrol Listesi",
                new Vector2(11.62f, 1.91f), new Vector2(.72f, .52f));
            RegisterDepotItemName(nameUI, "Pencil", "Kalem",
                new Vector2(12.23f, 1.83f), new Vector2(.42f, .16f));
            RegisterDepotItemName(nameUI, "Flashlight", "El Feneri",
                new Vector2(12.54f, 2.02f), new Vector2(.4f, .18f));

            RectPiece("Depot closed cabinet", new Vector2(7.25f, 1.55f), new Vector2(1.05f, 1.8f), new Color32(91, 108, 111, 255), 0, true);
            RectPiece("Depot cabinet door", new Vector2(7.25f, 1.55f), new Vector2(.78f, 1.5f), new Color32(143, 157, 157, 255), 1, false);
            RegisterRoomObjectName(nameUI, "Depot closed cabinet", "Dolap", new Vector3(0f, 1.05f, 0f));

            CreateBatteryCrate(new Vector2(10.1f, 4.15f), new Vector2(1.25f, .95f));
            RegisterRoomObjectName(nameUI, "Battery crate", "Batarya", new Vector3(0f, .6f, 0f));
            CreateStorageBox("Depot parts box A", new Vector2(8.45f, 1.35f));
            CreateStorageBox("Depot parts box B", new Vector2(9.25f, 1.35f));
            CreateStorageBox("Depot parts box C", new Vector2(10.05f, 1.35f));
            RegisterRoomObjectName(nameUI, "Depot parts box A", "Koli", new Vector3(0f, .5f, 0f));
            RegisterRoomObjectName(nameUI, "Depot parts box B", "Koli", new Vector3(0f, .5f, 0f));
            RegisterRoomObjectName(nameUI, "Depot parts box C", "Koli", new Vector3(0f, .5f, 0f));
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
            // Small laboratory props arranged on the maintenance workbench.
            RectPiece("Experiment test tube one", new Vector2(6.98f, -6.98f), new Vector2(.09f, .34f), lightSteel, 3, false);
            RectPiece("Experiment test tube one sample", new Vector2(6.98f, -7.08f), new Vector2(.065f, .11f), new Color32(104, 174, 165, 255), 4, false);
            RectPiece("Experiment test tube two", new Vector2(7.16f, -6.98f), new Vector2(.09f, .34f), lightSteel, 3, false);
            RectPiece("Experiment test tube two sample", new Vector2(7.16f, -7.08f), new Vector2(.065f, .11f), new Color32(194, 166, 101, 255), 4, false);
            RectPiece("Experiment test tube three", new Vector2(7.34f, -6.98f), new Vector2(.09f, .34f), lightSteel, 3, false);
            RectPiece("Experiment test tube three sample", new Vector2(7.34f, -7.08f), new Vector2(.065f, .11f), new Color32(126, 157, 194, 255), 4, false);
            RectPiece("Experiment beaker one", new Vector2(8.82f, -6.99f), new Vector2(.28f, .24f), lightSteel, 3, false);
            RectPiece("Experiment beaker one liquid", new Vector2(8.82f, -7.055f), new Vector2(.2f, .07f), new Color32(105, 178, 163, 255), 4, false);
            RectPiece("Experiment beaker two", new Vector2(9.13f, -7.02f), new Vector2(.24f, .2f), new Color32(190, 198, 190, 255), 3, false);
            RectPiece("Experiment beaker two liquid", new Vector2(9.13f, -7.075f), new Vector2(.17f, .055f), new Color32(190, 147, 100, 255), 4, false);
            RectPiece("Experiment microscope base", new Vector2(8.02f, -7.28f), new Vector2(.4f, .1f), dark, 3, false);
            RectPiece("Experiment microscope arm", new Vector2(8.02f, -7.15f), new Vector2(.1f, .28f), lightSteel, 4, false);
            RectPiece("Experiment microscope stage", new Vector2(8.02f, -7.1f), new Vector2(.28f, .07f), steel, 5, false);
            RectPiece("Experiment microscope eyepiece", new Vector2(8.02f, -6.99f), new Vector2(.08f, .15f), dark, 5, false);
            RectPiece("Experiment petri dish", new Vector2(8.57f, -7.28f), new Vector2(.22f, .12f), new Color32(174, 190, 185, 255), 3, false);
            RectPiece("Experiment petri dish sample", new Vector2(8.57f, -7.28f), new Vector2(.1f, .055f), new Color32(177, 126, 125, 255), 4, false);
            RectPiece("Maintenance tool board", new Vector2(7.35f, -1.05f), new Vector2(1.25f, .6f), steel, 0, true);
            RectPiece("Tool board wrench", new Vector2(7.12f, -1.05f), new Vector2(.08f, .38f), lightSteel, 2, false);
            RectPiece("Tool board driver", new Vector2(7.48f, -1.05f), new Vector2(.08f, .42f), new Color32(132, 148, 149, 255), 2, false);
            RectPiece("Tool board shelf", new Vector2(7.35f, -1.36f), new Vector2(1.25f, .1f), dark, 2, false);
            RectPiece("Tool board fastener left", new Vector2(6.84f, -1.05f), new Vector2(.07f, .07f), lightSteel, 3, false);
            RectPiece("Tool board fastener right", new Vector2(7.86f, -1.05f), new Vector2(.07f, .07f), lightSteel, 3, false);

            // Surface-mounted elevator assembly sits on the east wall; its two leaves remain separate for future animation.
            RectPiece("Elevator wall mounted faceplate", new Vector2(13.82f, -4f), new Vector2(.25f, 2f), new Color32(119, 132, 135, 255), 1, false);
            RectPiece("Elevator jamb north", new Vector2(13.78f, -2.95f), new Vector2(.42f, .12f), lightSteel, 3, true);
            RectPiece("Elevator jamb south", new Vector2(13.78f, -5.05f), new Vector2(.42f, .12f), lightSteel, 3, true);
            RectPiece("Elevator door leaf north", new Vector2(13.86f, -3.5f), new Vector2(.3f, .94f), new Color32(113, 126, 130, 255), 4, true);
            RectPiece("Elevator door leaf south", new Vector2(13.86f, -4.5f), new Vector2(.3f, .94f), new Color32(128, 140, 143, 255), 4, true);
            RectPiece("Elevator door inset north", new Vector2(13.84f, -3.5f), new Vector2(.045f, .78f), new Color32(82, 94, 98, 255), 5, false, firstFloorRoot.Find("Elevator door leaf north"));
            RectPiece("Elevator door inset south", new Vector2(13.84f, -4.5f), new Vector2(.045f, .78f), new Color32(94, 106, 109, 255), 5, false, firstFloorRoot.Find("Elevator door leaf south"));
            RectPiece("Elevator seam north", new Vector2(13.86f, -3.97f), new Vector2(.18f, .035f), new Color32(38, 47, 51, 255), 6, false, firstFloorRoot.Find("Elevator door leaf north"));
            RectPiece("Elevator seam south", new Vector2(13.86f, -4.03f), new Vector2(.18f, .035f), new Color32(38, 47, 51, 255), 6, false, firstFloorRoot.Find("Elevator door leaf south"));
            RectPiece("Elevator top indicator housing", new Vector2(13.78f, -2.78f), new Vector2(.3f, .18f), dark, 3, true);
            RectPiece("Elevator top indicator", new Vector2(13.77f, -2.78f), new Vector2(.12f, .07f), new Color32(99, 190, 171, 255), 4, false);
            RectPiece("Elevator door warning strip north", new Vector2(13.68f, -3.5f), new Vector2(.045f, .72f), new Color32(202, 177, 106, 255), 4, false, firstFloorRoot.Find("Elevator door leaf north"));
            RectPiece("Elevator door warning strip south", new Vector2(13.68f, -4.5f), new Vector2(.045f, .72f), new Color32(202, 177, 106, 255), 4, false, firstFloorRoot.Find("Elevator door leaf south"));

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

            WorldObjectNameUI nameUI = GetComponent<WorldObjectNameUI>();
            RegisterRoomObjectName(nameUI, "Maintenance workbench body", "Deney Masası", new Vector3(0f, .9f, 0f));
            RegisterRoomObjectName(nameUI, "Maintenance tool board", "Deney Malzemeleri Dolabı", new Vector3(0f, .65f, 0f));
            for (int i = 0; i < firstFloorRoot.childCount; i++)
            {
                Transform drawer = firstFloorRoot.GetChild(i);
                if (drawer.name != "Workbench drawer face") continue;

                BoxCollider2D drawerClickArea = drawer.gameObject.AddComponent<BoxCollider2D>();
                drawerClickArea.size = new Vector2(.7f, .28f);
                drawerClickArea.isTrigger = true;
                nameUI.Register(drawerClickArea, "Çekmece", drawer.position + Vector3.up * .45f);
            }
            Transform elevatorFaceplate = firstFloorRoot.Find("Elevator wall mounted faceplate");
            if (nameUI != null && elevatorFaceplate != null)
            {
                BoxCollider2D elevatorClickArea = elevatorFaceplate.gameObject.AddComponent<BoxCollider2D>();
                elevatorClickArea.size = new Vector2(.8f, 2.2f);
                elevatorClickArea.isTrigger = true;
                nameUI.Register(elevatorClickArea, "Güvenlik Kapısı", elevatorFaceplate.position + Vector3.up * 1.25f);
                Transform servicePanel = firstFloorRoot.Find("Elevator service panel");
                if (servicePanel != null)
                {
                    BoxCollider2D panelClickArea = servicePanel.gameObject.AddComponent<BoxCollider2D>();
                    panelClickArea.size = new Vector2(1f, 1.1f);
                    panelClickArea.isTrigger = true;
                    nameUI.Register(panelClickArea, "Kapı Kontrol Paneli", servicePanel.position + Vector3.up * .7f);
                }

                Transform batteryBay = firstFloorRoot.Find("Battery bay backing");
                if (batteryBay != null)
                {
                    BoxCollider2D batteryClickArea = batteryBay.gameObject.AddComponent<BoxCollider2D>();
                    batteryClickArea.size = new Vector2(1.5f, 1.5f);
                    batteryClickArea.isTrigger = true;
                    nameUI.Register(batteryClickArea, "Batarya Yuvası", batteryBay.position + Vector3.up * .9f);
                }
            }

            firstFloorNorthDoor = firstFloorRoot.Find("Elevator door leaf north");
            firstFloorSouthDoor = firstFloorRoot.Find("Elevator door leaf south");
        }

        private void CreateStorageBox(string name, Vector2 position)
        {
            RectPiece(name, position, new Vector2(.58f, .48f), new Color32(190, 157, 111, 255), 1, true);
            RectPiece(name + " seam", position, new Vector2(.06f, .48f), new Color32(133, 103, 72, 255), 2, false);
        }

        private void CreateBatteryCrate(Vector2 position, Vector2 size)
        {
            var crate = new GameObject("Battery crate");
            crate.transform.SetParent(firstFloorRoot, true);
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
            // Oyuncuyu oluştur.
            player = CreateCharacter(
                "Player",
                new Vector2(-10.7f, -3.2f),
                false
            ).transform;

            sceneCamera.transform.position =
                player.position + new Vector3(0f, 0.2f, -10f);

            // Robotu oyuncunun yanında ve aynı Y koordinatında başlat.
            // NavMesh örneklemesi Y ekseninde kaydırmasın diye küçük yarıçap kullan;
            // sol taraf uygun değilse aynı hizada sağ tarafı dene.
            Vector3 robotSpawn = new Vector3(player.position.x - 2f, player.position.y, 0f);
            int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
            int walkableAreaMask = notWalkableArea >= 0
                ? NavMesh.AllAreas & ~(1 << notWalkableArea)
                : NavMesh.AllAreas;

            bool foundSpawn = NavMesh.SamplePosition(robotSpawn, out NavMeshHit spawnHit, 0.45f, walkableAreaMask);
            if (!foundSpawn)
            {
                robotSpawn.x = player.position.x + 2f;
                foundSpawn = NavMesh.SamplePosition(robotSpawn, out spawnHit, 0.45f, walkableAreaMask);
            }

            if (!foundSpawn)
            {
                Debug.LogError("Oyuncuyla aynı hizada robot için yürünebilir başlangıç noktası bulunamadı.");
                spawnHit.position = robotSpawn;
            }

            GameObject robotObject = CreateCharacter("Companion Robot", spawnHit.position, true);
            robot = robotObject.transform;
            robotBody = robotObject.GetComponent<Rigidbody2D>();
            if (robotBody != null)
                robotBody.position = spawnHit.position;

            // Robotun başlangıçta aşağıya doğru baköasını sağla.
            robotAnimator = robotObject.GetComponent<CharacterAnimator>();

            if (robotAnimator != null)
            {
                robotAnimator.SetMotion(Vector2.down);
            }

            // Mevcut takip sistemini koru.
            robotController = robotObject.AddComponent<RobotMovementController>();

            robotController.SetPlayer(player);

            RobotRouteGuideUI routeGuide = gameObject.AddComponent<RobotRouteGuideUI>();
            routeGuide.Initialize(player, robot, robotController, sceneCamera, firstFloorRoot.Find("Battery crate"));

            playerBody = player.GetComponent<Rigidbody2D>();
            playerController = player.GetComponent<PlayerWASDController>();
            playerAnimator = player.GetComponent<CharacterAnimator>();
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
            bool collider,
            Transform parent = null)
        {
            if (parent != null && secondFloorRoot != null
                && (parent == secondFloorRoot || parent.IsChildOf(secondFloorRoot)))
                center.x += SecondFloorOffsetX;

            GameObject piece = new GameObject(name);
            piece.transform.position = center;
            piece.transform.localScale =
                new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer =
                piece.AddComponent<SpriteRenderer>();

            if (parent == null)
            {
                parent = firstFloorRoot;
            }

            if (parent != null)
            {
                piece.transform.SetParent(parent, true);
            }

            renderer.sprite = GetPixel();
            renderer.color = color;
            renderer.sortingOrder = sorting;

            NavMeshModifier navMeshModifier = piece.AddComponent<NavMeshModifier>();
            if (collider)
            {
                navMeshModifier.overrideArea = true;
                navMeshModifier.area = NavMesh.GetAreaFromName("Not Walkable");
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

    }
}








