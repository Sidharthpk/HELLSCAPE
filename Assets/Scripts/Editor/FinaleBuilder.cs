using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// The escape and the finale in MainGameScene:
//   HellScape > Finale > Build Horde Chase    - the horde replaces the blood orb (same hooks: car keys, bridge, checkpoint)
//   HellScape > Finale > Build Bridge Boss    - Kessler's true form on the bridge, before the judgement
//   HellScape > Finale > Use Selected As Boss Model - select your demon (in the scene or a prefab/FBX), run it:
//       it replaces the stand-in, scaled to bossHeight, and keeps the heart + hitbox fitted to it.
public static class FinaleBuilder
{
    const string ZombiePrefab = "Assets/Enemies/Scary Zombie Pack/PrimeZombie.prefab";
    static readonly Vector3 BridgeStart = new Vector3(300.92f, -3.9f, 44.03f);
    static readonly Vector3 BridgeDir = new Vector3(0.79f, 0f, 0.61f).normalized;
    const float BossHeight = 6.5f;

    static Vector3 OnBridge(float along, float side, float y = -3.9f)
    {
        Vector3 across = Vector3.Cross(Vector3.up, BridgeDir);
        Vector3 p = BridgeStart + BridgeDir * along + across * side;
        p.y = y;
        return p;
    }

    // ---------------------------------------------------------------- horde

    [MenuItem("HellScape/Finale/Build Horde Chase")]
    public static void BuildHorde()
    {
        var bw = Object.FindFirstObjectByType<BloodWave>(FindObjectsInactive.Include);
        var old = GameObject.Find("OfficeAct/Horde (escape)");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var go = new GameObject("Horde (escape)");
        Undo.RegisterCreatedObjectUndo(go, "Build Horde");
        go.transform.SetParent(GameObject.Find("OfficeAct").transform, false);
        var h = go.AddComponent<HordeChase>();
        h.car = bw.car;
        h.playerRoot = bw.playerRoot;
        h.health = bw.health;
        h.carHealth = bw.car.GetComponent<CarHealth>();
        h.cutsceneCam = bw.cutsceneCam;
        h.barTop = bw.barTop; h.barBottom = bw.barBottom;
        h.carControl = bw.carControl;
        h.fader = bw.fader;
        h.zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefab);
        h.road = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("RoadPoint ") && !t.name.Contains("Start"))
            .OrderBy(t => t.name).ToArray();

        var line = new GameObject("Horde line (bridge mouth)").transform;
        line.SetParent(go.transform, false);
        line.SetPositionAndRotation(OnBridge(0f, 0f), Quaternion.LookRotation(BridgeDir));
        h.bridgeLine = line;

        // rewire the orb's three hooks
        var ci = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        Undo.RecordObject(ci, "Rewire car");
        Remove(ci.onEnter, bw);
        UnityEventTools.AddPersistentListener(ci.onEnter, h.Begin);

        var stop = GameObject.Find("OfficeAct/BloodWaveStop (Bridge)");
        stop.name = "HordeStop (Bridge)";
        var st = stop.GetComponent<StoryTrigger>();
        Undo.RecordObject(st, "Rewire stop");
        Remove(st.onEnter, bw);
        UnityEventTools.AddPersistentListener(st.onEnter, h.Stop);

        var cp = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name.Contains("Escape"));
        Undo.RecordObject(cp, "Rewire checkpoint");
        Remove(cp.onRespawn, bw);
        UnityEventTools.AddPersistentListener(cp.onRespawn, h.ResetHorde);

        bw.gameObject.SetActive(false);
        Debug.Log($"Horde built: {h.road.Length} road points, orb unhooked.");
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    static void Remove(UnityEngine.Events.UnityEventBase e, Object target)
    {
        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
            if (e.GetPersistentTarget(i) == target) UnityEventTools.RemovePersistentListener(e, i);
    }

    // ---------------------------------------------------------------- boss

    [MenuItem("HellScape/Finale/Build Bridge Boss")]
    public static void BuildBoss()
    {
        var bridgeEnding = GameObject.Find("BridgeEnding");
        var old = bridgeEnding.transform.Find("Kessler Demon (bridge boss)");
        Transform keepModel = null;
        if (old != null)
        {
            // keep a model the user already put in
            var m = old.Find("Model");
            if (m != null && m.childCount > 0 && m.GetChild(0).name != "Stand-in (PrimeZombie)") { keepModel = m.GetChild(0); keepModel.SetParent(null, true); }
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        foreach (var n in new[] { "Boss Trigger (bridge)", "Boss fight walls", "Checkpoint - Bridge", "Boss arena" })
        {
            var o = bridgeEnding.transform.Find(n);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        var root = new GameObject("Kessler Demon (bridge boss)");
        Undo.RegisterCreatedObjectUndo(root, "Build Boss");
        root.transform.SetParent(bridgeEnding.transform, false);
        var boss = root.AddComponent<DemonBoss>();

        var arena = new GameObject("Boss arena").transform;
        arena.SetParent(bridgeEnding.transform, false);
        arena.SetPositionAndRotation(OnBridge(38f, 0f), Quaternion.LookRotation(BridgeDir));
        boss.arena = arena;
        boss.arenaHalfLength = 29f;
        boss.arenaHalfWidth = 7.2f;

        var stand = new GameObject("StandAt").transform;
        stand.SetParent(arena, false);
        stand.SetPositionAndRotation(OnBridge(56f, 0f), Quaternion.LookRotation(-BridgeDir));
        var rise = new GameObject("RiseFrom (river)").transform;
        rise.SetParent(arena, false);
        rise.SetPositionAndRotation(OnBridge(56f, 16f, -40f), Quaternion.LookRotation(-BridgeDir));
        boss.standAt = stand; boss.riseFrom = rise;
        root.transform.SetPositionAndRotation(stand.position, stand.rotation);

        // the model
        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);
        boss.model = model;
        if (keepModel != null) { keepModel.SetParent(model, true); FitModel(boss, keepModel.gameObject); }
        else MakeStandIn(boss);

        // the walls you can't leave through (the river is a long way down)
        var walls = new GameObject("Boss fight walls").transform;
        walls.SetParent(bridgeEnding.transform, false);
        walls.SetPositionAndRotation(arena.position, arena.rotation);
        boss.fightWalls = new[]
        {
            Wall(walls, "Left", new Vector3(-8.3f, 3f, 0f), new Vector3(0.6f, 8f, 64f)),
            Wall(walls, "Right", new Vector3(8.3f, 3f, 0f), new Vector3(0.6f, 8f, 64f)),
            Wall(walls, "Back (city side)", new Vector3(0f, 3f, -30.5f), new Vector3(17f, 8f, 0.6f)),
            Wall(walls, "Front (dead end)", new Vector3(0f, 3f, 30.5f), new Vector3(17f, 8f, 0.6f)),
        };

        // scene links
        var ending = Object.FindFirstObjectByType<EndingSequence>(FindObjectsInactive.Include);
        var bw = Object.FindFirstObjectByType<BloodWave>(FindObjectsInactive.Include);
        var ci = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        boss.player = bw.playerRoot;
        boss.playerHealth = bw.health;
        boss.carInteract = ci;
        boss.carRb = bw.car.GetComponent<Rigidbody>();
        boss.cutsceneCam = bw.cutsceneCam;
        boss.barTop = bw.barTop; boss.barBottom = bw.barBottom;
        boss.fader = bw.fader;
        boss.zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefab);
        boss.healthPickup = MakePickup(root.transform);
        UnityEventTools.AddPersistentListener(boss.onDefeated, ending.PlayEnding);

        // health bar: a copy of the killer's
        var killer = Object.FindFirstObjectByType<KillerBoss>(FindObjectsInactive.Include);
        if (killer != null && killer.healthBar != null)
        {
            var oldBar = killer.healthBar.transform.parent.Find("DemonHealthBar");
            if (oldBar != null) Undo.DestroyObjectImmediate(oldBar.gameObject);
            var bar = Object.Instantiate(killer.healthBar, killer.healthBar.transform.parent);
            bar.name = "DemonHealthBar";
            Undo.RegisterCreatedObjectUndo(bar, "Boss bar");
            bar.SetActive(false);
            boss.healthBar = bar;
            string fillPath = PathFrom(killer.healthBar.transform, killer.healthFill);
            boss.healthFill = fillPath != null ? bar.transform.Find(fillPath) as RectTransform : null;
            boss.bossName = bar.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        }

        // the trigger just onto the bridge (past the horde's line)
        var trig = new GameObject("Boss Trigger (bridge)");
        trig.transform.SetParent(bridgeEnding.transform, false);
        trig.transform.SetPositionAndRotation(OnBridge(14f, 0f, -2f), Quaternion.LookRotation(BridgeDir));
        var box = trig.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(18f, 8f, 6f);
        var st = trig.AddComponent<StoryTrigger>();
        UnityEventTools.AddPersistentListener(st.onEnter, boss.Begin);

        // the old ending trigger at the dead end would start the judgement mid-fight: the boss's death does it now
        var endTrig = bridgeEnding.transform.Find("Ending Trigger");
        if (endTrig != null) endTrig.gameObject.SetActive(false);

        // checkpoint: back at the near end of the bridge, the fight starts again
        var escape = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name.Contains("Escape"));
        var cpGo = new GameObject("Checkpoint - Bridge");
        cpGo.transform.SetParent(bridgeEnding.transform, false);
        var cp = cpGo.AddComponent<Checkpoint>();
        cp.title = "The Bridge";
        cp.health = 100f;
        cp.restorePoses = new Transform[0];
        cp.player = escape.player; cp.fader = escape.fader; cp.notice = escape.notice;
        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(cpGo.transform, false);
        spawn.SetPositionAndRotation(OnBridge(12f, 0f, -2.9f), Quaternion.LookRotation(BridgeDir));
        cp.spawn = spawn;
        UnityEventTools.AddPersistentListener(cp.onRespawn, boss.ResetFight);
        boss.checkpoint = cp;

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Bridge boss built.");
    }

    static string PathFrom(Transform root, Transform t)
    {
        if (t == null) return null;
        string p = t.name;
        while (t.parent != null && t.parent != root) { t = t.parent; p = t.name + "/" + p; }
        return t.parent == root ? p : null;
    }

    static GameObject Wall(Transform parent, string name, Vector3 local, Vector3 size)
    {
        var g = new GameObject("Wall " + name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = local;
        g.AddComponent<BoxCollider>().size = size;
        g.SetActive(false);
        return g;
    }

    static Material GlowMat(string path, Color baseCol, Color emission)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", baseCol);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject MakePickup(Transform parent)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Health pickup (template)";
        g.transform.SetParent(parent, false);
        g.transform.localScale = Vector3.one * 0.5f;
        var c = g.GetComponent<SphereCollider>(); c.isTrigger = true; c.radius = 1.6f;
        var mat = GlowMat("Assets/Materials/OfficeAct/HealthPickup.mat", new Color(0.6f, 1f, 0.7f), new Color(0.5f, 2.5f, 1f));
        g.GetComponent<Renderer>().sharedMaterial = mat;
        var l = new GameObject("Glow").AddComponent<Light>();
        l.transform.SetParent(g.transform, false);
        l.color = new Color(0.5f, 1f, 0.7f); l.intensity = 3f; l.range = 5f;
        var hp = g.AddComponent<HealthPickup>();
        hp.amount = 40f;
        hp.line = "Tomas's light... it's still warm.";
        g.SetActive(false);
        return g;
    }

    // Stand-in until the real demon is in: the damned body, three times the size, burning
    static void MakeStandIn(DemonBoss boss)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefab);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        g.name = "Stand-in (PrimeZombie)";
        Object.DestroyImmediate(g.GetComponent<ZombieAI>());
        Object.DestroyImmediate(g.GetComponent<UnityEngine.AI.NavMeshAgent>());
        Object.DestroyImmediate(g.GetComponent<Zombie>());
        g.transform.SetParent(boss.model, false);
        FitModel(boss, g);
    }

    // scale a model to BossHeight, feet on the pivot, facing +z; strip its physics, give the boss one hitbox and a heart
    static Bounds ModelBounds(GameObject g)
    {
        // skinned meshes: the bones (plus a margin), not the renderer's bounds, which some rigs ship far too big
        var smrs = g.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs.Length > 0 && smrs[0].bones.Length > 0)
        {
            var bones = smrs.SelectMany(x => x.bones).Where(x => x != null).ToArray();
            var bb = new Bounds(bones[0].position, Vector3.zero);
            foreach (var bn in bones) bb.Encapsulate(bn.position);
            bb.Expand(bb.size.magnitude * 0.08f);
            return bb;
        }
        var rs = g.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void FitModel(DemonBoss boss, GameObject g, float height = BossHeight)
    {
        Transform root = boss.transform;
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        foreach (var j in g.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
        foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var c in g.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);

        var rs = g.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) { Debug.LogWarning("Boss model has no renderers."); return; }
        foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = false;
        Bounds b = ModelBounds(g);
        float scale = height / Mathf.Max(0.01f, b.size.y);
        g.transform.localScale *= scale;
        b = ModelBounds(g);
        g.transform.position += new Vector3(root.position.x - b.center.x, root.position.y - b.min.y, root.position.z - b.center.z);
        b = ModelBounds(g);

        var anim = g.GetComponentInChildren<Animator>();
        if (anim != null) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        boss.animator = anim;

        // one hitbox for the body
        foreach (var c in root.GetComponents<CapsuleCollider>()) Object.DestroyImmediate(c);
        var cap = root.gameObject.AddComponent<CapsuleCollider>();
        float radius = Mathf.Min(b.extents.x, b.extents.z) * 0.8f;
        radius = Mathf.Clamp(radius, 0.6f, 2f);
        cap.radius = radius;
        cap.height = b.size.y;
        cap.center = new Vector3(0f, b.size.y * 0.5f, 0f);

        // its heart: glowing, in front of the chest, just outside the hitbox so bullets find it first
        var oldHeart = root.Find("Heart");
        if (oldHeart != null) Object.DestroyImmediate(oldHeart.gameObject);
        var heart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        heart.name = "Heart";
        heart.transform.SetParent(root, false);
        heart.transform.localPosition = new Vector3(0f, b.size.y * 0.68f, radius + 0.15f);
        heart.transform.localScale = Vector3.one * 0.75f;
        var mat = GlowMat("Assets/Materials/OfficeAct/DemonHeart.mat", new Color(0.5f, 0.02f, 0.02f), new Color(4f, 0.25f, 0.05f));
        heart.GetComponent<Renderer>().sharedMaterial = mat;
        var hl = new GameObject("Glow").AddComponent<Light>();
        hl.transform.SetParent(heart.transform, false);
        hl.transform.localPosition = Vector3.forward * 1.2f;
        hl.color = new Color(1f, 0.2f, 0.05f); hl.intensity = 6f; hl.range = 9f;
        boss.heart = heart.transform;
        var chest = g.GetComponentsInChildren<Transform>(true)
            .Where(t => { string n = t.name.ToLower(); return n.Contains("spine") || n.Contains("chest"); })
            .OrderBy(t => Mathf.Abs(t.position.y - heart.transform.position.y)).FirstOrDefault();
        if (chest != null) heart.transform.SetParent(chest, true);   // it moves with the body

        // a red key light so it reads against the hell sky
        var oldKey = root.Find("KeyLight");
        if (oldKey != null) Object.DestroyImmediate(oldKey.gameObject);
        var key = new GameObject("KeyLight").AddComponent<Light>();
        key.transform.SetParent(root, false);
        key.transform.localPosition = new Vector3(0f, b.size.y * 0.8f, 4f);
        key.color = new Color(1f, 0.3f, 0.12f); key.intensity = 10f; key.range = 16f;

        // stand-in only: animation clips are the damned's
        boss.moveClipSpeed = 3.67f;
    }

    // ---------------------------------------------------------------- the escape: Kessler hunts the van

    [MenuItem("HellScape/Finale/Wire Kessler Chase")]
    public static void WireChase()
    {
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        var horde = Object.FindFirstObjectByType<HordeChase>(FindObjectsInactive.Include);
        var ci = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        var bridgeEnding = GameObject.Find("BridgeEnding").transform;
        Undo.RecordObject(boss, "Chase"); Undo.RecordObject(horde, "Chase"); Undo.RecordObject(ci, "Chase");

        boss.chaseRoad = horde.road;
        horde.playIntro = false;

        // it bursts out of the office's glass doors, facing the van
        var door = bridgeEnding.Find("Kessler bursts out (office door)");
        if (door == null) { door = new GameObject("Kessler bursts out (office door)").transform; door.SetParent(bridgeEnding, false); }
        Vector3 doorPos = new Vector3(12.4f, -3.85f, -47.3f);
        Vector3 toVan = ci.transform.position - doorPos; toVan.y = 0f;
        door.SetPositionAndRotation(doorPos, Quaternion.LookRotation(toVan.normalized));
        boss.officeDoor = door;

        // the fight: it stands at the city end, you at the dead end
        var start = boss.arena.Find("FightStart");
        if (start == null) { start = new GameObject("FightStart").transform; start.SetParent(boss.arena, false); }
        start.SetPositionAndRotation(OnBridge(20f, 0f), Quaternion.LookRotation(BridgeDir));
        boss.fightStart = start;
        boss.standAt.SetPositionAndRotation(start.position, start.rotation);
        var cp = bridgeEnding.Find("Checkpoint - Bridge").GetComponent<Checkpoint>();
        cp.spawn.SetPositionAndRotation(OnBridge(60f, 0f, -2.9f), Quaternion.LookRotation(-BridgeDir));

        // the trigger at the far end of the bridge
        var trig = bridgeEnding.Find("Boss Trigger (bridge)");
        trig.SetPositionAndRotation(OnBridge(50f, 0f, -2f), Quaternion.LookRotation(BridgeDir));
        var st = trig.GetComponent<StoryTrigger>();
        Undo.RecordObject(st, "Chase");
        Remove(st.onEnter, boss);
        UnityEventTools.AddPersistentListener(st.onEnter, boss.ArriveAtBridgeEnd);

        // hooks: the keys start it, the Escape checkpoint puts it back in the office
        Remove(ci.onEnter, boss);
        UnityEventTools.AddPersistentListener(ci.onEnter, boss.BeginChase);
        var escape = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name.Contains("Escape"));
        Undo.RecordObject(escape, "Chase");
        Remove(escape.onRespawn, boss);
        UnityEventTools.AddPersistentListener(escape.onRespawn, boss.ResetChase);

        EditorUtility.SetDirty(boss); EditorUtility.SetDirty(horde); EditorUtility.SetDirty(ci); EditorUtility.SetDirty(st); EditorUtility.SetDirty(escape);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Debug.Log("Kessler chase wired.");
    }

    // ---------------------------------------------------------------- the arena at the end of the bridge

    // The user widened the end of the bridge into an arena. This measures it (deck rows much wider than the
    // bridge road, past the bridge mouth) and fits everything the fight uses to it: the fight box, the walls round
    // it, the trigger at its entrance, where Kessler lands and where you stand, the checkpoint, the dead-end barrier.
    [MenuItem("HellScape/Finale/Fit Boss Arena")]
    public static void FitArena()
    {
        var bridgeEnding = GameObject.Find("BridgeEnding").transform;
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        Physics.SyncTransforms();
        Vector3 across = Vector3.Cross(Vector3.up, BridgeDir);
        bool Deck(float along, float side)
        {
            Vector3 p = OnBridge(along, side, 40f);
            var hit = Physics.RaycastAll(p, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(bridgeEnding) && !h.collider.isTrigger && h.collider.GetComponentInParent<Rigidbody>() == null)
                .OrderBy(h => h.distance).FirstOrDefault();
            return hit.collider != null && hit.point.y > -6.5f && hit.point.y < 0f;
        }
        float sMin = float.MaxValue, sMax = float.MinValue, xMin = float.MaxValue, xMax = float.MinValue;
        for (float along = 25f; along <= 250f; along += 2f)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (float side = -120f; side <= 120f; side += 2f)
                if (Deck(along, side)) { lo = Mathf.Min(lo, side); hi = Mathf.Max(hi, side); }
            if (hi - lo < 30f) { if (sMax > float.MinValue) break; continue; }   // still the bridge road, or past the arena
            sMin = Mathf.Min(sMin, along); sMax = Mathf.Max(sMax, along);
            xMin = Mathf.Max(xMin == float.MaxValue ? lo : xMin, lo); xMax = Mathf.Min(xMax == float.MinValue ? hi : xMax, hi);
        }
        if (sMax <= sMin) { Debug.LogWarning("No arena found past the bridge mouth."); return; }
        float cs = (sMin + sMax) * 0.5f, cx = (xMin + xMax) * 0.5f;
        float halfLen = (sMax - sMin) * 0.5f, halfWid = (xMax - xMin) * 0.5f;
        Debug.Log($"Arena: along {sMin:0}..{sMax:0}, across {xMin:0}..{xMax:0} ({halfWid * 2f:0} x {halfLen * 2f:0} m)");

        Undo.RecordObject(boss, "Arena");
        boss.arena.SetPositionAndRotation(OnBridge(cs, cx), Quaternion.LookRotation(BridgeDir));
        boss.arenaHalfLength = halfLen - 1f;
        boss.arenaHalfWidth = halfWid - 1f;

        // walls round the edge: nobody falls in the river mid-fight
        var walls = bridgeEnding.Find("Boss fight walls");
        walls.SetPositionAndRotation(boss.arena.position, boss.arena.rotation);
        void Wall(int i, Vector3 local, Vector3 size)
        {
            var w = boss.fightWalls[i];
            w.transform.localPosition = local;
            w.GetComponent<BoxCollider>().size = size;
        }
        Wall(0, new Vector3(-halfWid - 0.3f, 3f, 0f), new Vector3(0.6f, 10f, halfLen * 2f + 2f));
        Wall(1, new Vector3(halfWid + 0.3f, 3f, 0f), new Vector3(0.6f, 10f, halfLen * 2f + 2f));
        Wall(2, new Vector3(0f, 3f, -halfLen - 0.3f), new Vector3(halfWid * 2f + 2f, 10f, 0.6f));
        Wall(3, new Vector3(0f, 3f, halfLen + 0.3f), new Vector3(halfWid * 2f + 2f, 10f, 0.6f));

        // the trigger just inside the entrance (the width of the arena), the van rolls to a stop mid-arena
        var trig = bridgeEnding.Find("Boss Trigger (bridge)");
        trig.SetPositionAndRotation(OnBridge(sMin + 10f, cx, -2f), Quaternion.LookRotation(BridgeDir));
        trig.GetComponent<BoxCollider>().size = new Vector3(halfWid * 2f, 8f, 6f);

        // it lands at the entrance, you stand nearer the middle, facing it
        boss.fightStart.SetPositionAndRotation(OnBridge(sMin + 9f, cx), Quaternion.LookRotation(BridgeDir));
        boss.standAt.SetPositionAndRotation(boss.fightStart.position, boss.fightStart.rotation);
        var ps = boss.arena.Find("PlayerStart");
        if (ps == null) { ps = new GameObject("PlayerStart").transform; ps.SetParent(boss.arena, false); }
        ps.SetPositionAndRotation(OnBridge(sMin + 42f, cx), Quaternion.LookRotation(-BridgeDir));
        boss.playerStart = ps;
        var cp = bridgeEnding.Find("Checkpoint - Bridge").GetComponent<Checkpoint>();
        cp.spawn.SetPositionAndRotation(OnBridge(sMin + 42f, cx, -2.9f), Quaternion.LookRotation(-BridgeDir));

        // the dead-end barrier goes to the far edge (it was in the middle of the new floor)
        var barrier = bridgeEnding.Find("DeadEndBarrier");
        if (barrier != null) { Undo.RecordObject(barrier, "Arena"); barrier.position = OnBridge(sMax - 1f, cx, barrier.position.y); }

        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
    }

    // (the escape cameras are placed by EscapeCameras.cs: HellScape > Finale > Place Escape Cameras)

    static Vector3 FlatV(Vector3 v) { v.y = 0f; return v; }

    // ---------------------------------------------------------------- the arena fight, in the van

    [MenuItem("HellScape/Finale/Wire Van Boss Fight")]
    public static void WireVanFight()
    {
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        var bw = Object.FindFirstObjectByType<BloodWave>(FindObjectsInactive.Include);
        Undo.RecordObject(boss, "Van fight");
        boss.fightInVan = true;
        boss.chaseBolts = false;
        if (bw != null && bw.orb != null) boss.orbModel = bw.orb.gameObject;   // the old escape's blood orb
        EditorUtility.SetDirty(boss);
        AddDriveBy();   // (adds the lock-on marker too)
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Debug.Log("Van boss fight wired: orbs " + (boss.orbModel != null ? boss.orbModel.name : "spheres"));
    }

    // ---------------------------------------------------------------- drive-by: shoot from the van

    [MenuItem("HellScape/Finale/Add Drive-By Shooting")]
    public static void AddDriveBy()
    {
        var ci = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        var db = ci.GetComponent<DriveBy>();
        if (db == null) db = Undo.AddComponent<DriveBy>(ci.gameObject);
        db.car = ci;
        db.carCamera = ci.carCamera;
        db.follow = ci.carCamera != null ? ci.carCamera.GetComponent<CameraFollow>() : null;

        // crosshair + ammo next to the rifle's own ammo counter
        var gun = Object.FindFirstObjectByType<GunController>(FindObjectsInactive.Include);
        Transform canvas = gun != null && gun.ammoText != null ? gun.ammoText.canvas.transform : Object.FindFirstObjectByType<Canvas>().transform;
        TMPro.TextMeshProUGUI Text(string name, string text, Vector2 anchor, Vector2 pos, float size)
        {
            var old = canvas.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            t.transform.SetParent(canvas, false);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(300f, 60f);
            t.text = text; t.fontSize = size; t.alignment = TMPro.TextAlignmentOptions.Center;
            t.color = new Color(1f, 0.92f, 0.85f, 0.9f);
            if (gun != null && gun.ammoText != null) t.font = gun.ammoText.font;
            t.gameObject.SetActive(false);
            return t;
        }
        db.crosshair = Text("DriveBy Crosshair", "+", new Vector2(0.5f, 0.5f), Vector2.zero, 44f);
        db.ammoText = Text("DriveBy Ammo", "30 / 30", new Vector2(0.85f, 0.08f), Vector2.zero, 30f);
        db.lockMarker = Text("DriveBy Lock", "[   ]", new Vector2(0.5f, 0.5f), Vector2.zero, 52f);
        db.lockMarker.color = new Color(1f, 0.15f, 0.1f, 0.95f);
        db.lockMarker.rectTransform.anchorMin = db.lockMarker.rectTransform.anchorMax = Vector2.zero;   // placed in screen pixels
        if (gun != null && gun.ammoText != null)
        {
            var a = (RectTransform)db.ammoText.transform; var g = gun.ammoText.rectTransform;
            a.anchorMin = g.anchorMin; a.anchorMax = g.anchorMax; a.pivot = g.pivot; a.anchoredPosition = g.anchoredPosition; a.sizeDelta = g.sizeDelta;
            db.ammoText.alignment = gun.ammoText.alignment; db.ammoText.fontSize = gun.ammoText.fontSize;
        }
        EditorUtility.SetDirty(db);
        EditorSceneManager.MarkSceneDirty(ci.gameObject.scene);
        Debug.Log("Drive-by added: hold right mouse in the van to lean out and aim, left mouse to shoot.");
    }

    // ---------------------------------------------------------------- the user's demon: Tillagemon

    const string TillagemonFbx = "Assets/New Folder/tillagemon-boss-skeleton/source/TillagemonBoss01_Skeleton.fbx";
    const string TillagemonController = "Assets/Materials/OfficeAct/Anim/TillagemonBoss.controller";

    [MenuItem("HellScape/Finale/Use Tillagemon As Boss")]
    public static void UseTillagemon()
    {
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if (boss == null) { BuildBoss(); boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include); }

        // loops for the ones that repeat
        var imp = (ModelImporter)AssetImporter.GetAtPath(TillagemonFbx);
        var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        foreach (var c in clips)
            c.loopTime = c.name.EndsWith("Battle_Walk") || c.name.EndsWith("Battle_Run") || c.name.EndsWith("Battle_Stand")
                      || c.name.EndsWith("Abnormal_Stun") || c.name.EndsWith("10_Loop");
        imp.clipAnimations = clips;
        imp.SaveAndReimport();

        AnimationClip Clip(string suffix) => AssetDatabase.LoadAllAssetsAtPath(TillagemonFbx).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview") && c.name.EndsWith(suffix));

        if (!AssetDatabase.IsValidFolder("Assets/Materials/OfficeAct/Anim")) AssetDatabase.CreateFolder("Assets/Materials/OfficeAct", "Anim");
        AssetDatabase.DeleteAsset(TillagemonController);
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(TillagemonController);
        var sm = ctrl.layers[0].stateMachine;
        void State(string name, string suffix, bool isDefault = false)
        {
            var st = sm.AddState(name);
            st.motion = Clip(suffix);
            if (isDefault) sm.defaultState = st;
        }
        State("Idle", "Battle_Stand", true);
        State("Walk", "Battle_Walk");
        State("Run", "Battle_Run");
        State("Slam", "Attack_Base_09");
        State("Roar", "Attack_Base_04");
        State("Throw", "Attack_Base_02");
        State("Stun", "Abnormal_Stun");
        State("Hurt", "Damage_Weak_01");
        State("Die", "Damage_Die");
        AssetDatabase.SaveAssets();

        Undo.RegisterFullObjectHierarchyUndo(boss.gameObject, "Tillagemon");
        for (int i = boss.model.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(boss.model.GetChild(i).gameObject);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TillagemonFbx));
        Undo.RegisterCreatedObjectUndo(g, "Tillagemon");
        g.name = "Tillagemon";
        g.transform.SetParent(boss.model, false);
        var anim = g.GetComponent<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        FitModel(boss, g, 6f);

        boss.idleState = "Idle";
        boss.moveState = "Walk";
        boss.chargeState = "Run";
        boss.slamState = "Slam";
        boss.roarState = "Roar";
        boss.throwState = "Throw";
        boss.stunState = "Stun";
        boss.dieState = "Die";
        boss.moveClipSpeed = boss.walkSpeed;   // the walk plays at 1x at its walking pace
        boss.slamWindup = 1.6f;                // the overhead pitchfork swing lands about here
        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Debug.Log("Tillagemon is Kessler's true form now.");
    }

    [MenuItem("HellScape/Finale/Use Selected As Boss Model")]
    public static void UseSelected()
    {
        var sel = Selection.activeGameObject;
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if (sel == null || boss == null) { EditorUtility.DisplayDialog("Boss model", "Select your demon model (in the scene or in the Project window) first, and build the bridge boss.", "OK"); return; }
        GameObject g = EditorUtility.IsPersistent(sel) ? (GameObject)PrefabUtility.InstantiatePrefab(sel) : sel;
        Undo.RegisterFullObjectHierarchyUndo(boss.gameObject, "Boss model");
        for (int i = boss.model.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(boss.model.GetChild(i).gameObject);
        Undo.SetTransformParent(g.transform, boss.model, "Boss model");
        FitModel(boss, g);
        EditorUtility.DisplayDialog("Boss model", $"{g.name} is now Kessler's true form.\n\nIf it has its own animations, put their state names into the DemonBoss component (Move / Slam / Roar / Throw / Charge). Without them it still fights, just stiffly.", "OK");
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
    }
}
