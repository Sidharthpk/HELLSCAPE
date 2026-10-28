using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Discovery-led office opening. Progress survives the existing Truth/checkpoint system.
// The first flash uses PalmLight's actual hit/visibility checks, never a timed cutscene flash.
public sealed class OfficeOpening : MonoBehaviour
{
    public OfficeV52Sequence sequence;
    public bool Busy { get; private set; }
    public bool AwaitingFlash { get; private set; }
    public bool Cleared => Truth.Has("OfficeOpening.Flashed") || Truth.Has("OfficeV52.CCTV");
    public bool FusesDiscovered => Truth.Has("OfficeOpening.MissingFuses");
    public bool Powered => (breaker != null && breaker.counter != null && breaker.counter.Count >= breaker.counter.required) || Truth.Has("OfficeV52.CCTV");
    public bool PryVisible { get; private set; }
    PunchController pryPunch;
    public float PryProgress { get; private set; }
    public bool ControlsShown { get; private set; }
    FuseBox breaker;
    Transform breakerUse;
    readonly List<Behaviour> held = new List<Behaviour>();
    bool introducing;
    float hintTime, shoutAt, retryAt;
    int shoutIndex;
    string hint;
    GameObject pryTool;
    Light encounterLight,pryLight;
    Animator reachArms;
    float reachSpeed;
    PalmLight heldPalm;bool palmEnabled;
    static readonly string[] Shouts = {
        "Vane: You put your name on the deliveries. Don't pretend you weren't there.",
        "Vane: He told me you were the one talking. Why would he tell me that?",
        "Vane: Keep that hand down. I can see it from here.",
        "Vane: I can still hear that lock. Every time I shut my eyes.",
        "Vane: You don't get to walk out and leave me with this."
    };

    public void Initialize(OfficeV52Sequence owner)
    {
        sequence=owner;
        breaker=FindFirstObjectByType<FuseBox>(FindObjectsInactive.Include);
        if(breaker!=null)
        {
            foreach(var use in breaker.GetComponentsInChildren<Interactable>(true))
                if(use.requiredItem=="Fuse") breakerUse=use.transform;
            if(breaker.counter!=null) breaker.counter.onProgress += n => {
                if(n>=breaker.counter.required) Truth.Set("OfficeOpening.Powered");
            };
        }
        // Existing runs can continue from their completed electrical puzzle.
        foreach(var room in RevisedPlot.All<InteriorDarkness>(gameObject.scene))
            if(room.blackoutUntilPowered && room.Powered) Truth.Set("OfficeOpening.Powered");
        shoutAt=Time.time+18f;
    }
    bool Ready => sequence!=null && sequence.player!=null && !PauseMenu.Paused && !GameOverScreen.Open
        && !TitleScreen.Showing && !CutsceneHUD.Hidden && !NoteViewer.Blocking && !GhostNPC.Conversing
        && (DialogueBox.Instance==null || (!DialogueBox.Instance.Busy && !DialogueBox.Instance.CardOpen))
        && (ChoiceDialogue.Instance==null || !ChoiceDialogue.Instance.Open);
    bool InOffice => sequence!=null && OfficeHorrorDirector.In(sequence.player.position)
        && Checkpoint.Current!=null && (Checkpoint.Current.name=="Checkpoint - Office entrance" || Checkpoint.Current.name=="Checkpoint - Office armory");
    void Update()
    {
        if(!InOffice || sequence.Completed) return;
        if(!Cleared && !Truth.Has("OfficeOpening.Arrived") && !introducing && Ready && !sequence.InSequence)
            StartCoroutine(Arrival());
        if(!Cleared && !Busy && !introducing && Ready)
        {
            Vector3 p=sequence.player.position;
            bool inside=p.x>1003.3f && p.x<1011.8f && p.z>901.8f && p.z<908.4f;
            if(!FusesDiscovered && inside && breakerUse!=null && Vector3.Distance(p,breakerUse.position)<4f)
                ObserveBreaker();
            else if(FusesDiscovered && !inside && Time.time>=retryAt && breakerUse!=null && Vector3.Distance(p,breakerUse.position)>3.8f
                && VaneHunt.Instance!=null && VaneHunt.Instance.OpeningRouteReady)
                StartCoroutine(Confrontation());
        }
        if(Cleared && !Busy && !sequence.InSequence && !Truth.Has("OfficeV52.CCTV") && Ready
            && Time.time>=shoutAt && VaneHunt.Instance!=null && VaneHunt.Instance.Actor!=null
            && VaneHunt.Instance.Actor.gameObject.activeInHierarchy
            && Vector3.Distance(sequence.player.position,VaneHunt.Instance.Actor.position)<22f
            && !(RevisedPlotDirector.Instance!=null && RevisedPlotDirector.Instance.Echo))
        {
            DialogueBox.Instance?.SayNow(Shouts[shoutIndex++ % Shouts.Length]);
            shoutAt=Time.time+Random.Range(23f,36f);
        }
    }
    IEnumerator Arrival()
    {
        introducing=true;
        ControlsShown=true;
        Hint("PALM LIGHT\n[Q] raise / lower your hand    [LEFT CLICK] flash while raised\nThe light stays on. The flash needs time to recharge.",7f);
        float t=0f;
        while(t<4f) { if(!PauseMenu.Paused) t+=Time.deltaTime; yield return null; }
        while(!Ready) yield return null;
        Truth.Set("OfficeOpening.Arrived");
        DialogueBox.Instance?.Say(HS5.S("A3V53_electricity"));
        introducing=false;
    }
    public void Hint(string text,float seconds=7f) { hint=text; hintTime=seconds; }
    public void OfferSoulHint()
    {
        if(Truth.Has("OfficeOpening.TalkHint")) return;
        Truth.Set("OfficeOpening.TalkHint");
        Hint("TALKING TO THE LOST\n[F] talk    [E] next line    [1-5] choose an answer\nYou can speak to them now and return when you know more.",9f);
    }
    // Called before the breaker can consume a fuse, as well as on approaching its empty slots.
    public bool BeforeInteract(Interactable use)
    {
        if(Busy) return false;
        if(breakerUse!=null && use.transform==breakerUse && !FusesDiscovered)
        { ObserveBreaker(); return false; }
        return true;
    }
    public void ObserveBreaker()
    {
        if(FusesDiscovered) return;
        Truth.Set("OfficeOpening.MissingFuses");
        EvidenceJournal.Record("Empty fuse slots","The electrical-room breaker has three empty fuse slots. Find the missing fuses and bring them back.");
        DialogueBox.Instance?.Say(HS5.S("A3V53_missing_fuses"));
        Hint("THE BREAKER\nFind three fuses. [V] enters the Echo, where the burnt contacts still work.",9f);
    }
    IEnumerator Say(string trigger)
    {
        DialogueBox.Instance?.Say(HS5.S(trigger));
        yield return null;
        while(DialogueBox.Instance!=null && DialogueBox.Instance.Busy) yield return null;
    }
    IEnumerator Confrontation()
    {
        Busy=true;
        Lock();
        yield return Say("A3V53_watched");
        encounterLight=ActionLight("Palm mark - confrontation light");
        yield return VaneHunt.Instance.OpeningApproach();
        if(!VaneHunt.Instance.OpeningApproached)
        {
            if(encounterLight!=null) Destroy(encounterLight.gameObject);
            Unlock();Busy=false;
            retryAt=Time.time+8f;   // (not every frame: the "watched" line would replay on a loop)
            Hint("Move into the corridor outside the electrical room.",5f);
            yield break;
        }
        // Face the encounter once; look input is returned for aiming the real flash.
        if(VaneHunt.Instance.Actor!=null) FacePoint(VaneHunt.Instance.Actor.position+Vector3.up*1.45f);
        yield return Say("A3V53_vane_accusation");
        var palm=sequence.player.GetComponentInChildren<PalmLight>(true);
        if(palm!=null) palm.PrepareOpeningFlash();
        AwaitingFlash=true;
        Unlock();
        Hint("FLASH VANE\n[Q] raise your hand. Aim at Vane, then [LEFT CLICK].",999f);
        while(!Cleared) yield return null;
        if(encounterLight!=null) Destroy(encounterLight.gameObject);
        hintTime=0f; AwaitingFlash=false; Busy=false;
        shoutAt=Time.time+14f;
        yield return Say("A3V53_after_flash");
    }
    public void VaneFlashed()
    {
        if(!AwaitingFlash) return;
        Truth.Set("OfficeOpening.Flashed");
    }
    Camera View => sequence.player.GetComponent<FirstPersonController>().playerCamera;
    void FacePoint(Vector3 point)
    {
        var view=View;if(view==null) return;
        Vector3 delta=point-view.transform.position;
        float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
        float pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
        sequence.player.rotation=Quaternion.Euler(0f,yaw,0f);
        view.transform.localRotation=Quaternion.Euler(pitch,0f,0f);
        sequence.player.GetComponent<FirstPersonController>().SetPitch(pitch);
    }
    Light ActionLight(string name)
    {
        var light=new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(View.transform,false);light.transform.localPosition=new Vector3(0f,.12f,.8f);
        light.type=LightType.Spot;light.spotAngle=80f;light.innerSpotAngle=45f;
        light.color=new Color(1f,.85f,.65f);light.range=7f;light.intensity=4f;
        light.shadows=LightShadows.None;return light;
    }
    void Lock()
    {
        foreach(var b in sequence.player.GetComponentsInChildren<Behaviour>(true))
            if((b is FirstPersonController || b is PlayerInteractor || b is PunchController || b is GunController) && b.enabled)
            {b.enabled=false;held.Add(b);}
        if(sequence.player.TryGetComponent<Rigidbody>(out var body)) body.linearVelocity=Vector3.zero;
    }
    void Unlock() {foreach(var b in held) if(b!=null) b.enabled=true;held.Clear();}
    public void BeginPry()
    {
        EndPry();
        pryPunch=sequence.player.GetComponentInChildren<PunchController>(true);
        // only hand punching back in EndPry if we were the ones to take it away
        if(pryPunch!=null) { if(pryPunch.enabled) pryPunch.enabled=false; else pryPunch=null; }
        heldPalm=sequence.player.GetComponentInChildren<PalmLight>(true);
        if(heldPalm!=null) {heldPalm.LowerForAction();palmEnabled=heldPalm.enabled;heldPalm.enabled=false;}
        pryLight=ActionLight("Palm mark - crowbar work light");
        PryVisible=true; PryProgress=0f;
        var source=System.Array.Find(RevisedPlot.All<Interactable>(gameObject.scene),i=>i.giveItem=="Crowbar");
        if(source!=null)
        {
            // Visual mesh copy only: no pickup events, collision, or inventory script is duplicated.
            pryTool=new GameObject("Crowbar levering concealed door");
            pryTool.transform.SetParent(sequence.transform,false);
            pryTool.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.TryGetComponent<MeshFilter>(out var mesh) || mesh.sharedMesh==null) continue;
                var visual=new GameObject(renderer.name);visual.transform.SetParent(pryTool.transform,false);
                visual.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);
                visual.transform.localScale=renderer.transform.lossyScale;
                visual.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                visual.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            }
            var bounds=new Bounds();bool first=true;
            foreach(var r in pryTool.GetComponentsInChildren<Renderer>()) {if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            var center=pryTool.transform.InverseTransformPoint(bounds.center);
            foreach(Transform child in pryTool.transform) child.localPosition-=center;
            float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            if(size>.001f) pryTool.transform.localScale=Vector3.one*(.85f/size);
        }
        reachArms=sequence.player.GetComponentInChildren<PunchController>(true)?.armsAnimator;
        if(reachArms!=null) {reachSpeed=reachArms.speed;reachArms.speed=1f;reachArms.Play("Take",0,0f);}
        UpdatePry(0f);
    }
    public void UpdatePry(float progress)
    {
        PryProgress=Mathf.Clamp01(progress);
        if(pryTool==null) return;
        var cam=View;
        var frame=sequence.concealedDoor.transform;
        Vector3 toward=cam!=null ? cam.transform.position-frame.position : -frame.forward;
        toward.y=0f;toward.Normalize();
        pryTool.transform.position=frame.position+Vector3.up*.05f+frame.right*.38f+toward*.16f;
        FacePoint(pryTool.transform.position);
        pryTool.transform.rotation=Quaternion.LookRotation(toward)*Quaternion.Euler(15f+Mathf.Sin(progress*Mathf.PI*3f)*22f,0,25f);
        // The existing reach take holds the hands against the moving tool during each pull.
        if(reachArms!=null) {reachArms.Play("Take",0,.35f+Mathf.Sin(progress*Mathf.PI*3f)*.12f);reachArms.Update(0f);reachArms.speed=0f;}
    }
    public void EndPry()
    {
        if(pryTool!=null) Destroy(pryTool);
        if(pryLight!=null) Destroy(pryLight.gameObject);
        if(reachArms!=null) {reachArms.speed=reachSpeed;reachArms.Play("Locomotion",0,0f);reachArms=null;}
        if(heldPalm!=null) {heldPalm.enabled=palmEnabled;heldPalm=null;}
        if(pryPunch!=null) {pryPunch.enabled=true;pryPunch=null;}
        PryVisible=false;
    }
    void OnDisable()
    {
        StopAllCoroutines(); Unlock();EndPry();
        if(encounterLight!=null) Destroy(encounterLight.gameObject);
        introducing=false;Busy=AwaitingFlash=false;hintTime=0f;
    }
    public void Retry()
    {
        StopAllCoroutines();Unlock();EndPry();
        if(encounterLight!=null) Destroy(encounterLight.gameObject);
        Busy=AwaitingFlash=introducing=false;
        hintTime=0f;
    }
    void OnGUI()
    {
        if(hintTime<=0f || !InOffice || PauseMenu.Paused || GameOverScreen.Open || TitleScreen.Showing || CutsceneHUD.Hidden
            || (DialogueBox.Instance!=null && (DialogueBox.Instance.Busy || DialogueBox.Instance.CardOpen))) return;
        if(Event.current.type==EventType.Repaint) hintTime=Mathf.Max(0f,hintTime-Time.deltaTime);
        var style=new GUIStyle(GUI.skin.box){font=GameFont.Ttf,fontSize=21,alignment=TextAnchor.MiddleCenter,wordWrap=true,padding=new RectOffset(20,20,12,12)};
        style.normal.textColor=new Color(.95f,.93f,.86f);
        float width=Mathf.Min(Screen.width-40f,900f);
        GUI.Box(new Rect((Screen.width-width)/2f,Screen.height*.69f,width,110f),hint,style);
    }
}
