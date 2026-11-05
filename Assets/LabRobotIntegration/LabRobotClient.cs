using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using Woken.Characters;

// Yalnız çıkış bölümündeki Gemma robotuna yaklaşınca açılır.
public class LabRobotClient : MonoBehaviour
{
    public string apiUrl = "http://127.0.0.1:8765";
    public UnityEvent<string> onReply = new UnityEvent<string>();
    public UnityEvent onDoorOpen = new UnityEvent();
    public UnityEvent onDoorDenied = new UnityEvent();
    [HideInInspector] public bool showTestPanel = false;
    public float conversationDistance = 1.4f;
    public float doorOpeningSeconds = 3.2f;
    public float escapeFadeSeconds = 5.2f;
    Transform upperDoorHinge, lowerDoorHinge;
    bool exitAllowed, escaping, escaped;
    float escapeFade;
    Transform gemma;
    PlayerWASDController player;
    bool suppressedUntilLeave, movementWasEnabled, movementLocked;
    float nextLookup;
    string sessionId = "", draft = "", transcript = "", status = "";
    int turn;
    bool busy, finished, exitDenied, gameOver;
    Vector2 scroll;
    float maxScroll;
    bool scrollToBottom;
    GUIStyle titleStyle, bodyStyle, inputStyle, buttonStyle, hintStyle;
    Texture2D fieldBackground, buttonBackground, hoverBackground, successHeart;
    bool focusMessage;
    void Awake() { showTestPanel = false; }
    void Update()
    {
        if (gameOver || escaping) { SetPanel(false); return; }
        if (Time.unscaledTime >= nextLookup && (gemma == null || player == null))
        {
            nextLookup = Time.unscaledTime + .5f;
            if (player == null)
            {
                var target = GameObject.Find("Player");
                if (target != null) player = target.GetComponent<PlayerWASDController>();
            }
            if (gemma == null)
            {
                var target = GameObject.Find("Floor 2 room 5 LLM robot torso");
                if (target != null) gemma = target.transform;
            }
            PrepareExitDoor();
        }
        bool near = gemma != null && gemma.gameObject.activeInHierarchy && player != null
            && player.gameObject.activeInHierarchy
            && Vector2.Distance(player.transform.position, gemma.position) <= conversationDistance;
        // Çıkış eşiği biraz geniş: sınırda panel titremez.
        bool left = gemma == null || player == null || !gemma.gameObject.activeInHierarchy
            || Vector2.Distance(player.transform.position, gemma.position) > conversationDistance + .35f;
        if (left)
        {
            suppressedUntilLeave = false;
            SetPanel(false);
            return;
        }
        if (near && !suppressedUntilLeave) SetPanel(true);
        if (near && !showTestPanel && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        { suppressedUntilLeave = false; SetPanel(true); }
        if (showTestPanel && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseConversation();
        if (showTestPanel && Keyboard.current != null)
        {
            float direction = (Keyboard.current.downArrowKey.isPressed ? 1f : 0f)
                            - (Keyboard.current.upArrowKey.isPressed ? 1f : 0f);
            scroll.y = Mathf.Clamp(scroll.y + direction * 260f * Time.unscaledDeltaTime, 0, maxScroll);
        }
    }
    void SetPanel(bool open)
    {
        if (open && !showTestPanel) focusMessage = true;
        showTestPanel = open;
        bool lockMovement = open || gameOver || escaping;
        if (lockMovement && !movementLocked && player != null)
        {
            movementWasEnabled = player.enabled;
            player.enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
            var animator = player.GetComponent<CharacterAnimator>();
            if (animator != null) animator.SetMotion(Vector2.zero);
            movementLocked = true;
        }
        else if (!lockMovement && movementLocked)
        {
            if (player != null) player.enabled = movementWasEnabled;
            movementLocked = false;
        }
    }
    public void CloseConversation()
    {
        suppressedUntilLeave = true;
        if (finished && exitDenied) gameOver = true;
        else if (finished && exitAllowed && !escaping)
        {
            if (!PrepareExitDoor())
            {
                status = "Çıkış kapısı bulunamadı; sahne bağlantısını kontrol edin.";
                Debug.LogError(status, this);
                return;
            }
            escaping = true;
            onDoorOpen.Invoke();
            StartCoroutine(EscapeSequence());
        }
        SetPanel(false);
    }
    void OnDisable() { gameOver = false; escaping = false; SetPanel(false); }
    [Serializable] class Payload { public string session_id; public string message; public int turn; }
    [Serializable] class Result
    {
        public string session_id, reply, decision, error;
        public int turn, remaining, score;
        public bool door_open;
    }
    public void SendMessageToRobot(string message)
    {
        if (busy || finished) return;
        if (string.IsNullOrWhiteSpace(message) || message.Length > 400)
        { status = "1–400 karakter yazın."; return; }
        StartCoroutine(Send(message));
    }
    IEnumerator Request(string path, Payload data, Action<Result> done)
    {
        using (var request = new UnityWebRequest(apiUrl.TrimEnd('/') + path, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 600;
            yield return request.SendWebRequest();
            Result result = null;
            try { result = JsonUtility.FromJson<Result>(request.downloadHandler.text); }
            catch (Exception) { }
            if (request.result != UnityWebRequest.Result.Success || result == null)
            {
                string detail = result != null && !string.IsNullOrEmpty(result.error) ? result.error : request.error;
                Debug.LogWarning("Robot API: " + detail);
                done(null);
            }
            else done(result);
        }
    }
    IEnumerator Send(string message)
    {
        busy = true;
        if (string.IsNullOrEmpty(sessionId))
        {
            yield return Request("/session", new Payload(), r => { if (r != null) sessionId = r.session_id; });
            if (string.IsNullOrEmpty(sessionId))
            { ShowRetryResponse(message); busy = false; yield break; }
        }
        status = "Robot düşünüyor…";
        yield return Request("/message", new Payload { session_id = sessionId, message = message, turn = turn + 1 }, result => {
            if (!ValidMessageResult(result)) { ShowRetryResponse(message); return; }
            turn = result.turn; finished = turn >= 5 || result.decision != "pending";
            exitDenied = result.decision == "deny";
            exitAllowed = result.decision == "allow" && result.door_open;
            transcript += "\nSen: " + message + "\nRobot: " + result.reply + "\n";
            draft = ""; status = finished ? "Görüşme tamamlandı — 5/5" : "Konuşma: " + turn + "/5";
            scrollToBottom = true;
            focusMessage = !finished;
            onReply.Invoke(result.reply);
            if (result.decision == "deny") onDoorDenied.Invoke();
            // Son yanıt beklenirken pencere kapatılmışsa sonuç geldiğinde bitişi uygula.
            if (finished && !showTestPanel) CloseConversation();
        });
        busy = false;
    }
    bool ValidMessageResult(Result result)
    {
        if (result == null || !string.IsNullOrEmpty(result.error)
            || result.session_id != sessionId || string.IsNullOrWhiteSpace(result.reply)
            || result.turn != turn+1 || result.turn > 5
            || result.remaining != 5-result.turn) return false;
        if (result.turn < 5)
            return result.decision == "pending" && !result.door_open;
        return (result.decision == "allow" && result.door_open)
            || (result.decision == "deny" && !result.door_open);
    }
    void ShowRetryResponse(string message)
    {
        // Uygulama hata karşılığıdır; model cevabı sayılmaz ve tur artırılmaz.
        status = "Konuşma hakkın azalmadı. Kalan hak: " + (5-turn) + "/5";
        string reply = "Ne dediğini anlayamadım. Biraz daha açık anlatır mısın?";
        transcript += "\nSen: " + message + "\nRobot: " + reply + "\n";
        scrollToBottom = true;
        focusMessage = true;
        onReply.Invoke(reply);
    }
    void OnGUI()
    {
        if (escaping) { DrawEscape(); return; }
        if (gameOver) { DrawGameOver(); return; }
        if (!showTestPanel) return;
        EnsureStyles();
        var inputEvent = Event.current;
        // Oklar mesaj alanı odaktayken de geçmişi kaydırır; metin imlecine gitmez.
        if (inputEvent.type == EventType.KeyDown
            && (inputEvent.keyCode == KeyCode.UpArrow || inputEvent.keyCode == KeyCode.DownArrow))
            inputEvent.Use();
        if (inputEvent.type == EventType.KeyDown
            && (inputEvent.keyCode == KeyCode.Return || inputEvent.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == "GemmaMessage" && !busy && !finished)
        {
            inputEvent.Use();
            SendMessageToRobot(draft);
        }
        float width = Mathf.Min(Screen.width - 24, Mathf.Max(340, Screen.width * .5f));
        float height = Mathf.Min(Screen.height - 24, Mathf.Max(280, Screen.height * .5f));
        var panel = new Rect((Screen.width-width)*.5f, (Screen.height-height)*.5f, width, height);
        Color oldColor = GUI.color;
        GUI.color = Color.white;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(panel.x-1, panel.y-1, width+2, height+2), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUILayout.BeginArea(new Rect(panel.x+18, panel.y+14, width-36, height-28));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Gemma", titleStyle);
        if (GUILayout.Button("Kapat · Esc", buttonStyle, GUILayout.Width(100), GUILayout.Height(28))) CloseConversation();
        GUILayout.EndHorizontal();
        GUILayout.Label(string.IsNullOrEmpty(status) ? "Çıkış isteğini Gemma'ya anlat." : status, hintStyle);
        GUILayout.Space(8);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        GUILayout.Label(string.IsNullOrEmpty(transcript) ? "Konuşmaya başlamak için mesajını yaz." : transcript, bodyStyle);
        Rect historyContent = GUILayoutUtility.GetLastRect();
        GUILayout.EndScrollView();
        Rect historyViewport = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.Repaint)
        {
            maxScroll = Mathf.Max(0, historyContent.height - historyViewport.height + 12);
            scroll.y = scrollToBottom ? maxScroll : Mathf.Clamp(scroll.y, 0, maxScroll);
            scrollToBottom = false;
        }
        GUILayout.Space(8);
        if (finished)
            GUILayout.Label("Beş konuşma hakkın bitti. Yukarı/aşağı oklarıyla geçmişi inceleyebilirsin.", hintStyle);
        else
        {
        GUILayout.Label("Mesajın", hintStyle);
        GUI.enabled = !busy && !finished;
        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("GemmaMessage");
        // Sabit genişlik, uzun metnin Gönder butonunu sağa itmesini engeller.
        // Tek satırlı TextField imleci görünür tutmak için içeriği yatay kaydırır.
        float messageWidth = Mathf.Max(40, width - 36 - 100 - 4);
        draft = GUILayout.TextField(draft, 400, inputStyle,
            GUILayout.Width(messageWidth), GUILayout.MinWidth(0), GUILayout.Height(34));
        if (focusMessage && Event.current.type == EventType.Repaint)
        { GUI.FocusControl("GemmaMessage"); focusMessage = false; }
        if (GUILayout.Button("Gönder ↵", buttonStyle, GUILayout.Width(100), GUILayout.Height(34))) SendMessageToRobot(draft);
        GUILayout.EndHorizontal();
        GUI.enabled = true;
        }
        GUILayout.EndArea();
        GUI.color = oldColor;
    }
    bool PrepareExitDoor()
    {
        if (upperDoorHinge != null && lowerDoorHinge != null) return true;
        var source = GameObject.Find("Floor 2 exit door");
        if (source == null) return false;
        var renderer = source.GetComponent<SpriteRenderer>();
        if (renderer == null) return false;
        Vector3 center = renderer.bounds.center;
        Vector3 size = renderer.bounds.size;
        var face = GameObject.Find("Floor 2 exit door face");
        var faceRenderer = face != null ? face.GetComponent<SpriteRenderer>() : null;
        upperDoorHinge = CreateDoorLeaf("Exit upper door", source.transform.parent,
            center + Vector3.up*size.y*.5f, -1, size, renderer, faceRenderer);
        lowerDoorHinge = CreateDoorLeaf("Exit lower door", source.transform.parent,
            center - Vector3.up*size.y*.5f, 1, size, renderer, faceRenderer);
        source.SetActive(false);
        if (face != null) face.SetActive(false);
        return true;
    }
    static Transform CreateDoorLeaf(string name, Transform parent, Vector3 hingePosition,
        float direction, Vector3 size, SpriteRenderer source, SpriteRenderer face)
    {
        var hinge = new GameObject(name + " hinge").transform;
        hinge.SetParent(parent, false);
        hinge.position = hingePosition;
        var leaf = new GameObject(name);
        leaf.transform.SetParent(hinge, false);
        leaf.transform.localPosition = new Vector3(0,direction*size.y*.25f,0);
        leaf.transform.localScale = new Vector3(size.x,size.y*.5f,1);
        var visual = leaf.AddComponent<SpriteRenderer>();
        visual.sprite = source.sprite; visual.color = source.color;
        visual.sortingLayerID = source.sortingLayerID; visual.sortingOrder = source.sortingOrder;
        var collider = leaf.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        if (face != null)
        {
            var trim = new GameObject(name + " face");
            trim.transform.SetParent(hinge,false);
            trim.transform.localPosition = new Vector3(face.bounds.center.x-hingePosition.x,
                direction*size.y*.25f,0);
            trim.transform.localScale = new Vector3(face.bounds.size.x,face.bounds.size.y*.5f,1);
            var trimVisual = trim.AddComponent<SpriteRenderer>();
            trimVisual.sprite = face.sprite; trimVisual.color = face.color;
            trimVisual.sortingLayerID = face.sortingLayerID; trimVisual.sortingOrder = face.sortingOrder;
        }
        return hinge;
    }
    IEnumerator EscapeSequence()
    {
        // Aynı saatten ilerler; ekran kapıdan en az 1,5 saniye sonra tamamlanır.
        float doorDuration = Mathf.Max(.5f,doorOpeningSeconds);
        float fadeDuration = Mathf.Max(doorDuration+1.5f,escapeFadeSeconds);
        float elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed = Mathf.Min(fadeDuration,elapsed+Time.unscaledDeltaTime);
            float opening = Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/doorDuration));
            upperDoorHinge.localRotation = Quaternion.Euler(0,0,-85*opening);
            lowerDoorHinge.localRotation = Quaternion.Euler(0,0,85*opening);
            escapeFade = Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/fadeDuration));
            yield return null;
        }
        escapeFade = 1;
        escaped = true;
    }
    void DrawEscape()
    {
        int oldDepth = GUI.depth;
        Color oldColor = GUI.color;
        GUI.depth = -10000;
        GUI.color = new Color(236f/255f,245f/255f,253f/255f,escapeFade);
        GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (escaped)
        {
            var style = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height*.075f),24,64)
            };
            StableLabel(style,new Color32(28,43,57,255));
            var message = new GUIContent("Tebrikler başardınız");
            float textWidth = Mathf.Max(1,Screen.width-48);
            float textHeight = style.CalcHeight(message,textWidth);
            float heartSize = Mathf.Clamp(Screen.height*.22f,80,180);
            float gap = 20;
            float top = (Screen.height-textHeight-gap-heartSize)*.5f;
            GUI.Label(new Rect(24,top,textWidth,textHeight),message,style);
            if (successHeart == null) successHeart = CreateSuccessHeart();
            GUI.DrawTexture(new Rect((Screen.width-heartSize)*.5f,top+textHeight+gap,
                heartSize,heartSize),successHeart);
        }
        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }
    static Texture2D CreateSuccessHeart()
    {
        // Kalbi şekil olarak çiz: yazı tipinin emoji desteğine bağlı değildir.
        const int size = 192;
        var texture = new Texture2D(size,size,TextureFormat.RGBA32,false) {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color[size*size];
        for (int y=0; y<size; y++)
            for (int x=0; x<size; x++)
            {
                int inside = 0;
                for (int sy=0; sy<2; sy++)
                    for (int sx=0; sx<2; sx++)
                    {
                        float px = ((x+(sx+.5f)/2)/size)*2.6f-1.3f;
                        float py = ((y+(sy+.5f)/2)/size)*2.6f-1.2f;
                        float a = px*px+py*py-1;
                        if (a*a*a-px*px*py*py*py <= 0) inside++;
                    }
                Color pink = new Color32(245,139,181,255); // #F58BB5
                pink.a = inside/4f;
                pixels[y*size+x] = pink;
            }
        texture.SetPixels(pixels); texture.Apply();
        return texture;
    }
    void DrawGameOver()
    {
        int oldDepth = GUI.depth;
        Color oldColor = GUI.color;
        GUI.depth = -10000;
        GUI.color = new Color32(16,20,25,255);
        GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        float availableWidth = Mathf.Max(1, Screen.width - 32);
        var heading = new GUIStyle(GUI.skin.label) {
            alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
            wordWrap = true, fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height*.085f),24,72)
        };
        StableLabel(heading, Color.white);
        var subtitle = new GUIStyle(heading) {
            fontStyle = FontStyle.Normal, fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height*.045f),18,38)
        };
        StableLabel(subtitle, Color.white);
        var title = new GUIContent("Oyun bitti");
        var reason = new GUIContent("Çıkış yapamadın");
        float titleHeight = heading.CalcHeight(title, availableWidth);
        float reasonHeight = subtitle.CalcHeight(reason, availableWidth);
        float gap = 12;
        float top = (Screen.height-titleHeight-reasonHeight-gap)*.5f;
        GUI.Label(new Rect(16,top,availableWidth,titleHeight),title,heading);
        GUI.Label(new Rect(16,top+titleHeight+gap,availableWidth,reasonHeight),reason,subtitle);
        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }
    void EnsureStyles()
    {
        if (titleStyle != null) return;
        fieldBackground = Solid(new Color32(242, 245, 248, 255));
        buttonBackground = Bordered(new Color32(226, 233, 240, 255));
        hoverBackground = Bordered(new Color32(207, 222, 237, 255));
        Color ink = new Color32(28, 43, 57, 255);
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
        StableLabel(titleStyle, ink);
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = false };
        StableLabel(bodyStyle, ink);
        hintStyle = new GUIStyle(bodyStyle) { fontSize = 12 };
        StableLabel(hintStyle, new Color32(86, 102, 117, 255));
        inputStyle = new GUIStyle(GUI.skin.textField) {
            fontSize = 14, padding = new RectOffset(10,10,8,6),
            wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft
        };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
        buttonStyle.border = new RectOffset(1,1,1,1);
        foreach (var s in new[] { inputStyle.normal, inputStyle.hover, inputStyle.active, inputStyle.focused })
        { s.textColor = ink; s.background = fieldBackground; }
        foreach (var s in new[] { buttonStyle.normal, buttonStyle.focused })
        { s.textColor = ink; s.background = buttonBackground; s.scaledBackgrounds = null; }
        foreach (var s in new[] { buttonStyle.hover, buttonStyle.active })
        { s.textColor = ink; s.background = hoverBackground; s.scaledBackgrounds = null; }
    }
    static void StableLabel(GUIStyle style, Color color)
    {
        foreach (var state in new[] { style.normal, style.hover, style.active, style.focused,
                                      style.onNormal, style.onHover, style.onActive, style.onFocused })
        { state.textColor = color; state.background = null; state.scaledBackgrounds = null; }
    }
    static Texture2D Bordered(Color fill)
    {
        var texture = new Texture2D(3,3) {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y=0; y<3; y++)
            for (int x=0; x<3; x++)
                texture.SetPixel(x,y, x==1 && y==1 ? fill : Color.black);
        texture.Apply();
        return texture;
    }
    static Texture2D Solid(Color color)
    {
        var texture = new Texture2D(1,1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0,0,color); texture.Apply(); return texture;
    }
    void OnDestroy()
    {
        if (fieldBackground != null) Destroy(fieldBackground);
        if (buttonBackground != null) Destroy(buttonBackground);
        if (hoverBackground != null) Destroy(hoverBackground);
        if (successHeart != null) Destroy(successHeart);
    }
}
