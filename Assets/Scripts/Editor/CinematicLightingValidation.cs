using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Profiling;

// Opt-in Play Mode integration checks, never included in builds or run during normal gameplay.
public static class CinematicLightingValidation
{
    static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    static readonly StringBuilder report = new StringBuilder();
    static int lastFrame;
    static void Line(string text) { report.AppendLine(text); File.WriteAllText(CinematicLightingReview.Output + "/runtime-validation.txt", report.ToString()); }
    static void Check(bool condition, string text) => Line((condition ? "PASS " : "FAIL ") + text);
    public static void Start(bool performance)
    {
        if(!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required");
        if(routines.Count>0) throw new InvalidOperationException("Review already running");
        if(!performance) { report.Clear(); Line("Editor Play Mode integration review — " + DateTime.UtcNow.ToString("O")); }
        routines.Push(performance ? MeasureBoth() : Validate()); lastFrame=-1;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    public static void StartMotion() => Queue(Motion());
    public static void StartEncounter() => Queue(Encounter());
    public static void StartEcho() => Queue(EchoRealm());
    static void Queue(IEnumerator routine)
    {
        if(!EditorApplication.isPlaying||routines.Count>0)throw new InvalidOperationException("Start an idle Play Mode review first");
        routines.Push(routine);lastFrame=-1;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) { routines.Clear(); EditorApplication.update -= Tick; return; }
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try
        {
            for(int guard=0;guard<8 && routines.Count>0;guard++)
            {
                var routine=routines.Peek(); if(!routine.MoveNext()){routines.Pop();continue;}
                if(routine.Current is IEnumerator inner){routines.Push(inner);continue;} break;
            }
            if(routines.Count==0)EditorApplication.update-=Tick;
        }
        catch(Exception e){Line("EXCEPTION "+e);routines.Clear();EditorApplication.update-=Tick;Debug.LogException(e);}
    }
    static IEnumerator Wait(float seconds) { float until=Time.time+seconds; while(Time.time<until)yield return null; }
    static DeadCityAtmosphere City => UnityEngine.Object.FindFirstObjectByType<DeadCityAtmosphere>();
    static IEnumerator Validate()
    {
        var city=City; var look=city.look; var cam=city.playerCamera;
        CinematicLightingReview.Position(); yield return Wait(2f);
        Check(city.Allows(cam) && city.State=="Exploration", "Exterior owns exploration; local scattering permitted");
        Check(Mathf.Abs(RenderSettings.fogDensity-.008f)<.0001f,"Exploration fog .008");
        Check(DeadCityScatteringFeature.LastRenderedLightCount>0,"Render Graph selected local streetlights: "+DeadCityScatteringFeature.LastRenderedLightCount);
        Check(city.FogWeight>.99f&&city.DrawsFog(cam),"Volumetric fog drawn in the street; weight="+city.FogWeight);
        var cityLights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled&&l.intensity>0f&&(l.transform.root.name=="STATIC_MODELS"||l.transform.root.name=="Burning Trees"||l.type==LightType.Directional)).ToArray();
        Check(cityLights.All(l=>l.type!=LightType.Directional&&city.trees.lamps.Contains(l)),"Street lamps are the only city lights: "+cityLights.Length+" lit");
        Check(RenderSettings.ambientSkyColor==Color.black&&RenderSettings.reflectionIntensity==0f,"No ambient or reflected light");
        Check(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).All(p=>!p.name.Contains("Fire")&&!p.name.Contains("Ember")),"No fire left in the city");
        Check(city.trees.lights.All(l=>l!=null)&&city.trees.fireClip==null,"Damned anchors clean, no fire crackle; gathering spots="+city.trees.lights.Length);
        Check(city.trees.lamps.Count(l=>l!=null&&l.enabled)<=city.trees.maxLamps,"Streetlight pool cap respected");
        CinematicLightingReview.Capture("after-street");
        city.scatteringEnabled=false; CinematicLightingReview.Capture("after-street-no-scattering"); city.scatteringEnabled=true;
        Color sky=RenderSettings.ambientSkyColor, equator=RenderSettings.ambientEquatorColor, ground=RenderSettings.ambientGroundColor;

        // Exercise the existing Echo implementation in the actual blackout interior.
        var plot=RevisedPlotDirector.Instance;
        var interior=UnityEngine.Object.FindObjectsByType<InteriorDarkness>(FindObjectsSortMode.None).FirstOrDefault(i=>i.blackoutUntilPowered);
        if(plot!=null&&interior!=null)
        {
            var root=look.playerRoot.transform;
            Vector3 offset=cam.transform.position-root.position;
            root.position=interior.zone.center-offset; Physics.SyncTransforms(); yield return Wait(1f);
            Check(!city.FogBelongsHere(cam),"Volumetric fog stays out of the interior");
            plot.SetEcho(true); yield return Wait(.75f);
            Check(plot.Echo && !city.Allows(cam),"Real SetEcho(true) yields exterior renderer to Echo");
            Check(RenderSettings.ambientMode==AmbientMode.Flat,"Echo interior retains flat memory lighting");
            Line("Echo actual fog "+RenderSettings.fogColor+" density="+RenderSettings.fogDensity);
            CinematicLightingReview.Capture("echo-interior");
            plot.SetEcho(false);yield return Wait(.75f);
            Check(!plot.Echo&&!city.Allows(cam),"Real SetEcho(false) restores Dead City interior without exterior override");
            CinematicLightingReview.Position(); yield return Wait(2f);
            Check(city.Allows(cam)&&Mathf.Abs(RenderSettings.fogDensity-.008f)<.0001f,"Exterior fog restores after leaving Echo interior");
            Check(Vector4.Distance(RenderSettings.ambientSkyColor,sky)<.01f && Vector4.Distance(RenderSettings.ambientEquatorColor,equator)<.01f && Vector4.Distance(RenderSettings.ambientGroundColor,ground)<.01f,"Ambient gradient restores after interior/Echo");
        }
        else Line("UNVERIFIED Echo manager or blackout interior absent");

        // Explicit cinematic ownership: use the aerial fog value used by FirstSightCutscene.
        RenderSettings.fogDensity=.0007f; yield return Wait(.75f);
        Check(Mathf.Abs(RenderSettings.fogDensity-.0007f)<.00001f,"Aerial cinematic fog remains untouched");
        look.SendMessage("ApplyLook",true);yield return Wait(.3f);

        // Existing vehicle entry path; no vehicle tuning, route geometry or physics edits.
        var vehicle=city.vehicle;
        var entryEvents=vehicle.onEnter;
        vehicle.onEnter=new UnityEngine.Events.UnityEvent();
        try { vehicle.SendMessage("EnterCar"); } finally { vehicle.onEnter=entryEvents; }
        Line("Driving test: original EnterCar path, encounter callbacks temporarily held for this isolated Play Mode test; restored immediately.");
        yield return Wait(6f);
        Check(vehicle.InCar&&city.State=="Driving","EnterCar outside a scripted encounter selects driving atmosphere");
        Check(Mathf.Abs(RenderSettings.fogDensity-.0035f)<.00015f,"Driving fog settles near .0035; actual="+RenderSettings.fogDensity);
        Check(city.Allows(vehicle.carCamera),"Actual driving camera supports local scattering");
        CinematicLightingReview.Capture("driving-camera",vehicle.carCamera);
        vehicle.ForceExit(false); CinematicLightingReview.Position();yield return Wait(6f);
        Check(!vehicle.InCar&&Mathf.Abs(RenderSettings.fogDensity-.008f)<.00015f,"Vehicle exit restores exploration visibility");

        // Dense horror preset is selected spatially, with no added trigger collider.
        var dense=city.denseStreets.FirstOrDefault();
        look.playerRoot.transform.position+=dense.center-cam.transform.position;Physics.SyncTransforms();yield return Wait(6f);
        Check(city.State=="Dense horror"&&Mathf.Abs(RenderSettings.fogDensity-.016f)<.0002f,"Selected horror street reaches dense .016 preset");
        CinematicLightingReview.Position();yield return Wait(1f);

        SleepSequence.BloodTide(.1f);yield return Wait(.4f);
        Check(look.bloodVolume.weight>.99f&&!city.Allows(cam),"Existing BloodTide takes exclusive crimson lighting ownership");
        Check(city.FogBelongsHere(cam),"Volumetric fog stays on through the blood tide");
        CinematicLightingReview.Capture("danger-blood-tide");
        SleepSequence.BloodTideOff();yield return Wait(.3f);
        Check(look.bloodVolume.weight<.01f&&city.Allows(cam),"BloodTideOff restores exterior state");

        // Execute the original arena lighting coroutine, without beginning/changing boss AI.
        var boss=UnityEngine.Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if(boss!=null)
        {
            var method=typeof(DemonBoss).GetMethod("LightArena",BindingFlags.Instance|BindingFlags.NonPublic);
            look.StartCoroutine((IEnumerator)method.Invoke(boss,new object[]{.5f}));yield return Wait(.9f);
            Check(RenderSettings.fogDensity<.0001f&&!city.Allows(cam),"Original bridge LightArena keeps clear fog and red judgement light");
            Line("Arena actual sun="+(look.sun!=null?look.sun.intensity:0f)+" fog="+RenderSettings.fogDensity+" tide="+look.bloodVolume.weight);
            CinematicLightingReview.Capture("bridge-lighting-state");
        }
        else Line("UNVERIFIED bridge lighting coroutine: boss absent");
        Line("UNVERIFIED full Vane chase/fight AI, checkpoint death/retry and scene traversal; these checks exercised lighting integration only.");
        look.SendMessage("ApplyLook",true);SleepSequence.BloodTideOff();HellMist.Strength=1f;
        CinematicLightingReview.Position();yield return Wait(1f);
        Line("COMPLETE integration review");
    }
    static IEnumerator MeasureBoth()
    {
        CinematicLightingReview.Position();var city=City;yield return Wait(2f);
        city.scatteringEnabled=true;yield return Sample("after-performance");
        city.scatteringEnabled=false;yield return Sample("after-without-scattering-performance");
        city.scatteringEnabled=true;
        File.WriteAllText(CinematicLightingReview.Output+"/performance-complete.txt","Completed same-run on/off frame observations. Editor timings are not a standalone PC benchmark.");
    }
    static IEnumerator Motion()
    {
        CinematicLightingReview.Position();var city=City;var vehicle=city.vehicle;
        var events=vehicle.onEnter;vehicle.onEnter=new UnityEngine.Events.UnityEvent();
        try{vehicle.SendMessage("EnterCar");}finally{vehicle.onEnter=events;}
        vehicle.carController.enabled=false;
        PlaytestDriver.DriveVan(18f,0f,false);
        float start=Time.time,top=0;
        while(Time.time-start<10f){top=Mathf.Max(top,vehicle.GetComponent<Rigidbody>().linearVelocity.magnitude);yield return null;}
        string text="Existing wheel-driven route test; no road obstacles cleared or physics settings changed.\nEncounter callbacks held for ordinary entry, restored immediately.\nTop observed m/s="+top+"\nTop km/h="+top*3.6f+"\nState="+city.State+"\nFog="+RenderSettings.fogDensity+"\nDriving camera allowed="+city.Allows(city.drivingCamera)+"\n";
        File.WriteAllText(CinematicLightingReview.Output+"/driving-motion-test.txt",text);
        CinematicLightingReview.Capture("driving-motion",city.drivingCamera);
        PlaytestDriver.Stop();vehicle.ForceExit(false);CinematicLightingReview.Position();
    }
    static IEnumerator Encounter()
    {
        CinematicLightingReview.Position();var city=City;var boss=UnityEngine.Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if(boss==null)throw new InvalidOperationException("No original boss");
        // Invoke the real entry callbacks and the game's existing skip mechanism. No AI/animation fields are changed.
        city.vehicle.SendMessage("EnterCar");yield return null;
        typeof(CutsceneSkip).GetProperty("Skipping",BindingFlags.Public|BindingFlags.Static).SetValue(null,true);
        double timeout=EditorApplication.timeSinceStartup+90;
        while(!boss.IsChasing&&EditorApplication.timeSinceStartup<timeout)yield return null;
        if(!boss.IsChasing){File.WriteAllText(CinematicLightingReview.Output+"/encounter-test.txt","UNVERIFIED: opening did not reach chase before timeout");yield break;}
        yield return Wait(6f);
        Vector3 position=boss.transform.position;yield return Wait(1.5f);
        float movement=Vector3.Distance(position,boss.transform.position);
        string text="Original vehicle onEnter callbacks executed; original opening advanced with existing CutsceneSkip mechanism.\nChasing="+boss.IsChasing+"\nBoss movement over 1.5 game seconds="+movement+"\nState="+city.State+"\nFog="+RenderSettings.fogDensity+"\nFog colour="+RenderSettings.fogColor+"\nBlood volume="+city.look.bloodVolume.weight+"\nExterior scattering permitted="+city.Allows(city.drivingCamera)+"\nFull fight and judgement outcome were not played.\n";
        File.WriteAllText(CinematicLightingReview.Output+"/encounter-test.txt",text);
        CinematicLightingReview.Capture("vane-chase-camera",city.drivingCamera);
        boss.ResetChase();city.vehicle.ForceExit(false);CinematicLightingReview.Position();
    }

    static IEnumerator EchoRealm()
    {
        var output=new StringBuilder();
        Teleporter.ArriveAt="OfficeArrival";Teleporter.ArriveCheckpoint="Checkpoint - Office entrance";SleepSequence.WakeInHell=true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("OfficeSilo");yield return null;yield return Wait(3f);
        var sleep=UnityEngine.Object.FindFirstObjectByType<SleepSequence>();
        var player=sleep.playerRoot;var camera=player.GetComponentInChildren<Camera>(true);
        var zone=UnityEngine.Object.FindObjectsByType<InteriorDarkness>(FindObjectsSortMode.None).First(z=>z.blackoutUntilPowered);
        var plot=RevisedPlotDirector.Instance;
        var controller=player.GetComponent<FirstPersonController>();if(controller!=null)controller.enabled=false;
        var body=player.GetComponent<Rigidbody>();if(body!=null){if(!body.isKinematic)body.linearVelocity=Vector3.zero;body.isKinematic=true;}
        player.GetComponent<PlayerHealth>()?.Revive(1000000f);
        if(!zone.zone.Contains(camera.transform.position))player.transform.position+=zone.zone.center-camera.transform.position;
        Physics.SyncTransforms();yield return Wait(1f);
        Color previousFog=RenderSettings.fogColor;
        CinematicLightingReview.Capture("echo-dead-city-interior",camera);
        output.AppendLine((DeadCityAtmosphere.Active==null?"PASS ":"FAIL ")+"City atmosphere is absent in OfficeSilo");
        var cameraData=new SerializedObject(camera.GetUniversalAdditionalCameraData());
        output.AppendLine("Office camera renderer index="+cameraData.FindProperty("m_RendererIndex").intValue);
        plot.SetEcho(true);yield return Wait(.8f);
        output.AppendLine((plot.Echo&&zone.Echo?"PASS ":"FAIL ")+"Real Echo switch entered");
        output.AppendLine("Echo fog="+RenderSettings.fogColor+" density="+RenderSettings.fogDensity+" ambient="+RenderSettings.ambientMode);
        output.AppendLine((Vector4.Distance(previousFog,RenderSettings.fogColor)>.03f?"PASS ":"FAIL ")+"Echo changed the interior atmosphere");
        CinematicLightingReview.Capture("echo-realm-interior",camera);
        plot.SetEcho(false);yield return Wait(.8f);
        output.AppendLine((!plot.Echo&&!zone.Echo?"PASS ":"FAIL ")+"Real Echo return completed");
        output.AppendLine((Vector4.Distance(previousFog,RenderSettings.fogColor)<.01f?"PASS ":"FAIL ")+"Dead City interior fog restored");
        CinematicLightingReview.Capture("echo-return-interior",camera);
        Teleporter.ArriveAt="CityReturnPoint";Teleporter.ArriveCheckpoint=null;SleepSequence.WakeInHell=true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainGameScene");yield return null;yield return Wait(3f);
        CinematicLightingReview.Position();yield return Wait(1f);
        var city=City;
        output.AppendLine((city!=null&&city.Allows(city.playerCamera)&&Mathf.Abs(RenderSettings.fogDensity-.008f)<.0001f?"PASS ":"FAIL ")+"Cinematic city restored after actual scene reload");
        output.AppendLine("Returned city state="+(city!=null?city.State:"absent")+" fog="+RenderSettings.fogDensity);
        File.WriteAllText(CinematicLightingReview.Output+"/echo-validation.txt",output.ToString());
    }

    static IEnumerator Sample(string name)
    {
        for(int i=0;i<45;i++)yield return null;
        var frames=new List<float>();var gpu=new List<double>();var cpu=new List<double>();var draws=new List<long>();var timing=new FrameTiming[1];
        using(var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
        {
            for(int i=0;i<180;i++)
            {
                FrameTimingManager.CaptureFrameTimings();yield return null;
                frames.Add(Time.unscaledDeltaTime*1000);draws.Add(draw.LastValue);
                if(FrameTimingManager.GetLatestTimings(1,timing)>0){if(timing[0].gpuFrameTime>0)gpu.Add(timing[0].gpuFrameTime);cpu.Add(timing[0].cpuFrameTime);}
            }
        }
        double Median(IEnumerable<double> values){var sorted=values.OrderBy(x=>x).ToArray();return sorted.Length>0?sorted[sorted.Length/2]:-1;}
        var camera=CinematicLightingReview.PlayerCamera;
        File.WriteAllText(CinematicLightingReview.Output+"/"+name+".txt",$"Editor Play Mode; 180 distinct game frames, 45 warmup frames\nFrame median ms={Median(frames.Select(x=>(double)x))}\nMean ms={frames.Average()}\nGPU frame median ms={Median(gpu)}\nCPU frame median ms={Median(cpu)}\nDraw calls median={Median(draws.Select(x=>(double)x))}\nCamera={camera.pixelWidth}x{camera.pixelHeight}\nQuality={QualitySettings.names[QualitySettings.GetQualityLevel()]} vsync={QualitySettings.vSyncCount}\nScattering={City.scatteringEnabled}\nLocal scattering lights={DeadCityScatteringFeature.LastRenderedLightCount}\nGraphics device={SystemInfo.graphicsDeviceName}\n");
    }
}
