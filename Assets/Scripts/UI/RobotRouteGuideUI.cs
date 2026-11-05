using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.AI;
using Woken.Characters;

namespace Woken.UI
{
    /// <summary>Opening dialogue and automatic elevator guidance for the robot.</summary>
    public sealed class RobotRouteGuideUI : MonoBehaviour
    {
        private const float MinimumStatusDuration = 3f;
        private const string OpeningMessage = "Sonunda uyandın. Bu terk edilmiş laboratuvardan acilen çıkmamız lazım. Acele et.";
        private const string DepartureMessage = "Hadi hızlı ol, hemen asansörle çıkmalıyız buradan. Takip et beni.";
        private static Sprite panelSprite;
        private static Sprite circleSprite;

        private Transform player;
        private Transform robot;
        private Transform batterySource;
        private Transform secondFloorEntryStep;
        private Transform secondFloorStepDestinationCabinet;
        private Transform room3PowerButton;
        private Transform robotOnlyPassageTrigger;
        private Transform room3DoorWest;
        private Transform room3DoorEast;
        private RobotMovementController movement;
        private Rigidbody2D robotBody;
        private CharacterAnimator robotAnimator;
        private Collider2D robotCollider;
        private NavMeshAgent robotAgent;
        private Vector3 robotOriginalScale;
        private PlayerWASDController playerController;
        private Rigidbody2D playerBody;
        private CharacterAnimator playerAnimator;
        private Collider2D playerCollider;
        private Vector3 playerOriginalScale;
        private Action refreshNavigation;
        private Camera worldCamera;
        private Canvas canvas;
        private RectTransform canvasRect;

        private RectTransform introPanel;


        private RectTransform statusPanel;
        private Text statusText;
        private Text dialogueText;
        private bool initialized;

        private bool waitingForRoute;
        private bool waitingForBatteryPickup;
        private bool depotHintShown;
        private float depotNearSince = -1f;
        private bool batteryHelpDialogueShown;
        private float batteryNearSince = -1f;
        private bool batteryDepotHintShown;
        private bool resumeFollowAfterStatus;
        private Coroutine statusRoutine;
        private Coroutine introRoutine;
        private Coroutine secondFloorCabinetHelpRoutine;
        private bool secondFloorCabinetHelpShown;
        private bool robotTaskSucceeded;
        private bool room3DoorHintShown;
        private float room3PassageNearSince = -1f;
        private bool room3DoorOpened;
        private Coroutine room3DoorRoutine;

        public void Initialize(Transform playerTransform, Transform robotTransform, RobotMovementController robotMovement,
            Camera cameraToUse, Transform batterySourceTransform, Transform stepTransform = null,
            Transform destinationCabinetTransform = null, Transform powerButtonTransform = null,
            Transform passageTriggerTransform = null, Transform doorWestTransform = null,
            Transform doorEastTransform = null, Action navigationRefresh = null)
        {
            if (initialized) return;
            player = playerTransform;
            robot = robotTransform;
            batterySource = batterySourceTransform;
            secondFloorEntryStep = stepTransform;
            secondFloorStepDestinationCabinet = destinationCabinetTransform;
            room3PowerButton = powerButtonTransform;
            robotOnlyPassageTrigger = passageTriggerTransform;
            room3DoorWest = doorWestTransform;
            room3DoorEast = doorEastTransform;
            refreshNavigation = navigationRefresh;
            movement = robotMovement;
            robotBody = robotTransform.GetComponent<Rigidbody2D>();
            robotAnimator = robotTransform.GetComponent<CharacterAnimator>();
            robotCollider = robotTransform.GetComponent<Collider2D>();
            robotAgent = robotTransform.GetComponent<NavMeshAgent>();
            robotOriginalScale = robotTransform.localScale;
            playerController = playerTransform.GetComponent<PlayerWASDController>();
            playerBody = playerTransform.GetComponent<Rigidbody2D>();
            playerAnimator = playerTransform.GetComponent<CharacterAnimator>();
            playerCollider = playerTransform.GetComponent<Collider2D>();
            playerOriginalScale = playerTransform.localScale;
            worldCamera = cameraToUse != null ? cameraToUse : Camera.main;
            if (player == null || robot == null || movement == null || worldCamera == null)
            {
                Debug.LogError("Robot route guide needs player, robot, movement, and camera references.", this);
                return;
            }

            EnsureEventSystem();
            BuildCanvas();

            BuildPopups();
            movement.TaskFinished += OnTaskFinished;
            initialized = true;
            introRoutine = StartCoroutine(ShowOpeningMessage());
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("Woken UI Event System");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            else if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
            {
                var legacyModule = EventSystem.current.GetComponent<StandaloneInputModule>();
                if (legacyModule != null) legacyModule.enabled = false;
                EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private void BuildCanvas()
        {
            var go = new GameObject("Robot Route Guide Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            canvasRect = go.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
        }

        private void BuildPopups()
        {
            introPanel = CreatePanel("Robot opening message", new Vector2(760f, 154f));
            dialogueText = MakeText("Robot dialogue text", introPanel, OpeningMessage, 29, Color.black, TextAnchor.MiddleCenter);
            Stretch(dialogueText.rectTransform, 38f, 22f, 38f, 22f);

            statusPanel = CreatePanel("Robot route status", new Vector2(420f, 122f));
            statusText = MakeText("Robot status text", statusPanel, string.Empty, 27, Color.black, TextAnchor.MiddleCenter);
            Stretch(statusText.rectTransform, 34f, 20f, 34f, 18f);

            statusPanel.gameObject.SetActive(false);
        }
        private IEnumerator ShowOpeningMessage()
        {
            introPanel.gameObject.SetActive(true);
            dialogueText.text = OpeningMessage;
            yield return new WaitForSeconds(3f);
            dialogueText.text = DepartureMessage;
            yield return new WaitForSeconds(3f);
            introPanel.gameObject.SetActive(false);
            introRoutine = null;
            StartElevatorRoute();
        }

        private void StartElevatorRoute()
        {
            waitingForRoute = true;
            if (!movement.StartMoveTask(new Vector2(12.4f, -4f), false))
                waitingForRoute = false;
        }

        private static bool IsInElevatorRoom(Vector2 position)
        {
            return position.x > 6f && position.x < 14f && position.y > -8f && position.y < -.2f;
        }

        private void OnTaskFinished(RobotTaskResult result)
        {
            if (waitingForBatteryPickup)
            {
                waitingForBatteryPickup = false;
                if (result == RobotTaskResult.Completed || Vector2.Distance(robot.position, batterySource.position) <= 1.1f)
                {
                    BatteryBayInteractionUI batteryInteraction = GetComponent<BatteryBayInteractionUI>();
                    if (batteryInteraction != null)
                        batteryInteraction.RobotPickedUpBattery();
                }
                movement.ResumeFollowingPlayer();
                return;
            }

            if (!waitingForRoute) return;
            waitingForRoute = false;
            if (result == RobotTaskResult.Completed || IsInElevatorRoom(robot.position))
                ShowStatus("Asansöre geldik.", true);
            else if (result == RobotTaskResult.NoRoute)
                ShowStatus("Oraya ulaşamıyorum; yol kapalı görünüyor.", true);
        }

        private void ShowStatus(string message, bool resumeFollowAfterDelay = false, float displayDuration = MinimumStatusDuration)
        {
            displayDuration = Mathf.Max(displayDuration, MinimumStatusDuration);
            StopStatusRoutine(false);
            resumeFollowAfterStatus = false;
            if (canvas != null && !canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(true);
            HidePopups();
            bool longMessage = message.Length > 45;
            statusPanel.sizeDelta = longMessage ? new Vector2(680f, 150f) : new Vector2(420f, 122f);
            statusText.fontSize = longMessage ? 24 : 27;
            statusText.text = message;
            statusPanel.gameObject.SetActive(true);

            if (resumeFollowAfterDelay)
            {
                resumeFollowAfterStatus = true;
                statusRoutine = StartCoroutine(CloseStatusAfterDelay(displayDuration));
            }
        }

        private void ShowStatusTemporarily(string message, float duration)
        {
            duration = Mathf.Max(duration, MinimumStatusDuration);
            ShowStatus(message);
            StartCoroutine(HideStatusMessageAfterDelay(message, duration));
        }

        private IEnumerator HideStatusMessageAfterDelay(string message, float duration)
        {
            yield return new WaitForSeconds(duration);
            if (statusPanel != null && statusPanel.gameObject.activeSelf && statusText.text == message)
                statusPanel.gameObject.SetActive(false);
        }
        private IEnumerator CloseStatusAfterDelay(float displayDuration)
        {
            yield return new WaitForSeconds(displayDuration);
            statusRoutine = null;
            statusPanel.gameObject.SetActive(false);
            ResumeFollowAfterStatus();
        }

        private void StopStatusRoutine(bool resumeImmediately)
        {
            if (statusRoutine != null)
            {
                StopCoroutine(statusRoutine);
                statusRoutine = null;
            }

            if (resumeImmediately)
                ResumeFollowAfterStatus();
        }

        private void ResumeFollowAfterStatus()
        {
            if (!resumeFollowAfterStatus)
                return;

            resumeFollowAfterStatus = false;
            if (movement != null)
                movement.ResumeFollowingPlayer();
        }

        private void HidePopups()
        {
            if (introPanel != null) introPanel.gameObject.SetActive(false);
            if (statusPanel != null) statusPanel.gameObject.SetActive(false);
        }
        private RectTransform CreatePanel(string name, Vector2 size)
        {
            RectTransform panel = MakeRect(name, canvas.transform, size);
            var image = panel.gameObject.AddComponent<Image>();
            image.sprite = PanelSprite();
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            return panel;
        }

        private void Update()
        {
            if (!initialized || introRoutine != null || movement == null
                || movement.IsExecutingTask || player == null)
                return;

            UpdateDepotHint();
            UpdateBatteryHelpDialogue();
            UpdateSecondFloorDoorPuzzle();
            UpdateSecondFloorCabinetHelp();
        }

        private void UpdateDepotHint()
        {
            if (depotHintShown)
                return;

            Vector2 keypadPosition = new Vector2(5.58f, 7.62f);
            bool isNearKeypad = Vector2.Distance(player.position, keypadPosition) <= 1.35f;
            if (!isNearKeypad)
            {
                depotNearSince = -1f;
                return;
            }

            if (depotNearSince < 0f)
                depotNearSince = Time.unscaledTime;

            // Let the keypad's 1.3 second F prompt appear before the robot speaks.
            if (Time.unscaledTime - depotNearSince < 1.4f)
                return;

            depotHintShown = true;
            ShowStatus("Depoya girmek için şifreyi bulmalısın.", true);
        }

        private void UpdateBatteryHelpDialogue()
        {
            if (batteryHelpDialogueShown || batterySource == null)
                return;

            bool nearBattery = Vector2.Distance(player.position, batterySource.position) <= 1.35f;
            if (!nearBattery)
            {
                batteryNearSince = -1f;
                return;
            }

            if (batteryNearSince < 0f)
                batteryNearSince = Time.unscaledTime;
            if (Time.unscaledTime - batteryNearSince < 2.3f)
                return;

            batteryHelpDialogueShown = true;
            ShowStatus(
                "Bataryayı taşıyabilmem için enerjim yetmez. Bazı parçalarımı değiştirmen gerekli; ancak sana o zaman yardım edebilirim.",
                true,
                3f);
        }

        public void ShowBatteryDepotHintAfterElevatorError()
        {
            if (batteryDepotHintShown)
                return;

            batteryDepotHintShown = true;
            StartCoroutine(ShowBatteryDepotHintAfterDelay());
        }

        private IEnumerator ShowBatteryDepotHintAfterDelay()
        {
            yield return new WaitForSeconds(2f);
            ShowStatus("Batarya bu odanın yukarısındaki depoda gibi görünüyor.", true, 3f);
        }

        private void UpdateSecondFloorCabinetHelp()
        {
            if (secondFloorCabinetHelpShown || secondFloorCabinetHelpRoutine != null
                || secondFloorEntryStep == null || secondFloorStepDestinationCabinet == null)
                return;

            Collider2D cabinetCollider = secondFloorStepDestinationCabinet.GetComponent<Collider2D>();
            float cabinetLeft = cabinetCollider != null
                ? cabinetCollider.bounds.min.x
                : secondFloorStepDestinationCabinet.position.x - .64f;
            Vector2 cabinetFront = new Vector2(cabinetLeft - 1.45f, secondFloorStepDestinationCabinet.position.y);
            bool playerIsInFront = player.position.x < cabinetLeft
                && Vector2.Distance(player.position, cabinetFront) <= .65f;
            if (!playerIsInFront)
                return;

            secondFloorCabinetHelpShown = true;
            movement.HoldPosition();
            if (playerController != null) playerController.enabled = false;
            if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
            if (playerAnimator != null) playerAnimator.SetMotion(Vector2.zero);
            secondFloorCabinetHelpRoutine = StartCoroutine(MoveEntryStepToCabinet());
        }

        private void UpdateSecondFloorDoorPuzzle()
        {
            if (room3DoorOpened || room3DoorRoutine != null || room3DoorWest == null
                || room3DoorEast == null || room3PowerButton == null || robotOnlyPassageTrigger == null)
                return;

            Vector2 doorCenter = ((Vector2)room3DoorWest.position + (Vector2)room3DoorEast.position) * .5f;
            Vector2 doorFront = doorCenter + Vector2.down * .9f;
            if (!room3DoorHintShown && player.position.y < doorCenter.y
                && Vector2.Distance(player.position, doorFront) <= 1.2f)
            {
                room3DoorHintShown = true;
                ShowStatusTemporarily("Şurada bir buton var sanırım, kapı butona basılınca açılıyor.", 2f);
            }

            if (!room3DoorHintShown)
                return;

            Vector2 passage = robotOnlyPassageTrigger.position;
            bool nearYellowMarks = Vector2.Distance(player.position, passage) <= 1.05f
                && player.position.x < passage.x + .2f;
            if (!nearYellowMarks)
            {
                if (room3PassageNearSince >= 0f
                    && Time.unscaledTime - room3PassageNearSince >= 2f)
                    room3DoorRoutine = StartCoroutine(RobotOpensRoom2Door());
                return;
            }

            if (room3PassageNearSince < 0f)
            {
                room3PassageNearSince = Time.unscaledTime;
                // Let the player try the opening without the robot following close behind.
                movement.HoldPosition();
            }

            if (Time.unscaledTime - room3PassageNearSince >= 2f)
                room3DoorRoutine = StartCoroutine(RobotOpensRoom2Door());
        }
        private IEnumerator RobotOpensRoom2Door()
        {
            ShowStatus("Burası senin için fazla dar, bekle bir de ben deneyeyim.");
            yield return new WaitForSeconds(1f);

            Vector2 passage = robotOnlyPassageTrigger.position;

            while (player != null && Vector2.Distance(player.position, passage) <= 1.2f
                && player.position.x >= passage.x - .55f)
                yield return null;
            Vector2 passageWest = passage + Vector2.left * 1.15f;
            Vector2 passageWestStaging = passageWest + Vector2.up * 1.05f;
            yield return StartCoroutine(MoveRobotForCabinetTask(passageWestStaging));
            if (!robotTaskSucceeded)
            {
                yield return StartCoroutine(FinishDoorTaskWithFailure("Geçidin önüne ulaşamıyorum."));
                yield break;
            }

            // NavMesh clearance is too narrow for the service slot, so guide the robot through it directly.
            movement.HoldPosition();
            movement.enabled = false;
            if (robotAgent != null && robotAgent.enabled)
                robotAgent.enabled = false;
            if (robotCollider != null)
                robotCollider.enabled = false;

            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, passageWest, .55f, false));
            Vector2 passageEast = passage + Vector2.right * .62f;
            Vector2 buttonApproach = (Vector2)room3PowerButton.position + Vector2.left * .58f;
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, passageEast, .8f, false));
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, buttonApproach, .72f, false));

            SpriteRenderer buttonRenderer = room3PowerButton.GetComponent<SpriteRenderer>();
            Vector3 buttonScale = room3PowerButton.localScale;
            room3PowerButton.localScale = buttonScale * .72f;
            if (buttonRenderer != null)
                buttonRenderer.color = new Color32(110, 196, 143, 255);
            yield return new WaitForSeconds(.22f);
            room3PowerButton.localScale = buttonScale;

            // The door opens as soon as the robot presses the button.
            Vector3 westStart = room3DoorWest.position;
            Vector3 eastStart = room3DoorEast.position;
            Vector3 westOpen = westStart + Vector3.left * .72f;
            Vector3 eastOpen = eastStart + Vector3.right * .72f;
            float elapsed = 0f;
            const float openingDuration = .7f;
            while (elapsed < openingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / openingDuration);
                t = t * t * (3f - 2f * t);
                room3DoorWest.position = Vector3.Lerp(westStart, westOpen, t);
                room3DoorEast.position = Vector3.Lerp(eastStart, eastOpen, t);
                yield return null;
            }

            room3DoorWest.position = westOpen;
            room3DoorEast.position = eastOpen;
            Collider2D westCollider = room3DoorWest.GetComponent<Collider2D>();
            Collider2D eastCollider = room3DoorEast.GetComponent<Collider2D>();
            if (westCollider != null) westCollider.enabled = false;
            if (eastCollider != null) eastCollider.enabled = false;
            Physics2D.SyncTransforms();
            if (refreshNavigation != null) refreshNavigation();
            room3DoorOpened = true;

            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, passageEast, .72f, false));
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, passageWest, .8f, false));
            Vector2 playerSideReturnPoint = (Vector2)player.position + Vector2.left * .85f + Vector2.up * .8f;
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, playerSideReturnPoint, .7f, false));

            Physics2D.SyncTransforms();
            if (robotCollider != null)
                robotCollider.enabled = true;
            if (robotAgent != null)
            {
                robotAgent.enabled = true;
                robotAgent.Warp(robotBody.position);
            }
            movement.enabled = true;
            movement.ResumeFollowingPlayer();
            HidePopups();
            room3DoorRoutine = null;
        }
        private IEnumerator FinishDoorTaskWithFailure(string message)
        {
            ShowStatus(message);
            yield return new WaitForSeconds(3f);
            HidePopups();
            if (playerController != null)
                playerController.enabled = true;
            if (movement != null)
                movement.ResumeFollowingPlayer();
            room3DoorRoutine = null;
        }
        private IEnumerator MoveEntryStepToCabinet()
        {
            if (canvas != null && !canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(true);
            ShowStatus("Aa, bunun için girişte gördüğümüz basamağı kullanabiliriz.");
            yield return new WaitForSeconds(3f);
            ShowStatus("Beni burada bekle, getireceğim.");
            yield return new WaitForSeconds(3f);
            HidePopups();

            Vector2 pickupPoint = (Vector2)secondFloorEntryStep.position + Vector2.down * .92f;
            yield return StartCoroutine(MoveRobotForCabinetTask(pickupPoint));
            if (!robotTaskSucceeded)
            {
                yield return StartCoroutine(FinishCabinetHelpWithFailure("Basamağa ulaşamıyorum; yol kapalı görünüyor."));
                yield break;
            }

            BoxCollider2D stepCollider = secondFloorEntryStep.GetComponentInChildren<BoxCollider2D>(true);
            if (stepCollider == null)
            {
                yield return StartCoroutine(FinishCabinetHelpWithFailure("Basamağı taşıyamıyorum."));
                yield break;
            }

            float stepHalfWidth = stepCollider.bounds.extents.x;
            stepCollider.enabled = false;
            secondFloorEntryStep.SetParent(robot, true);
            secondFloorEntryStep.localPosition = new Vector3(0f, .78f, 0f);
            yield return new WaitForSeconds(.45f);

            Collider2D cabinetCollider = secondFloorStepDestinationCabinet.GetComponent<Collider2D>();
            Bounds cabinetBounds = cabinetCollider != null
                ? cabinetCollider.bounds
                : new Bounds(secondFloorStepDestinationCabinet.position, new Vector3(1.28f, 1.55f, 0f));
            float cabinetLeft = cabinetBounds.min.x;
            Vector2 stepDropPoint = new Vector2(cabinetLeft - stepHalfWidth, secondFloorStepDestinationCabinet.position.y);
            Vector2 robotDropPoint = stepDropPoint + Vector2.down * .95f;

            yield return StartCoroutine(MoveRobotForCabinetTask(robotDropPoint));
            if (!robotTaskSucceeded)
            {
                secondFloorEntryStep.SetParent(secondFloorStepDestinationCabinet.root, true);
                secondFloorEntryStep.position = robot.position + Vector3.left * .7f;
                secondFloorEntryStep.rotation = Quaternion.identity;
                stepCollider.enabled = true;
                if (refreshNavigation != null) refreshNavigation();
                yield return StartCoroutine(FinishCabinetHelpWithFailure("Basamağı dolabın yanına kadar getiremedim."));
                yield break;
            }

            secondFloorEntryStep.SetParent(secondFloorStepDestinationCabinet.root, true);
            secondFloorEntryStep.SetPositionAndRotation(
                new Vector3(stepDropPoint.x, stepDropPoint.y, secondFloorEntryStep.position.z),
                Quaternion.identity);
            stepCollider.enabled = true;
            Physics2D.SyncTransforms();
            if (refreshNavigation != null) refreshNavigation();

            movement.HoldPosition();
            yield return new WaitForSeconds(2f);
            yield return StartCoroutine(AnimatePlayerOverCabinet(stepDropPoint, cabinetBounds));
            yield return new WaitForSeconds(.4f);
            yield return StartCoroutine(AnimateRobotOverCabinet(stepDropPoint, cabinetBounds));
            if (playerController != null) playerController.enabled = true;
            movement.ResumeFollowingPlayer();
            secondFloorCabinetHelpRoutine = null;
        }

        private IEnumerator FinishCabinetHelpWithFailure(string message)
        {
            ShowStatus(message, true, 3f);
            yield return new WaitForSeconds(3f);
            if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
            if (playerController != null) playerController.enabled = true;
            secondFloorCabinetHelpRoutine = null;
        }

        private IEnumerator AnimatePlayerOverCabinet(Vector2 stepDropPoint, Bounds cabinetBounds)
        {
            if (playerCollider != null)
                playerCollider.enabled = false;

            float laneY = secondFloorStepDestinationCabinet.position.y;
            yield return StartCoroutine(AnimatePlayerSegment(new Vector2(stepDropPoint.x - .52f, laneY), .42f, false));
            yield return new WaitForSeconds(.15f);
            yield return StartCoroutine(AnimatePlayerSegment(new Vector2(stepDropPoint.x - .22f, laneY), .32f, false));
            yield return new WaitForSeconds(.15f);
            yield return StartCoroutine(AnimatePlayerSegment(new Vector2(stepDropPoint.x + .2f, laneY), .38f, false));
            yield return new WaitForSeconds(.6f);

            Vector2 cabinetFarEdge = new Vector2(cabinetBounds.max.x - .18f, laneY);
            yield return StartCoroutine(AnimatePlayerSegment(cabinetFarEdge, 1.05f, false));

            Vector2 landingPoint = new Vector2(cabinetBounds.max.x + .75f, laneY - .45f);
            yield return StartCoroutine(AnimatePlayerSegment(landingPoint, .68f, true));
            Physics2D.SyncTransforms();
            if (playerCollider != null)
                playerCollider.enabled = true;
            if (playerBody != null)
                playerBody.linearVelocity = Vector2.zero;
            if (playerAnimator != null)
                playerAnimator.SetMotion(Vector2.zero);
            if (player != null)
                player.localScale = playerOriginalScale;
        }

        private IEnumerator AnimatePlayerSegment(Vector2 target, float duration, bool jump)
        {
            yield return StartCoroutine(AnimateActorSegment(
                player, playerBody, playerAnimator, playerOriginalScale, target, duration, jump));
        }

        private IEnumerator AnimateRobotOverCabinet(Vector2 stepDropPoint, Bounds cabinetBounds)
        {
            movement.HoldPosition();
            if (movement != null) movement.enabled = false;
            if (robotAgent != null && robotAgent.enabled) robotAgent.enabled = false;
            if (robotCollider != null) robotCollider.enabled = false;

            float laneY = secondFloorStepDestinationCabinet.position.y;
            Vector2 stepFront = new Vector2(stepDropPoint.x - .52f, laneY);
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale,
                new Vector2(stepFront.x, laneY - .22f), .5f, false));
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, stepFront, .32f, false));
            yield return new WaitForSeconds(.16f);
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale,
                new Vector2(stepDropPoint.x - .22f, laneY), .32f, false));
            yield return new WaitForSeconds(.16f);
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale,
                new Vector2(stepDropPoint.x + .2f, laneY), .38f, false));
            yield return new WaitForSeconds(.45f);

            Vector2 cabinetFarEdge = new Vector2(cabinetBounds.max.x - .18f, laneY);
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, cabinetFarEdge, 1.05f, false));
            Vector2 robotLandingPoint = new Vector2(cabinetBounds.max.x + .75f, laneY - .45f);
            yield return StartCoroutine(AnimateActorSegment(
                robot, robotBody, robotAnimator, robotOriginalScale, robotLandingPoint, .68f, true));
            Physics2D.SyncTransforms();

            if (robotCollider != null) robotCollider.enabled = true;
            if (robotAgent != null)
            {
                robotAgent.enabled = true;
                if (robotBody != null) robotAgent.Warp(robotBody.position);
            }
            if (movement != null) movement.enabled = true;
        }

        private IEnumerator AnimateActorSegment(Transform actor, Rigidbody2D body,
            CharacterAnimator animator, Vector3 originalScale, Vector2 target, float duration, bool jump)
        {
            if (actor == null)
                yield break;

            Vector2 start = body != null ? body.position : (Vector2)actor.position;
            Vector2 direction = target - start;
            if (animator != null && direction.sqrMagnitude > .001f)
                animator.SetMotion(direction.normalized);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 position = Vector2.Lerp(start, target, t);
                if (jump)
                {
                    float arc = Mathf.Sin(t * Mathf.PI);
                    position.y += arc * .18f;
                    actor.localScale = new Vector3(
                        originalScale.x * (1f + arc * .06f),
                        originalScale.y * (1f + arc * .16f),
                        originalScale.z);
                }

                if (body != null)
                    body.MovePosition(position);
                else
                    actor.position = new Vector3(position.x, position.y, actor.position.z);
            }

            if (body != null)
            {
                body.position = target;
                body.linearVelocity = Vector2.zero;
            }
            actor.position = new Vector3(target.x, target.y, actor.position.z);
            actor.localScale = originalScale;
            if (animator != null)
                animator.SetMotion(Vector2.zero);
        }

        private IEnumerator MoveRobotForCabinetTask(Vector2 target)
        {
            robotTaskSucceeded = false;
            if (movement == null || !movement.StartMoveTask(target, false))
                yield break;

            float deadline = Time.unscaledTime + 25f;
            while (movement.IsExecutingTask && Time.unscaledTime < deadline)
                yield return null;

            if (movement.IsExecutingTask)
            {
                movement.CancelTask();
                movement.HoldPosition();
                yield break;
            }

            robotTaskSucceeded = Vector2.Distance(robot.position, target) <= 1.4f;
        }

        public void ShowRepairCompleteMessage()
        {
            batteryHelpDialogueShown = true;
            StopStatusRoutine(false);
            ShowStatus("Artık hazırım, taşıyabilirim bataryayı.");
            StartCoroutine(GoPickUpBatteryAfterDelay());
        }

        public void ShowBatteryInstallPrompt()
        {
            ShowStatus("Bataryayı yerine koyabilirim. T tuşuna bas.", true, 3f);
        }

        private IEnumerator GoPickUpBatteryAfterDelay()
        {
            yield return new WaitForSeconds(3f);
            if (statusPanel != null)
                statusPanel.gameObject.SetActive(false);

            if (movement == null || batterySource == null)
                yield break;

            waitingForBatteryPickup = true;
            movement.StartMoveTask(batterySource.position, false);
        }

        private void LateUpdate()
        {
            if (!initialized || robot == null || worldCamera == null) return;
            Vector3 screen = worldCamera.WorldToScreenPoint(robot.position + Vector3.up * .58f);
            if (introPanel.gameObject.activeSelf) PositionPopup(introPanel, screen, 8f);
            if (statusPanel.gameObject.activeSelf) PositionPopup(statusPanel, screen);
        }
        private void PositionPopup(RectTransform popup, Vector3 characterScreen, float gap = 48f)
        {
            Vector3 screen = characterScreen;
            screen.y += popup.rect.height * canvas.scaleFactor * .5f + gap * canvas.scaleFactor;
            float halfW = popup.rect.width * canvas.scaleFactor * .5f;
            float halfH = popup.rect.height * canvas.scaleFactor * .5f;
            screen.x = Mathf.Clamp(screen.x, halfW + 12f, Screen.width - halfW - 12f);
            screen.y = Mathf.Clamp(screen.y, halfH + 12f, Screen.height - halfH - 12f);
            SetScreenPosition(popup, screen);
        }

        private void SetScreenPosition(RectTransform rect, Vector3 screen)
        {
            screen.z = 0f;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                rect.anchoredPosition = local;
        }

        private RectTransform MakeRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            return rect;
        }

        private Text MakeText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
        {
            var rect = MakeRect(name, parent, Vector2.zero);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignByGeometry = true;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Sprite PanelSprite()
        {
            if (panelSprite == null) panelSprite = MakeShape(64, 12f, new Vector4(14f, 14f, 14f, 14f));
            return panelSprite;
        }

        private static Sprite CircleSprite()
        {
            if (circleSprite == null) circleSprite = MakeShape(64, 32f, Vector4.zero);
            return circleSprite;
        }

        private static Sprite MakeShape(int size, float radius, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Woken route UI shape",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            float center = (size - 1f) * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center) - (center - radius);
                float dy = Mathf.Abs(y - center) - (center - radius);
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                texture.SetPixel(x, y, distance > 0f ? Color.clear : distance > -2.5f ? new Color32(34, 48, 56, 255) : Color.white);
            }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect, border);
        }

        private void OnDestroy()
        {
            if (movement != null) movement.TaskFinished -= OnTaskFinished;
            if (introRoutine != null) StopCoroutine(introRoutine);
        }
    }
}
