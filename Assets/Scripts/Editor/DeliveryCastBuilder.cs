using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// The prologue's delivery customers on the user's Meshy avatars (Assets/Models/New Npc Avatar, 2026-11-03):
//   No. 31        - the African woman: walks out of her door to the kerb, idles, carries the box back in
//   Flat 104      - the Asian woman (was the corner shop): nobody's home, she shouts WAIT, runs, slips, falls
//   Harlow Clinic - the doctor (idle + walk)
// Re-runnable: the old models are switched off, the new one is rebuilt.
public static class DeliveryCastBuilder
{
    const string D = "Assets/Models/New Npc Avatar/";
    const string Af = D + "Meshy_AI_ps2_african_woman_tpo_biped";
    const string As = D + "Meshy_AI_ps2_asian_woman_tpose_biped";
    const string Dr = D + "Meshy_AI_ps2_male_doctor_40s_3_biped";
    static string F(string dir, string n, string anim) => $"{dir}{n}/Meshy_AI_ps2_{Short(dir)}/Meshy_AI_ps2_{Short(dir)}_Animation_{anim}_withSkin.fbx";
    static string Short(string dir) => dir.Substring((D + "Meshy_AI_ps2_").Length);
    static string Tex(string dir) => $"{dir}/Meshy_AI_ps2_{Short(dir)}/Meshy_AI_ps2_{Short(dir)}_texture_0.png";
    const string AvatarName = "Avatar (Meshy)";
    // Man() scales by the bind pose, but in game (Humanoid retarget, idle) they stand shorter. These bind heights
    // give, measured in Play from the posed mesh: Asian woman 1.66 m, African woman 1.70 m, doctor 1.75 m, next to
    // the player's ~1.70 m (camera 1.56 m above the ground).
    const float AsianH = 1.814f, AfricanH = 1.87f, DoctorH = 1.936f;

    [MenuItem("HellScape/Prologue: Delivery Cast (new avatars)")]
    public static void Build()
    {
        string afCarry = F(Af, "", "Carry_Heavy_Object_Walk"), afWalk = F(Af, " (1)", "Walking_Woman"), afIdle = F(Af, " (2)", "Idle_4");
        string asWalk = F(As, "", "Walking_Woman"), asRun = F(As, " (1)", "Running");
        string drIdle = F(Dr, "", "Idle_11"), drWalk = F(Dr, " (1)", "Walking");
        foreach (var f in new[] { drIdle, drWalk }) MeshyCastBuilder.Loop(f, true);
        foreach (var f in new[] { afCarry, afWalk, afIdle }) Humanoid(f);   // (Generic left her arms out to the sides)

        var run = Object.FindFirstObjectByType<DeliveryRun>(FindObjectsInactive.Include);
        if (run == null) { Debug.LogError("No DeliveryRun in the open scene (open the Prologue)."); return; }
        Undo.RecordObject(run, "Delivery cast");

        // No. 31: out of her door, idle at the kerb, box in her arms back inside
        var s31 = run.stops.First(s => s.customer != null && s.customer.name.Contains("Number 31"));
        var afCtrl = Ctrl("DeliveryAfricanWoman", MeshyCastBuilder.Clip(afIdle), MeshyCastBuilder.Clip(afWalk), MeshyCastBuilder.Clip(afCarry), null);
        s31.customerAnimator = Swap(s31.customer.transform, afIdle, AfricanH, MeshyCastBuilder.Mat("DeliveryAfricanWoman", Tex(Af)), afCtrl);
        s31.entrance = Door(s31, "Number 31 front door");
        s31.via = Via(s31, "Number 31 gap in the fence");
        s31.carryBox = true;
        s31.exitSpeed = 1.1f;
        s31.arriveLine = "*Number 31.*";

        // Flat 104 (was the corner shop): the comedy stop
        var s104 = run.stops.First(s => s.customer != null && (s.customer.name.Contains("Corner shop") || s.customer.name.Contains("Flat 104")));
        // she has no idle: the African woman's, retargeted through Humanoid (a copy of that FBX, so hers stays Generic)
        const string asIdleFbx = "Assets/Generated/Meshy/DeliveryAsianWoman_Idle_4 (from the African woman).fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(asIdleFbx) == null) AssetDatabase.CopyAsset(afIdle, asIdleFbx);
        // and her fall: Mixamo's Falling Flat Impact (face down, she stays there), also through Humanoid
        const string asFallFbx = "Assets/Generated/Meshy/DeliveryAsianWoman_Fall (Falling Flat Impact).fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(asFallFbx) == null) AssetDatabase.CopyAsset("Assets/Models/FallAnimation/Falling Flat Impact.fbx", asFallFbx);
        foreach (var f in new[] { asWalk, asRun, asIdleFbx }) Humanoid(f);
        Humanoid(asFallFbx, false);
        var asCtrl = Ctrl("DeliveryAsianWoman", MeshyCastBuilder.Clip(asIdleFbx), MeshyCastBuilder.Clip(asWalk), null, MeshyCastBuilder.Clip(asRun), MeshyCastBuilder.Clip(asFallFbx));
        s104.customerAnimator = Swap(s104.customer.transform, asWalk, AsianH, MeshyCastBuilder.Mat("DeliveryAsianWoman", Tex(As)), asCtrl);
        s104.customer.name = "Parcel customer - Flat 104";
        // the user's two markers by her block: "Cube" = where she runs out from, "Cube (1)" = where she falls
        var housing = GameObject.Find("Urban Housing - marked lots/Housing 10 - 3_Window_0.004");
        var from = housing != null ? housing.transform.Find("Cube") : null;
        var fall = housing != null ? housing.transform.Find("Cube (1)") : null;
        if (from != null && fall != null)
        {
            foreach (var m in new[] { from, fall })
            {
                if (m.GetComponent<Renderer>() is Renderer r) { Undo.RecordObject(r, "marker"); r.enabled = false; }
                if (m.GetComponent<Collider>() is Collider c) { Undo.RecordObject(c, "marker"); c.enabled = false; }
            }
            s104.entrance = from;
            s104.fallAt = fall;
        }
        else { Debug.LogWarning("Flat 104: the Cube / Cube (1) markers under Housing 10 are gone; she runs from her front door at you."); s104.entrance = Door(s104, "Flat 104 front door"); s104.fallAt = null; }
        s104.carryBox = false;
        s104.lateRunner = true;
        s104.exitSpeed = 0.9f;   // (a sore walk home)
        s104.objective = "Deliver the parcel - Flat 104";
        s104.arriveLine = "*Flat 104. My old block. We lived here when we were broke, before the company took off and we moved somewhere better.*|*Nobody's waiting.*|Parcel for Flat 104?|*Nothing. Fine. Back to the van.*";
        s104.waitLine = "Woman: WAIT!";
        s104.handOverLines = "HEY! Are you okay? I'm coming to help.|Woman: No, don't come! I'm fine. It's alright, I'm okay. Just leave the package there, I'll come pick it up.|Alright, ma'am. You sure you don't want my help?|Woman: I'm fine. Just leave.";
        s104.afterLines = "";

        // Harlow Clinic: the doctor
        var sDoc = run.stops.First(s => s.customer != null && s.customer.name.Contains("Harlow Clinic"));
        var drCtrl = Ctrl("DeliveryDoctor", MeshyCastBuilder.Clip(drIdle), MeshyCastBuilder.Clip(drWalk), null, null);
        sDoc.customerAnimator = Swap(sDoc.customer.transform, drIdle, DoctorH, MeshyCastBuilder.Mat("DeliveryDoctor", Tex(Dr)), drCtrl);

        EditorUtility.SetDirty(run);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(run.gameObject.scene);
        Debug.Log("Delivery cast built: No. 31 (African woman), Flat 104 (Asian woman, the slip), Harlow Clinic (doctor).");
    }

    // The cafe worker on the user's Nadir avatar (Assets/Models/Nadir Idle / Nadir Walk, 2026-11-04), a red-brown
    // takeaway cup in his right hand. Re-runnable.
    [MenuItem("HellScape/Prologue: Cafe Worker (Nadir)")]
    public static void BuildCafeWorker()
    {
        const string N = "Assets/Models/Nadir {0}/Meshy_AI_Full_body_character_model_in_A_pose_20261104_animated_mixamo_{1}/Meshy_AI_Full_body_character_model_in_A_pose_20261104_animated_mixamo_{1}";
        string idle = string.Format(N, "Idle", "Idle_02"), walk = string.Format(N, "Walk", "Walking");
        foreach (var f in new[] { idle, walk }) MeshyCastBuilder.Loop(f + ".fbx", true);

        var run = Object.FindFirstObjectByType<DeliveryRun>(FindObjectsInactive.Include);
        if (run == null) { Debug.LogError("No DeliveryRun in the open scene (open the Prologue)."); return; }
        var s = run.stops.FirstOrDefault(x => x.customer != null && x.customer.name.Contains("Cafe worker"));
        if (s == null) { Debug.LogError("No cafe stop (run HellScape > Prologue > Build Cafe Stop first)."); return; }
        Undo.RecordObject(run, "Cafe worker");

        var ctrl = Ctrl("CafeWorkerNadir", MeshyCastBuilder.Clip(idle + ".fbx"), MeshyCastBuilder.Clip(walk + ".fbx"), null, null);
        s.customerAnimator = Swap(s.customer.transform, idle + ".fbx", DoctorH, MeshyCastBuilder.Mat("CafeWorkerNadir", idle + ".png"), ctrl);

        // the cup: a tapered paper cup with a darker lid, parented to the right hand
        var hand = s.customerAnimator.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:RightHand");
        var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cup.name = "Coffee cup";
        Object.DestroyImmediate(cup.GetComponent<Collider>());
        cup.GetComponent<Renderer>().sharedMaterial = CupMat("CoffeeCup", new Color(.42f, .14f, .08f));
        cup.transform.localScale = new Vector3(.08f, .06f, .08f);   // 8 cm wide, 12 cm tall
        var lid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lid.name = "Lid";
        Object.DestroyImmediate(lid.GetComponent<Collider>());
        lid.GetComponent<Renderer>().sharedMaterial = CupMat("CoffeeCupLid", new Color(.16f, .07f, .04f));
        lid.transform.SetParent(cup.transform, false);
        lid.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        lid.transform.localScale = new Vector3(1.08f, .08f, 1.08f);
        // in the palm: a little below the wrist bone, toward the fingers, standing upright
        var fingers = hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "mixamorig:RightHandMiddle1");
        Vector3 at = fingers != null ? Vector3.Lerp(hand.position, fingers.position, .7f) : hand.position;
        cup.transform.SetPositionAndRotation(at + Vector3.down * .02f, Quaternion.identity);
        cup.transform.SetParent(hand, true);
        Undo.RegisterCreatedObjectUndo(cup, "Cafe cup");

        EditorUtility.SetDirty(run);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(run.gameObject.scene);
        Debug.Log("Cafe worker is Nadir now (idle + walk), cup in his right hand.");
    }

    static Material CupMat(string name, Color c)
    {
        string path = "Assets/Generated/Meshy/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .25f); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // the old model off, the Meshy one in its place (the customer root keeps its collider and Interactable)
    static Animator Swap(Transform customer, string fbx, float height, Material mat, AnimatorController ctrl)
    {
        foreach (Transform c in customer.Cast<Transform>().ToArray())
        {
            if (c.name == AvatarName) Undo.DestroyObjectImmediate(c.gameObject);
            else if (c.GetComponentInChildren<Animator>(true) != null) { Undo.RecordObject(c.gameObject, "off"); c.gameObject.SetActive(false); }
        }
        var g = MeshyCastBuilder.Man(fbx, AvatarName, customer, height, mat, ctrl);
        Undo.RegisterCreatedObjectUndo(g, "Delivery cast");
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        return g.GetComponent<Animator>();
    }

    // Idle / Walk on Speed, Carry and Run on bools (Carry wins over walking, Run over everything)
    // (Fall, when given: a one-shot that wins over everything and holds its last frame)
    static AnimatorController Ctrl(string name, AnimationClip idle, AnimationClip walk, AnimationClip carry, AnimationClip runClip, AnimationClip fallClip = null)
    {
        string path = "Assets/Generated/Meshy/" + name + ".controller";
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var p in ac.parameters.ToArray()) ac.RemoveParameter(p);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Carry", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Run", AnimatorControllerParameterType.Bool);
        if (fallClip != null) ac.AddParameter("Fall", AnimatorControllerParameterType.Bool);
        var sm = ac.layers[0].stateMachine;
        foreach (var old in sm.states) sm.RemoveState(old.state);
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

        AnimatorState State(string n, Motion m, params (AnimatorConditionMode mode, float v, string p)[] when)
        {
            var s = sm.AddState(n); s.motion = m;
            var t = sm.AddAnyStateTransition(s);
            t.canTransitionToSelf = false; t.hasExitTime = false; t.duration = .2f; t.hasFixedDuration = true;
            foreach (var (mode, v, p) in when) t.AddCondition(mode, v, p);
            if (fallClip != null && n != "Fall") t.AddCondition(AnimatorConditionMode.IfNot, 0, "Fall");
            return s;
        }
        if (fallClip != null) State("Fall", fallClip, (AnimatorConditionMode.If, 0, "Fall"));
        var idleS = State("Idle", idle, (AnimatorConditionMode.Less, .1f, "Speed"), (AnimatorConditionMode.IfNot, 0, "Carry"), (AnimatorConditionMode.IfNot, 0, "Run"));
        State("Walk", walk, (AnimatorConditionMode.Greater, .1f, "Speed"), (AnimatorConditionMode.IfNot, 0, "Carry"), (AnimatorConditionMode.IfNot, 0, "Run"));
        if (carry != null) State("Carry", carry, (AnimatorConditionMode.If, 0, "Carry"), (AnimatorConditionMode.IfNot, 0, "Run"));
        if (runClip != null) State("Run", runClip, (AnimatorConditionMode.If, 0, "Run"));
        sm.defaultState = idleS;
        EditorUtility.SetDirty(ac);
        return ac;
    }

    // Humanoid (mixamorig names map on their own), each file its own avatar, in place
    static void Humanoid(string fbx, bool loop = true)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        if (imp.animationType == ModelImporterAnimationType.Human) { MeshyCastBuilder.Loop(fbx, loop); return; }
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.SaveAndReimport();
        imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = loop;
            c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
            c.keepOriginalPositionY = true; c.keepOriginalOrientation = true; c.keepOriginalPositionXZ = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
        if (AssetDatabase.LoadAssetAtPath<Avatar>(fbx) is Avatar a && !a.isHuman) Debug.LogError("Humanoid avatar failed for " + fbx);
    }

    // when a fence, low wall or bollard line stands between their door and their spot: the point within 10 m of the
    // spot that both legs can be walked to in a straight line (shortest detour); none needed -> no `via`
    static Transform Via(DeliveryRun.Stop s, string name)
    {
        var parent = s.customer.transform.parent;
        var old = parent.Find(name);
        Vector3 spot = s.customer.transform.position, door = s.entrance.position;
        var mine = s.customer.GetComponentsInChildren<Collider>(true);
        bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; d.y = 0f;
            if (d.magnitude < 0.05f) return true;
            return !Physics.CapsuleCastAll(a + Vector3.up * 0.6f, a + Vector3.up * 1.6f, 0.28f, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore)
                .Any(h => !mine.Contains(h.collider) && h.normal.y < 0.6f);   // (the ground and steps don't count)
        }
        if (Clear(spot, door))
        {
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            return null;
        }
        Vector3 best = Vector3.zero; float bestLen = float.MaxValue;
        for (float x = -10f; x <= 10f; x += 0.4f)
            for (float z = -10f; z <= 10f; z += 0.4f)
            {
                var p = spot + new Vector3(x, 0f, z);
                if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore) || g.normal.y < 0.8f) continue;
                p.y = g.point.y;
                if (Physics.CheckCapsule(p + Vector3.up * 0.6f, p + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float len = Vector3.Distance(spot, p) + Vector3.Distance(p, door);
                if (len >= bestLen || !Clear(spot, p) || !Clear(p, door)) continue;
                best = p; bestLen = len;
            }
        if (bestLen == float.MaxValue) { Debug.LogWarning($"{s.customer.name}: no clear way from the door to the kerb found; she walks straight."); return null; }
        var t = old != null ? old : new GameObject(name).transform;
        if (old == null) { Undo.RegisterCreatedObjectUndo(t.gameObject, "via"); t.SetParent(parent, true); }
        t.position = best;
        return t;
    }

    // their front door: the building wall behind where they stand (away from the bay), facing the street
    static Transform Door(DeliveryRun.Stop s, string name)
    {
        var parent = s.customer.transform.parent;
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; Undo.RegisterCreatedObjectUndo(t.gameObject, "door"); t.SetParent(parent, true); }
        Vector3 at = s.customer.transform.position;
        Vector3 dir = at - s.bay.position; dir.y = 0f; dir.Normalize();
        Vector3 door = at + dir * 12f;
        var mine = s.customer.GetComponentsInChildren<Collider>(true);
        var hits = Physics.RaycastAll(at + Vector3.up * 1.2f, dir, 40f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
        bool wallFound = false;
        // (a real wall: lamp posts, bollards and railings are thinner than 2 m and get walked past, not into)
        foreach (var h in hits)
        {
            if (mine.Contains(h.collider) || h.distance < 1.5f) continue;
            var sz = h.collider.bounds.size;
            if (Mathf.Max(sz.x, sz.z) < 2f || sz.y < 2.5f) continue;
            door = h.point - dir * 1.2f; wallFound = true; break;
        }
        if (Physics.Raycast(new Vector3(door.x, at.y + 3f, door.z), Vector3.down, out var g, 8f, ~0, QueryTriggerInteraction.Ignore)) door.y = g.point.y;
        else door.y = at.y;
        // in open ground (no wall to go in at) she walks on 12 m and fades when unseen: turn that way to a clear line
        Vector3 away = dir;
        bool Blocked(Vector3 d, float len) => Physics.CapsuleCastAll(door + Vector3.up * 0.6f, door + Vector3.up * 1.6f, 0.28f, d, len, ~0, QueryTriggerInteraction.Ignore)
            .Any(h => !mine.Contains(h.collider) && h.normal.y < 0.6f);
        if (!wallFound && Blocked(dir, 12f))
            for (int a = 10; a <= 90; a += 10)
            {
                var l = Quaternion.Euler(0, a, 0) * dir; var r = Quaternion.Euler(0, -a, 0) * dir;
                if (!Blocked(l, 12f)) { away = l; break; }
                if (!Blocked(r, 12f)) { away = r; break; }
            }
        t.SetPositionAndRotation(door, Quaternion.LookRotation(-away));
        return t;
    }
}
