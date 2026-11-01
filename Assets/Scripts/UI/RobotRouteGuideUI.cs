using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Woken.Characters;

namespace Woken.UI
{
    /// <summary>Opening dialogue and automatic elevator guidance for the robot.</summary>
    public sealed class RobotRouteGuideUI : MonoBehaviour
    {
        private const string OpeningMessage = "Sonunda uyandın. Bu terk edilmiş laboratuvardan acilen çıkmamız lazım. Acele et.";
        private const string DepartureMessage = "Hadi hızlı ol, hemen asansörle çıkmalıyız buradan. Takip et beni.";
        private static Sprite panelSprite;
        private static Sprite circleSprite;

        private Transform player;
        private Transform robot;
        private Transform batterySource;
        private RobotMovementController movement;
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
        private bool resumeFollowAfterStatus;
        private Coroutine statusRoutine;
        private Coroutine introRoutine;

        public void Initialize(Transform playerTransform, Transform robotTransform, RobotMovementController robotMovement, Camera cameraToUse, Transform batterySourceTransform)
        {
            if (initialized) return;
            player = playerTransform;
            robot = robotTransform;
            batterySource = batterySourceTransform;
            movement = robotMovement;
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
            yield return new WaitForSeconds(2f);
            dialogueText.text = DepartureMessage;
            yield return new WaitForSeconds(2f);
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

        private void ShowStatus(string message, bool resumeFollowAfterDelay = false, float displayDuration = 2f)
        {
            StopStatusRoutine(false);
            resumeFollowAfterStatus = false;
            HidePopups();
            bool longMessage = message.Length > 70;
            statusPanel.sizeDelta = longMessage ? new Vector2(760f, 190f) : new Vector2(420f, 122f);
            statusText.fontSize = longMessage ? 24 : 27;
            statusText.text = message;
            statusPanel.gameObject.SetActive(true);

            if (resumeFollowAfterDelay)
            {
                resumeFollowAfterStatus = true;
                statusRoutine = StartCoroutine(CloseStatusAfterDelay(displayDuration));
            }
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
                5f);
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
            ShowStatus("Bataryayı yerine koyabilirim. T tuşuna bas.", true, 2f);
        }

        private IEnumerator GoPickUpBatteryAfterDelay()
        {
            yield return new WaitForSeconds(1f);
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