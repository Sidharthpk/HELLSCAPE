using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The Eye: the "EyeBalls" model in the scene. When you first walk into the office it is a dull red glow at the far
// end of the room, watching. Some seconds into your first talk with one of the dead it goes out. After that it is
// on the move: it floats round the office like a night guard, a red beam showing where it looks. Stay out of the
// beam, or put a wall or a desk between you. If it sees you it comes for you until it has lost you for a moment,
// then goes to where it last saw you, looks about, and takes up its round again. It can't be killed; the palm flash
// (PalmLight) blinds it for a while. Caught, it holds you in its gaze until you break it (OfficeEyeCapture.cs), then
// is gone for a bit. Not in the Echo.
// Lives on the director's object, not on the model: the model is switched off while the Eye is away.
public partial class OfficeEye : MonoBehaviour
{
    public float patrolSpeed = 2.2f, chaseSpeed = 5.6f;     // you walk at 5 and sprint at 7
    public float sightRange = 15f, sightAngle = 40f;        // the beam: what it sees in front of it
    public float senseRange = 3f;                           // this close it knows, whichever way it looks
    public float loseAfter = 2.5f;                          // seconds out of its sight before it gives up the chase
    public float leaveAfterTalk = 1.5f;                     // seconds of talking before the far glow goes out
    public float returnAfter = 3f;                          // ...and until it is back, on its round (once the talk is over)
    public float glowPower = 1.5f;                          // the red light round it ("not too much")

    enum S { Waiting, Watching, Leaving, Away, Patrol, Chase, Search, Blind, Capture }
    S state = S.Waiting;
    public bool Chasing => state == S.Chase || state == S.Capture;

    Transform root, player; Camera cam; PlayerHealth health; OfficeStalker finder;
    Func<bool> act, shown;
    NavMeshAgent agent;
    Animator eyes;   // the model's own clip (EyeMove): every eyeball rolling about
    Light glow, gaze;
    readonly List<Material> glowing = new List<Material>(), made = new List<Material>();
    readonly List<float> glowBy = new List<float>();
    float unscale = 1f;   // a NavMeshAgent's sizes are scaled by its transform; the model is not at scale 1
    AudioSource hum, sfx; AudioClip humClip, stingClip;
    Vector3 lastSeen;
    float hover = 1f, timer, talked, lost, repath, level;
    bool saidSeen, saidMoving, saidChase, saidBlind;

    public void Init(Transform eye, Transform body, Camera camera, PlayerHealth hp, OfficeStalker stalker, Func<bool> canAct, Func<bool> visible)
    {
        root = eye; player = body; cam = camera; health = hp; finder = stalker; act = canAct; shown = visible;
        eyes = root.GetComponent<Animator>();

        // its own copies of the three materials (so the assets stay as they are), lit from inside: the irises
        // brightly, the rest only enough to be a shape in the dark
        var copies = new Dictionary<Material, Material>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null) continue;
                if (!copies.TryGetValue(src, out var m))
                {
                    copies[src] = m = new Material(src); made.Add(m);
                    if (m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
                        glowing.Add(m); glowBy.Add(src.name.Contains("Iris") ? 1.8f : .12f);
                    }
                }
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
        glow = MakeLight("Eye glow", LightType.Point); glow.range = 7f;
        gaze = MakeLight("Eye beam", LightType.Spot); gaze.range = sightRange; gaze.spotAngle = sightAngle * 2f; gaze.innerSpotAngle = sightAngle;
        gaze.shadows = LightShadows.Hard; gaze.enabled = false;

        // the office floor under it (the roof has navmesh too: a plain nearest-navmesh search put it up there, out of sight)
        if (Physics.Raycast(root.position, Vector3.down, out var under, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            hover = Mathf.Max(.6f, root.position.y - under.point.y);   // it floats as high as it was placed
            if (OnFloor(under.point, under.point.y, 4f, out var floor)) root.position = floor;
        }
        agent = root.gameObject.AddComponent<NavMeshAgent>();
        unscale = 1f / Mathf.Max(.01f, root.lossyScale.y);
        agent.baseOffset = hover * unscale; agent.radius = .5f * unscale; agent.height = 2f * unscale;
        agent.angularSpeed = 200f; agent.acceleration = 14f; agent.stoppingDistance = .3f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        humClip = ProceduralAudio.Rumble(2f); stingClip = ProceduralAudio.Rumble(1.4f);
        hum = root.gameObject.AddComponent<AudioSource>();
        hum.clip = humClip; hum.loop = true; hum.spatialBlend = 1f; hum.rolloffMode = AudioRolloffMode.Linear; hum.maxDistance = 16f; hum.volume = .3f; hum.pitch = .5f;
        hum.Play();
        sfx = new GameObject("Eye sfx").AddComponent<AudioSource>();
        sfx.transform.SetParent(root, false); sfx.spatialBlend = 1f; sfx.rolloffMode = AudioRolloffMode.Linear; sfx.maxDistance = 30f; sfx.playOnAwake = false;
        BuildCapture();
    }

    Light MakeLight(string name, LightType type)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(root, false);
        l.type = type; l.color = new Color(1f, .08f, .04f); l.intensity = 0f; l.shadows = LightShadows.None;
        return l;
    }

    void OnDestroy()
    {
        Release(0);
        foreach (var m in made) if (m != null) Destroy(m);
        foreach (var o in new UnityEngine.Object[] { humClip, stingClip, whisperClip, thudClip, profile }) if (o != null) Destroy(o);
    }

    // ---------------------------------------------------------------- senses

    Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    bool Sees()
    {
        Vector3 to = cam.transform.position - root.position; float d = to.magnitude;
        if (d > sightRange || (d > senseRange && Vector3.Angle(root.forward, to) > sightAngle)) return false;
        return finder.Clear(root.position, cam.transform.position);
    }

    // somewhere on the office floor it can get to, at least `min` from you and (unseen) not in your view
    bool Point(float min, bool unseen, out Vector3 at)
    {
        at = default;
        var path = new NavMeshPath();
        bool from = OnFloor(player.position, player.position.y - 1f, 3f, out var you);
        for (int i = 0; i < 24; i++)
        {
            Vector3 p = player.position + Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward * UnityEngine.Random.Range(min, min + 22f);
            if (!OfficeHorrorDirector.In(p) || !OnFloor(p, player.position.y - 1f, 3f, out var h)) continue;
            if (Flat(h - player.position).magnitude < min) continue;
            if (unseen && Vector3.Angle(cam.transform.forward, h - cam.transform.position) < 60f && finder.Clear(cam.transform.position, h + Vector3.up * hover)) continue;
            // (Vane's shut door cuts the navmesh: only the side of it you are on)
            if (from && (!NavMesh.CalculatePath(you, h, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)) continue;
            at = h; return true;
        }
        return false;
    }

    // navmesh near p on the floor at floorY (not the roof's navmesh above the ceiling)
    static bool OnFloor(Vector3 p, float floorY, float radius, out Vector3 at)
    {
        at = default;
        if (!NavMesh.SamplePosition(new Vector3(p.x, floorY, p.z), out var h, radius, NavMesh.AllAreas) || Mathf.Abs(h.position.y - floorY) > 1.2f) return false;
        at = h.position; return true;
    }

    // ---------------------------------------------------------------- the palm flash

    public void Flashed(Vector3 from, float range, float seconds)
    {
        if (root == null || !root.gameObject.activeSelf || state < S.Patrol) return;
        if (Vector3.Distance(from, root.position) > range || !finder.Clear(from, root.position)) return;
        if (state == S.Capture) Release(0);   // the flash breaks its hold too
        state = S.Blind; timer = seconds;
        Sting(1.5f);
        if (!saidBlind) { saidBlind = true; Say("It's blind. Go."); }
    }

    // ---------------------------------------------------------------- update

    void Update()
    {
        if (root == null || player == null) return;
        CaptureFrame();
        bool visible = shown() && state != S.Away;
        if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        if (!shown()) return;
        bool free = act();
        float dist = Flat(player.position - root.position).magnitude;

        switch (state)
        {
            case S.Waiting:   // until you are in the office and looking about: then it is down the far end of the room
                if (!free) break;
                if (finder.TryFind(22f, true, out var far) && OnFloor(far, far.y, 1.5f, out far)) agent.Warp(far);
                state = S.Watching;
                saidSeen = true;   // (the line for this is said at the office door, HS_0150)
                break;

            case S.Watching:
                Turn(player.position, 2f);
                if (GhostNPC.Conversing) talked += Time.deltaTime;
                if (talked > leaveAfterTalk) { state = S.Leaving; timer = 1.5f; }
                else if (free && dist < 6f && Point(14f, true, out var back)) agent.Warp(back);   // never lets you near it
                break;

            case S.Leaving:
                timer -= Time.deltaTime;
                if (timer <= 0f) { state = S.Away; timer = returnAfter; }
                break;

            case S.Away:
                if (!free) break;
                timer -= Time.deltaTime;
                if (timer > 0f || !Point(12f, true, out var start)) break;
                root.gameObject.SetActive(true);
                agent.Warp(start);
                state = S.Patrol; timer = 0f; level = 0f;
                if (!saidMoving) { saidMoving = true; Say(HS.S("379")); }
                break;

            case S.Patrol:
                if (Hold(!free)) break;
                if (Mathf.Abs(agent.nextPosition.y - (player.position.y - 1f)) > 2f && Point(10f, true, out var down)) { agent.Warp(down); break; }   // strayed off your floor (the roof): back
                agent.speed = patrolSpeed;
                if (Sees()) { Spot(); break; }
                if (agent.pathPending || agent.remainingDistance > 1f) break;
                timer -= Time.deltaTime;   // at the end of a leg it stops and looks about
                root.Rotate(0f, 50f * Time.deltaTime, 0f);
                if (timer <= 0f && Point(8f, false, out var next)) { agent.SetDestination(next); timer = UnityEngine.Random.Range(1.5f, 4f); }
                break;

            case S.Chase:
                if (Hold(!free)) break;
                agent.speed = chaseSpeed;
                if (Sees()) { lastSeen = player.position; lost = 0f; } else lost += Time.deltaTime;
                repath -= Time.deltaTime;
                if (repath <= 0f) { repath = .2f; agent.SetDestination(lastSeen); }
                if (lost > loseAfter) { state = S.Search; timer = 4f; }
                else if (dist < 1.7f && lost <= 0f) BeginCapture();
                else if (Captures >= 1 && dist < 6f && !teased) { teased = true; EyeWalls(Mathf.Min(24, 8 * Captures), 4f); }   // it has had you before: the walls start early
                break;

            case S.Capture:
                Captured(dist);
                break;

            case S.Search:   // to where it last saw you; a look round; back to its round
                if (Hold(!free)) break;
                agent.speed = patrolSpeed * 1.4f;
                if (Sees()) { Spot(); break; }
                if (agent.pathPending || agent.remainingDistance > 1f) break;
                root.Rotate(0f, 110f * Time.deltaTime, 0f);
                timer -= Time.deltaTime;
                if (timer <= 0f) state = S.Patrol;
                break;

            case S.Blind:
                Hold(true);
                root.Rotate(0f, 260f * Time.deltaTime, 0f);
                if (free) timer -= Time.deltaTime;
                if (timer <= 0f) { state = S.Patrol; timer = 0f; }
                break;
        }
        Look();
    }

    // stopped where it is (a talk, a note, a cutscene, a flash)
    bool Hold(bool on)
    {
        if (agent.isOnNavMesh) agent.isStopped = on;
        return on;
    }

    void Turn(Vector3 at, float speed)
    {
        Vector3 d = Flat(at - root.position);
        if (d.sqrMagnitude > .01f) root.rotation = Quaternion.Slerp(root.rotation, Quaternion.LookRotation(d), speed * Time.deltaTime);
    }

    void Spot()
    {
        state = S.Chase; lastSeen = player.position; lost = 0f; repath = 0f; teased = false;
        Sting(.7f);
        if (!saidChase) { saidChase = true; Say(HS.S("380")); }
    }

    // Caught: every wall round you is eyes, the same ones as the sky of the void (VoidEyes), opening, looking about
    // and closing again. `count` more of them each call, each open for about `life` seconds (OfficeEyeCapture shuts
    // them sooner).
    public int wallEyes = 70;               // how many a first capture grows to
    public float wallEyesTime = 15f;
    ParticleSystem walls;

    void EyeWalls(int count, float life)
    {
        if (walls == null)
        {
            var tex = Resources.Load<Texture2D>("VoidEyes"); var shader = Shader.Find("HellScape/VoidEyes");
            if (tex == null || shader == null) return;
            var mat = new Material(shader) { mainTexture = tex, renderQueue = 3000 };   // after the walls, and hidden by what stands in front of them
            mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            made.Add(mat);
            walls = new GameObject("Wall eyes").AddComponent<ParticleSystem>();
            walls.transform.SetParent(transform, false);
            walls.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = walls.main;
            main.loop = true; main.playOnAwake = false; main.startSpeed = 0f; main.startRotation3D = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = MaxWallEyes;
            var em = walls.emission; em.enabled = false;   // only the ones placed by hand below
            var shape = walls.shape; shape.enabled = false;
            var sheet = walls.textureSheetAnimation;
            sheet.enabled = true; sheet.numTilesX = 8; sheet.numTilesY = 6;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
            sheet.cycleCount = Mathf.Max(1, Mathf.RoundToInt(wallEyesTime / 3.72f));   // (the gif is 3.72 s long)
            var fade = walls.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(1f, .75f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = walls.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.alignment = ParticleSystemRenderSpace.World;   // flat on the wall, not turned to the camera
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (!walls.isPlaying) walls.Play();
        Vector3 from = cam.transform.position;
        for (int placed = 0, tries = 0; placed < count && tries < count * 5; tries++)
        {
            Vector3 dir = Quaternion.Euler(UnityEngine.Random.Range(-35f, 30f), UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
            // (from outside your own collider, which sits round the camera)
            if (!Physics.Raycast(from + dir * .6f, dir, out var hit, 14f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Mathf.Abs(hit.normal.y) > .5f || hit.rigidbody != null || hit.transform.IsChildOf(root)) continue;   // walls and what stands against them: not floors, not bodies
            walls.Emit(new ParticleSystem.EmitParams
            {
                position = hit.point + hit.normal * .03f,
                rotation3D = Quaternion.LookRotation(-hit.normal).eulerAngles,
                startSize = UnityEngine.Random.Range(.45f, 1.5f),
                startLifetime = life * UnityEngine.Random.Range(.75f, 1.1f),
                startColor = Color.white,
            }, 1);
            placed++;
        }
    }

    // glow, beam, bob and hum for the state it is in
    void Look()
    {
        if (!root.gameObject.activeSelf) return;
        float want = state == S.Leaving ? 0f : state == S.Capture ? (failAt > 0f ? 5f : 2.8f) : state == S.Chase ? 2.2f : state == S.Blind ? .25f * Mathf.PerlinNoise(Time.time * 18f, 0f) : 1f;
        level = Mathf.MoveTowards(level, want, Time.deltaTime * (state == S.Blind || failAt > 0f ? 8f : 1.2f));
        float pulse = .85f + .15f * Mathf.Sin(Time.time * 2.3f);
        glow.intensity = glowPower * level * pulse;
        for (int i = 0; i < glowing.Count; i++) glowing[i].SetColor("_EmissionColor", new Color(1f, .07f, .03f) * (glowBy[i] * level * pulse));
        bool beam = state == S.Patrol || state == S.Chase || state == S.Search || state == S.Capture;
        gaze.enabled = beam;
        gaze.intensity = (state == S.Chase || state == S.Capture ? 14f : 7f) * level;
        agent.baseOffset = (hover + Mathf.Sin(Time.time * 1.3f) * .1f) * unscale;
        hum.pitch = Mathf.MoveTowards(hum.pitch, state == S.Chase ? 1.1f : .5f, Time.deltaTime);
        hum.volume = .3f * Mathf.Clamp01(level);
        if (eyes != null) eyes.speed = state == S.Blind ? 4f : state == S.Chase || state == S.Capture ? 2.2f : state == S.Watching ? .5f : 1f;   // frantic when it hunts or is blinded, slow while it only watches
    }

    void Sting(float pitch) { sfx.pitch = pitch; sfx.PlayOneShot(Sounds.Clip("Office/Auditor sting", null) ?? stingClip, .8f); }

    static void Say(string lines) { if (DialogueBox.Instance != null) DialogueBox.Instance.Say(lines); }
}
