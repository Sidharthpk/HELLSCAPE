using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Vane himself walks the office, always (it replaces the shadow people). No navmesh in the office, so the floor plan is
// a grid of 0.8 m cells built from the colliders on the first calm moment; he walks A* paths over it.
//   patrol   walks between rooms, stops and looks about; now and then he goes to search one of the dark corners
//   seen     he sees you (range, field of view and a clear line; crouching shrinks it, the palm light and running give you away)
//            -> he runs you down
//   hiding   dark pools on the floor in corners: crouch inside one and he cannot see you. When he comes to search YOUR pool
//            you must hold your breath: squares slide through a heart, [SPACE] as one is on it. Miss once and he looks for
//            longer; miss twice and he has you. Get through it and he stands there for ten seconds, then walks away.
//   caught   he chokes you: the screen closes in (slow vignette, blur, muffled sound). [SPACE] again and again to break his
//            hold, or flash him ([LEFT CLICK]: PalmLight, charged) to blind him. It is faster every time he catches you
//            (and faster still if you failed to hold your breath). Fill the vignette and you are dead.
public class VaneHunt : MonoBehaviour
{
    public static VaneHunt Instance { get; private set; }
    public static bool Seizing { get; private set; }   // his hands are on you

    [Header("Vane")]
    public float patrolSpeed = 1.6f;
    public float chaseSpeed = 5.3f;         // faster than you walk (5), slower than you sprint (7)
    public float sightRange = 16f, crouchSight = 6f, hiddenSight = 1.4f, fieldOfView = 150f;
    public float runHearing = 10f, walkHearing = 5f;   // your footsteps: he hears these behind him too (crouched steps are muffled: never)
    public float firstAppearance = 12f;     // seconds into the act

    [Header("Holding your breath")]
    public float lookTime = 10f;            // how long he stands at your dark corner
    public float missExtra = 5f;            // what the first miss adds
    public float beatGap = 2f;
    public float hitWindow = .22f;          // seconds either side of the heart
    public float squareSpeed = 420f;        // px/s across the screen

    [Header("The choke")]
    public float chokeTime = 8f;            // first time, seconds to black out with no struggle
    public float minChoke = 2.2f;
    public float fastChoke = .55f;          // times, after missing both beats
    public float pressPower = .075f;        // what one [SPACE] takes off
    public float blindTime = 8f;

    OfficeHorrorDirector dir; Transform player; Camera cam; FirstPersonController fpc; PlayerHealth health; Rigidbody body; PalmLight palm;
    Func<bool> canAct, present;
    GameObject vane; Animator anim; AudioSource sfx; AudioClip stomp, thump; Light ember;
    enum S { Away, Patrol, Pause, Search, Look, Chase, Choke, Stagger, Blind }
    S state = S.Away;
    Coroutine task;
    bool busy, carded, gridBuilding;
    int caught;
    float spawnT, pauseT, alert, lostT, repathT, stepT, searchCool, timer, baseScaleY = 1f;
    Vector3 lastSeen;
    bool sequenceHold, officeEnded, officeForm;
    float sequenceGrace, shotReady;
    GameObject human;
    Zombie borrowedForm;
    bool borrowedEnabled;
    readonly Dictionary<Behaviour, bool> borrowedBehaviours = new Dictionary<Behaviour, bool>();
    readonly Dictionary<Rigidbody, (bool kinematic, bool gravity)> borrowedBodies = new Dictionary<Rigidbody, (bool, bool)>();
    public float StaggerRemaining => state == S.Stagger ? timer : 0f;
    public bool BulletImmune => Time.time < shotReady;
    public bool SequenceHeld => sequenceHold;
    public bool OfficeForm => officeForm;
    public Transform Actor => vane != null ? vane.transform : null;
    public bool CanStageAt(Transform marker)
    {
        if (marker == null || cam == null || Flat(marker.position - player.position).magnitude < 3f) return false;
        return LineClear(cam.transform.position, marker.position + Vector3.up * 1.5f);
    }

    // Small explicit override used by the office story. The existing patrol/search/chase loop resumes afterward.
    public bool HoldForSequence(Transform marker)
    {
        if (vane == null) return false;
        if (marker != null)
        {
            Vector3 chosen = marker.position; bool found = false;
            for (int ring = 0; ring <= 6 && !found; ring++)
                for (int i = 0; i < (ring == 0 ? 1 : 12); i++)
                {
                    Vector3 p = marker.position + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * (ring * .4f);
                    if (Flat(p - player.position).magnitude < 3f) continue;
                    if (!Physics.Raycast(p + Vector3.up, Vector3.down, out var floor, 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    p.y = floor.point.y;
                    bool blocked = false;
                    foreach (var c in Physics.OverlapCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.65f, .32f, ~0, QueryTriggerInteraction.Ignore))
                        if (!c.transform.IsChildOf(vane.transform)) { blocked = true; break; }
                    if (blocked) continue;
                    chosen = p; found = true; break;
                }
            if (!found) return false;
            if (task != null) StopCoroutine(task);
            task = null; CloseBreath(); GiveBack(); busy = false;
            vane.transform.SetPositionAndRotation(chosen, marker.rotation);
        }
        if (task != null) StopCoroutine(task);
        task = null; CloseBreath(); GiveBack(); busy = false;
        sequenceHold = true;
        vane.SetActive(true); Move(0f);
        Vector3 face = Flat(player.position - Pos);
        if (face.sqrMagnitude > .01f) vane.transform.rotation = Quaternion.LookRotation(face);
        return true;
    }
    public bool OpeningRouteReady => GridReady;
    bool sequenceMoving;
    public bool OpeningApproached { get; private set; }
    // Runs over the same collision-checked grid as pursuit; never translates through furniture or walls.
    public IEnumerator OpeningApproach()
    {
        OpeningApproached=false;sequenceHold=true; sequenceMoving=true;carded=true;
        path.Clear();pi=0;
        int chosen=-1; float score=float.MaxValue;
        for(int i=0;i<walk.Length;i++)
        {
            if(!walk[i]) continue;
            Vector3 p=CellPos(i);float d=Flat(p-player.position).magnitude;
            if(d<7f || d>13f || !LineClear(cam.transform.position,p+Vector3.up*1.5f)) continue;
            float k=Mathf.Abs(d-9f)+Vector3.Angle(cam.transform.forward,p-cam.transform.position)*.045f;
            if(k<score) {chosen=i;score=k;}
        }
        if(chosen<0)
        {
            // A cramped doorway can have no nine-metre sightline. Use the nearest visible clear cell.
            for(int i=0;i<walk.Length;i++)
            {
                if(!walk[i]) continue;
                float d=Flat(CellPos(i)-player.position).magnitude;
                if(d<3.2f || d>14f || d>=score || !LineClear(cam.transform.position,CellPos(i)+Vector3.up*1.5f)) continue;
                chosen=i;score=d;
            }
        }
        if(chosen<0) {sequenceMoving=false;sequenceHold=false;yield break;}
        vane.transform.position=CellPos(chosen);vane.SetActive(true);state=S.Pause;
        Vector3 approach=Flat(Pos-player.position).normalized;
        Vector3 stop=player.position+approach*3.1f;
        if(Route(Pos,stop))
            for(float t=0f;t<8f && Flat(Pos-player.position).magnitude>3.2f;t+=Time.deltaTime)
            {
                if(!PauseMenu.Paused && Follow(chaseSpeed)) break;
                yield return null;
            }
        sequenceMoving=false;OpeningApproached=true;Move(0f);
        Turn(player.position-Pos,1000f);
    }

    public void ReleaseSequence(bool chase, float grace = 0f)
    {
        sequenceHold = false; sequenceGrace = Mathf.Max(0f, grace);
        busy = false; path.Clear(); pi = 0; alert = 0f;
        if (vane == null || !vane.activeSelf) return;
        if (chase) { lastSeen = player.position; Pursue(); }
        else { state = S.Pause; pauseT = 2f; }
    }
    public void PlaySequenceClip(AnimationClip clip)
    {
        if (clip == null || anim == null) return;
        // Authored controllers may expose a transformation state. Without it the sequence uses the existing fade.
        if (anim.HasState(0, Animator.StringToHash(clip.name))) anim.CrossFadeInFixedTime(clip.name, .15f);
    }
    public void UseOfficeForm(Zombie form)
    {
        if (officeForm || form == null || vane == null) return;
        Vector3 p = Pos; Quaternion r = vane.transform.rotation;
        human = vane; human.SetActive(false);
        vane = form.gameObject;
        borrowedForm = form; borrowedEnabled = form.enabled;
        form.enabled = false;  // office shots go through OfficeVaneHit, never Zombie.TakeDamage
        foreach (var ai in vane.GetComponentsInChildren<ZombieAI>(true)) { borrowedBehaviours[ai] = ai.enabled; ai.enabled = false; }
        foreach (var agent in vane.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) { borrowedBehaviours[agent] = agent.enabled; agent.enabled = false; }
        foreach (var rb in vane.GetComponentsInChildren<Rigidbody>(true))
        { borrowedBodies[rb] = (rb.isKinematic, rb.useGravity); rb.isKinematic = true; rb.useGravity = false; }
        anim = form.animator;
        if (anim == null) foreach (var a in vane.GetComponentsInChildren<Animator>(true))
            if (a.enabled && a.runtimeAnimatorController != null) { anim = a; break; }
        if (anim != null) { anim.applyRootMotion = false; anim.speed = 1f; }
        vane.transform.SetPositionAndRotation(p, r); vane.SetActive(true);
        officeForm = true; clip = null;
        var receiver = vane.GetOrAdd<OfficeVaneHit>(); receiver.hunt = this;
        // Keep audio and the ember on the active form, using the same sources.
        sfx.transform.SetParent(vane.transform, false);
        ember.transform.SetParent(vane.transform, false); ember.transform.localPosition = Vector3.up * 1.3f;
        if (vane.GetComponentsInChildren<Collider>().Length == 0)
        {
            var c = vane.AddComponent<CapsuleCollider>(); c.center = Vector3.up; c.height = 2f; c.radius = .4f;
        }
        Move(0f);
    }
    public bool BulletHit()
    {
        var seq = OfficeV52Sequence.Instance;
        if (!officeForm || seq == null || seq.Completed) return false;
        if (sequenceHold || Time.time < shotReady) return true; // still absorb: no damage or defeat
        shotReady = Time.time + Mathf.Max(seq.staggerCooldown, seq.staggerDuration + .1f);
        sequenceGrace = 0f;
        if (task != null) StopCoroutine(task);
        task = null; busy = false; CloseBreath(); GiveBack();
        state = S.Stagger; timer = Mathf.Max(.1f, seq.staggerDuration); Move(0f);
        if (anim != null && anim.HasState(0, Animator.StringToHash("Hit"))) anim.CrossFadeInFixedTime("Hit", .1f);
        return true;
    }
    public void EndOfficeSequence()
    {
        officeEnded = true; sequenceHold = false; ResetAll(false);
        if (borrowedForm != null) borrowedForm.enabled = borrowedEnabled;
        foreach (var pair in borrowedBehaviours) if (pair.Key != null) pair.Key.enabled = pair.Value;
        foreach (var pair in borrowedBodies) if (pair.Key != null) { pair.Key.isKinematic = pair.Value.kinematic; pair.Key.useGravity = pair.Value.gravity; }
        borrowedBehaviours.Clear(); borrowedBodies.Clear();
        if (sfx != null) { sfx.Stop(); if (human != null) sfx.transform.SetParent(human.transform, false); }
        if (ember != null) { ember.enabled = false; if (human != null) ember.transform.SetParent(human.transform, false); }
    }
    public void RefreshOfficeRoute()
    {
        // The original grid only includes reachable cells. The breach makes a previously sealed room reachable.
        if (!gridBuilding) { walk = null; path.Clear(); pi = 0; }
    }

    // ------------------------------------------------------------------ setup

    public void Init(OfficeHorrorDirector director, Transform playerBody, Camera camera, FirstPersonController controller, PlayerHealth hp, Transform template, GameObject vaneTemplate, Func<bool> act, Func<bool> inOffice)
    {
        Instance = this;
        dir = director; player = playerBody; cam = camera; fpc = controller; health = hp; canAct = act; present = inOffice;
        body = player.GetComponent<Rigidbody>(); palm = player.GetComponentInChildren<PalmLight>(true);
        baseScaleY = player.localScale.y;
        spawnT = firstAppearance;

        var human = Resources.Load<GameObject>("VaneHuman");   // the man himself (Meshy Vane: Idle, Walk and Run states); the monster form only comes later
        if (human != null)
        {
            vane = Instantiate(human, transform);
            vane.transform.localScale = Vector3.one;
        }
        else if (vaneTemplate != null)
        {
            // fallback: his damned body, stripped of ragdoll and scripts; the original is inactive, so the copy is too
            vane = Instantiate(vaneTemplate);
            vane.transform.SetParent(transform, false);
            vane.transform.localScale = vaneTemplate.transform.lossyScale;
            foreach (var c in vane.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);   // (scripts first: they may require the rigidbody)
            foreach (var c in vane.GetComponentsInChildren<Behaviour>(true)) if (c != null && !(c is Animator)) DestroyImmediate(c);
            foreach (var c in vane.GetComponentsInChildren<CharacterJoint>(true)) DestroyImmediate(c);
            foreach (var c in vane.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(c);
            foreach (var c in vane.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        }
        else if (template != null)
        {
            vane = OfficeBodies.Clone(template);
            vane.transform.SetParent(transform, false);
            vane.transform.localScale = template.lossyScale * 1.1f;
            var mat = WitnessLook.Load("AuditorSkin");
            foreach (var r in vane.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
        }
        if (vane == null) { enabled = false; return; }
        vane.name = "Vane (hunting)";
        // (the root of his old body carries a dead Animator; the Cry Wolf body under it is the one that moves)
        foreach (var a in vane.GetComponentsInChildren<Animator>(true))
            if (a.runtimeAnimatorController != null && a.enabled && anim == null) anim = a;
            else if (a != anim) a.enabled = false;
        if (anim != null) anim.applyRootMotion = false;
        sfx = new GameObject("Vane sfx").AddComponent<AudioSource>();
        sfx.transform.SetParent(vane.transform, false); sfx.spatialBlend = 1f; sfx.maxDistance = 28f; sfx.rolloffMode = AudioRolloffMode.Linear;
        // a dull ember glow: in the blacked-out office he is only ever this (and his footsteps)
        ember = new GameObject("Vane ember").AddComponent<Light>();
        ember.transform.SetParent(vane.transform, false); ember.transform.localPosition = Vector3.up * 1.3f;
        ember.type = LightType.Point; ember.color = new Color(1f, .45f, .25f); ember.range = 5f; ember.intensity = 1f; ember.shadows = LightShadows.None;
        vane.SetActive(false);
        thump = Thump();
        // the body bags are gone from the office
        foreach (var b in RevisedPlot.All<BodyBagScare>(gameObject.scene)) Destroy(b.gameObject);
    }

    void OnDestroy() { if (Instance == this) Instance = null; Seizing = false; PalmLight.FlashAnytime = false; if (stomp != null) Destroy(stomp); if (thump != null) Destroy(thump); }

    static AudioClip Thump()
    {
        int rate = 22050, n = (int)(rate * .35f);
        var d = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)rate; d[i] = Mathf.Exp(-t * 14f) * Mathf.Sin(t * 2f * Mathf.PI * 58f) * .9f; }
        var c = AudioClip.Create("VaneThump", n, 1, rate, false); c.SetData(d, 0);
        return c;
    }

    // ------------------------------------------------------------------ the floor plan

    const float Cell = .8f;
    bool[] walk; int nx, nz; Vector3 org; float floorY;
    float[] gs, fs; int[] from, seen; int stamp;
    readonly List<Vector3> path = new List<Vector3>(); int pi;
    class Spot { public Vector3 pos; public GameObject pool; }
    readonly List<Spot> spots = new List<Spot>();
    bool GridReady => walk != null;

    IEnumerator BuildGrid()
    {
        gridBuilding = true;
        if (!Physics.Raycast(player.position + Vector3.up * .5f, Vector3.down, out var g, 3f, ~0, QueryTriggerInteraction.Ignore)) { gridBuilding = false; yield break; }
        floorY = g.point.y;
        var z = OfficeHorrorDirector.Zone;
        org = new Vector3(z.min.x, 0f, z.min.z);
        nx = Mathf.CeilToInt(z.size.x / Cell); nz = Mathf.CeilToInt(z.size.z / Cell);
        var ok = new bool[nx * nz]; var buf = new Collider[8]; int work = 0;
        for (int zi = 0; zi < nz; zi++)
            for (int xi = 0; xi < nx; xi++)
            {
                ok[zi * nx + xi] = Open(new Vector3(org.x + (xi + .5f) * Cell, floorY, org.z + (zi + .5f) * Cell), buf);
                if (++work % 500 == 0) yield return null;
            }
        // only what you could walk to from where you stand (not the street, not a shut-in room)
        var reach = new bool[ok.Length];
        int start = -1;
        int px = Mathf.Clamp((int)((player.position.x - org.x) / Cell), 0, nx - 1), pz = Mathf.Clamp((int)((player.position.z - org.z) / Cell), 0, nz - 1);
        for (int r = 0; r < 6 && start < 0; r++)
            for (int dz = -r; dz <= r && start < 0; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = px + dx, zz = pz + dz;
                    if (x >= 0 && zz >= 0 && x < nx && zz < nz && ok[zz * nx + x]) { start = zz * nx + x; break; }
                }
        if (start < 0) { Debug.LogWarning("[VaneHunt] no walkable floor round the player: no patrol"); gridBuilding = false; yield break; }
        var stack = new Stack<int>(); stack.Push(start); reach[start] = true;
        while (stack.Count > 0)
        {
            int c = stack.Pop(); int cx = c % nx, cz = c / nx;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, zz = cz + dz;
                    if ((dx == 0 && dz == 0) || x < 0 || zz < 0 || x >= nx || zz >= nz) continue;
                    int n = zz * nx + x;
                    if (ok[n] && !reach[n]) { reach[n] = true; stack.Push(n); }
                }
            if (++work % 4000 == 0) yield return null;
        }
        gs = new float[reach.Length]; fs = new float[reach.Length]; from = new int[reach.Length]; seen = new int[reach.Length];
        PickSpots(reach);
        walk = reach;
        gridBuilding = false;
        Debug.Log("[VaneHunt] floor plan " + nx + "x" + nz + ", " + CountTrue(reach) + " cells, " + spots.Count + " hiding spots");
    }

    static int CountTrue(bool[] a) { int n = 0; foreach (var b in a) if (b) n++; return n; }

    bool Open(Vector3 p, Collider[] buf)
    {
        if (!Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var h, 1.6f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < .9f || Mathf.Abs(h.point.y - floorY) > .25f) return false;
        if (!Physics.Raycast(p + Vector3.up * 1.9f, Vector3.up, 8f, ~0, QueryTriggerInteraction.Ignore)) return false;   // open sky: outside
        int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * .4f, p + Vector3.up * 1.7f, .3f, buf, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) if (!buf[i].transform.IsChildOf(player.root)) return false;
        return true;
    }

    // dark pools in the corners and alcoves: spread over the floor, never on top of you
    void PickSpots(bool[] reach)
    {
        bool W(int x, int z) => x >= 0 && z >= 0 && x < nx && z < nz && reach[z * nx + x];
        var cand = new List<int>();
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                if (!W(x, z)) continue;
                int near = 0, ring = 0;
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (dx == 0 && dz == 0 || W(x + dx, z + dz)) continue;
                        ring++; if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1) near++;
                    }
                if (near >= 3 && ring >= 13) cand.Add(z * nx + x);
            }
        var rng = new System.Random(7);
        for (int i = cand.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (cand[i], cand[j]) = (cand[j], cand[i]); }
        Texture2D tex = null; Material mat = null;
        foreach (int c in cand)
        {
            if (spots.Count >= 14) break;
            Vector3 p = new Vector3(org.x + (c % nx + .5f) * Cell, floorY, org.z + (c / nx + .5f) * Cell);
            bool far = Vector3.Distance(p, player.position) > 6f;
            foreach (var s in spots) if (Vector3.Distance(s.pos, p) < 8f) far = false;
            if (!far) continue;
            if (tex == null) { tex = PoolTexture(); mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = new Color(0f, 0f, 0f, .92f) }; }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            q.name = "Hiding spot (dark)"; q.transform.SetParent(transform, true);
            q.transform.position = p + Vector3.up * .03f; q.transform.rotation = Quaternion.Euler(90f, 0f, 0f); q.transform.localScale = Vector3.one * 2.6f;
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            spots.Add(new Spot { pos = p, pool = q });
        }
    }

    static Texture2D PoolTexture()
    {
        var t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
                t.SetPixel(x, y, new Color(0, 0, 0, Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f)));
            }
        t.Apply(); return t;
    }

    bool Near(Vector3 p, out int cell)
    {
        cell = -1;
        int cx = (int)((p.x - org.x) / Cell), cz = (int)((p.z - org.z) / Cell);
        for (int r = 0; r <= 4; r++)
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x >= 0 && z >= 0 && x < nx && z < nz && walk[z * nx + x]) { cell = z * nx + x; return true; }
                }
        return false;
    }

    Vector3 CellPos(int i) => new Vector3(org.x + (i % nx + .5f) * Cell, floorY, org.z + (i / nx + .5f) * Cell);

    // A* over the 8-connected cells; fills `path` (cell centres, start to goal)
    bool Route(Vector3 a, Vector3 b)
    {
        path.Clear(); pi = 0;
        if (!Near(a, out int s) || !Near(b, out int t)) return false;
        stamp++;
        var open = new SortedSet<(float f, int i)>();
        gs[s] = 0f; fs[s] = H(s, t); from[s] = -1; seen[s] = stamp; open.Add((fs[s], s));
        while (open.Count > 0)
        {
            var top = open.Min; open.Remove(top);
            int c = top.i;
            if (c == t) break;
            int cx = c % nx, cz = c / nx;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if ((dx == 0 && dz == 0) || x < 0 || z < 0 || x >= nx || z >= nz) continue;
                    int n = z * nx + x;
                    if (!walk[n]) continue;
                    if (dx != 0 && dz != 0 && (!walk[cz * nx + x] || !walk[z * nx + cx])) continue;   // no cutting corners
                    float g = gs[c] + (dx != 0 && dz != 0 ? 1.414f : 1f);
                    if (seen[n] == stamp && g >= gs[n]) continue;
                    if (seen[n] == stamp) open.Remove((fs[n], n));
                    seen[n] = stamp; gs[n] = g; fs[n] = g + H(n, t); from[n] = c;
                    open.Add((fs[n], n));
                }
        }
        if (seen[t] != stamp) return false;
        for (int c = t; c >= 0; c = from[c]) path.Add(CellPos(c));
        path.Reverse();
        return true;
    }

    float H(int a, int b)
    {
        float dx = Mathf.Abs(a % nx - b % nx), dz = Mathf.Abs(a / nx - b / nx);
        return Mathf.Max(dx, dz) + .414f * Mathf.Min(dx, dz);
    }

    // ------------------------------------------------------------------ moving, seeing

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    Vector3 Pos => vane.transform.position;
    bool Crouched => player.localScale.y < baseScaleY * .9f;

    Spot HiddenIn()
    {
        if (!Crouched) return null;
        foreach (var s in spots) if (Flat(player.position - s.pos).magnitude < 1.25f) return s;
        return null;
    }

    bool Follow(float speed)
    {
        while (pi < path.Count && Flat(path[pi] - Pos).sqrMagnitude < .09f) pi++;
        if (pi >= path.Count) { Move(0f); return true; }
        Vector3 to = Flat(path[pi] - Pos);
        float step = Mathf.Min(speed * Time.deltaTime, to.magnitude);
        Vector3 p = Pos + to.normalized * step; p.y = floorY;
        vane.transform.position = p;
        Turn(to, 9f);
        Move(speed);
        stepT -= Time.deltaTime;
        if (stepT <= 0f) { stepT = speed > 3f ? .3f : .62f; sfx.pitch = UnityEngine.Random.Range(.85f, 1.05f); sfx.PlayOneShot(stomp != null ? stomp : stomp = ProceduralAudio.Stomp(), speed > 3f ? .8f : .4f); }
        return false;
    }

    void Turn(Vector3 dir, float rate)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > .001f) vane.transform.rotation = Quaternion.Slerp(vane.transform.rotation, Quaternion.LookRotation(dir), rate * Time.deltaTime);
    }

    // his controller (VaneHuman): Idle, Walk, Run
    string clip;
    void Move(float v)
    {
        if (anim == null || !anim.isActiveAndEnabled) return;
        string st = v > 2.8f ? "Run" : v > .05f ? "Walk" : "Idle";
        if (st == clip) return;
        clip = st;
        if (anim.HasState(0, Animator.StringToHash(st))) anim.CrossFadeInFixedTime(st, .2f, 0);
        else if (officeForm)
        {
            foreach (var parameter in anim.parameters)
                if (parameter.name == "Speed") { anim.SetFloat("Speed", v); break; }
        }
    }

    bool LineClear(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; float len = d.magnitude;
        foreach (var h in Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(player.root) && !h.transform.IsChildOf(vane.transform)) return false;
        return true;
    }

    bool Sees(out float dist)
    {
        Vector3 flat = Flat(player.position - Pos); dist = flat.magnitude;
        bool hiding = HiddenIn() != null, crouched = Crouched;
        float range = hiding ? hiddenSight : crouched ? crouchSight : sightRange;
        if (palm != null && palm.Held && !hiding) range *= 1.3f;   // a light in your hand gives you away
        float pace = body != null ? Flat(body.linearVelocity).magnitude : 0f;
        bool noisy = !crouched && (pace > 5.6f ? dist < runHearing : pace > .5f && dist < walkHearing);   // running, or walking close
        if (dist > range && !noisy) return false;
        if (!noisy && dist > 2.5f && Vector3.Angle(vane.transform.forward, flat) > fieldOfView * .5f) return false;
        return LineClear(Pos + Vector3.up * 1.6f, cam.transform.position);
    }

    // ------------------------------------------------------------------ the loop

    void Update()
    {
        if (player == null) return;
        if (GameOverScreen.Open) { if (state != S.Away || busy) ResetAll(true); return; }
        if (officeEnded) return;
        if (sequenceHold) { if(!sequenceMoving) Move(0f); return; }
        if (!present()) { if (state != S.Away || busy) ResetAll(false); return; }
        if (busy) return;
        if (!GridReady)
        {
            if (!gridBuilding && canAct()) StartCoroutine(BuildGrid());
            return;
        }
        var opening=OfficeV52Sequence.Instance!=null ? OfficeV52Sequence.Instance.Opening : null;
        if(opening!=null && !opening.Cleared) { if(vane.activeSelf) vane.SetActive(false); return; }
        bool puzzleThreat = officeForm && OfficeV52Sequence.Instance != null && OfficeV52Sequence.Instance.PursueDuringPuzzle;
        if (!canAct() && !puzzleThreat) { if (state != S.Away) Move(0f); return; }
        float dt = Time.deltaTime;
        if (sequenceGrace > 0f) { sequenceGrace -= dt; Move(0f); return; }
        searchCool -= dt;
        switch (state)
        {
            case S.Away:
                spawnT -= dt;
                if (spawnT <= 0f) Spawn();
                break;
            case S.Patrol:
                if (Notice(dt)) break;
                if (Follow(patrolSpeed)) { state = S.Pause; pauseT = UnityEngine.Random.Range(1.5f, 3.5f); }
                break;
            case S.Pause:
                if (Notice(dt)) break;
                Move(0f);
                vane.transform.Rotate(0f, Mathf.Sin(Time.time * .9f) * 25f * dt, 0f);   // looks about
                pauseT -= dt;
                if (pauseT <= 0f) Next();
                break;
            case S.Search:
                if (Notice(dt)) break;
                if (Follow(patrolSpeed * 1.15f)) task = StartCoroutine(Look(target));
                break;
            case S.Chase: Chase(dt); break;
            case S.Stagger:
                Move(0f); timer -= dt;
                if (timer <= 0f) Pursue();
                break;
            case S.Blind:
                timer -= dt;
                if (Follow(1f) && timer > 0f) Wander();
                if (timer <= 0f) { searchCool = 15f; state = S.Pause; pauseT = 2f; }
                break;
        }
    }

    // robot tests: where he is and what he is doing, and put him a walk away from you
    public string Report => state + " busy=" + busy + " caught=" + caught + " spots=" + spots.Count + " pos=" + (vane != null ? vane.transform.position.ToString("F1") : "-");
    public void PlaceNear(float metres)
    {
        if (!GridReady) return;
        for (int i = 0; i < 400; i++)
        {
            int c = UnityEngine.Random.Range(0, walk.Length);
            if (!walk[c]) continue;
            float d = Flat(CellPos(c) - player.position).magnitude;
            if (Mathf.Abs(d - metres) > 2f) continue;
            vane.transform.position = CellPos(c); vane.SetActive(true); state = S.Pause; pauseT = 1f; alert = 0f; return;
        }
    }

    void Spawn()
    {
        // somewhere well away from you
        for (int i = 0; i < 60; i++)
        {
            int c = UnityEngine.Random.Range(0, walk.Length);
            if (!walk[c]) continue;
            Vector3 p = CellPos(c);
            if (Flat(p - player.position).magnitude < 28f) continue;
            vane.transform.position = p; vane.SetActive(true);
            state = S.Pause; pauseT = 1f; alert = 0f;
            if (!carded && DialogueBox.Instance != null)
            {
                carded = true;
                DialogueBox.Instance.ShowCard("VANE",
                    "He walks the office. <color=#C70D0A>Listen</color> for him\n" +
                    "<color=#C70D0A>[CTRL]</color> crouch in a <color=#C70D0A>dark corner</color> and he cannot see you\n" +
                    "If he comes for you: <color=#C70D0A>[SPACE]</color> tear his hands off, or <color=#C70D0A>[Q]</color> + <color=#C70D0A>[LEFT CLICK]</color> flash him blind");
            }
            return;
        }
        spawnT = 5f;
    }

    // he catches sight of you: the alert fills faster the closer you are
    bool Notice(float dt)
    {
        if (Sees(out float d)) alert += dt / Mathf.Lerp(.3f, 1.1f, Mathf.Clamp01(d / sightRange));
        else alert = Mathf.Max(0f, alert - dt * .8f);
        if (alert < 1f) return false;
        alert = 0f; lastSeen = player.position;
        sfx.pitch = .8f; sfx.PlayOneShot(Sounds.Clip("Office/Vane jumpscare", null) ?? ProceduralAudio.Rumble(1.2f), .8f);
        Pursue();
        return true;
    }

    void Pursue() { state = S.Chase; lostT = 0f; repathT = 0f; }

    void Chase(float dt)
    {
        bool sees = Sees(out float d);
        if (sees) { lastSeen = player.position; lostT = 0f; } else lostT += dt;
        repathT -= dt;
        if (repathT <= 0f) { repathT = .35f; Route(Pos, sees || lostT < 1f ? player.position : lastSeen); }
        bool end = Follow(Mathf.Min(chaseSpeed * (1f + .05f * caught), 6.2f));
        if (Flat(player.position - Pos).magnitude < 1.35f && LineClear(Pos + Vector3.up * 1.2f, player.position + Vector3.up * .8f))
        {
            if (officeForm && OfficeV52Sequence.Instance != null && OfficeV52Sequence.Instance.ClosePuzzleForThreat()) return;
            task = StartCoroutine(Choke(false)); return;
        }
        if (lostT > 5f || (end && !sees))
        {
            // lost you: he goes to where he saw you last, then looks in the nearest dark corner
            Spot s = Closest(lastSeen);
            if (s != null && Flat(s.pos - lastSeen).magnitude < 14f) GoSearch(s);
            else { state = S.Pause; pauseT = 2f; }
        }
    }

    Spot Closest(Vector3 p)
    {
        Spot best = null; float bd = float.MaxValue;
        foreach (var s in spots) { float d = Flat(s.pos - p).sqrMagnitude; if (d < bd) { bd = d; best = s; } }
        return best;
    }

    Spot target;
    void GoSearch(Spot s)
    {
        target = s;
        Vector3 approach = Flat(Pos - s.pos).sqrMagnitude > 4f ? Flat(Pos - s.pos).normalized : Vector3.forward;
        if (Route(Pos, s.pos + approach * 2.1f)) state = S.Search;
        else { state = S.Pause; pauseT = 1.5f; }
    }

    void Next()
    {
        // when you are crouched in a dark corner he is drawn to it
        var mine = HiddenIn();
        if (searchCool <= 0f && spots.Count > 0)
        {
            if (mine != null && UnityEngine.Random.value < .7f) { GoSearch(mine); return; }
            if (UnityEngine.Random.value < .3f) { GoSearch(Closest(player.position)); return; }
        }
        Wander();
    }

    void Wander()
    {
        for (int i = 0; i < 20; i++)
        {
            int c = UnityEngine.Random.Range(0, walk.Length);
            if (!walk[c]) continue;
            float d = Flat(CellPos(c) - Pos).magnitude;
            if (d < 10f || d > 45f) continue;
            if (Route(Pos, CellPos(c))) { state = S.Patrol; return; }
        }
        state = S.Pause; pauseT = 2f;
    }

    // a flash: whoever it hits full in the face is blind for a while (the choke checks this itself)
    public void OnFlash(Vector3 eye, Vector3 forward, float range)
    {
        var opening=OfficeV52Sequence.Instance!=null ? OfficeV52Sequence.Instance.Opening : null;
        if(opening!=null && opening.AwaitingFlash && vane!=null && vane.activeSelf)
        {
            Vector3 target=Pos+Vector3.up*1.5f-eye;
            if(target.magnitude<=range && Vector3.Angle(forward,target)<=60f && LineClear(eye,Pos+Vector3.up*1.5f))
            {
                sequenceHold=sequenceMoving=false;state=S.Pause;
                opening.VaneFlashed();Blind();
            }
            return;
        }
        if (sequenceHold || (officeForm && OfficeV52Sequence.Instance != null && !OfficeV52Sequence.Instance.palmFlashUsable)) return;
        if (vane == null || !vane.activeSelf || state == S.Away || state == S.Choke) return;
        Vector3 to = Pos + Vector3.up * 1.5f - eye;
        if (to.magnitude > range || Vector3.Angle(forward, to) > 60f || !LineClear(eye, Pos + Vector3.up * 1.5f)) return;
        Blind();
    }

    void Blind()
    {
        if (task != null) StopCoroutine(task);
        task = null; busy = false; CloseBreath();
        state = S.Blind; timer = blindTime; alert = 0f;
        Wander(); state = S.Blind;
        sfx.pitch = .7f; sfx.PlayOneShot(ProceduralAudio.Thud(), .9f);
    }

    // ------------------------------------------------------------------ holding your breath

    class Beat { public float at; public RectTransform sq; public Image img; public bool done, thumped, hit; }
    RectTransform heartBox; Image heart; TextMeshProUGUI breathText, chokeText;
    Canvas canvas; RectTransform vig; Image vigImg, black;
    Volume blur; AudioLowPassFilter lowpass;
    bool breathCaught, breathLock, jumpWas;
    // what the choke switched off (given back when it ends, or when he is taken away)
    bool heldFpc, heldPunch; PunchController punchRef;

    IEnumerator Look(Spot s)
    {
        busy = true; state = S.Look; Move(0f);
        for (float t = 0f; t < .6f; t += Time.deltaTime) { Turn(s.pos - Pos, 8f); yield return null; }
        if (HiddenIn() != s)
        {
            // nobody here: a few seconds of looking, and if you stand up in view he has you
            for (float t = 0f; t < 3.5f; t += Time.deltaTime)
            {
                if (canAct() && Sees(out _)) { busy = false; lastSeen = player.position; Pursue(); yield break; }
                Turn(s.pos - Pos, 3f); yield return null;
            }
            searchCool = 8f; busy = false; state = S.Pause; pauseT = .5f;
            yield break;
        }
        yield return Breath(s);
        if (breathCaught) { yield return Choke(true); yield break; }
        // he stood there for ten seconds and found nothing: gone
        searchCool = 25f; busy = false; Wander();
    }

    IEnumerator Breath(Spot s)
    {
        BuildUi();
        heartBox.gameObject.SetActive(true);
        breathLock = true; jumpWas = fpc.enableJump; fpc.playerCanMove = false; fpc.enableJump = false;
        float t0 = Time.time, end = t0 + lookTime;
        var beats = new List<Beat>();
        for (float at = t0 + 1.6f; at < end - .3f; at += beatGap) beats.Add(Make(at));
        int misses = 0; bool extended = false; breathCaught = false;
        breathText.text = "HOLD YOUR BREATH\n<size=60%>[SPACE] as a square is on the heart</size>";
        while (true)
        {
            float now = Time.time;
            if (!canAct() && !GameOverScreen.Open)
            {
                // a line of dialogue or the pause: the beats wait with you
                foreach (var b in beats) b.at += Time.deltaTime;
                end += Time.deltaTime; yield return null; continue;
            }
            if (HiddenIn() != s) { breathCaught = true; break; }   // you stood up
            Turn(s.pos - Pos, 3f);
            bool press = Input.GetKeyDown(KeyCode.Space);
            bool pending = false;
            foreach (var b in beats)
            {
                if (b.done) continue;
                if (!b.thumped && now >= b.at) { b.thumped = true; sfx.pitch = 1f; sfx.PlayOneShot(thump, .7f); heart.rectTransform.localScale = Vector3.one * 1.35f; }
                if (press && Mathf.Abs(now - b.at) <= hitWindow) { b.done = b.hit = true; press = false; b.img.color = new Color(.3f, 1f, .4f); continue; }
                if (now > b.at + hitWindow) { b.done = true; misses++; b.img.color = new Color(1f, .1f, .1f); heart.color = Color.black; continue; }
                pending = true;
            }
            if (press) { misses++; heart.color = Color.black; }   // off the beat
            heart.color = Color.Lerp(heart.color, new Color(.85f, .05f, .08f), 6f * Time.deltaTime);
            heart.rectTransform.localScale = Vector3.Lerp(heart.rectTransform.localScale, Vector3.one, 8f * Time.deltaTime);
            if (misses >= 2) { breathCaught = true; break; }
            if (misses == 1 && !extended)
            {
                // he looks for longer
                extended = true; end += missExtra;
                float last = 0f; foreach (var b in beats) last = Mathf.Max(last, b.at);
                for (float at = Mathf.Max(last, now) + beatGap; at < end - .3f; at += beatGap) beats.Add(Make(at));
                breathText.text = "HE IS STILL LOOKING\n<size=60%>one more mistake and he has you</size>";
            }
            foreach (var b in beats) Place(b, now);
            if (!pending && now >= end - .3f) break;
            yield return null;
        }
        CloseBreath();
    }

    Beat Make(float at)
    {
        var img = new GameObject("beat", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(heartBox, false); img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(34f, 34f); img.rectTransform.anchoredPosition = new Vector2(2000f, 0f);
        return new Beat { at = at, sq = img.rectTransform, img = img };
    }

    void Place(Beat b, float now)
    {
        float x = (b.at - now) * squareSpeed;
        b.sq.anchoredPosition = new Vector2(x, 0f);
        b.sq.gameObject.SetActive(x < 1100f && x > -300f);
    }

    void CloseBreath()
    {
        if (heartBox != null) { foreach (Transform c in heartBox) if (c.name == "beat") Destroy(c.gameObject); heartBox.gameObject.SetActive(false); }
        if (breathLock && fpc != null) { fpc.playerCanMove = true; fpc.enableJump = jumpWas; }
        breathLock = false;
    }

    // ------------------------------------------------------------------ the choke

    IEnumerator Choke(bool fast)
    {
        busy = true; state = S.Choke; Seizing = true; caught++;
        BuildUi();
        punchRef = player.GetComponentInChildren<PunchController>(true); heldPunch = punchRef != null && punchRef.enabled;
        if (punchRef != null) punchRef.enabled = false;
        heldFpc = fpc.enabled; fpc.enabled = false;
        if (body != null) body.linearVelocity = Vector3.zero;
        PalmLight.FlashAnytime = true;
        float t0 = Time.time;
        Transform c = cam.transform;

        // he is on you
        Vector3 away = Flat(Pos - player.position); if (away.sqrMagnitude < .01f) away = Flat(-player.forward);
        Vector3 hold = player.position + away.normalized * .9f; hold.y = floorY;
        for (float t = 0f; t < .25f; t += Time.deltaTime)
        {
            vane.transform.position = Vector3.Lerp(vane.transform.position, hold, 18f * Time.deltaTime);
            Turn(player.position - Pos, 14f); Move(0f);
            c.rotation = Quaternion.Slerp(c.rotation, Quaternion.LookRotation(Pos + Vector3.up * 1.6f - c.position), 14f * Time.deltaTime);
            yield return null;
        }
        vane.transform.position = hold;
        sfx.pitch = .8f; sfx.PlayOneShot(ProceduralAudio.Thud(), 1f);
        float full = Mathf.Max(minChoke, chokeTime / (1f + .6f * (caught - 1)));
        if (fast) full = Mathf.Max(minChoke * .8f, full * fastChoke);
        float p = .3f, beatT = 0f; bool freed = false;
        chokeText.gameObject.SetActive(true);
        if (lowpass == null) { var l = FindFirstObjectByType<AudioListener>(); if (l != null) lowpass = l.GetComponent<AudioLowPassFilter>() ?? l.gameObject.AddComponent<AudioLowPassFilter>(); }
        while (p < 1f)
        {
            float dt = Time.deltaTime;
            p += dt / full;
            if (Input.GetKeyDown(KeyCode.Space)) p -= pressPower;
            if (PalmLight.FlashedAt > t0) { freed = true; Blind(true); break; }   // flash: he lets go, blind
            if (p <= 0f) { freed = true; break; }
            p = Mathf.Clamp(p, 0f, 1.2f);
            Choking(p, dt, ref beatT);
            chokeText.text = "<color=#C70D0A>[SPACE]</color> tear his hands away" + (palm != null && palm.Charge >= 1f ? "\n<color=#C70D0A>[LEFT CLICK]</color> flash him" : "");
            Turn(player.position - Pos, 10f);
            c.rotation = Quaternion.Slerp(c.rotation, Quaternion.LookRotation(Pos + Vector3.up * 1.6f - c.position), 10f * dt)
                * Quaternion.Euler(UnityEngine.Random.Range(-1f, 1f) * (.5f + 2f * p), UnityEngine.Random.Range(-1f, 1f) * (.5f + 2f * p), Mathf.Sin(Time.time * 1.6f) * 5f * p);
            yield return null;
        }
        chokeText.gameObject.SetActive(false);
        if (!freed)
        {
            // black. Nothing more of you.
            black.color = Color.black;
            if (health != null && !health.IsDead) health.TakeDamage(health.maxHealth * 2f);
            yield break;   // (the game-over screen ends it: ResetAll)
        }
        if (state != S.Blind)
        {
            // thrown off: he reels back, then comes again
            Vector3 push = Flat(Pos - player.position).normalized;
            vane.transform.position = new Vector3(Pos.x + push.x * 1.6f, floorY, Pos.z + push.z * 1.6f);
            state = S.Stagger; timer = 2.5f; sfx.pitch = .7f; sfx.PlayOneShot(ProceduralAudio.Thud(), .8f);
        }
        GiveBack();
        // easing the screen back
        float dummy = 0f;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { Choking(.55f * (1f - t / 1.2f), 0f, ref dummy); yield return null; }
        Choking(0f, 0f, ref dummy);
        busy = false;
    }

    void GiveBack()
    {
        if (heldFpc && fpc != null) fpc.enabled = true;
        if (heldPunch && punchRef != null) punchRef.enabled = true;
        heldFpc = heldPunch = false;
        PalmLight.FlashAnytime = false; Seizing = false;
    }

    // the screen as p goes 0..1: the circle of sight closing, blur, muffled sound, a heartbeat
    void Choking(float p, float dt, ref float beatT)
    {
        p = Mathf.Clamp01(p);
        float e = p * p * (3f - 2f * p);
        vig.localScale = Vector3.one * Mathf.Lerp(1.15f, .07f, e);
        vigImg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(p * 2.5f));
        black.color = new Color(0f, 0f, 0f, Mathf.Clamp01((p - .8f) * 5f));
        if (blur != null) blur.weight = Mathf.Clamp01(p * 1.6f);
        if (lowpass != null) { lowpass.enabled = p > .02f; lowpass.cutoffFrequency = Mathf.Lerp(22000f, 450f, e); }
        if (dt > 0f)
        {
            beatT -= dt;
            if (beatT <= 0f) { beatT = Mathf.Lerp(.9f, .35f, p); sfx.pitch = Mathf.Lerp(.9f, 1.3f, p); sfx.PlayOneShot(thump, .4f + .5f * p); }
        }
    }

    // the flash in the face during the choke
    void Blind(bool fromChoke)
    {
        state = S.Blind; timer = blindTime; alert = 0f;
        Wander(); state = S.Blind;
        vane.transform.position += Flat(Pos - player.position).normalized * 1.2f;
    }

    // ------------------------------------------------------------------ interface

    void BuildUi()
    {
        if (canvas != null) return;
        canvas = new GameObject("Vane hunt UI").AddComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 405;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

        var vt = new Texture2D(256, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                float r = Mathf.Sqrt((x - 127.5f) * (x - 127.5f) + (y - 127.5f) * (y - 127.5f)) / 128f;
                vt.SetPixel(x, y, new Color(0, 0, 0, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.14f, .42f, r))));
            }
        vt.Apply();
        vigImg = UiImage("Choke vignette", canvas.transform, Sprite.Create(vt, new Rect(0, 0, 256, 256), Vector2.one * .5f));
        vig = vigImg.rectTransform; vig.anchorMin = vig.anchorMax = Vector2.one * .5f; vig.sizeDelta = new Vector2(7000f, 7000f);
        vigImg.color = Color.clear;
        black = UiImage("Choke black", canvas.transform, null);
        black.rectTransform.anchorMin = Vector2.zero; black.rectTransform.anchorMax = Vector2.one; black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;
        black.color = Color.clear;

        heartBox = new GameObject("Breath", typeof(RectTransform)).GetComponent<RectTransform>();
        heartBox.SetParent(canvas.transform, false); heartBox.anchorMin = heartBox.anchorMax = new Vector2(.5f, .5f); heartBox.anchoredPosition = new Vector2(0f, -250f);
        var line = UiImage("Track", heartBox, null); line.rectTransform.sizeDelta = new Vector2(2000f, 3f); line.color = new Color(1f, 1f, 1f, .12f);
        heart = UiImage("Heart", heartBox, HeartSprite()); heart.rectTransform.sizeDelta = new Vector2(150f, 150f); heart.color = new Color(.85f, .05f, .08f);
        breathText = Label("Breath text", heartBox, new Vector2(0f, 150f), 34f);
        heartBox.gameObject.SetActive(false);
        chokeText = Label("Choke text", canvas.transform, new Vector2(0f, -380f), 34f);
        chokeText.rectTransform.anchorMin = chokeText.rectTransform.anchorMax = new Vector2(.5f, .5f);
        chokeText.gameObject.SetActive(false);

        // the blur: a global volume that only does a gaussian depth of field, its weight is the blur
        var profile = Resources.Load<VolumeProfile>("VaneBlur");
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(0f); dof.gaussianEnd.Override(.1f); dof.gaussianMaxRadius.Override(1.5f);
        }
        blur = new GameObject("Vane blur").AddComponent<Volume>();
        blur.transform.SetParent(transform, false);
        blur.isGlobal = true; blur.priority = 30; blur.sharedProfile = profile; blur.weight = 0f;
    }

    static Image UiImage(string name, Transform parent, Sprite s)
    {
        var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(parent, false); img.sprite = s; img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Label(string name, Transform parent, Vector2 at, float size)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        if (GameFont.Main != null) t.font = GameFont.Main;
        t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = new Color(.9f, .9f, .9f, .9f); t.raycastTarget = false;
        t.rectTransform.anchoredPosition = at; t.rectTransform.sizeDelta = new Vector2(1400f, 160f);
        return t;
    }

    static Sprite heartSprite;
    static Sprite HeartSprite()
    {
        if (heartSprite != null) return heartSprite;
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x / (n - 1f) * 2f - 1f) * 1.25f, v = (y / (n - 1f) * 2f - 1f) * 1.25f + .1f;
                float a = u * u + v * v - 1f, f = a * a * a - u * u * v * v * v;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(.5f - f * 10f)));
            }
        t.Apply();
        return heartSprite = Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * .5f);
    }

    // ------------------------------------------------------------------ giving everything back

    void Despawn()
    {
        if (vane != null) vane.SetActive(false);
        state = S.Away; alert = 0f;
    }

    // out of the office, an Echo, a death: he is gone and you have your controls back (a retry restores the rest)
    void ResetAll(bool died)
    {
        if (task != null) StopCoroutine(task);
        task = null; busy = false;
        CloseBreath();
        if (died) { heldFpc = heldPunch = false; }   // (the game-over screen and the checkpoint own the controls now)
        GiveBack();
        if (vig != null) { vig.localScale = Vector3.one * 1.15f; vigImg.color = Color.clear; black.color = died && health != null && health.IsDead ? Color.black : Color.clear; chokeText.gameObject.SetActive(false); }
        if (blur != null) blur.weight = 0f;
        if (lowpass != null) lowpass.enabled = false;
        Despawn();
        spawnT = firstAppearance;
        if (!died && black != null) black.color = Color.clear;
    }

    void LateUpdate()
    {
        // after the game-over screen has gone, the screen is clear again
        if (black != null && !GameOverScreen.Open && state == S.Away && !busy && black.color.a > 0f) black.color = Color.clear;
    }
}
