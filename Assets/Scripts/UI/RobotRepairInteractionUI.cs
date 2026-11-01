using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Woken.UI
{
    /// <summary>Collects repair tools and shows a non-blocking view of the robot's open back.</summary>
    public sealed class RobotRepairInteractionUI : MonoBehaviour
    {
        private enum RepairMaterial { None, Wrench, Pliers, SpareParts }

        private static Sprite pixelSprite;
        private static Sprite roundedSprite;
        private static Sprite circleSprite;

        private Transform player;
        private Transform robot;
        private Transform materialTable;
        private Transform wrenchTarget;
        private Transform pliersTarget;
        private Transform sparePartsTarget;
        private Transform[] wrenchVisuals;
        private Transform[] pliersVisuals;
        private Transform[] sparePartsVisuals;
        private Camera worldCamera;
        private RobotRouteGuideUI routeGuide;

        private RepairMaterial heldMaterial;
        private bool backOpen;
        private bool oldPartRemoved;
        private bool replacementInstalled;
        private bool wrenchUsed;
        private bool pliersUsed;
        private float tableHintUntil;
        private float repairDialogueAt = -1f;
        private float promptHideAt;
        private string activePrompt;

        private RectTransform canvasRect;
        private RectTransform promptRect;
        private Text promptText;
        private RectTransform interiorPanel;
        private Image oldPartImage;
        private Image replacementImage;

        private const float InteractionDistance = 1.35f;
        public bool IsRepaired => replacementInstalled;
        public bool IsBackOpen => backOpen;

        public void Initialize(Transform playerTransform, Transform robotTransform,
            Transform materialTableTransform, Transform wrenchPickup, Transform pliersPickup,
            Transform sparePartsPickup, Transform[] wrenchParts, Transform[] pliersParts,
            Transform[] spareParts, Camera cameraToUse)
        {
            player = playerTransform;
            robot = robotTransform;
            materialTable = materialTableTransform;
            wrenchTarget = wrenchPickup;
            pliersTarget = pliersPickup;
            sparePartsTarget = sparePartsPickup;
            wrenchVisuals = wrenchParts;
            pliersVisuals = pliersParts;
            sparePartsVisuals = spareParts;
            worldCamera = cameraToUse;
            routeGuide = GetComponent<RobotRouteGuideUI>();

            if (player == null || robot == null || materialTable == null || worldCamera == null)
            {
                Debug.LogError("Robot repair interaction needs player, robot, materials table, and camera.", this);
                enabled = false;
                return;
            }

            BuildUI();
        }

        private void Update()
        {
            if (player == null || robot == null)
                return;

            if (repairDialogueAt >= 0f && Time.unscaledTime >= repairDialogueAt)
            {
                repairDialogueAt = -1f;
                backOpen = false;
                interiorPanel.gameObject.SetActive(false);
                if (routeGuide != null)
                    routeGuide.ShowRepairCompleteMessage();
            }

            UpdatePrompt();
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.fKey.wasPressedThisFrame && heldMaterial == RepairMaterial.None)
            {
                if (!wrenchUsed)
                {
                    TryPickUp(wrenchTarget, wrenchVisuals, RepairMaterial.Wrench);
                    return;
                }

                if (!pliersUsed)
                {
                    TryPickUp(pliersTarget, pliersVisuals, RepairMaterial.Pliers);
                    return;
                }

                if (!replacementInstalled)
                    TryPickUp(sparePartsTarget, sparePartsVisuals, RepairMaterial.SpareParts);
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame && backOpen)
            {
                backOpen = false;
                interiorPanel.gameObject.SetActive(false);
            }

            if (keyboard.tKey.wasPressedThisFrame && repairDialogueAt < 0f)
                UseMaterialOnRobot();
        }
        private bool TryPickUp(Transform target, Transform[] visuals, RepairMaterial material)
        {
            if (target == null || !target.gameObject.activeInHierarchy
                || Vector2.Distance(player.position, target.position) > InteractionDistance)
                return false;

            heldMaterial = material;
            HideVisuals(visuals);
            tableHintUntil = Time.unscaledTime + 3f;
            return true;
        }

        private static void ShowVisuals(Transform[] visuals)
        {
            if (visuals == null) return;
            foreach (Transform visual in visuals)
                if (visual != null)
                    visual.gameObject.SetActive(true);
        }
        private static void HideVisuals(Transform[] visuals)
        {
            if (visuals == null) return;
            foreach (Transform visual in visuals)
                if (visual != null)
                    visual.gameObject.SetActive(false);
        }

        private void UseMaterialOnRobot()
        {
            if (!backOpen)
            {
                if (heldMaterial != RepairMaterial.Wrench)
                    return;

                heldMaterial = RepairMaterial.None;
                wrenchUsed = true;
                ShowVisuals(wrenchVisuals);
                backOpen = true;
                interiorPanel.gameObject.SetActive(true);
                RefreshInterior();
                return;
            }

            if (heldMaterial == RepairMaterial.Pliers && !oldPartRemoved)
            {
                heldMaterial = RepairMaterial.None;
                pliersUsed = true;
                ShowVisuals(pliersVisuals);
                oldPartRemoved = true;
                RefreshInterior();
            }
            else if (heldMaterial == RepairMaterial.SpareParts && oldPartRemoved && !replacementInstalled)
            {
                heldMaterial = RepairMaterial.None;
                replacementInstalled = true;
                RefreshInterior();
                repairDialogueAt = Time.unscaledTime + 1f;
            }
            else if (heldMaterial == RepairMaterial.None)
            {
                backOpen = false;
                interiorPanel.gameObject.SetActive(false);
            }
        }
        private void UpdatePrompt()
        {
            bool nearAnyMaterial = heldMaterial == RepairMaterial.None
                && (IsNearAvailable(wrenchTarget) || IsNearAvailable(pliersTarget) || IsNearAvailable(sparePartsTarget));
            string message = null;
            Vector3 position = Vector3.zero;
            float duration = 1.3f;

            if (Time.unscaledTime < tableHintUntil && heldMaterial != RepairMaterial.None)
            {
                message = "Elindeki malzemeyi robot üzerinde kullanmak için T tuşuna bas.";
                position = materialTable.position + Vector3.up * .9f;
                duration = 3f;
            }
            else if (nearAnyMaterial)
            {
                message = "Masanın üstündeki malzemeleri F tuşu ile alabilirsin.";
                position = materialTable.position + Vector3.up * .9f;
            }

            if (string.IsNullOrEmpty(message))
            {
                activePrompt = null;
                promptRect.gameObject.SetActive(false);
                return;
            }

            if (activePrompt != message)
                promptHideAt = Time.unscaledTime + duration;
            activePrompt = message;
            promptText.text = message;
            bool visible = Time.unscaledTime < promptHideAt;
            promptRect.gameObject.SetActive(visible);
            if (!visible)
                return;

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(position);
            if (screenPoint.z <= 0f)
            {
                promptRect.gameObject.SetActive(false);
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                screenPoint, null, out Vector2 localPoint))
                promptRect.anchoredPosition = localPoint;
        }

        private bool IsNearAvailable(Transform target)
        {
            return target != null && target.gameObject.activeInHierarchy
                && Vector2.Distance(player.position, target.position) <= InteractionDistance;
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("Robot repair side UI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            BuildPrompt();
            BuildInteriorPanel();
        }

        private void BuildPrompt()
        {
            GameObject bubble = new GameObject("Repair materials prompt",
                typeof(RectTransform), typeof(Image), typeof(Outline));
            promptRect = bubble.GetComponent<RectTransform>();
            promptRect.SetParent(canvasRect, false);
            promptRect.sizeDelta = new Vector2(430f, 76f);
            Image background = bubble.GetComponent<Image>();
            background.sprite = RoundedSprite();
            background.type = Image.Type.Sliced;
            background.color = new Color32(245, 245, 242, 250);
            background.raycastTarget = false;
            Outline outline = bubble.GetComponent<Outline>();
            outline.effectColor = new Color32(30, 35, 37, 220);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject textObject = new GameObject("Repair materials prompt text",
                typeof(RectTransform), typeof(Text));
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

        private void BuildInteriorPanel()
        {
            GameObject panel = new GameObject("Robot wiring compartment view",
                typeof(RectTransform), typeof(Image), typeof(Outline));
            interiorPanel = panel.GetComponent<RectTransform>();
            interiorPanel.SetParent(canvasRect, false);
            interiorPanel.anchorMin = interiorPanel.anchorMax = new Vector2(1f, .5f);
            interiorPanel.pivot = new Vector2(1f, .5f);
            interiorPanel.anchoredPosition = new Vector2(-24f, 0f);
            interiorPanel.sizeDelta = new Vector2(350f, 460f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color32(248, 247, 241, 255);
            background.raycastTarget = false;
            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -4f);

            AddEdge(interiorPanel, "Compartment frame top", new Vector2(300f, 5f), new Vector2(0f, 195f));
            AddEdge(interiorPanel, "Compartment frame bottom", new Vector2(300f, 5f), new Vector2(0f, -195f));
            AddEdge(interiorPanel, "Compartment frame left", new Vector2(5f, 390f), new Vector2(-147f, 0f));
            AddEdge(interiorPanel, "Compartment frame right", new Vector2(5f, 390f), new Vector2(147f, 0f));

            AddSolid(interiorPanel, "Cable box", new Vector2(230f, 255f), new Vector2(0f, -12f),
                Color.white, true);
            AddLine(interiorPanel, "Red cable upper", new Vector2(105f, 8f), new Vector2(-36f, 88f), new Color32(185, 67, 61, 255));
            AddLine(interiorPanel, "Yellow cable upper", new Vector2(92f, 8f), new Vector2(42f, 88f), new Color32(193, 157, 66, 255));
            AddLine(interiorPanel, "Blue cable left", new Vector2(8f, 128f), new Vector2(-66f, 0f), new Color32(74, 123, 166, 255));
            AddLine(interiorPanel, "Green cable right", new Vector2(8f, 128f), new Vector2(66f, 0f), new Color32(75, 146, 100, 255));
            AddLine(interiorPanel, "Red cable lower", new Vector2(96f, 8f), new Vector2(-37f, -87f), new Color32(185, 67, 61, 255));
            AddLine(interiorPanel, "Yellow cable lower", new Vector2(88f, 8f), new Vector2(40f, -87f), new Color32(193, 157, 66, 255));

            oldPartImage = AddSolid(interiorPanel, "Old control module", new Vector2(95f, 72f),
                new Vector2(0f, -12f), new Color32(109, 123, 126, 255), true);
            replacementImage = AddSolid(interiorPanel, "Replacement control module", new Vector2(95f, 72f),
                new Vector2(0f, -12f), new Color32(78, 170, 110, 255), true);
            replacementImage.gameObject.SetActive(false);
            interiorPanel.gameObject.SetActive(false);
        }

        private void RefreshInterior()
        {
            oldPartImage.gameObject.SetActive(!oldPartRemoved && !replacementInstalled);
            replacementImage.gameObject.SetActive(replacementInstalled);
        }

        private static Image AddEdge(RectTransform parent, string name, Vector2 size, Vector2 position)
        {
            return AddSolid(parent, name, size, position, Color.black, false);
        }

        private static Image AddLine(RectTransform parent, string name, Vector2 size,
            Vector2 position, Color color)
        {
            return AddSolid(parent, name, size, position, color, false);
        }

        private static Image AddSolid(RectTransform parent, string name, Vector2 size,
            Vector2 position, Color color, bool outlined)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = go.GetComponent<Image>();
            image.sprite = PixelSprite();
            image.color = color;
            image.raycastTarget = false;
            if (outlined)
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, -2f);
            }
            return image;
        }

        private static Sprite PixelSprite()
        {
            if (pixelSprite != null) return pixelSprite;
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "Robot repair pixel"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
            return pixelSprite;
        }

        private static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            const int size = 32;
            const float radius = 7f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Robot repair prompt bubble"
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
            roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10f, 10f, 10f, 10f));
            return roundedSprite;
        }
    }
}