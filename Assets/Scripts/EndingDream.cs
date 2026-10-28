using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The first death, A01 (EndingSequence.DeathVision calls this when Resources/DreamHall exists): a walk down the nave of a
// cathedral. At first it is white, misty, bright as heaven, with cloud on the floor. Footsteps start behind you. A
// stalker stands far off and watches. Walk on and they move when you are not looking; the light dulls, there are more
// of them, the cloud on the floor turns red and the floor becomes the soul floor. One runs at you out of the distance
// shouting WHY DID YOU?, shoves you down, and the walls are red. Ahead the Demon King sits on his throne, a black
// silhouette, and speaks. The nave is the same every bay, so it is one endless straight line (DreamHall).
public partial class EndingSequence
{
    [Header("First death: the dream hall (Resources/DreamHall, built by HellScape > Void: Dream Hall)")]
    public float dreamWalkSpeed = 3.6f;         // you walk slowly here (no sprint)
    public float dreamStalkerSpeed = 4.4f;      // faster than you, but only while you are not looking
    public float dreamThroneDistance = 52f;     // how far the throne is when you get up in the red hall
    public float dreamTalkDistance = 15f;       // the King speaks when you are this close

    struct DreamMood { public Color fog, ambient, sun, hall, glow, bloomTint, cloud; public float density, sunPower, bloom; }

    static readonly DreamMood Heaven = new DreamMood
    {
        fog = new Color(1f, .93f, .72f), ambient = new Color(1f, .93f, .72f), sun = new Color(1f, .86f, .45f), sunPower = 1.5f,
        hall = new Color(1f, .97f, .85f), glow = new Color(.4f, .3f, .1f), bloomTint = new Color(1f, .9f, .6f), bloom = 3.4f, cloud = new Color(1f, .95f, .8f, .6f), density = .05f
    };
    // the hall you walk into after the shut eyes: dark red, nearly black walls; only the glow behind the throne is bright
    static readonly DreamMood Dark = new DreamMood
    {
        fog = new Color(.09f, .008f, .006f), ambient = new Color(.2f, .025f, .02f), sun = new Color(.7f, .12f, .1f), sunPower = .45f,
        hall = new Color(.2f, .014f, .014f), glow = new Color(.05f, 0f, 0f), bloomTint = new Color(1f, .3f, .25f), bloom = 2.2f, cloud = new Color(.5f, .02f, .015f, .55f), density = .03f
    };
    static readonly DreamMood Dull = new DreamMood
    {
        fog = new Color(.42f, .40f, .42f), ambient = new Color(.32f, .31f, .33f), sun = new Color(.8f, .8f, .85f), sunPower = .5f,
        hall = new Color(.5f, .5f, .52f), glow = Color.black, bloomTint = new Color(1f, .9f, .9f), bloom = 1.2f, cloud = new Color(.55f, .5f, .52f, .5f), density = .07f
    };
    static readonly DreamMood Red = new DreamMood
    {
        fog = new Color(.3f, .025f, .02f), ambient = new Color(.35f, .04f, .03f), sun = new Color(1f, .25f, .2f), sunPower = .6f,
        hall = new Color(.65f, .05f, .04f), glow = new Color(.14f, 0f, 0f), bloomTint = new Color(1f, .35f, .3f), bloom = 1.8f, cloud = new Color(.8f, .04f, .03f, .6f), density = .06f
    };

    static DreamMood Mix(DreamMood a, DreamMood b, float t) => new DreamMood
    {
        fog = Color.Lerp(a.fog, b.fog, t), ambient = Color.Lerp(a.ambient, b.ambient, t), sun = Color.Lerp(a.sun, b.sun, t),
        hall = Color.Lerp(a.hall, b.hall, t), glow = Color.Lerp(a.glow, b.glow, t), bloomTint = Color.Lerp(a.bloomTint, b.bloomTint, t),
        cloud = Color.Lerp(a.cloud, b.cloud, t), density = Mathf.Lerp(a.density, b.density, t),
        sunPower = Mathf.Lerp(a.sunPower, b.sunPower, t), bloom = Mathf.Lerp(a.bloom, b.bloom, t)
    };

    // 0 = heaven, .5 = dull, 1 = red
    static DreamMood DreamMoodAt(float p) =>
        p < .5f ? Mix(Heaven, Dull, Mathf.SmoothStep(0f, 1f, p * 2f)) : Mix(Dull, Red, Mathf.SmoothStep(0f, 1f, (p - .5f) * 2f));

    // a soft round puff for the cloud on the floor, and for the glow behind the throne
    static Texture2D PuffTexture(float power)
    {
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Mathf.Clamp01(new Vector2(x - 31.5f, y - 31.5f).magnitude / 32f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - d, power)));
            }
        tex.Apply();
        return tex;
    }

    // cloud lying on the floor all round you, drifting; the emitter is carried along with you
    ParticleSystem MakeFloorCloud(Texture2D puff, out Material mat, Vector3 at)
    {
        var shader = Shader.Find("Sprites/Default");
        mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = puff };
        var go = new GameObject("Dream cloud");
        go.transform.position = at;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 5f; main.loop = true; main.playOnAwake = false; main.prewarm = true;
        main.startLifetime = 14f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(6f, 13f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1800;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(80f, .5f, 80f);
        var em = ps.emission; em.rateOverTime = 120f;   // thick smoke lying on the floor
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-.5f, .5f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(-.5f, .5f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(1f, .8f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;   // smoke lying flat on the floor: it never hides the people standing in it
        r.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    IEnumerator DreamWalk(DreamHall src)
    {
        Transform you = playerMovementScript != null ? playerMovementScript.transform : playerCamera.transform.root;
        var body = you.GetComponent<Rigidbody>();
        bool wasKinematic = body != null && body.isKinematic;
        Vector3 pos = you.position; Quaternion rot = you.rotation;
        float fovWas = playerCamera.fieldOfView; fov0 = fovWas;
        bool fogWas = RenderSettings.fog; Color fogColorWas = RenderSettings.fogColor;
        FogMode fogModeWas = RenderSettings.fogMode; float fogDensityWas = RenderSettings.fogDensity;
        Material skyWas = RenderSettings.skybox;
        var ambientModeWas = RenderSettings.ambientMode; Color ambientWas = RenderSettings.ambientLight;
        var clearWas = playerCamera.clearFlags; Color backgroundWas = playerCamera.backgroundColor;
        var fpc = playerMovementScript as FirstPersonController;
        float walkWas = fpc != null ? fpc.walkSpeed : 0f; bool sprintWas = fpc != null && fpc.enableSprint, unlimWas = fpc != null && fpc.unlimitedSprint;
        // no hands, no HUD, no footsteps of your own (all back once the King is done with you)
        bool hudWasHidden = CutsceneHUD.Hidden; CutsceneHUD.Hide(true);
        var armsRig = Arms() != null ? Arms().transform.parent.gameObject : null;
        if (armsRig != null) armsRig.SetActive(false);
        var steps = you.GetComponentInChildren<PlayerFootStep>(); bool stepsWere = steps != null && steps.enabled;
        if (steps != null) steps.enabled = false;
        MusicManager.Stop(0.5f);
        if (body != null) { body.linearVelocity = Vector3.zero; body.isKinematic = true; }
        you.SetPositionAndRotation(new Vector3(0f, -3000f, 0f), Quaternion.LookRotation(Vector3.forward));
        playerCamera.transform.localRotation = Quaternion.identity;
        Physics.SyncTransforms();
        yield return null;   // (the hell fog follows you: let it arrive, then put it away)
        var mist = new List<GameObject>();
        foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            if (ps.gameObject.activeInHierarchy && Vector3.Distance(ps.transform.position, playerCamera.transform.position) < 150f)
            { mist.Add(ps.gameObject); ps.gameObject.SetActive(false); }
        // the scene's own suns and colour grading would tint it: off for the dream
        var sunsOff = new List<Light>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.enabled && l.gameObject.activeInHierarchy) { l.enabled = false; sunsOff.Add(l); }
        var gradeWas = new List<(Volume v, float w)>();
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v.isGlobal && v.enabled && v.gameObject.name != "Retro look") { gradeWas.Add((v, v.weight)); v.weight = 0f; }

        // the look: bloom is what makes the white hall glow
        var look = new GameObject("Dream volume").AddComponent<Volume>();
        look.isGlobal = true; look.priority = 100f; look.weight = 0f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(.5f); bloom.scatter.Override(.9f); bloom.highQualityFiltering.Override(true); bloom.intensity.Override(Heaven.bloom);
        var aberration = profile.Add<ChromaticAberration>(true); aberration.intensity.Override(.35f);
        var blur = profile.Add<DepthOfField>(true);   // the far end of the nave goes soft
        blur.mode.Override(DepthOfFieldMode.Gaussian); blur.gaussianStart.Override(12f); blur.gaussianEnd.Override(48f); blur.gaussianMaxRadius.Override(.8f);
        look.sharedProfile = profile;
        playerCamera.clearFlags = CameraClearFlags.SolidColor;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        var sun = new GameObject("Dream sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.transform.rotation = Quaternion.Euler(48f, 25f, 0f);

        // where you stand: the feet, then the floor under them
        float feet = 1f, lowest = float.MaxValue;
        foreach (var c in you.GetComponentsInChildren<Collider>()) if (!c.isTrigger) lowest = Mathf.Min(lowest, c.bounds.min.y);
        if (lowest < float.MaxValue) feet = you.position.y - lowest;
        float floorY = you.position.y - feet - 0.01f;

        // the hall: you start in the middle of its arm; z is the way you walk
        // (three copies end to end, so there is always hall ahead and behind: the nave never seems to end or start in front of you)
        var hall = Instantiate(src); hall.name = "Dream hall";
        var hallBehind = Instantiate(src); var hallAhead = Instantiate(src);
        hallBehind.name = "Dream hall (behind)"; hallAhead.name = "Dream hall (ahead)";
        var hallMat = hall.hall[0].material;   // (an instance: recoloured as the dream sours)
        hallMat.EnableKeyword("_EMISSION");
        foreach (var h in new[] { hall, hallBehind, hallAhead }) { foreach (var r in h.hall) r.sharedMaterial = hallMat; h.seat.gameObject.SetActive(false); }
        void PlaceHalls(float z)
        {
            hall.transform.position = new Vector3(0f, floorY, z);
            hallBehind.transform.position = new Vector3(0f, floorY, z - hall.length);
            hallAhead.transform.position = new Vector3(0f, floorY, z + hall.length);
        }
        PlaceHalls(-hall.armLength * .5f);   // you start in the middle of the east arm

        // the floor: the soul floor, under a white layer that thins away; invisible walls keep you in the nave
        const float size = 600f, tile = 6f;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "SoulFloor";
        DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.localScale = Vector3.one * (size / 10f);
        var box = floor.AddComponent<BoxCollider>(); box.size = new Vector3(10f, 0.2f, 10f); box.center = new Vector3(0f, -0.1f, 0f);
        var floorMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        var floorTex = Resources.Load<Texture2D>("SoulFloor");
        if (floorTex != null) { floorTex.wrapMode = TextureWrapMode.Repeat; floorMat.mainTexture = floorTex; floorMat.mainTextureScale = new Vector2(size / tile, size / tile); }
        floorMat.color = new Color(0.6f, 0.6f, 0.6f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        floor.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var puff = PuffTexture(1.6f);
        var whiteShader = Shader.Find("Sprites/Default");
        var whiteMat = new Material(whiteShader != null ? whiteShader : Shader.Find("Universal Render Pipeline/Unlit"));
        var white = GameObject.CreatePrimitive(PrimitiveType.Quad);
        white.name = "Dream floor white";
        DestroyImmediate(white.GetComponent<Collider>());
        white.GetComponent<Renderer>().sharedMaterial = whiteMat;
        white.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        white.transform.SetPositionAndRotation(new Vector3(0f, floorY + .03f, 0f), Quaternion.Euler(90f, 0f, 0f));
        white.transform.localScale = Vector3.one * size;
        GameObject Barrier(string n, Vector3 sz) { var g = new GameObject(n); g.AddComponent<BoxCollider>().size = sz; return g; }
        var wallL = Barrier("Dream wall L", new Vector3(1f, 30f, 400f)); var wallR = Barrier("Dream wall R", new Vector3(1f, 30f, 400f));
        var behind = Barrier("Dream back", new Vector3(40f, 30f, 1f));
        you.position = new Vector3(0f, floorY + feet + 0.05f, 0f);
        if (body != null) { body.isKinematic = false; body.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms();
        var cloud = MakeFloorCloud(puff, out var cloudMat, new Vector3(0f, floorY + .3f, 0f));
        // the bright yellow light at the far end of the nave: the bloom spreads it into the mist
        var lightGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        lightGlow.name = "Dream light";
        DestroyImmediate(lightGlow.GetComponent<Collider>());
        var lightMat = new Material(whiteShader != null ? whiteShader : Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = puff, color = new Color(1f, .85f, .35f, 1f) };
        lightGlow.GetComponent<Renderer>().sharedMaterial = lightMat;
        lightGlow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        lightGlow.transform.localScale = Vector3.one * 70f;

        void Apply(DreamMood m)
        {
            aberration.intensity.Override(Mathf.Lerp(.7f, .3f, Mathf.InverseLerp(.08f, .3f, m.fog.r)));   // more the darker it is
            RenderSettings.fogColor = m.fog; RenderSettings.fogDensity = m.density; RenderSettings.ambientLight = m.ambient;
            playerCamera.backgroundColor = m.fog;
            sun.color = m.sun; sun.intensity = m.sunPower;
            hallMat.SetColor("_BaseColor", m.hall); hallMat.SetColor("_EmissionColor", m.glow);
            bloom.intensity.Override(m.bloom); bloom.tint.Override(m.bloomTint);
            cloudMat.color = m.cloud;
        }
        Apply(Heaven);

        // the stalkers are ordinary people (the PSX character pack), pure black, mist or no mist; feet on the floor
        GameObject Person(Vector3 at, Quaternion facing)
        {
            var g = Instantiate(src.people[Random.Range(0, src.people.Length)], new Vector3(at.x, floorY + .02f, at.z), facing);
            g.SetActive(true);
            return g;
        }
        DreamStalker.Spotted = false;
        AudioSource Bed(string slot, float vol) { var a = new GameObject(slot).AddComponent<AudioSource>(); a.spatialBlend = 0f; a.loop = true; a.volume = 0f; a.clip = Sounds.Clip(slot, null); a.ignoreListenerPause = true; if (a.clip != null) a.Play(); return a; }
        voidSfx = Bed("Void sfx", 0f); voidSfx.loop = false; voidSfx.volume = 1f;
        var whispers = Bed("Ending/Shadow whispers", 0f); var bed = Bed("Ending/Shadow music", 0f);
        float whisperVol = Sounds.Volume("Ending/Shadow whispers"), bedVol = Sounds.Volume("Ending/Shadow music");

        if (fpc != null) { fpc.walkSpeed = dreamWalkSpeed; fpc.enableSprint = false; }
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 2f));
        if (playerMovementScript != null) playerMovementScript.enabled = true;
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("Where am I?|It's so calm here. So peaceful.");

        // the walk
        // they only ever come in behind you, out of sight: one at first, more each time you look back and look away again
        var stalkers = new List<DreamStalker>();
        bool chasing = false, wasSeen = false, started = false, asked = false;
        float clock = 0f, maxZ = 0f, musicAt = -1f, lastAdd = 0f;
        void AddStalker(float dist)
        {
            var at = new Vector3(Random.Range(-4f, 4f), 0f, you.position.z - dist);
            var s = SpawnStalker(Person(at, Quaternion.LookRotation(Vector3.forward)), you);
            s.running = chasing;
            stalkers.Add(s);
            lastAdd = clock;
        }
        // (ten calm seconds first; then footsteps behind you)
        while (clock < soulWalkTime)
        {
            clock += Time.deltaTime;
            float p = Mathf.Clamp01(clock / soulWalkTime);
            Vector3 yp = you.position; maxZ = Mathf.Max(maxZ, yp.z);
            // endless: after one hall length of walking the three halls move one length with you (end to end, nothing changes)
            while (yp.z > hall.transform.position.z + hall.armLength * .5f + hall.length * .5f) PlaceHalls(hall.transform.position.z + hall.length);
            floor.transform.position = new Vector3(Mathf.Round(yp.x / tile) * tile, floorY, Mathf.Round(yp.z / tile) * tile);
            white.transform.position = new Vector3(yp.x, floorY + .03f, yp.z);
            cloud.transform.position = new Vector3(yp.x, floorY + .3f, yp.z);
            wallL.transform.position = new Vector3(-hall.width, floorY + .3f, yp.z); wallR.transform.position = new Vector3(hall.width, floorY + .3f, yp.z);
            behind.transform.position = new Vector3(0f, floorY + .3f, maxZ - 12f);
            look.weight = Mathf.Clamp01(clock / 2f);
            var mood = DreamMoodAt(p);
            Apply(mood);
            // the white layer thins away (the soul floor under it), the cloud keeps its place and goes red
            var grey = mood.hall * .95f; grey.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, .78f, p));
            whiteMat.color = grey;
            lightGlow.transform.position = new Vector3(0f, floorY + 9f, yp.z + 48f);
            lightMat.color = new Color(1f, .85f, .35f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .65f, p)));
            // the horror music starts when you first see one of them
            if (musicAt < 0f && DreamStalker.Spotted) { musicAt = clock; voidSfx.PlayOneShot(ProceduralAudio.Rumble(2f), 0.5f); StartCoroutine(VoidWhispers()); }
            if (!started && clock >= 10f) { started = true; AddStalker(22f); }   // the first one, far behind you: you hear it before you see it
            if (!asked && clock >= 12f) { asked = true; if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("Wait. What was that?"); }
            float music = musicAt < 0f ? 0f : Mathf.Clamp01((clock - musicAt) / 3f);
            bed.volume = 0.7f * bedVol * music * Mathf.Lerp(.4f, 1f, p);
            whispers.volume = 0.9f * whisperVol * music * Mathf.Lerp(0.15f, 1f, p);
            // keep looking back and there are more of them: when you look away again, new ones are standing out there behind you
            bool seenNow = false;
            foreach (var s in stalkers) if (s != null && s.IsSeen) seenNow = true;
            if (wasSeen && !seenNow && clock - lastAdd > 2.5f && stalkers.Count < 14)
            {
                int more = 1 + stalkers.Count / 3;
                for (int i = 0; i < more; i++) AddStalker(Random.Range(17f, 27f));
                voidSfx.PlayOneShot(ProceduralAudio.Rumble(.8f), 0.2f);
            }
            wasSeen = seenNow;
            if (started && clock - lastAdd > 9f && stalkers.Count < 1 + Mathf.FloorToInt(p * 8f)) AddStalker(Random.Range(20f, 27f));   // (even if you never look: they gather)
            // enough of them (or late enough): they run, and so must you
            if (started && !chasing && (stalkers.Count >= 6 || p >= .62f))
            {
                chasing = true;
                foreach (var s in stalkers) s.running = true;
                if (fpc != null) { fpc.walkSpeed = dreamWalkSpeed + 1.2f; fpc.enableSprint = true; fpc.unlimitedSprint = true; }
                if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("*They're coming. Run.*");
                voidSfx.PlayOneShot(ProceduralAudio.Rumble(1.5f), .6f);
            }
            float hold = chasing ? 8f : p < .26f ? 14f : Mathf.Lerp(14f, 8f, Mathf.SmoothStep(0f, 1f, (p - .26f) / .36f));
            foreach (var s in stalkers) s.holdAt = hold;
            yield return null;
        }

        // WHY DID YOU? one of them comes running at you from ahead, out of the distance, flat out
        var runner = Person(new Vector3(Random.Range(-2f, 2f), 0f, you.position.z + 40f), Quaternion.LookRotation(Vector3.back));
        var runAnim = runner.GetComponentInChildren<Animator>();
        if (runAnim != null) { runAnim.applyRootMotion = false; runAnim.speed = 2.6f; }
        Sounds.OneShot(voidSfx, "Ending/Shadow scream", null, 1f);
        bool shouted = false;
        while (true)
        {
            Vector3 to = you.position - runner.transform.position; to.y = 0f;
            float d = to.magnitude;
            if (d < 1.3f) break;
            runner.transform.rotation = Quaternion.LookRotation(to);
            runner.transform.position += to / d * 18f * Time.deltaTime;
            if (!shouted && d < 20f) { shouted = true; if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("???: WHY DID YOU?"); }
            yield return null;
        }

        // it reaches you: a bright red, black and white flash, then the screen fades into the throne room
        if (playerMovementScript != null) playerMovementScript.enabled = false;
        if (body != null) body.linearVelocity = Vector3.zero;
        foreach (var s in stalkers) if (s != null) s.frozen = true;
        Transform cam = playerCamera.transform;
        Vector3 cam0 = cam.localPosition; Quaternion camRot0 = cam.localRotation;
        Sounds.OneShot(voidSfx, "Ending/Shadow scream", null, 1f);
        voidSfx.PlayOneShot(ProceduralAudio.Stomp(), 1f);
        var flashCanvas = new GameObject("Dream flash").AddComponent<Canvas>();
        flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay; flashCanvas.sortingOrder = 600;
        var flash = new GameObject("Flash").AddComponent<UnityEngine.UI.Image>();
        flash.transform.SetParent(flashCanvas.transform, false);
        flash.raycastTarget = false; flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
        flash.rectTransform.offsetMin = flash.rectTransform.offsetMax = Vector2.zero;
        flash.color = Color.clear;
        Color[] strobe = { new Color(1f, .03f, .02f), Color.black, Color.white };
        for (int i = 0; i < 13; i++)
        {
            flash.color = strobe[i % 3];
            cam.localPosition = cam0 + Random.insideUnitSphere * .05f;
            yield return new WaitForSeconds(.06f);
        }
        flash.color = Color.black; cam.localPosition = cam0;
        yield return new WaitForSeconds(.3f);
        foreach (var s in stalkers) if (s != null) Destroy(s.gameObject);
        Destroy(runner); Destroy(whispers.gameObject); Destroy(bed.gameObject);
        Destroy(white); Destroy(whiteMat); Destroy(lightGlow); Destroy(lightMat);
        hallAhead.gameObject.SetActive(false);   // (the cloud on the floor stays: it goes dark red)

        // the hall has gone dark red, nearly black, and the throne is across the nave
        Apply(Dark); RenderSettings.fogDensity = .14f;
        blur.gaussianStart.Override(0f); blur.gaussianEnd.Override(16f); blur.gaussianMaxRadius.Override(1.9f);   // (heavy blur and fog when you come to: both clear as you walk)
        you.rotation = Quaternion.LookRotation(Vector3.forward);
        cam.localPosition = cam0; cam.localRotation = camRot0;
        if (fpc != null) { fpc.SetPitch(0f); fpc.unlimitedSprint = unlimWas; fpc.enableSprint = false; fpc.walkSpeed = dreamWalkSpeed * .55f; }
        hall.transform.position = new Vector3(0f, floorY, you.position.z + dreamThroneDistance - hall.armLength);
        hallBehind.transform.position = hall.transform.position - Vector3.forward * hall.length;   // (the nave goes on behind you: the throne is further than one hall)
        hall.seat.gameObject.SetActive(true);
        var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);   // the red glow behind the throne, so that he stands out of it
        glow.name = "Dream glow";
        DestroyImmediate(glow.GetComponent<Collider>());
        var glowMat = new Material(whiteShader != null ? whiteShader : Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = puff, color = new Color(1f, .1f, .06f, 1f) };
        glow.GetComponent<Renderer>().sharedMaterial = glowMat;
        glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        glow.transform.position = hall.kingBack.position;
        glow.transform.localScale = Vector3.one * 34f;
        // the room is full of fog: thick smoke on the floor and a red haze up to the vault, thinning as you walk in
        var haze = MakeFloorCloud(puff, out var hazeMat, new Vector3(you.position.x, floorY + 3f, you.position.z));
        haze.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var hazeShape = haze.shape; hazeShape.scale = new Vector3(70f, 7f, 70f);
        var hazeMain = haze.main; hazeMain.startSize = new ParticleSystem.MinMaxCurve(9f, 17f);
        var hazeEm = haze.emission; hazeEm.rateOverTime = 45f;
        haze.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
        hazeMat.color = new Color(.4f, .03f, .025f, .3f);
        haze.Play();
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.6f);
        if (body != null) { body.isKinematic = false; body.linearVelocity = Vector3.zero; }
        for (float t = 0f; t < 1.8f; t += Time.deltaTime) { flash.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, t / 1.8f)); yield return null; }
        Destroy(flashCanvas.gameObject);
        if (playerMovementScript != null) playerMovementScript.enabled = true;

        // the walk to the throne (the walls are red, the whispers are back)
        var murmur = Bed("Ending/Shadow whispers", 0f);
        float approach0 = Mathf.Max(1f, hall.kingHead.position.z - you.position.z - dreamTalkDistance), walkT = 0f;
        while (hall.kingHead.position.z - you.position.z > dreamTalkDistance)
        {
            // thick fog at first (you cannot see him), thinning as you walk: he comes out of it slowly
            // six seconds of walking blind in thick fog; then the fog thins and the picture clears, and he comes out of it
            walkT += Time.deltaTime;
            float prog = Mathf.Clamp01(1f - (hall.kingHead.position.z - you.position.z - dreamTalkDistance) / approach0);
            float clear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((walkT - 6f) / 9f));
            RenderSettings.fogDensity = Mathf.Lerp(.14f, .006f, clear);
            blur.gaussianEnd.Override(Mathf.Lerp(16f, 90f, clear)); blur.gaussianMaxRadius.Override(Mathf.Lerp(1.9f, 0f, clear));
            hazeMat.color = new Color(.4f, .03f, .025f, Mathf.Lerp(.55f, .0f, clear));
            glowMat.color = new Color(1f, .1f, .06f, Mathf.Lerp(.1f, 1f, clear * clear));
            haze.transform.position = new Vector3(you.position.x, floorY + 3f, you.position.z);
            behind.transform.position = new Vector3(0f, floorY + .3f, Mathf.Max(maxZ, you.position.z) - 12f); maxZ = Mathf.Max(maxZ, you.position.z);
            floor.transform.position = new Vector3(Mathf.Round(you.position.x / tile) * tile, floorY, Mathf.Round(you.position.z / tile) * tile);
            cloud.transform.position = new Vector3(you.position.x, floorY + .3f, you.position.z);
            murmur.volume = 0.5f * whisperVol;
            yield return null;
        }
        Destroy(murmur.gameObject);

        // he speaks: a black silhouette on his throne, huge, still
        if (playerMovementScript != null) playerMovementScript.enabled = false;
        if (body != null) body.linearVelocity = Vector3.zero;
        Sounds.OneShot(voidSfx, "Ending/Demon rises", null, 1f);
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(deathLines))
        {
            DialogueBox.Instance.SayNow(deathLines.Replace("The Demon King:", "???:"));
            Sounds.OneShot(voidSfx, "Ending/Demon King talking", null);
            yield return null;
            while (DialogueBox.Instance.Busy)
            {
                playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, 55f, 1f - Mathf.Exp(-1.5f * Time.deltaTime));
                TurnCamera(hall.kingHead.position + Vector3.down * 1.2f, 2.5f);
                Vector3 creep = you.position + Vector3.forward * .25f * Time.deltaTime;   // a slow push in on him while he speaks
                if (body != null && !body.isKinematic) body.position = creep; else you.position = creep;
                yield return null;
            }
        }

        // the brand: red flare, then the hands
        var arms = Arms();
        if (arms != null && markedSkin != null)
        {
            Sounds.OneShot(voidSfx, "Ending/Demon rises", null, 0.8f);
            bool burning = true;
            StartCoroutine(PainShake(() => burning));   // the picture shakes while your hands burn
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float k = t / 1.1f;
                if (k >= 0.5f && arms.material.mainTexture != markedSkin)
                {
                    arms.material.mainTexture = markedSkin;
                    arms.transform.parent.gameObject.SetActive(true);
                }
                if (fader != null) fader.fade.alpha = 0.6f * Mathf.Sin(k * Mathf.PI);
                yield return null;
            }
            if (fader != null) fader.fade.alpha = 0f;
            var punch = playerCamera.GetComponentInChildren<PunchController>(true);
            if (punch != null) { punch.Draw(); yield return new WaitForSeconds(1.2f); }
            if (DialogueBox.Instance != null && !string.IsNullOrEmpty(deathBrandLine)) DialogueBox.Instance.SayNow(deathBrandLine.Replace("The Demon King:", "???:"));
            yield return LookAtHands(arms, 0.8f);
            burning = false;
            yield return RevisedPlotDirector.WaitForDialogue();
        }

        // back to the red flat
        if (fader != null) yield return fader.FadeTo(1f, 1.2f);
        Destroy(voidSfx.gameObject);
        foreach (var s in FindObjectsByType<DreamStalker>(FindObjectsSortMode.None)) Destroy(s.gameObject);   // (none left, but never leave one in the city)
        Destroy(hall.gameObject); Destroy(hallBehind.gameObject); Destroy(hallAhead.gameObject); Destroy(floor); Destroy(glow); Destroy(cloud.gameObject); Destroy(cloudMat); Destroy(haze.gameObject); Destroy(hazeMat);
        Destroy(wallL); Destroy(wallR); Destroy(behind); Destroy(sun.gameObject); Destroy(look.gameObject); Destroy(profile);
        Destroy(floorMat); Destroy(glowMat); Destroy(puff); Destroy(hallMat);
        foreach (var l in sunsOff) if (l != null) l.enabled = true;
        foreach (var (v, w) in gradeWas) if (v != null) v.weight = w;
        if (fpc != null) { fpc.walkSpeed = walkWas; fpc.enableSprint = sprintWas; fpc.unlimitedSprint = unlimWas; }
        if (!hudWasHidden) CutsceneHUD.Hide(false);
        if (armsRig != null) armsRig.SetActive(true);
        if (steps != null) steps.enabled = stepsWere;
        foreach (var g in mist) if (g != null) g.SetActive(true);
        RenderSettings.fog = fogWas; RenderSettings.fogColor = fogColorWas;
        RenderSettings.fogMode = fogModeWas; RenderSettings.fogDensity = fogDensityWas;
        RenderSettings.skybox = skyWas;
        RenderSettings.ambientMode = ambientModeWas; RenderSettings.ambientLight = ambientWas;
        playerCamera.clearFlags = clearWas; playerCamera.backgroundColor = backgroundWas;
        playerCamera.fieldOfView = fovWas;
        playerCamera.transform.localPosition = cam0;
        playerCamera.transform.localRotation = Quaternion.identity;
        if (body != null) { body.linearVelocity = Vector3.zero; body.isKinematic = true; }
        you.SetPositionAndRotation(pos, rot);
        if (body != null) body.isKinematic = wasKinematic;
        Physics.SyncTransforms();
    }

    // the picture shakes, harder with each throb, for as long as `on` says (the mark burning into your hands)
    IEnumerator PainShake(System.Func<bool> on)
    {
        Transform cam = playerCamera.transform;
        Vector3 p0 = cam.localPosition; Quaternion r0 = cam.localRotation;
        for (float t = 0f; on(); t += Time.deltaTime)
        {
            float throb = .6f + .4f * Mathf.Abs(Mathf.Sin(t * 5f));
            cam.localPosition = p0 + Random.insideUnitSphere * .04f * throb;
            cam.localRotation = r0 * Quaternion.Euler(Random.Range(-1f, 1f) * 1.6f * throb, Random.Range(-1f, 1f) * 1.6f * throb, Random.Range(-1f, 1f) * 2.6f * throb);
            yield return null;
        }
        cam.localPosition = p0; cam.localRotation = r0;
    }

    // makes a person (already standing where he should) a stalker
    DreamStalker SpawnStalker(GameObject person, Transform you)
    {
        var s = person.AddComponent<DreamStalker>();
        s.cam = playerCamera; s.target = you; s.speed = dreamStalkerSpeed;
        s.animator = person.GetComponentInChildren<Animator>();
        if (s.animator != null) s.animator.applyRootMotion = false;
        return s;
    }
}
