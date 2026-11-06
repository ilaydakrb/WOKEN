#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Gerçek UnityWebRequest/coroutine/GUI akışını kontrollü localhost hatalarıyla sınar.
[InitializeOnLoad]
public static class LLMErrorIntegrationMeasurement
{
    const string Key="Woken.LLMErrorProbe.Active";
    static readonly string[] Cases={"service_down","ollama_unavailable","model_missing","timeout","broken_json","empty_body","empty_reply"};
    static LabRobotClient client;
    static Report report;
    static Item item;
    static int index, phase, frameStart, replyEvents, doorEvents, screenshotFrame;
    static bool setup;
    static double waitStart,started;
    [Serializable] public class Item
    {
        public string scenario, status, screenshot, error;
        public int turnBefore,turnAfterFailure,remainingAfterFailure,turnAfterRecovery,remainingAfterRecovery,framesDuringFailure;
        public float seconds;
        public bool busyCleared,fallbackShown,noDoorDecision,gameStillRunning,recoveryAccepted,passed;
    }
    [Serializable] public class Report
    {
        public string recordedUtc,unity,method,setupError;
        public int normalTimeoutSeconds=600,testTimeoutSeconds=2;
        public List<Item> cases=new List<Item>();
    }
    static LLMErrorIntegrationMeasurement()
    {
        if(SessionState.GetBool(Key,false))
        {waitStart=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
    }
    [MenuItem("Tools/WOKEN/LLM Hata Testleri (7 Durum)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play kapalı olmalı.");
        SessionState.SetBool(Key,true);
        setup=false;index=0;phase=0;report=null;
        EditorSceneManager.OpenScene("Assets/Scenes/CharactersOnly.unity",OpenSceneMode.Single);
        waitStart=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        EditorApplication.EnterPlaymode();
    }
    static FieldInfo Field(string name)=>typeof(LabRobotClient).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
    static T Get<T>(string name)=>(T)Field(name).GetValue(client);
    static void Tick()
    {
        try
        {
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.timeSinceStartup-waitStart>180)Fail("Play startup timeout");
                return;
            }
            if(!setup)
            {
                var player=GameObject.Find("Player");var gemma=GameObject.Find("Floor 2 room 5 LLM robot torso");
                if(player==null||gemma==null||Time.frameCount<20)return;
                var original=GameObject.Find("LabRobot");
                if(original!=null){var c=original.GetComponent<LabRobotClient>();if(c!=null)c.enabled=false;}
                var pos=gemma.transform.position+Vector3.down*1.05f;
                player.transform.position=pos;var rb=player.GetComponent<Rigidbody2D>();if(rb!=null)rb.position=pos;
                Physics2D.SyncTransforms();
                report=new Report {recordedUtc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion,
                    method="Real Unity Play LabRobotClient, UnityWebRequest, coroutine and OnGUI. Separate scripted localhost server at 18765; refused connection at 18766. Original production Ollama/API unchanged. Per-test timeout set to 2 seconds on temporary client only; production default 600. Error statuses are injected, not physical model deletion or shutdown."};
                setup=true;frameStart=Time.frameCount;phase=-1;
                Debug.Log("LLMFAULT START 7 cases");
            }
            if(index>=Cases.Length){Finish();return;}
            if(phase==-1){if(Time.frameCount-frameStart<30)return;phase=0;}
            if(phase==0)
            {
                client=new GameObject("LLM fault probe - "+Cases[index]).AddComponent<LabRobotClient>();
                Field("requestTimeoutSeconds").SetValue(client,2);
                client.apiUrl=Cases[index]=="service_down"?"http://127.0.0.1:18766":"http://127.0.0.1:18765/"+Cases[index];
                replyEvents=0;doorEvents=0;
                client.onReply.AddListener(_=>replyEvents++);
                client.onDoorOpen.AddListener(()=>doorEvents++);client.onDoorDenied.AddListener(()=>doorEvents++);
                item=new Item {scenario=Cases[index],turnBefore=Get<int>("turn")};
                frameStart=Time.frameCount;phase=1;return;
            }
            if(phase==1)
            {
                if(Time.frameCount-frameStart<5||!client.showTestPanel)return;
                started=EditorApplication.timeSinceStartup;frameStart=Time.frameCount;
                client.SendMessageToRobot("Hata dayanıklılığı testi: Çıkış isteğimi değerlendirir misin?");phase=2;return;
            }
            if(phase==2)
            {
                if(Get<bool>("busy"))
                {if(EditorApplication.timeSinceStartup-started>15)Fail("Probe request did not finish");return;}
                item.turnAfterFailure=Get<int>("turn");item.remainingAfterFailure=5-item.turnAfterFailure;
                item.status=Get<string>("status");
                item.fallbackShown=Get<string>("transcript").Contains("Ne dediğini anlayamadım. Biraz daha açık anlatır mısın?")&&replyEvents==1;
                item.busyCleared=!Get<bool>("busy");item.noDoorDecision=doorEvents==0&&!Get<bool>("finished");
                item.framesDuringFailure=Time.frameCount-frameStart;item.gameStillRunning=EditorApplication.isPlaying&&item.framesDuringFailure>0;
                item.seconds=(float)(EditorApplication.timeSinceStartup-started);
                item.screenshot="llm-hata-unity-"+Cases[index]+".png";
                string path=Path.Combine(Application.dataPath,"../Docs/testler/"+item.screenshot);
                ScreenCapture.CaptureScreenshot(path);
                screenshotFrame=Time.frameCount;phase=3;return;
            }
            if(phase==3)
            {
                if(Time.frameCount-screenshotFrame<5)return;
                client.apiUrl="http://127.0.0.1:18765/ok";
                started=EditorApplication.timeSinceStartup;
                client.SendMessageToRobot("Tekrar deniyorum; test isteğimi değerlendirir misin?");phase=4;return;
            }
            if(phase==4)
            {
                if(Get<bool>("busy")){if(EditorApplication.timeSinceStartup-started>15)Fail("Recovery did not finish");return;}
                item.turnAfterRecovery=Get<int>("turn");item.remainingAfterRecovery=5-item.turnAfterRecovery;
                item.recoveryAccepted=item.turnAfterRecovery==1&&replyEvents==2&&doorEvents==0;
                item.passed=item.turnBefore==0&&item.turnAfterFailure==0&&item.remainingAfterFailure==5
                    &&item.status.Contains("Konuşma hakkın azalmadı")&&item.status.Contains("5/5")
                    &&item.busyCleared&&item.fallbackShown&&item.noDoorDecision&&item.gameStillRunning&&item.recoveryAccepted;
                report.cases.Add(item);Save();
                Debug.Log("LLMFAULT "+item.scenario+" passed="+item.passed+" remaining="+item.remainingAfterFailure+" recovery="+item.turnAfterRecovery);
                client.enabled=false;UnityEngine.Object.Destroy(client.gameObject);client=null;
                index++;phase=0;
            }
        }
        catch(Exception exc){Fail(exc.ToString());}
    }
    static void Save()
    {
        var path=Path.Combine(Application.dataPath,"../Docs/testler/llm-unity-hata-test-sonuclari.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
    }
    static void Finish()
    {
        Save();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
        Debug.Log("LLMFAULT DONE "+report.cases.Count+" cases");EditorApplication.ExitPlaymode();
    }
    static void Fail(string message)
    {
        if(report==null)report=new Report {recordedUtc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion};
        report.setupError=message;Save();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
        Debug.LogError("LLMFAULT FAILURE "+message);EditorApplication.ExitPlaymode();
    }
}
#endif
