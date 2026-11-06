#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Woken.Characters;

// Sadece Editor ölçümü; normal oyun başlangıcında veya build'de çalışmaz.
[InitializeOnLoad]
public static class NavMeshMeasurement
{
    const string ActiveKey = "Woken.NavMeshMeasurement.Active";
    const float Timeout = 15f;
    static RobotMovementController robot;
    static Rigidbody2D body;
    static NavMeshAgent agent;
    static Report report;
    static Trial current;
    static int index, settle;
    static bool prepared, ready, running, ended;
    static RobotTaskResult result;
    static double waitingSince, started, simStarted;
    static Vector2 lastPosition;
    static readonly Vector2[] Starts = { new Vector2(-11.9f,-3.2f), new Vector2(-11.9f,3.2f), new Vector2(-7.2f,-3.2f) };
    static readonly Vector2[] Goals = { new Vector2(-7.2f,4.8f), new Vector2(-4.7f,0f) };
    [Serializable] public class Trial
    {
        public int id, startId, goalId, repetition, corners;
        public Vector2 requestedStart, actualStart, requestedGoal, actualGoal, finalPosition;
        public bool startSampled, goalSampled, warped, accepted, success;
        public string outcome, pathStatus;
        public float seconds, simulationSeconds, plannedLength, travelled, finalDistance;
    }
    [Serializable] public class Report
    {
        public string recordedUtc, unity, scene, processor, graphics, method, setupError;
        public int ramMB, graphicsMB, total, successCount;
        public float timeScale, fixedDeltaTime, timeoutSeconds, arrivalTolerance, moveSpeed;
        public float successRatePercent, meanSuccessSeconds, meanSuccessSimulationSeconds;
        public List<Trial> trials = new List<Trial>();
    }
    static NavMeshMeasurement()
    {
        if (SessionState.GetBool(ActiveKey,false))
        { waitingSince=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; }
    }
    [MenuItem("Tools/WOKEN/NavMesh Ölçümü (18 Deneme)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Ölçümü Play kapalıyken başlatın.");
        SessionState.SetBool(ActiveKey,true);
        EditorSceneManager.OpenScene("Assets/Scenes/CharactersOnly.unity",OpenSceneMode.Single);
        waitingSince=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup-waitingSince>180) FailSetup("Play mode did not start within 180 seconds.");
                return;
            }
            if (!prepared)
            {
                var go=GameObject.Find("Companion Robot");
                if (go==null || Time.frameCount<20)
                {
                    if (EditorApplication.timeSinceStartup-waitingSince>180) FailSetup("Runtime Companion Robot not available.");
                    return;
                }
                robot=go.GetComponent<RobotMovementController>();
                body=go.GetComponent<Rigidbody2D>(); agent=go.GetComponent<NavMeshAgent>();
                if (robot==null || body==null || agent==null) { FailSetup("Movement components missing."); return; }
                foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (behaviour==robot || behaviour is CharacterAnimator) continue;
                    string ns=behaviour.GetType().Namespace ?? "";
                    if (ns.StartsWith("Woken") || behaviour.GetType().Name=="LabRobotClient")
                    { behaviour.StopAllCoroutines(); behaviour.enabled=false; }
                }
                var player=GameObject.Find("Player");
                if (player!=null)
                {
                    var rb=player.GetComponent<Rigidbody2D>(); if(rb!=null) rb.simulated=false;
                }
                Time.timeScale=1; Application.targetFrameRate=60; QualitySettings.vSyncCount=0;
                report=new Report {
                    recordedUtc=DateTime.UtcNow.ToString("o"), unity=Application.unityVersion,
                    scene="Assets/Scenes/CharactersOnly.unity", processor=SystemInfo.processorType,
                    graphics=SystemInfo.graphicsDeviceName, ramMB=SystemInfo.systemMemorySize,
                    graphicsMB=SystemInfo.graphicsMemorySize, timeScale=Time.timeScale,
                    fixedDeltaTime=Time.fixedDeltaTime, timeoutSeconds=Timeout, arrivalTolerance=.3f,
                    moveSpeed=robot.MoveSpeed,
                    method="Actual Play mode; existing scene bake + RobotMovementController.StartMoveTask. Three starts x two goals x three repeats. Other Woken task/UI scripts disabled and player physics disabled to isolate static-world navigation. No controller/scene source change."
                };
                robot.TaskFinished+=OnTaskFinished; prepared=true;
                Debug.Log("NAVMEASURE START: 18 trials");
            }
            if (index>=18) { Finish(); return; }
            if (!ready) { PrepareTrial(); return; }
            if (settle>0) { settle--; return; }
            if (!running)
            {
                started=EditorApplication.timeSinceStartup;simStarted=Time.timeAsDouble;
                running=true;ended=false;
                current.accepted=robot.StartMoveTask(current.actualGoal,false);
                if (agent.isOnNavMesh && agent.hasPath)
                {
                    var path=agent.path;current.pathStatus=path.status.ToString();current.corners=path.corners.Length;
                    for(int i=1;i<path.corners.Length;i++)current.plannedLength+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                }
                if (!current.accepted) { CompleteTrial("StartRejected",false); return; }
            }
            Vector2 position=body.position;
            current.travelled+=Vector2.Distance(position,lastPosition);lastPosition=position;
            if (ended)
            {
                bool success=result==RobotTaskResult.Completed && Vector2.Distance(position,current.actualGoal)<=.35f;
                CompleteTrial(result.ToString(),success);
            }
            else if (EditorApplication.timeSinceStartup-started>=Timeout)
                CompleteTrial("Timeout",false);
        }
        catch(Exception exc) { FailSetup(exc.ToString()); }
    }
    static void PrepareTrial()
    {
        int start=index/6, goal=(index%6)/3, repeat=index%3;
        current=new Trial {id=index+1,startId=start+1,goalId=goal+1,repetition=repeat+1,
            requestedStart=Starts[start],requestedGoal=Goals[goal],outcome="Preparing",pathStatus="NotCalculated"};
        robot.HoldPosition();
        current.startSampled=NavMesh.SamplePosition(current.requestedStart,out var sh,.35f,agent.areaMask);
        current.goalSampled=NavMesh.SamplePosition(current.requestedGoal,out var gh,.35f,agent.areaMask);
        started=EditorApplication.timeSinceStartup;simStarted=Time.timeAsDouble;
        if (!current.startSampled || !current.goalSampled) { CompleteTrial("SampleFailed",false);return; }
        current.actualStart=sh.position;current.actualGoal=gh.position;
        body.position=current.actualStart;body.linearVelocity=Vector2.zero;robot.transform.position=sh.position;
        Physics2D.SyncTransforms();current.warped=agent.Warp(sh.position);
        if (!current.warped) { CompleteTrial("WarpFailed",false);return; }
        agent.nextPosition=sh.position;
        lastPosition=current.actualStart;ready=true;settle=3;
    }
    static void OnTaskFinished(RobotTaskResult value) { if(running){ result=value; ended=true; } }
    static void CompleteTrial(string outcome,bool success)
    {
        current.outcome=outcome;current.success=success;
        current.seconds=(float)(EditorApplication.timeSinceStartup-started);
        current.simulationSeconds=(float)(Time.timeAsDouble-simStarted);
        current.finalPosition=body.position;
        current.finalDistance=Vector2.Distance(current.finalPosition,current.actualGoal);
        report.trials.Add(current);
        Debug.Log("NAVMEASURE "+current.id+" "+outcome+" "+current.seconds.ToString("F3",CultureInfo.InvariantCulture)+" sec");
        running=false;robot.HoldPosition();ready=false;index++;
        Save();
    }
    static void Save()
    {
        report.total=report.trials.Count;report.successCount=0;float real=0,sim=0;
        foreach(var t in report.trials)if(t.success){report.successCount++;real+=t.seconds;sim+=t.simulationSeconds;}
        report.successRatePercent=report.total==0?0:100f*report.successCount/report.total;
        report.meanSuccessSeconds=report.successCount==0?0:real/report.successCount;
        report.meanSuccessSimulationSeconds=report.successCount==0?0:sim/report.successCount;
        string path=Path.Combine(Application.dataPath,"../Docs/testler/navmesh-olcum-sonuclari.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
    }
    static void Finish()
    {
        Save();SessionState.SetBool(ActiveKey,false);EditorApplication.update-=Tick;
        Debug.Log("NAVMEASURE DONE "+report.successCount+"/"+report.total);
        if(Application.isBatchMode)EditorApplication.Exit(0);else EditorApplication.ExitPlaymode();
    }
    static void FailSetup(string error)
    {
        if(report==null)report=new Report {recordedUtc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion};
        report.setupError=error;Save();SessionState.SetBool(ActiveKey,false);EditorApplication.update-=Tick;
        Debug.LogError("NAVMEASURE SETUP FAILURE "+error);
        if(Application.isBatchMode)EditorApplication.Exit(2);else EditorApplication.ExitPlaymode();
    }
}
#endif
