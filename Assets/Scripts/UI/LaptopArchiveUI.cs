using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Woken.UI
{
    /// <summary>Opens the north bench laptop archive and exposes narrative clues for the depot code.</summary>
    public sealed class LaptopArchiveUI : MonoBehaviour
    {
        private static Sprite pixelSprite;
        private static Sprite folderSprite;

        private Transform player;
        private Transform laptop;
        private SpriteRenderer laptopScreen;
        private WorldObjectNameUI nameUI;
        private Camera worldCamera;
        private Canvas canvas;
        private RectTransform contentRoot;
        private Text pathText;
        private GameObject window;
        private bool isOpen;
        private bool wasNearLaptop;

        private const float InteractionDistance = 1.3f;
        private static readonly Color Ink = new Color32(29, 47, 62, 255);
        private static readonly Color FolderColor = new Color32(247, 174, 77, 255);
        private static readonly Color PaperColor = new Color32(247, 248, 244, 255);

        private readonly string[] reportNames = { "LUCI.4", "LUCI.1", "LUCI.3", "LUCI.4" };
        private readonly string[] reportText =
        {
            "KOL AKTÜATÖRÜ / KALİBRASYON\nSonuç: ONAYLANDI.\n\nTutucu kolun yeni denge ayarı laboratuvar kapısındaki eski yedek kilitle eşleşti. Rapor etiketini değiştirmeyin; arşiv sırası korunmalı.",
            "OPTİK SENSÖR / HİZALAMA\nSonuç: ONAYLANDI.\n\nLuci, kuzey tezgâhındaki ışık ölçümünü yeniden yaptı. Tek numaralı profil, giriş panelindeki ilk kontağı doğru okudu.",
            "GÜÇ HATTI / KARARLILIK\nSonuç: ONAYLANDI.\n\nÜçüncü deney konfigürasyonunda enerji aktarımı kesintisiz kaldı. Eski notta, yedek kilit dizisinin deney kayıtlarından üretildiği yazıyor.",
            "SON DOĞRULAMA / TUTUCU KOL\nSonuç: ONAYLANDI.\n\nAynı kol kalibrasyonu son kontrolde yeniden kabul edildi. Kayıt tekrarını atlamayın; kilit yedeği raporların görünen sırasını izliyor."
        };

        public void Initialize(Transform playerTransform, Transform laptopTransform,
            Transform laptopScreenTransform, Camera cameraToUse, WorldObjectNameUI objectNameUI)
        {
            player = playerTransform;
            laptop = laptopTransform;
            laptopScreen = laptopScreenTransform != null
                ? laptopScreenTransform.GetComponent<SpriteRenderer>()
                : null;
            worldCamera = cameraToUse != null ? cameraToUse : Camera.main;
            nameUI = objectNameUI;

            if (player == null || laptop == null || worldCamera == null || nameUI == null)
            {
                Debug.LogError("Laptop archive needs player, laptop, camera, and object name UI references.", this);
                enabled = false;
                return;
            }

            BuildUI();
        }

        private void Update()
        {
            if (player == null || laptop == null)
                return;

            bool nearLaptop = Vector2.Distance(player.position, laptop.position) <= InteractionDistance;
            if (nearLaptop && !wasNearLaptop && !isOpen)
                nameUI.ShowMessage("Dosyaları açmak için F tuşuna bas.", laptop.position + Vector3.up * .65f, 1.3f);
            wasNearLaptop = nearLaptop;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (isOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    CloseArchive();
                return;
            }

            if (nearLaptop && keyboard.fKey.wasPressedThisFrame)
                OpenArchive();
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("Laptop archive canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 260;
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

            window = new GameObject("Laptop file browser", typeof(RectTransform), typeof(Image), typeof(Outline));
            RectTransform windowRect = window.GetComponent<RectTransform>();
            windowRect.SetParent(canvasRect, false);
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(.5f, .5f);
            windowRect.pivot = new Vector2(.5f, .5f);
            windowRect.sizeDelta = new Vector2(900f, 570f);
            Image background = window.GetComponent<Image>();
            background.sprite = FolderSprite();
            background.type = Image.Type.Sliced;
            background.color = new Color32(229, 235, 237, 255);
            Outline outline = window.GetComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(3f, -3f);

            BuildHeader(windowRect);
            BuildContent(windowRect);
            window.SetActive(false);
        }

        private void BuildHeader(RectTransform parent)
        {
            Image bar = CreateImage("Laptop archive title bar", parent, new Vector2(900f, 58f),
                new Vector2(0f, 256f), Ink);
            bar.raycastTarget = false;
            CreateText("Archive application title", parent,
                "WOKEN LAB // YEREL DOSYA ARŞİVİ", 22, Color.white,
                new Vector2(16f, 256f), new Vector2(620f, 48f), TextAnchor.MiddleLeft);

            Button close = CreateButton("Close laptop archive", parent,
                new Vector2(38f, 38f), new Vector2(420f, 256f), new Color32(203, 63, 58, 255));
            CreateText("Close laptop archive X", close.transform,
                "×", 28, Color.white, Vector2.zero, new Vector2(38f, 38f), TextAnchor.MiddleCenter);
            close.onClick.AddListener(CloseArchive);

            pathText = CreateText("Archive path", parent,
                "Arşiv / Klasörler", 18, Ink,
                new Vector2(-350f, 207f), new Vector2(780f, 42f), TextAnchor.MiddleLeft);
        }

        private void BuildContent(RectTransform parent)
        {
            GameObject content = new GameObject("Laptop archive content", typeof(RectTransform));
            contentRoot = content.GetComponent<RectTransform>();
            contentRoot.SetParent(parent, false);
            contentRoot.anchorMin = contentRoot.anchorMax = new Vector2(.5f, .5f);
            contentRoot.pivot = new Vector2(.5f, .5f);
            contentRoot.anchoredPosition = new Vector2(0f, -25f);
            contentRoot.sizeDelta = new Vector2(860f, 470f);
        }

        private void OpenArchive()
        {
            isOpen = true;
            if (laptopScreen != null)
                laptopScreen.color = new Color32(53, 111, 143, 255);
            window.SetActive(true);
            ShowHome();
        }

        private void CloseArchive()
        {
            isOpen = false;
            if (laptopScreen != null)
                laptopScreen.color = new Color32(13, 17, 19, 255);
            if (window != null)
                window.SetActive(false);
        }

        private void ShowHome()
        {
            ClearContent();
            pathText.text = "Arşiv / Klasörler";
            CreateText("Folder hint", contentRoot,
                "Laboratuvar dosyaları — klasörleri incele", 20, Ink,
                new Vector2(0f, 185f), new Vector2(800f, 42f), TextAnchor.MiddleLeft);

            CreateFolderCard("Deney Robotları", new Vector2(-270f, 48f), ShowRobotFolder);
            CreateFolderCard("Güvenlik Protokolü", new Vector2(0f, 48f), ShowProtocol);
            CreateFolderCard("Laboratuvar Notları", new Vector2(270f, 48f), ShowNotes);

            CreateText("Archive footer", contentRoot,
                "Son eşitleme: 03:17  •  Yerel kullanıcı: LAB-07", 15,
                new Color32(94, 108, 114, 255), new Vector2(0f, -190f),
                new Vector2(800f, 30f), TextAnchor.MiddleCenter);
        }

        private void ShowRobotFolder()
        {
            ClearContent();
            pathText.text = "Arşiv / Deney Robotları";
            CreateBackButton(ShowHome);
            CreateText("Robot folder title", contentRoot,
                "Deney Robotları", 23, Ink,
                new Vector2(20f, 170f), new Vector2(700f, 42f), TextAnchor.MiddleLeft);
            CreateNoteCard("Deney arşivi notu", contentRoot,
                "Depo kilidinin yedeği, onaylı LUCI deney raporlarından oluşturuldu. Raporları bu klasörde göründükleri sırayla incele; dosya etiketleri deney kimliğini taşır. Kişisel notlar ve reddedilen taslaklar kilit dizisine dahil değildir.",
                new Vector2(0f, 105f), new Vector2(790f, 90f));

            for (int i = 0; i < reportNames.Length; i++)
            {
                int reportIndex = i;
                float x = (i % 2 == 0) ? -195f : 195f;
                float y = i < 2 ? -5f : -125f;
                CreateFileCard(reportNames[i], new Vector2(x, y),
                    () => ShowReport(reportIndex));
            }
        }

        private void ShowReport(int index)
        {
            ClearContent();
            pathText.text = "Arşiv / Deney Robotları / " + reportNames[index];
            CreateBackButton(ShowRobotFolder);
            CreateDocumentView(reportNames[index], reportText[index]);
        }

        private void ShowProtocol()
        {
            ClearContent();
            pathText.text = "Arşiv / Güvenlik Protokolü";
            CreateBackButton(ShowHome);
            CreateDocumentView("Kilit yedeği - bakım notu",
                "Depo kapısı yenilendiğinde eski anahtar kaydı silinmiş. Yedek erişim, başarılı LUCI deneylerinin etiket numaralarından yeniden kurulabiliyor.\n\nİşlem sırası önemlidir: Deney Robotları klasöründeki onaylı raporları yukarıdan aşağıya oku. Her dosya için LUCI adından sonraki numarayı kaydet. Rapor içerikleri, hangi denemelerin onaylandığını doğrulamak için tutuluyor.\n\nNot: Bu ekranda tam giriş kodu saklanmıyor; kodu rapor sırasından çıkarman gerekiyor.");
        }

        private void ShowNotes()
        {
            ClearContent();
            pathText.text = "Arşiv / Laboratuvar Notları";
            CreateBackButton(ShowHome);
            CreateDocumentView("Vardiya günlüğü",
                "Gece vardiyası notu:\n\nKuzey tezgâhındaki eski laptop hâlâ açılıyor. Depo kapısının kontrol paneli bağlantısız kaldı. LUCI test raporlarını silmeyin; bakım ekibi yedek anahtarı bu kayıtlardan oluşturmuştu.\n\nKahve makinesi yine çalışmıyor. Numune teslimi sabah vardiyasına bırakıldı.");
        }

        private void CreateFolderCard(string label, Vector2 position, Action onClick)
        {
            Button card = CreateCard("Folder " + label, contentRoot,
                new Vector2(220f, 220f), position, onClick);
            Image tab = CreateImage("Folder tab", card.transform,
                new Vector2(78f, 24f), new Vector2(-37f, 67f), FolderColor);
            tab.raycastTarget = false;
            Outline tabOutline = tab.gameObject.AddComponent<Outline>();
            tabOutline.effectColor = Ink;
            tabOutline.effectDistance = new Vector2(2f, -2f);
            Image body = CreateImage("Folder body", card.transform,
                new Vector2(150f, 92f), new Vector2(0f, 30f), FolderColor);
            body.raycastTarget = false;
            Outline bodyOutline = body.gameObject.AddComponent<Outline>();
            bodyOutline.effectColor = Ink;
            bodyOutline.effectDistance = new Vector2(3f, -3f);
            CreateText("Folder label", card.transform, label, 18, Ink,
                new Vector2(0f, -68f), new Vector2(205f, 42f), TextAnchor.MiddleCenter);
        }

        private void CreateFileCard(string label, Vector2 position, Action onClick)
        {
            Button card = CreateCard("File " + label, contentRoot,
                new Vector2(340f, 98f), position, onClick);
            Image page = CreateImage("Document icon", card.transform,
                new Vector2(44f, 58f), new Vector2(-130f, 0f), PaperColor);
            page.raycastTarget = false;
            Outline outline = page.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(2f, -2f);
            Image line1 = CreateImage("Document line", page.transform,
                new Vector2(26f, 3f), new Vector2(0f, 8f), new Color32(105, 139, 157, 255));
            line1.raycastTarget = false;
            Image line2 = CreateImage("Document line", page.transform,
                new Vector2(26f, 3f), new Vector2(0f, -2f), new Color32(105, 139, 157, 255));
            line2.raycastTarget = false;
            CreateText("Document label", card.transform, label, 18, Ink,
                new Vector2(43f, 0f), new Vector2(220f, 48f), TextAnchor.MiddleLeft);
        }

        private void CreateDocumentView(string title, string body)
        {
            CreateText("Document title", contentRoot, title, 23, Ink,
                new Vector2(20f, 174f), new Vector2(700f, 42f), TextAnchor.MiddleLeft);
            Image paper = CreateImage("Document paper", contentRoot,
                new Vector2(790f, 330f), new Vector2(0f, -16f), PaperColor);
            Outline outline = paper.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(148, 160, 160, 255);
            outline.effectDistance = new Vector2(1f, -1f);
            CreateText("Document contents", paper.transform, body, 19, new Color32(35, 44, 48, 255),
                new Vector2(0f, 0f), new Vector2(730f, 290f), TextAnchor.UpperLeft);
        }

        private void CreateNoteCard(string name, Transform parent, string body, Vector2 position, Vector2 size)
        {
            Image note = CreateImage(name, parent, size, position, new Color32(255, 250, 220, 255));
            Outline outline = note.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(170, 155, 106, 255);
            outline.effectDistance = new Vector2(1f, -1f);
            CreateText(name + " text", note.transform, body, 16, new Color32(60, 57, 45, 255),
                Vector2.zero, size - new Vector2(24f, 18f), TextAnchor.MiddleLeft);
        }

        private void CreateBackButton(Action onClick)
        {
            Button back = CreateButton("Archive back", contentRoot,
                new Vector2(90f, 34f), new Vector2(350f, 205f), new Color32(67, 88, 101, 255));
            CreateText("Archive back label", back.transform, "Geri", 16, Color.white,
                Vector2.zero, new Vector2(90f, 34f), TextAnchor.MiddleCenter);
            back.onClick.AddListener(() => onClick());
        }

        private Button CreateCard(string name, Transform parent, Vector2 size, Vector2 position, Action onClick)
        {
            Button button = CreateButton(name, parent, size, position, new Color32(239, 243, 241, 255));
            button.onClick.AddListener(() => onClick());
            return button;
        }

        private Button CreateButton(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = go.GetComponent<Image>();
            image.color = color;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => { });
            return button;
        }

        private Text CreateText(string name, Transform parent, string value, int size,
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
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Image CreateImage(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = go.GetComponent<Image>();
            image.sprite = PixelSprite();
            image.color = color;
            return image;
        }

        private void ClearContent()
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);
        }

        private static Sprite PixelSprite()
        {
            if (pixelSprite != null) return pixelSprite;
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "Laptop archive pixel"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
            return pixelSprite;
        }

        private static Sprite FolderSprite()
        {
            if (folderSprite != null) return folderSprite;
            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Laptop archive window sprite"
            };
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                texture.SetPixel(x, y, Color.white);
            texture.Apply(false, true);
            folderSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
            return folderSprite;
        }
    }
}
