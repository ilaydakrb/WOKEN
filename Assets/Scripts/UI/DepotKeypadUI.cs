using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NavMeshPlus.Components;

namespace Woken.UI
{
    /// <summary>Four-digit keypad used to unlock the depot entrance.</summary>
    public sealed class DepotKeypadUI : MonoBehaviour
    {
        private const string CorrectCode = "4134";
        private static Sprite panelSprite;
        private static Sprite keySprite;

        private Transform player;
        private Transform robot;
        private Transform keypad;
        private Transform door;
        private Collider2D doorCollider;
        private Vector3 doorClosedPosition;
        private Quaternion doorClosedRotation;
        private GameObject overlay;
        private RectTransform keypadPanel;
        private Image[] indicators;
        private Text statusText;
        private string enteredCode = string.Empty;
        private bool doorOpen;
        private bool closingDoor;
        private bool isOpen;
        private GameObject proximityPrompt;
        private RectTransform proximityPromptRect;
        private RectTransform promptCanvasRect;
        private Camera promptCamera;
        private bool wasNearKeypad;
        private float promptHideAt;
        private bool actorsEnteredDepot;
        private float actorsExitedAt = -1f;

        public void Initialize(
            Transform playerTransform,
            Transform robotTransform,
            Transform keypadTransform,
            Transform doorTransform)
        {
            player = playerTransform;
            robot = robotTransform;
            keypad = keypadTransform;
            door = doorTransform;
            if (player == null || robot == null || keypad == null || door == null)
            {
                Debug.LogError("Depot keypad needs player, panel, and door references.", this);
                enabled = false;
                return;
            }

            doorCollider = door.GetComponent<Collider2D>();
            doorClosedPosition = door.position;
            doorClosedRotation = door.rotation;
            BuildUI();
        }

        private void Update()
        {
            UpdateProximityPrompt();
            if (doorOpen)
            {
                UpdateAutoClose();
                return;
            }
            if (closingDoor)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (isOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
                    Close();
                else if (keyboard.backspaceKey.wasPressedThisFrame && enteredCode.Length > 0)
                {
                    enteredCode = enteredCode.Substring(0, enteredCode.Length - 1);
                    RefreshIndicators();
                }
                else
                {
                    for (int digit = 0; digit <= 9; digit++)
                    {
                        if (!WasDigitPressed(keyboard, digit))
                            continue;

                        AddDigit(digit);
                        break;
                    }
                }
                return;
            }

            if (!keyboard.fKey.wasPressedThisFrame)
                return;

            if (Vector2.Distance(player.position, keypad.position) <= 1.35f)
                Open();
        }

        private void BuildUI()
        {
            overlay = new GameObject("Depot keypad overlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvas.pixelPerfect = true;

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject dimmer = new GameObject("Keypad background dimmer",
                typeof(RectTransform), typeof(Image));
            RectTransform dimmerRect = dimmer.GetComponent<RectTransform>();
            dimmerRect.SetParent(overlayRect, false);
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;
            dimmer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

            GameObject panel = new GameObject("Four digit keypad", typeof(RectTransform), typeof(Image));
            keypadPanel = panel.GetComponent<RectTransform>();
            keypadPanel.SetParent(overlayRect, false);
            keypadPanel.sizeDelta = new Vector2(440f, 660f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.sprite = GetPanelSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color32(244, 244, 241, 255);
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = Color.black;
            panelOutline.effectDistance = new Vector2(3f, -3f);
            CreateCloseButton();

            indicators = new Image[4];
            for (int i = 0; i < indicators.Length; i++)
            {
                GameObject dot = new GameObject("Password digit indicator " + (i + 1),
                    typeof(RectTransform), typeof(Image));
                RectTransform rect = dot.GetComponent<RectTransform>();
                rect.SetParent(keypadPanel, false);
                rect.sizeDelta = new Vector2(22f, 22f);
                rect.anchoredPosition = new Vector2((i - 1.5f) * 46f, 255f);
                Image image = dot.GetComponent<Image>();
                image.sprite = GetKeySprite();
                image.color = new Color32(151, 151, 151, 255);
                indicators[i] = image;
            }

            int digit = 1;
            float[] rowY = { 145f, 25f, -95f };
            float[] columnX = { -132f, 0f, 132f };
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    CreateDigitButton(digit.ToString(), digit,
                        new Vector2(columnX[column], rowY[row]));
                    digit++;
                }
            }
            CreateDigitButton("0", 0, new Vector2(0f, -215f));

            statusText = CreateText("Keypad status", keypadPanel, "4 haneli şifreyi girin", 20);
            statusText.rectTransform.anchoredPosition = new Vector2(0f, -292f);
            statusText.rectTransform.sizeDelta = new Vector2(380f, 34f);
            statusText.color = new Color32(45, 45, 45, 255);

            overlay.SetActive(false);
            BuildProximityPrompt();
        }

        private void BuildProximityPrompt()
        {
            GameObject canvasObject = new GameObject("Depot keypad instruction canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            promptCanvasRect = canvasObject.GetComponent<RectTransform>();
            promptCanvasRect.anchorMin = Vector2.zero;
            promptCanvasRect.anchorMax = Vector2.one;
            promptCanvasRect.offsetMin = Vector2.zero;
            promptCanvasRect.offsetMax = Vector2.zero;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject bubble = new GameObject("Keypad instruction bubble",
                typeof(RectTransform), typeof(Image));
            proximityPromptRect = bubble.GetComponent<RectTransform>();
            proximityPromptRect.SetParent(promptCanvasRect, false);
            proximityPromptRect.sizeDelta = new Vector2(430f, 76f);
            Image background = bubble.GetComponent<Image>();
            background.sprite = GetPanelSprite();
            background.type = Image.Type.Sliced;
            background.color = new Color32(245, 245, 242, 250);
            background.raycastTarget = false;

            Text label = CreateText("Keypad instruction text", proximityPromptRect,
                "Şifreyi girmek için F’ye tıklayınız.", 22);
            label.fontStyle = FontStyle.Bold;
            label.color = new Color32(20, 20, 20, 255);
            label.raycastTarget = false;
            Stretch(label.rectTransform);

            proximityPrompt = bubble;
            promptCamera = Camera.main;
            proximityPrompt.SetActive(false);
        }

        private void UpdateProximityPrompt()
        {
            if (proximityPrompt == null || player == null || keypad == null)
                return;

            bool isNearKeypad = Vector2.Distance(player.position, keypad.position) <= 1.35f;
            if (isNearKeypad && !wasNearKeypad)
                promptHideAt = Time.unscaledTime + 1.3f;
            wasNearKeypad = isNearKeypad;

            bool shouldShow = isNearKeypad && Time.unscaledTime < promptHideAt
                && !doorOpen && !closingDoor && !isOpen;
            proximityPrompt.SetActive(shouldShow);
            if (!shouldShow)
                return;

            if (promptCamera == null)
                promptCamera = Camera.main;
            if (promptCamera == null)
                return;

            Vector3 screenPoint = promptCamera.WorldToScreenPoint(keypad.position + Vector3.up * .7f);
            if (screenPoint.z <= 0f)
            {
                proximityPrompt.SetActive(false);
                return;
            }

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                promptCanvasRect, screenPoint, null, out localPoint))
                proximityPromptRect.anchoredPosition = localPoint;
        }
        private void CreateCloseButton()
        {
            GameObject closeObject = new GameObject("Close keypad button",
                typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = closeObject.GetComponent<RectTransform>();
            rect.SetParent(keypadPanel, false);
            rect.sizeDelta = new Vector2(38f, 38f);
            rect.anchoredPosition = new Vector2(184f, 302f);

            Image image = closeObject.GetComponent<Image>();
            image.sprite = GetKeySprite();
            image.color = new Color32(215, 57, 54, 255);
            Button button = closeObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Close);

            Text label = CreateText("Close keypad X", rect, "×", 25);
            label.color = Color.white;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            Stretch(label.rectTransform);
        }

        private void CreateDigitButton(string title, int digit, Vector2 position)
        {
            GameObject buttonObject = new GameObject("Key " + title,
                typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(keypadPanel, false);
            rect.sizeDelta = new Vector2(100f, 100f);
            rect.anchoredPosition = position;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = GetKeySprite();
            image.color = new Color32(250, 250, 248, 255);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => AddDigit(digit));

            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            Text text = CreateText("Digit " + title, rect, title, 48);
            text.color = Color.black;
            text.fontStyle = FontStyle.Normal;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
        }

        private static bool WasDigitPressed(Keyboard keyboard, int digit)
        {
            switch (digit)
            {
                case 0: return keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame;
                case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
                case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
                case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
                case 4: return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
                case 5: return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
                case 6: return keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame;
                case 7: return keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame;
                case 8: return keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame;
                case 9: return keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame;
                default: return false;
            }
        }

        private void AddDigit(int digit)
        {
            if (!isOpen || enteredCode.Length >= 4)
                return;

            enteredCode += digit.ToString();
            RefreshIndicators();
            if (enteredCode.Length == 4)
                StartCoroutine(CheckCode());
        }

        private IEnumerator CheckCode()
        {
            if (enteredCode == CorrectCode)
            {
                statusText.text = "Şifre doğru. Kapı açılıyor.";
                yield return StartCoroutine(OpenDoor());
                yield break;
            }

            statusText.text = "Şifre yanlış.";
            for (int i = 0; i < indicators.Length; i++)
                indicators[i].color = new Color32(215, 57, 54, 255);
            yield return new WaitForSecondsRealtime(1f);
            enteredCode = string.Empty;
            statusText.text = "4 haneli şifreyi girin";
            RefreshIndicators();
        }

        private IEnumerator OpenDoor()
        {
            doorOpen = true;
            actorsEnteredDepot = false;
            actorsExitedAt = -1f;
            Vector3 closedPosition = doorClosedPosition;
            // Swing the leaf into the depot and park it against the north wall,
            // leaving a clear passage underneath it.
            Vector3 openPosition = doorClosedPosition + Vector3.right * 0.75f + Vector3.up * 0.75f;
            Quaternion openRotation = doorClosedRotation * Quaternion.Euler(0f, 0f, 90f);
            float elapsed = 0f;
            const float duration = 0.55f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                door.position = Vector3.Lerp(closedPosition, openPosition, t);
                door.rotation = Quaternion.Slerp(doorClosedRotation, openRotation, t);
                yield return null;
            }

            door.position = openPosition;
            door.rotation = openRotation;
            RefreshNavigation();

            yield return new WaitForSecondsRealtime(0.8f);
            Close();
        }

        private void UpdateAutoClose()
        {
            if (closingDoor)
                return;

            bool playerInside = IsInsideDepot(player.position);
            bool robotInside = IsInsideDepot(robot.position);
            bool playerAtDoorway = IsAtDepotDoorway(player.position);
            bool robotAtDoorway = IsAtDepotDoorway(robot.position);
            if (playerInside || robotInside)
                actorsEnteredDepot = true;

            if (playerInside || robotInside || playerAtDoorway || robotAtDoorway)
            {
                actorsExitedAt = -1f;
                return;
            }

            if (!actorsEnteredDepot)
                return;

            if (actorsExitedAt < 0f)
            {
                actorsExitedAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - actorsExitedAt >= 3f)
                StartCoroutine(CloseDoorAfterExit());
        }

        private static bool IsInsideDepot(Vector3 position)
        {
            return position.x > 6.3f && position.y > 0f && position.y < 8f;
        }

        private static bool IsAtDepotDoorway(Vector3 position)
        {
            return position.x >= 5.5f && position.x <= 6.5f
                && position.y >= 6.2f && position.y <= 7.9f;
        }

        private IEnumerator CloseDoorAfterExit()
        {
            closingDoor = true;
            Vector3 openPosition = door.position;
            Quaternion openRotation = door.rotation;
            float elapsed = 0f;
            const float duration = 0.55f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                door.position = Vector3.Lerp(openPosition, doorClosedPosition, t);
                door.rotation = Quaternion.Slerp(openRotation, doorClosedRotation, t);
                yield return null;
            }

            door.position = doorClosedPosition;
            door.rotation = doorClosedRotation;
            if (doorCollider != null)
                doorCollider.enabled = true;
            RefreshNavigation();

            doorOpen = false;
            closingDoor = false;
            actorsEnteredDepot = false;
            actorsExitedAt = -1f;
        }

        private void RefreshNavigation()
        {
            Physics2D.SyncTransforms();
            NavMeshSurface surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface != null)
                surface.BuildNavMesh();

            NavMeshAgent[] agents = UnityEngine.Object.FindObjectsByType<NavMeshAgent>();
            foreach (NavMeshAgent agent in agents)
                if (agent.enabled && agent.isOnNavMesh)
                    agent.ResetPath();
        }

        private void Open()
        {
            if (doorOpen || closingDoor)
                return;
            enteredCode = string.Empty;
            statusText.text = "4 haneli şifreyi girin";
            RefreshIndicators();
            overlay.SetActive(true);
            isOpen = true;
        }

        private void Close()
        {
            if (overlay != null)
                overlay.SetActive(false);
            isOpen = false;
            enteredCode = string.Empty;
            if (statusText != null)
                statusText.text = "4 haneli şifreyi girin";
            RefreshIndicators();
        }

        private void RefreshIndicators()
        {
            if (indicators == null)
                return;
            for (int i = 0; i < indicators.Length; i++)
                indicators[i].color = i < enteredCode.Length
                    ? new Color32(25, 25, 25, 255)
                    : new Color32(151, 151, 151, 255);
        }

        private static Text CreateText(string objectName, Transform parent, string value, int size)
        {
            GameObject textObject = new GameObject(objectName,
                typeof(RectTransform), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite GetPanelSprite()
        {
            if (panelSprite != null) return panelSprite;
            panelSprite = MakeRoundedSprite("Depot keypad panel sprite", 64, 14f, 16f);
            return panelSprite;
        }

        private static Sprite GetKeySprite()
        {
            if (keySprite != null) return keySprite;
            const int size = 64;
            const float center = 31.5f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Depot keypad key sprite"
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                texture.SetPixel(x, y, radius <= 29f ? Color.white : Color.clear);
            }
            texture.Apply(false, true);
            keySprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), 100f);
            keySprite.name = "Depot keypad key sprite";
            return keySprite;
        }

        private static Sprite MakeRoundedSprite(string spriteName, int size, float radius, float border)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = spriteName
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
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = spriteName;
            return sprite;
        }
    }
}
