using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Woken.UI
{
    /// <summary>Opens the Gemma notebook found in the second-floor hidden drawer.</summary>
    public sealed class HiddenDrawerNotebookUI : MonoBehaviour
    {
        private Transform player;
        private Transform drawer;
        private System.Func<bool> drawerIsOpen;
        private WorldObjectNameUI nameUI;
        private GameObject panel;
        private GameObject coverView;
        private GameObject pagesView;
        private bool isOpen;
        private bool wasNear;

        private const float InteractionDistance = 1.3f;
        private static readonly Color Ink = new Color32(32, 43, 49, 255);
        private static readonly Color Paper = new Color32(250, 247, 235, 255);
        private static readonly Color Navy = new Color32(37, 58, 70, 255);
        private static readonly Color Red = new Color32(190, 45, 42, 255);

        public void Initialize(Transform playerTransform, Transform drawerTransform,
            System.Func<bool> isDrawerOpen, WorldObjectNameUI objectNameUI)
        {
            player = playerTransform;
            drawer = drawerTransform;
            drawerIsOpen = isDrawerOpen;
            nameUI = objectNameUI;
            if (player == null || drawer == null || drawerIsOpen == null)
            {
                Debug.LogError("Hidden drawer notebook needs player and drawer references.", this);
                enabled = false;
                return;
            }
            BuildUI();
        }

        private void Update()
        {
            if (player == null || drawer == null)
                return;

            bool near = drawerIsOpen != null && drawerIsOpen()
                && Vector2.Distance(player.position, drawer.position) <= InteractionDistance;
            if (near && !wasNear && !isOpen && nameUI != null)
                nameUI.ShowMessage("Defteri okumak için F tuşuna bas.", drawer.position + Vector3.up * .9f, 1.3f);
            wasNear = near;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (isOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    CloseBook();
                else if (coverView.activeSelf && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                    OpenPages();
                return;
            }

            if (near && keyboard.fKey.wasPressedThisFrame)
                ShowBook();
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("Gemma notebook canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 280;
            canvas.pixelPerfect = true;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            panel = new GameObject("Gemma notebook", typeof(RectTransform), typeof(Image), typeof(Outline));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(canvasRect, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.pivot = new Vector2(.5f, .5f);
            panelRect.sizeDelta = new Vector2(1040f, 700f);
            panel.GetComponent<Image>().color = new Color32(224, 216, 195, 255);
            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color32(48, 52, 47, 255);
            outline.effectDistance = new Vector2(4f, -4f);

            Button close = CreateButton("Close Gemma notebook", panelRect,
                new Vector2(48f, 48f), new Vector2(474f, 306f), Red);
            CreateText("Close Gemma notebook label", close.transform, "×", 34, Color.white,
                Vector2.zero, new Vector2(48f, 48f), TextAnchor.MiddleCenter);
            close.onClick.AddListener(CloseBook);

            coverView = new GameObject("Gemma notebook cover", typeof(RectTransform), typeof(Image), typeof(Outline));
            RectTransform coverRect = coverView.GetComponent<RectTransform>();
            coverRect.SetParent(panelRect, false);
            coverRect.anchorMin = coverRect.anchorMax = new Vector2(.5f, .5f);
            coverRect.pivot = new Vector2(.5f, .5f);
            coverRect.sizeDelta = new Vector2(650f, 500f);
            coverRect.anchoredPosition = new Vector2(0f, -8f);
            coverView.GetComponent<Image>().color = new Color32(105, 67, 43, 255);
            Outline coverOutline = coverView.GetComponent<Outline>();
            coverOutline.effectColor = new Color32(61, 42, 31, 255);
            coverOutline.effectDistance = new Vector2(3f, -3f);
            CreateText("Notebook cover title", coverRect, "GEMMA", 62, new Color32(247, 223, 164, 255),
                new Vector2(0f, 42f), new Vector2(570f, 110f), TextAnchor.MiddleCenter);
            CreateText("Notebook cover subtitle", coverRect, "LABORATUVAR NOTLARI", 25, new Color32(239, 229, 208, 255),
                new Vector2(0f, -45f), new Vector2(570f, 60f), TextAnchor.MiddleCenter);
            Button open = CreateButton("Open Gemma notebook", coverRect,
                new Vector2(270f, 66f), new Vector2(0f, -155f), new Color32(222, 204, 165, 255));
            CreateText("Open notebook label", open.transform, "Defteri Aç", 27, Ink,
                Vector2.zero, new Vector2(270f, 66f), TextAnchor.MiddleCenter);
            open.onClick.AddListener(OpenPages);

            pagesView = new GameObject("Gemma notebook pages", typeof(RectTransform), typeof(Image));
            RectTransform pagesRect = pagesView.GetComponent<RectTransform>();
            pagesRect.SetParent(panelRect, false);
            pagesRect.anchorMin = pagesRect.anchorMax = new Vector2(.5f, .5f);
            pagesRect.pivot = new Vector2(.5f, .5f);
            pagesRect.sizeDelta = new Vector2(930f, 540f);
            pagesRect.anchoredPosition = new Vector2(0f, -12f);
            pagesView.GetComponent<Image>().color = Paper;

            CreateText("Notebook page heading", pagesRect, "GEMMA HAKKINDA", 32, Ink,
                new Vector2(0f, 220f), new Vector2(840f, 54f), TextAnchor.MiddleCenter);
            CreateText("Notebook page text", pagesRect,
                "Gemma, laboratuvarın güvenlik kapısını yöneten yapay zekâdır. Onu ikna etmek kolay değildir.\n\nLaboratuvardan çıkabilmek için Gemma'yı ikna etmen çok önemli. Onunla konuşurken ne istediğini açıkça anlat ve sorularına dikkatle cevap ver.\n\nGemma ikna olmazsa güvenlik kapısı açılmaz; laboratuvardan çıkış yolu kapalı kalır.",
                27, Ink, new Vector2(0f, 20f), new Vector2(820f, 350f), TextAnchor.MiddleLeft);
            CreateText("Notebook red note", pagesRect,
                "NOT: Kibar olman çok önemli! Gemma'yla saygılı ve nazik bir şekilde konuş.",
                27, Red, new Vector2(0f, -200f), new Vector2(820f, 78f), TextAnchor.MiddleCenter);

            coverView.SetActive(false);
            pagesView.SetActive(false);
            panel.SetActive(false);
        }

        private void ShowBook()
        {
            isOpen = true;
            panel.SetActive(true);
            coverView.SetActive(true);
            pagesView.SetActive(false);
        }

        private void OpenPages()
        {
            if (!isOpen)
                return;
            coverView.SetActive(false);
            pagesView.SetActive(true);
        }

        private void CloseBook()
        {
            isOpen = false;
            if (panel != null)
                panel.SetActive(false);
        }

        private static Button CreateButton(string name, Transform parent, Vector2 size,
            Vector2 position, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = color;
            return go.GetComponent<Button>();
        }

        private static Text CreateText(string name, Transform parent, string value, int size,
            Color color, Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
