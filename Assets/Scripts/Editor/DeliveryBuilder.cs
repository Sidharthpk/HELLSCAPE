using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Prologue: Build Van Deliveries (MainGameScene open; the Prologue is opened alongside it).
// The prologue now opens at 6:38 in the Kessler & Vane van with two drops left: Mrs. Okafor's parcel, then the
// clinic's cool box. Then the depot bay by the office, keys through the mail slot, 7:12, and the walk home.
//   - the van: a copy of MainGameScene's van (the same visuals, wheels, physics), stripped of the hell-city scripts,
//     with its own chase camera, engine sound and PrologueVan (get in / out)
//   - the route runs along MainGameScene's escape road mapped into the prologue (the prologue's street blocks are
//     copies of MainGameScene's blocks (1) and (2); block (0) was added as the delivery route's first stretch)
//   - two customers (copies of the street's pedestrians) with Interactables, a beacon, DeliveryRun wired to the director
public static class DeliveryBuilder
{
    [MenuItem("HellScape/Prologue: Build Van Deliveries")]
    public static void Build()
    {
        var main = SceneManager.GetSceneByName("MainGameScene");
        if (!main.isLoaded) { Debug.LogWarning("Open MainGameScene first."); return; }
        var pro = SceneManager.GetSceneByName("Prologue");
        if (!pro.isLoaded) pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);

        var props = main.GetRootGameObjects().First(g => g.name == "Props").transform;
        var m1 = props.Find("сити исчерп на скетч (1)");
        var p1 = pro.GetRootGameObjects().First(g => g.name == "сити исчерп на скетч (1)").transform;
        Matrix4x4 M = Matrix4x4.TRS(p1.position, p1.rotation, Vector3.one) * Matrix4x4.TRS(m1.position, m1.rotation, Vector3.one).inverse;
        Vector3 Map(Vector3 v) => M.MultiplyPoint3x4(v);
        Transform RoadPoint(string n) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.gameObject.scene == main && t.name == n);
        float Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject.scene == pro && !h.collider.isTrigger).OrderBy(h => h.distance).ToArray();
            return hits.Length > 0 ? hits[0].point.y : p.y;
        }
        Vector3 OnGround(Vector3 p) { p.y = Ground(p); return p; }

        var prologueRoot = pro.GetRootGameObjects().First(g => g.name == "Prologue").transform;
        var director = prologueRoot.GetComponentInChildren<PrologueDirector>(true);
        var old = prologueRoot.Find("Deliveries");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Deliveries").transform;
        root.SetParent(prologueRoot, false);

        // ---------------------------------------------------------------- the van
        var mainVan = main.GetRootGameObjects().First(g => g.name == "Prometheus");
        var mainCtl = mainVan.GetComponent<PrometeoCarController>();
        var van = Object.Instantiate(mainVan);
        SceneManager.MoveGameObjectToScene(van, pro);
        van.name = "Kessler & Vane Van";
        van.transform.SetParent(root, true);
        foreach (var c in new System.Type[] { typeof(CarInteract), typeof(CarHealth), typeof(CarZombieHit) })
        { var comp = van.GetComponent(c); if (comp != null) Object.DestroyImmediate(comp); }
        foreach (var bc in van.GetComponents<BoxCollider>()) if (bc.isTrigger) Object.DestroyImmediate(bc);   // the hell city's get-in trigger
        foreach (var n in new[] { "Body", "Wheels/Meshes" }) { var t = van.transform.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject); }
        var ctl = van.GetComponent<PrometeoCarController>();
        ctl.useUI = false;
        ctl.carSpeedText = null;

        // engine and tyre sounds of its own
        AudioSource Src(string name, AudioClip clip)
        {
            var s = new GameObject(name).AddComponent<AudioSource>();
            s.transform.SetParent(van.transform, false);
            s.clip = clip; s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0.6f;
            return s;
        }
        ctl.carEngineSound = Src("Engine", mainCtl.carEngineSound != null ? mainCtl.carEngineSound.clip : null);
        ctl.tireScreechSound = Src("Tyres", mainCtl.tireScreechSound != null ? mainCtl.tireScreechSound.clip : null);
        ctl.useSounds = ctl.carEngineSound.clip != null;

        // parked at the start: main's garage road point, facing along the road towards its office
        Vector3 start = Map(new Vector3(4f, -3.88f, 2f));   // (main's street outside the flats, a block before the first stop)
        Vector3 toward = Map(new Vector3(-4.78f, -3.88f, -44.62f)) - start; toward.y = 0f;
        van.transform.SetPositionAndRotation(OnGround(start) + Vector3.up * 0.05f, Quaternion.LookRotation(toward.normalized));

        // a chase camera
        var camGo = new GameObject("Van Camera");
        camGo.transform.SetParent(root, false);
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        var listener = camGo.AddComponent<AudioListener>(); listener.enabled = false;
        var follow = camGo.AddComponent<CameraFollow>();
        follow.carTransform = van.transform; follow.cam = cam; follow.carController = ctl;
        follow.baseOffset = new Vector3(0f, 2.9f, -7.2f); follow.lookHeightOffset = 1.5f;
        camGo.transform.position = van.transform.TransformPoint(follow.baseOffset);
        camGo.transform.LookAt(van.transform.position + Vector3.up * 1.5f);
        if (director.playerCam != null) { cam.nearClipPlane = director.playerCam.nearClipPlane; cam.farClipPlane = Mathf.Max(400f, director.playerCam.farClipPlane); }
        var urp = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        urp.renderPostProcessing = true;

        // getting in: an Interactable on the van, out: F again
        var getIn = van.AddComponent<Interactable>();
        getIn.prompt = "Get in the van";
        getIn.once = false;
        getIn.lockedLine = "";
        var pv = van.AddComponent<PrologueVan>();
        pv.controller = ctl; pv.body = van.GetComponent<Rigidbody>(); pv.vanCamera = cam;
        pv.player = director.fpc.gameObject; pv.playerCamera = director.playerCam;
        pv.exitPoint = van.transform.Find("Exit"); pv.getIn = getIn; pv.engine = ctl.carEngineSound;

        // ---------------------------------------------------------------- the route
        Transform Mark(string name, Vector3 at, Vector3 face)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            face.y = 0f;
            t.SetPositionAndRotation(OnGround(at), Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward));
            return t;
        }
        Vector3 bay1 = Map(new Vector3(-4.78f, -3.88f, -44.62f));             // the first street of the new block
        Vector3 dir1 = Map(RoadPoint("RoadPoint 00").position) - bay1;
        Vector3 bay2 = Map(RoadPoint("RoadPoint 03").position);
        Vector3 dir2 = Map(RoadPoint("RoadPoint 04").position) - bay2;
        var stop1 = Mark("Stop 1 - Mrs Okafor (bay)", bay1, dir1);
        var stop2 = Mark("Stop 2 - Harlow Clinic (bay)", bay2, dir2);
        var depot = Mark("Depot bay (office)", new Vector3(62f, 0f, 1.5f), Vector3.left);
        var after = Mark("After clocking out (office front)", director.fpc.transform.position, director.fpc.transform.forward);
        after.position = director.fpc.transform.position;

        // the customers: pedestrians off the street, waiting on the pavement beside each bay
        var walkers = prologueRoot.GetComponentsInChildren<PavementWalker>(true);
        Interactable Customer(string name, Transform bay, int pick, string prompt, out Animator anim)
        {
            var src = walkers[Mathf.Clamp(pick, 0, walkers.Length - 1)].gameObject;
            var g = Object.Instantiate(src, root);
            g.name = name;
            foreach (var w in g.GetComponentsInChildren<PavementWalker>(true)) Object.DestroyImmediate(w);
            anim = g.GetComponentInChildren<Animator>();
            // on the pavement: whichever side of the road has ground and a clear line from the bay
            Vector3 side = Vector3.Cross(Vector3.up, bay.forward);
            Vector3 best = bay.position + side * 7f;
            foreach (float s in new[] { 7.5f, -7.5f, 6f, -6f, 9f, -9f })
            {
                Vector3 p = bay.position + side * s;
                bool blocked = Physics.Linecast(bay.position + Vector3.up * 1.2f, p + Vector3.up * 1.2f, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.gameObject.scene == pro;
                if (!blocked) { best = p; break; }
            }
            Vector3 face = bay.position - best; face.y = 0f;
            g.transform.SetPositionAndRotation(OnGround(best), Quaternion.LookRotation(face.normalized));
            var cap = g.AddComponent<CapsuleCollider>();
            cap.height = 1.8f; cap.radius = 0.4f; cap.center = new Vector3(0f, 0.9f, 0f);
            var it = g.AddComponent<Interactable>();
            it.prompt = prompt; it.once = true; it.lockedLine = "";
            return it;
        }
        var okafor = Customer("Mrs Okafor", stop1, 1, "Hand over the parcel", out var okaforAnim);
        var clinic = Customer("Man at the door (Harlow Clinic)", stop2, 0, "Hand over the cool box", out var clinicAnim);

        // a column of light over wherever you're headed
        var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "Delivery beacon";
        beacon.transform.SetParent(root, false);
        Object.DestroyImmediate(beacon.GetComponent<Collider>());
        beacon.transform.localScale = new Vector3(1.4f, 30f, 1.4f);
        var bm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prologue_Beacon.mat");
        if (bm == null)
        {
            bm = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            bm.SetFloat("_Surface", 1f); bm.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); bm.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            bm.SetInt("_ZWrite", 0); bm.renderQueue = 3000; bm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(bm, "Assets/Materials/Prologue_Beacon.mat");
        }
        bm.SetColor("_BaseColor", new Color(1f, 0.78f, 0.35f, 0.22f));
        EditorUtility.SetDirty(bm);
        beacon.GetComponent<Renderer>().sharedMaterial = bm;
        beacon.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var bl = new GameObject("Glow").AddComponent<Light>();
        bl.transform.SetParent(beacon.transform, false);
        bl.transform.localPosition = new Vector3(0f, 0.05f, 0f);   // (the column is scaled 30 tall: this is about a metre up)
        bl.color = new Color(1f, 0.8f, 0.45f); bl.intensity = 4f; bl.range = 12f;
        beacon.SetActive(false);

        // ---------------------------------------------------------------- the run
        var run = root.gameObject.AddComponent<DeliveryRun>();
        run.van = pv;
        run.stops = new[]
        {
            new DeliveryRun.Stop
            {
                objective = "Deliver the parcel - Mrs. Okafor, No. 14",
                bay = stop1, bayRadius = 13f, customer = okafor, customerAnimator = okaforAnim,
                arriveLine = "Number 14. Mrs. Okafor's already out on the step. She always is.",
                handOverLines = "Mrs. Okafor: There he is. You look like death, love.|Mrs. Okafor: Your brother did my street on Thursday. Kept looking over his shoulder the whole time.|Mrs. Okafor: I asked him what was wrong. He said, 'Nothing I can fix.'|...Derek never tells me anything.|Mrs. Okafor: Go home. Make him talk to you.",
            },
            new DeliveryRun.Stop
            {
                objective = "Deliver the cool box - Harlow Clinic, back entrance",
                bay = stop2, bayRadius = 13f, customer = clinic, customerAnimator = clinicAnim,
                arriveLine = "The cool box. 'KEEP COLD - DO NOT OPEN.' Kessler's handwriting. Same back door every Saturday.",
                handOverLines = "Man at the door: You're late. Kessler said half six.|Man at the door: No paperwork. No names. Same as always.|...What's actually in these boxes?|Man at the door: Spare parts. You drive, you get your bonus, you don't ask. That's the deal.|He takes it and shuts the door in my face.|...Spare parts.",
            },
        };
        run.depotBay = depot; run.depotRadius = 12f;
        run.afterSpawn = after;
        run.beacon = beacon;
        run.objective = director.objective; run.titleCard = director.titleCard; run.fader = director.fader;
        director.deliveries = run;
        director.timeCard = "SATURDAY  -  6:38 AM";
        EditorUtility.SetDirty(director);

        // the round ends in the garage bay, if the garage is there (HellScape > Map > Build Garage)
        var garage = GarageBuilder.Find(pro);
        if (garage != null && garage.Find(GarageBuilder.ParkName) != null) GarageBuilder.WirePrologue(pro, main, garage, new System.Text.StringBuilder());

        EditorSceneManager.MarkSceneDirty(pro);
        EditorSceneManager.SaveScene(pro);
        Debug.Log($"Deliveries built: van at {van.transform.position}, stops at {stop1.position} and {stop2.position}, depot {depot.position}.");
    }
}
