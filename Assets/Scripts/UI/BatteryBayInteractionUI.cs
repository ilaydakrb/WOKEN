using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Woken.UI
{
    /// <summary>Handles collecting the depot battery and installing it in the elevator bay.</summary>
    public sealed class BatteryBayInteractionUI : MonoBehaviour
    {
        private static Sprite whiteSprite;
        private static Sprite circleSprite;

        private Transform player;
        private Transform robot;
        private RobotRouteGuideUI routeGuide;
        private GameObject batterySource;
        private Transform batteryBay;
        private SpriteRenderer bayStatusLight;
        private Camera worldCamera;
        private bool hasBattery;
        private bool batteryInstalled;
        private bool wasNearPrompt;
        private float promptHideAt;
        private string activePrompt;
        private GameObject carriedBattery;
        private GameObject insertedBattery;
        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform promptRect;
        private Text promptText;
        private GameObject statusPanel;
        private Image statusIndicator;
        private bool statusOpen;
        private bool wasNearBayWithBattery;

        private const float InteractionDistance = 1.35f;
        public bool BatteryInstalled => batteryInstalled;
        public bool IsNearBatteryBay => player != null && batteryBay != null && batteryBay.gameObject.activeInHierarchy && Vector2.Distance(player.position, batteryBay.position) <= InteractionDistance;

        public void Initialize(Transform playerTransform, Transform robotTransform, GameObject batteryObject,
            Transform bayTransform, Transform bayLight, Camera cameraToUse)
        {
            player = playerTransform;
            robot = robotTransform;
            routeGuide = GetComponent<RobotRouteGuideUI>();
            batterySource = batteryObject;
            batteryBay = bayTransform;
            bayStatusLight = bayLight != null ? bayLight.GetComponent<SpriteRenderer>() : null;
            worldCamera = cameraToUse;
            if (player == null || robot == null || batterySource == null || batteryBay == null || worldCamera == null)
            {
                Debug.LogError("Battery bay interaction needs player, battery, bay, and camera references.", this);
                enabled = false;
                return;
            }

            SetBayStatus(false);
            BuildUI();
        }

        private void Update()
        {
            if (player == null || batteryBay == null || !batteryBay.gameObject.activeInHierarchy)
                return;

            Keyboard keyboard = Keyboard.current;
            if (statusOpen)
            {
                if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame || keyboard.tKey.wasPressedThisFrame))
                    CloseStatusPanel();
                return;
            }

            bool nearBattery = !hasBattery && !batteryInstalled && batterySource != null
                && batterySource.activeInHierarchy
                && Vector2.Distance(player.position, batterySource.transform.position) <= InteractionDistance;
            bool nearBay = Vector2.Distance(player.position, batteryBay.position) <= InteractionDistance;
            string prompt = null;
            Vector3 promptPosition = Vector3.zero;
            if (nearBattery)
            {
                prompt = "Bataryayı taşımak için robottan yardım almalısın.";
                promptPosition = batterySource.transform.position + Vector3.up * .8f;
            }

            if (nearBay && hasBattery && !batteryInstalled)
            {
                if (!wasNearBayWithBattery && routeGuide != null)
                    routeGuide.ShowBatteryInstallPrompt();
                wasNearBayWithBattery = true;
            }
            else
            {
                wasNearBayWithBattery = false;
            }

            UpdatePrompt(prompt, promptPosition);

            if (keyboard == null || !nearBay || batteryInstalled)
                return;

            if (keyboard.fKey.wasPressedThisFrame)
                ShowStatusPanel();
            else if (keyboard.tKey.wasPressedThisFrame && hasBattery)
                InstallBattery();
        }
        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("Battery bay interaction canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 230;
            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            BuildPrompt(canvasRect);
            BuildStatusPanel(canvasRect);
        }

        private void BuildPrompt(RectTransform parent)
        {
            GameObject bubble = new GameObject("Battery interaction prompt",
                typeof(RectTransform), typeof(Image), typeof(Outline));
            promptRect = bubble.GetComponent<RectTransform>();
            promptRect.SetParent(parent, false);
            promptRect.sizeDelta = new Vector2(430f, 76f);
            Image background = bubble.GetComponent<Image>();
            background.sprite = MakeRoundedSprite("Battery prompt bubble", 32, 7f, 10f);
            background.type = Image.Type.Sliced;
            background.color = new Color32(245, 245, 242, 250);
            background.raycastTarget = false;
            Outline outline = bubble.GetComponent<Outline>();
            outline.effectColor = new Color32(30, 35, 37, 220);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject textObject = new GameObject("Battery prompt text", typeof(RectTransform), typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(promptRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 5f);
            textRect.offsetMax = new Vector2(-8f, -5f);
            promptText = textObject.GetComponent<Text>();
            promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptText.fontSize = 22;
            promptText.fontStyle = FontStyle.Bold;
            promptText.color = new Color32(20, 20, 20, 255);
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
            promptText.verticalOverflow = VerticalWrapMode.Truncate;
            promptText.raycastTarget = false;
            bubble.SetActive(false);
        }

        private void BuildStatusPanel(RectTransform parent)
        {
            statusPanel = new GameObject("Battery bay status panel",
                typeof(RectTransform), typeof(Image));
            RectTransform panelRoot = statusPanel.GetComponent<RectTransform>();
            panelRoot.SetParent(parent, false);
            panelRoot.anchorMin = Vector2.zero;
            panelRoot.anchorMax = Vector2.one;
            panelRoot.offsetMin = Vector2.zero;
            panelRoot.offsetMax = Vector2.zero;
            Image dimmer = statusPanel.GetComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, .35f);
            dimmer.raycastTarget = true;

            GameObject frame = new GameObject("Battery bay status frame",
                typeof(RectTransform), typeof(Image), typeof(Outline));
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            frameRect.SetParent(panelRoot, false);
            frameRect.sizeDelta = new Vector2(560f, 560f);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;
            Outline frameOutline = frame.GetComponent<Outline>();
            frameOutline.effectColor = Color.black;
            frameOutline.effectDistance = new Vector2(5f, -5f);

            AddFrameEdge(frameRect, "Inner frame top", new Vector2(500f, 6f), new Vector2(0f, 222f));
            AddFrameEdge(frameRect, "Inner frame bottom", new Vector2(500f, 6f), new Vector2(0f, -222f));
            AddFrameEdge(frameRect, "Inner frame left", new Vector2(6f, 450f), new Vector2(-247f, 0f));
            AddFrameEdge(frameRect, "Inner frame right", new Vector2(6f, 450f), new Vector2(247f, 0f));

            GameObject indicator = new GameObject("Battery status circle",
                typeof(RectTransform), typeof(Image), typeof(Outline));
            RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
            indicatorRect.SetParent(frameRect, false);
            indicatorRect.sizeDelta = new Vector2(44f, 44f);
            indicatorRect.anchoredPosition = new Vector2(0f, 202f);
            statusIndicator = indicator.GetComponent<Image>();
            statusIndicator.sprite = GetCircleSprite();
            statusIndicator.color = new Color32(210, 55, 49, 255);
            statusIndicator.raycastTarget = false;
            Outline indicatorOutline = indicator.GetComponent<Outline>();
            indicatorOutline.effectColor = Color.black;
            indicatorOutline.effectDistance = new Vector2(2f, -2f);

            GameObject close = new GameObject("Close battery bay status",
                typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.SetParent(frameRect, false);
            closeRect.sizeDelta = new Vector2(38f, 38f);
            closeRect.anchoredPosition = new Vector2(238f, 238f);
            Image closeImage = close.GetComponent<Image>();
            closeImage.sprite = GetCircleSprite();
            closeImage.color = new Color32(210, 55, 49, 255);
            Button closeButton = close.GetComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(CloseStatusPanel);
            AddText(closeRect, "Close battery bay X", "×", 28, Color.white, FontStyle.Bold);

            statusPanel.SetActive(false);
        }

        private static void AddFrameEdge(RectTransform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject edge = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            edge.GetComponent<Image>().color = Color.black;
        }

        private void UpdatePrompt(string message, Vector3 worldPosition)
        {
            if (string.IsNullOrEmpty(message))
            {
                wasNearPrompt = false;
                activePrompt = null;
                promptRect.gameObject.SetActive(false);
                return;
            }

            if (!wasNearPrompt || activePrompt != message)
                promptHideAt = Time.unscaledTime + 1.3f;
            wasNearPrompt = true;
            activePrompt = message;
            promptText.text = message;

            bool visible = Time.unscaledTime < promptHideAt;
            promptRect.gameObject.SetActive(visible);
            if (!visible)
                return;

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            if (screenPoint.z <= 0f)
            {
                promptRect.gameObject.SetActive(false);
                return;
            }
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out localPoint))
                promptRect.anchoredPosition = localPoint;
        }

        public void RobotPickedUpBattery()
        {
            if (batteryInstalled || hasBattery || batterySource == null || robot == null)
                return;

            hasBattery = true;
            batterySource.SetActive(false);
            carriedBattery = new GameObject("Battery carried by robot", typeof(SpriteRenderer));
            carriedBattery.transform.SetParent(robot, false);
            carriedBattery.transform.localPosition = new Vector3(.32f, .28f, -.1f);
            carriedBattery.transform.localScale = new Vector3(.3f, .18f, 1f);
            SpriteRenderer renderer = carriedBattery.GetComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
            renderer.color = new Color32(113, 128, 132, 255);
            renderer.sortingOrder = 20;
        }
        private void InstallBattery()
        {
            hasBattery = false;
            batteryInstalled = true;
            if (carriedBattery != null)
                Destroy(carriedBattery);

            insertedBattery = new GameObject("Installed elevator battery", typeof(SpriteRenderer));
            insertedBattery.transform.position = new Vector3(13.12f, -5.72f, 0f);
            if (batteryBay.parent != null)
                insertedBattery.transform.SetParent(batteryBay.parent, true);
            insertedBattery.transform.localScale = new Vector3(.78f, .58f, 1f);
            SpriteRenderer renderer = insertedBattery.GetComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
            renderer.color = new Color32(84, 99, 102, 255);
            renderer.sortingOrder = 3;
            SetBayStatus(true);
        }

        private void SetBayStatus(bool installed)
        {
            if (bayStatusLight != null)
                bayStatusLight.color = installed
                    ? new Color32(65, 205, 112, 255)
                    : new Color32(220, 57, 50, 255);
        }

        private void ShowStatusPanel()
        {
            promptRect.gameObject.SetActive(false);
            statusIndicator.color = batteryInstalled
                ? new Color32(65, 205, 112, 255)
                : new Color32(220, 57, 50, 255);
            statusPanel.SetActive(true);
            statusOpen = true;
        }

        private void CloseStatusPanel()
        {
            statusPanel.SetActive(false);
            statusOpen = false;
        }

        private static Text AddText(RectTransform parent, string name, string value,
            int size, Color color, FontStyle style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "Elevator battery pixel"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
            return whiteSprite;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Battery status circle sprite"
            };
            float center = (size - 1) * .5f;
            float radius = size * .47f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
            }
            texture.Apply(false, true);
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(.5f, .5f), 100f);
            return circleSprite;
        }

        private static Sprite MakeRoundedSprite(string name, int size, float radius, float border)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + .5f;
                float py = y + .5f;
                float nearestX = Mathf.Clamp(px, radius, size - radius);
                float nearestY = Mathf.Clamp(py, radius, size - radius);
                float dx = px - nearestX;
                float dy = py - nearestY;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
            }
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = name;
            return sprite;
        }
    }
}