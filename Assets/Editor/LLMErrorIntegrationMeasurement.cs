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
    static readonly string[] Cases={"service_down","ollama_unavailable","model_missing","timeout","broken_json","empty_body","empty_reply","lost_response","lost_uncommitted","lost_final"};
    static LabRobotClient client;
    static Report report;
    static Item item;
    static int index, phase, frameStart, replyEvents, doorEvents, screenshotFrame;
    static bool setup;
    static double waitStart,started;
    [Serializable] public class Item
    {
        public string scenario, status, screenshot, error, dialogMessage;
        public int turnBefore,turnAfterFailure,remainingAfterFailure,turnAfterRecovery,remainingAfterRecovery,framesDuringFailure;
        public float seconds;
        public bool busyCleared,dialogShown,noDoorDecision,gameStillRunning,recoveryAccepted,passed,uncertainAfterFailure,revisedDraftPreserved,noDuplicateTurn;
        public int turnAfterSync;
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
    [MenuItem("Tools/WOKEN/LLM Hata Testleri (10 Durum)")]
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
    static void Dismiss()=>typeof(LabRobotClient).GetMethod("DismissServiceError",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(client,null);
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
                Debug.Log("LLMFAULT START 10 cases");
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
                if(Cases[index]=="lost_final" && Get<int>("turn")<4)
                {
                    client.SendMessageToRobot("Hazırlık mesajı "+Get<int>("turn"));
                    started=EditorApplication.timeSinceStartup;phase=11;return;
                }
                replyEvents=0;doorEvents=0;item.turnBefore=Get<int>("turn");
                started=EditorApplication.timeSinceStartup;frameStart=Time.frameCount;
                client.SendMessageToRobot("Hata dayanıklılığı testi: Çıkış isteğimi değerlendirir misin?");phase=2;return;
            }
            if(phase==11)
            {
                if(Get<bool>("busy")){if(EditorApplication.timeSinceStartup-started>15)Fail("Warmup timeout");return;}
                if(Get<bool>("serviceErrorVisible")){Fail("Warmup failed");return;}
                phase=1;frameStart=Time.frameCount;return;
            }
            if(phase==2)
            {
                if(Get<bool>("busy"))
                {if(EditorApplication.timeSinceStartup-started>15)Fail("Probe request did not finish");return;}
                item.turnAfterFailure=Get<int>("turn");item.remainingAfterFailure=5-item.turnAfterFailure;
                item.status=Get<string>("status");
                item.dialogShown=Get<bool>("serviceErrorVisible")&&replyEvents==0;
                item.dialogMessage=Get<string>("serviceErrorMessage");
                if(Cases[index]=="timeout")item.dialogShown&=item.dialogMessage.Contains("zamanında yanıt alınamadı");
                item.uncertainAfterFailure=Get<bool>("needsSync");
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
                Dismiss();
                if(!Cases[index].StartsWith("lost_"))client.apiUrl="http://127.0.0.1:18765/ok";
                Field("draft").SetValue(client,"Değiştirilmiş test mesajı; isteğimi yeniden değerlendir.");
                started=EditorApplication.timeSinceStartup;
                client.SendMessageToRobot(Get<string>("draft"));phase=4;return;
            }
            if(phase==4)
            {
                if(Get<bool>("busy")){if(EditorApplication.timeSinceStartup-started>15)Fail("Recovery did not finish");return;}
                if(Get<bool>("serviceErrorVisible")){Fail("Recovery still shows error: "+Get<string>("serviceErrorMessage"));return;}
                item.turnAfterSync=Get<int>("turn");
                if(Cases[index]=="lost_response")
                {
                    item.revisedDraftPreserved=Get<string>("draft")=="Değiştirilmiş test mesajı; isteğimi yeniden değerlendir.";
                    item.noDuplicateTurn=item.turnAfterSync==1&&replyEvents==1;
                    started=EditorApplication.timeSinceStartup;
                    client.SendMessageToRobot(Get<string>("draft"));phase=5;return;
                }
                RecordRecovery();
            }
            if(phase==5)
            {
                if(Get<bool>("busy")){if(EditorApplication.timeSinceStartup-started>15)Fail("Revised send timeout");return;}
                RecordRecovery();
            }
        }
        catch(Exception exc){Fail(exc.ToString());}
    }
    static void RecordRecovery()
    {
                item.turnAfterRecovery=Get<int>("turn");item.remainingAfterRecovery=5-item.turnAfterRecovery;
                int expected=Cases[index]=="lost_response"?2:Cases[index]=="lost_final"?5:1;
                int expectedReplies=Cases[index]=="lost_response"?2:1;
                item.recoveryAccepted=item.turnAfterRecovery==expected&&replyEvents==expectedReplies&&doorEvents==0&&!Get<bool>("serviceErrorVisible");
                if(Cases[index]=="lost_final")item.recoveryAccepted&=Get<bool>("finished")&&Get<bool>("exitAllowed");
                item.passed=item.turnAfterFailure==item.turnBefore&&item.remainingAfterFailure==5-item.turnBefore
                    &&(item.uncertainAfterFailure?item.status.Contains("henüz doğrulanamadı"):item.status.Contains("Konuşma hakkın azalmadı"))
                    &&item.busyCleared&&item.dialogShown&&!string.IsNullOrEmpty(item.dialogMessage)&&item.noDoorDecision&&item.gameStillRunning&&item.recoveryAccepted;
                if(Cases[index]=="lost_response")item.passed&=item.revisedDraftPreserved&&item.noDuplicateTurn;
                report.cases.Add(item);Save();
                Debug.Log("LLMFAULT "+item.scenario+" passed="+item.passed+" remaining="+item.remainingAfterFailure+" recovery="+item.turnAfterRecovery);
                client.enabled=false;UnityEngine.Object.Destroy(client.gameObject);client=null;
                index++;phase=0;
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
