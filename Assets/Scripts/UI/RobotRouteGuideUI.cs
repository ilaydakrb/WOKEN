using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Woken.Characters;

namespace Woken.UI
{
    /// <summary>Opening message and first-floor route choices for the robot guide.</summary>
    public sealed class RobotRouteGuideUI : MonoBehaviour
    {
        private const string OpeningMessage = "Sonunda uyandın. Bu terk edilmiş laboratuvardan acilen çıkmamız lazım. Acele et.";
        private static Sprite panelSprite;
        private static Sprite circleSprite;

        private Transform player;
        private Transform robot;
        private RobotMovementController movement;
        private Camera worldCamera;
        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform questionButton;
        private RectTransform introPanel;
        private RectTransform routePanel;
        private RectTransform destinationPanel;
        private RectTransform statusPanel;
        private Text statusText;
        private bool initialized;
        private bool introDismissed;
        private bool waitingForRoute;
        private bool selectedDepot;
        private bool resumeFollowAfterStatus;
        private Coroutine statusRoutine;
        private Coroutine introRoutine;

        public void Initialize(Transform playerTransform, Transform robotTransform, RobotMovementController robotMovement, Camera cameraToUse)
        {
            if (initialized) return;
            player = playerTransform;
            robot = robotTransform;
            movement = robotMovement;
            worldCamera = cameraToUse != null ? cameraToUse : Camera.main;
            if (player == null || robot == null || movement == null || worldCamera == null)
            {
                Debug.LogError("Robot route guide needs player, robot, movement, and camera references.", this);
                return;
            }

            EnsureEventSystem();
            BuildCanvas();
            BuildQuestionButton();
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

        private void BuildQuestionButton()
        {
            questionButton = MakeRect("Robot route question button", canvas.transform, new Vector2(38f, 38f));
            var image = questionButton.gameObject.AddComponent<Image>();
            image.sprite = CircleSprite();
            image.color = new Color32(220, 65, 62, 255);
            var button = questionButton.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(ToggleRouteMenu);
            Text label = MakeText("Question mark", questionButton, "?", 25, Color.white, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
            questionButton.gameObject.SetActive(false);
        }

        private void BuildPopups()
        {
            introPanel = CreatePanel("Robot opening message", new Vector2(760f, 154f));
            Text introText = MakeText("Opening message text", introPanel, OpeningMessage, 29, Color.black, TextAnchor.MiddleCenter);
            Stretch(introText.rectTransform, 38f, 22f, 38f, 22f);
            AddCloseButton(introPanel, DismissOpeningMessage);

            routePanel = CreatePanel("Robot route menu", new Vector2(280f, 124f));
            CreateButton(routePanel, "Route button", "Rota", new Vector2(170f, 54f), new Vector2(0f, -10f), OpenDestinations);
            AddCloseButton(routePanel, HidePopups);
            routePanel.gameObject.SetActive(false);

            destinationPanel = CreatePanel("Route destination menu", new Vector2(340f, 188f));
            CreateButton(destinationPanel, "Depot destination", "Depo", new Vector2(235f, 52f), new Vector2(0f, 25f), () => SelectDestination(true));
            CreateButton(destinationPanel, "Elevator destination", "Asansör Odası", new Vector2(235f, 52f), new Vector2(0f, -38f), () => SelectDestination(false));
            AddCloseButton(destinationPanel, HidePopups);
            destinationPanel.gameObject.SetActive(false);

            statusPanel = CreatePanel("Robot route status", new Vector2(330f, 122f));
            statusText = MakeText("Robot status text", statusPanel, string.Empty, 27, Color.black, TextAnchor.MiddleCenter);
            Stretch(statusText.rectTransform, 34f, 20f, 34f, 18f);
            AddCloseButton(statusPanel, CloseStatusAndResume);
            statusPanel.gameObject.SetActive(false);
        }

        private IEnumerator ShowOpeningMessage()
        {
            introPanel.gameObject.SetActive(true);
            yield return new WaitForSeconds(3f);
            introRoutine = null;
            DismissOpeningMessage();
        }

        private void DismissOpeningMessage()
        {
            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }
            introPanel.gameObject.SetActive(false);
            introDismissed = true;
            questionButton.gameObject.SetActive(true);
        }

        private void ToggleRouteMenu()
        {
            if (routePanel.gameObject.activeSelf || destinationPanel.gameObject.activeSelf)
            {
                HidePopups();
                return;
            }

            if (statusPanel.gameObject.activeSelf)
                CloseStatusAndResume();

            HidePopups();
            routePanel.gameObject.SetActive(true);
        }
        private void OpenDestinations()
        {
            routePanel.gameObject.SetActive(false);
            destinationPanel.gameObject.SetActive(true);
        }

        private void SelectDestination(bool depot)
        {
            HidePopups();
            Vector2 playerPosition = player.position;

            if (depot && IsInDepot(playerPosition))
            {
                ShowStatus("Şu anda depodayız.", true);
                return;
            }

            if (!depot && IsInElevatorRoom(playerPosition))
            {
                ShowStatus("Şu anda asansör odasındayız.", true);
                return;
            }

            selectedDepot = depot;
            Vector2 destination = depot ? new Vector2(8f, 5.5f) : new Vector2(8f, -4f);
            waitingForRoute = true;
            if (!movement.StartMoveTask(destination, false))
                waitingForRoute = false;
        }

        private static bool IsInDepot(Vector2 position)
        {
            return position.x > 6f && position.x < 14f && position.y > .5f && position.y < 8f;
        }

        private static bool IsInElevatorRoom(Vector2 position)
        {
            return position.x > 6f && position.x < 14f && position.y > -8f && position.y < -.2f;
        }

        private void OnTaskFinished(RobotTaskResult result)
        {
            if (!waitingForRoute) return;
            waitingForRoute = false;
            bool robotReachedRoom = selectedDepot
                ? IsInDepot(robot.position)
                : IsInElevatorRoom(robot.position);

            if (result == RobotTaskResult.Completed || robotReachedRoom)
                ShowStatus(selectedDepot ? "Depoya geldik." : "Asansör odasına geldik.", true);
            else if (result == RobotTaskResult.NoRoute)
                ShowStatus("Oraya ulaşamıyorum; yol kapalı görünüyor.", true);
        }

        private void ShowStatus(string message, bool resumeFollowAfterDelay = false)
        {
            StopStatusRoutine(false);
            resumeFollowAfterStatus = false;
            HidePopups();
            statusText.text = message;
            statusPanel.gameObject.SetActive(true);

            if (resumeFollowAfterDelay)
            {
                resumeFollowAfterStatus = true;
                statusRoutine = StartCoroutine(CloseStatusAfterDelay());
            }
        }

        private IEnumerator CloseStatusAfterDelay()
        {
            yield return new WaitForSeconds(2f);
            statusRoutine = null;
            statusPanel.gameObject.SetActive(false);
            ResumeFollowAfterStatus();
        }

        private void CloseStatusAndResume()
        {
            StopStatusRoutine(false);
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
            if (routePanel != null) routePanel.gameObject.SetActive(false);
            if (destinationPanel != null) destinationPanel.gameObject.SetActive(false);
            if (statusPanel != null) statusPanel.gameObject.SetActive(false);
        }

        private void CreateButton(RectTransform parent, string name, string label, Vector2 size, Vector2 position, Action onClick)
        {
            RectTransform rect = MakeRect(name, parent, size);
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = PanelSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color32(240, 242, 239, 255);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            Text text = MakeText(name + " label", rect, label, 26, Color.black, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 4f, 2f, 4f, 2f);
        }

        private void AddCloseButton(RectTransform parent, Action onClose)
        {
            RectTransform close = MakeRect("Close popup button", parent, new Vector2(34f, 34f));
            close.anchorMin = close.anchorMax = Vector2.one;
            close.pivot = new Vector2(.5f, .5f);
            close.anchoredPosition = new Vector2(-5f, -5f);
            var image = close.gameObject.AddComponent<Image>();
            image.sprite = CircleSprite();
            image.color = new Color32(220, 65, 62, 255);
            var button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClose());
            Text text = MakeText("Close X", close, "X", 18, Color.white, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
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

        private void LateUpdate()
        {
            if (!initialized || robot == null || worldCamera == null) return;
            Vector3 screen = worldCamera.WorldToScreenPoint(robot.position + Vector3.up * .58f);
            bool visible = screen.z > 0f;
            questionButton.gameObject.SetActive(introDismissed && visible && !movement.IsExecutingTask);
            if (visible)
                SetScreenPosition(questionButton, screen + Vector3.up * (12f * canvas.scaleFactor));

            if (introPanel.gameObject.activeSelf) PositionPopup(introPanel, screen, 8f);
            if (routePanel.gameObject.activeSelf) PositionPopup(routePanel, screen);
            if (destinationPanel.gameObject.activeSelf) PositionPopup(destinationPanel, screen);
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