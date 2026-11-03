using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Woken.UI
{
    /// <summary>Displays an object's name when the player clicks its collider.</summary>
    public sealed class WorldObjectNameUI : MonoBehaviour
    {
        private struct NamedTarget
        {
            public Collider2D Collider;
            public string Name;
            public Vector3 BubblePosition;
            public string DelayedMessage;
            public float DelaySeconds;
        }

        private readonly List<NamedTarget> targets = new List<NamedTarget>();
        private Camera worldCamera;
        private RectTransform canvasRect;
        private RectTransform bubbleRect;
        private Text label;
        private float hideAtTime;
        private int clickSequence;

        public void Initialize(Camera cameraToUse, Collider2D collider, string displayName, Vector3 position)
        {
            worldCamera = cameraToUse;
            if (worldCamera == null || collider == null)
            {
                Debug.LogError("World object name UI needs a camera and a collider.", this);
                enabled = false;
                return;
            }

            BuildBubble();
            Register(collider, displayName, position);
        }

        public void Register(Collider2D collider, string displayName, Vector3 position, string delayedMessage = null, float delaySeconds = 0f)
        {
            if (collider == null) return;
            targets.Add(new NamedTarget
            {
                Collider = collider,
                Name = displayName,
                BubblePosition = position,
                DelayedMessage = delayedMessage,
                DelaySeconds = delaySeconds
            });
        }

        public void ShowMessage(string message, Vector3 position, float duration = 2f)
        {
            ShowMessage(message, position, duration, new Vector2(240f, 76f));
        }

        public void ShowMessage(string message, Vector3 position, float duration, Vector2 bubbleSize)
        {
            if (worldCamera == null || bubbleRect == null || label == null)
                return;

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(position);
            if (screenPoint.z <= 0f)
                return;

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, null, out localPoint))
                return;

            clickSequence++;
            label.text = message;
            bubbleRect.sizeDelta = bubbleSize;
            bubbleRect.anchoredPosition = localPoint;
            hideAtTime = Time.unscaledTime + duration;
            bubbleRect.gameObject.SetActive(true);
        }
        private void Update()
        {
            if (bubbleRect != null && bubbleRect.gameObject.activeSelf && Time.unscaledTime >= hideAtTime)
                bubbleRect.gameObject.SetActive(false);

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            int currentClickSequence = ++clickSequence;
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(
                new Vector3(mouse.x, mouse.y, -worldCamera.transform.position.z));

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                NamedTarget target = targets[i];
                if (target.Collider == null || !target.Collider.OverlapPoint(worldPoint))
                    continue;

                bubbleRect.sizeDelta = new Vector2(240f, 76f);
                label.text = target.Name;
                Vector3 screenPoint = worldCamera.WorldToScreenPoint(target.BubblePosition);
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPoint, null, out localPoint))
                {
                    bubbleRect.anchoredPosition = localPoint;
                    hideAtTime = string.IsNullOrEmpty(target.DelayedMessage)
                        ? Time.unscaledTime + 1f
                        : Time.unscaledTime + target.DelaySeconds + 1f;
                    bubbleRect.gameObject.SetActive(true);
                    if (!string.IsNullOrEmpty(target.DelayedMessage))
                        StartCoroutine(ShowDelayedMessage(target.DelayedMessage, target.DelaySeconds, currentClickSequence));
                }
                return;
            }

            bubbleRect.gameObject.SetActive(false);
        }

        private IEnumerator ShowDelayedMessage(string message, float delaySeconds, int sequence)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            if (sequence != clickSequence || bubbleRect == null || !bubbleRect.gameObject.activeSelf)
                yield break;

            label.text = message;
            hideAtTime = Time.unscaledTime + 2f;
        }
        private void BuildBubble()
        {
            GameObject canvasObject = new GameObject("Object name canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            canvas.pixelPerfect = true;
            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject bubble = new GameObject("Object name bubble",
                typeof(RectTransform), typeof(Image));
            bubbleRect = bubble.GetComponent<RectTransform>();
            bubbleRect.SetParent(canvasRect, false);
            bubbleRect.sizeDelta = new Vector2(240f, 76f);
            Image background = bubble.GetComponent<Image>();
            background.color = new Color32(245, 245, 242, 242);
            background.sprite = MakeRoundedSprite();
            background.type = Image.Type.Sliced;
            bubbleRect.gameObject.SetActive(false);

            GameObject textObject = new GameObject("Object name text",
                typeof(RectTransform), typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(bubbleRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 5f);
            textRect.offsetMax = new Vector2(-8f, -5f);
            label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color32(20, 20, 20, 255);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 22;
            label.resizeTextMaxSize = 22;
            label.raycastTarget = false;
        }

        private static Sprite MakeRoundedSprite()
        {
            const int size = 32;
            const float radius = 7f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Object name bubble sprite"
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float nearestX = Mathf.Clamp(px, radius, size - radius);
                float nearestY = Mathf.Clamp(py, radius, size - radius);
                float dx = px - nearestX;
                float dy = py - nearestY;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
            }
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(10f, 10f, 10f, 10f));
            sprite.name = "Object name bubble sprite";
            return sprite;
        }
    }
}









