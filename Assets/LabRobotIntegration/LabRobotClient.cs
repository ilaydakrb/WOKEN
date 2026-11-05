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
    Transform gemma;
    PlayerWASDController player;
    bool suppressedUntilLeave, movementWasEnabled, movementLocked;
    float nextLookup;
    string sessionId = "", draft = "", transcript = "", status = "";
    int turn;
    bool busy, finished;
    Vector2 scroll;
    GUIStyle titleStyle, bodyStyle, inputStyle, buttonStyle, hintStyle;
    Texture2D fieldBackground, buttonBackground, hoverBackground;
    bool focusMessage;
    void Awake() { showTestPanel = false; }
    void Update()
    {
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
    }
    void SetPanel(bool open)
    {
        if (open && !showTestPanel) focusMessage = true;
        showTestPanel = open;
        if (open && !movementLocked && player != null)
        {
            movementWasEnabled = player.enabled;
            player.enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
            var animator = player.GetComponent<CharacterAnimator>();
            if (animator != null) animator.SetMotion(Vector2.zero);
            movementLocked = true;
        }
        else if (!open && movementLocked)
        {
            if (player != null) player.enabled = movementWasEnabled;
            movementLocked = false;
        }
    }
    public void CloseConversation() { suppressedUntilLeave = true; SetPanel(false); }
    void OnDisable() { SetPanel(false); }
    [Serializable] class Payload { public string session_id; public string message; public int turn; }
    [Serializable] class Result
    {
        public string session_id, reply, decision, error;
        public int turn, remaining, score;
        public bool door_open;
    }
    public void NewGame() { if (!busy) StartCoroutine(Begin()); }
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
                status = result != null && !string.IsNullOrEmpty(result.error) ? result.error : request.error;
                Debug.LogWarning("Robot API: " + status);
                done(null);
            }
            else done(result);
        }
    }
    IEnumerator Begin()
    {
        busy = true;
        if (!string.IsNullOrEmpty(sessionId))
            yield return Request("/close", new Payload { session_id = sessionId }, _ => { });
        yield return Request("/session", new Payload(), result => {
            if (result == null) return;
            sessionId = result.session_id; turn = 0; finished = false;
            transcript = ""; status = "Yeni oyun hazır.";
        });
        busy = false;
    }
    IEnumerator Send(string message)
    {
        busy = true;
        if (string.IsNullOrEmpty(sessionId))
        {
            yield return Request("/session", new Payload(), r => { if (r != null) sessionId = r.session_id; });
            if (string.IsNullOrEmpty(sessionId)) { busy = false; yield break; }
        }
        status = "Robot düşünüyor…";
        yield return Request("/message", new Payload { session_id = sessionId, message = message, turn = turn + 1 }, result => {
            if (result == null) return;
            turn = result.turn; finished = result.decision != "pending";
            transcript += "\nSen: " + message + "\nRobot: " + result.reply + "\n";
            draft = ""; status = "Konuşma: " + turn + "/5";
            scroll.y = float.MaxValue;
            focusMessage = true;
            onReply.Invoke(result.reply);
            if (result.decision == "allow" && result.door_open) onDoorOpen.Invoke();
            else if (result.decision == "deny") onDoorDenied.Invoke();
        });
        busy = false;
    }
    void OnGUI()
    {
        if (!showTestPanel) return;
        EnsureStyles();
        var inputEvent = Event.current;
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
        GUILayout.EndScrollView();
        GUILayout.Space(8);
        GUI.enabled = !busy;
        GUILayout.BeginHorizontal();
        GUILayout.Label("Mesajın", hintStyle);
        if (GUILayout.Button("Yeni oyun", buttonStyle, GUILayout.Width(100), GUILayout.Height(26))) NewGame();
        GUILayout.EndHorizontal();
        GUI.enabled = !busy && !finished;
        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("GemmaMessage");
        draft = GUILayout.TextField(draft, 400, inputStyle, GUILayout.Height(34));
        if (focusMessage && Event.current.type == EventType.Repaint)
        { GUI.FocusControl("GemmaMessage"); focusMessage = false; }
        if (GUILayout.Button("Gönder ↵", buttonStyle, GUILayout.Width(100), GUILayout.Height(34))) SendMessageToRobot(draft);
        GUILayout.EndHorizontal();
        GUI.enabled = true;
        GUILayout.EndArea();
        GUI.color = oldColor;
    }
    void EnsureStyles()
    {
        if (titleStyle != null) return;
        fieldBackground = Solid(new Color32(242, 245, 248, 255));
        buttonBackground = Bordered(new Color32(226, 233, 240, 255));
        hoverBackground = Bordered(new Color32(207, 222, 237, 255));
        Color ink = new Color32(28, 43, 57, 255);
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = ink;
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = false };
        bodyStyle.normal.textColor = ink;
        hintStyle = new GUIStyle(bodyStyle) { fontSize = 12 };
        hintStyle.normal.textColor = new Color32(86, 102, 117, 255);
        inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 14, padding = new RectOffset(10,10,8,6) };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
        buttonStyle.border = new RectOffset(1,1,1,1);
        foreach (var s in new[] { inputStyle.normal, inputStyle.hover, inputStyle.active, inputStyle.focused })
        { s.textColor = ink; s.background = fieldBackground; }
        foreach (var s in new[] { buttonStyle.normal, buttonStyle.focused })
        { s.textColor = ink; s.background = buttonBackground; s.scaledBackgrounds = null; }
        foreach (var s in new[] { buttonStyle.hover, buttonStyle.active })
        { s.textColor = ink; s.background = hoverBackground; s.scaledBackgrounds = null; }
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
    }
}
