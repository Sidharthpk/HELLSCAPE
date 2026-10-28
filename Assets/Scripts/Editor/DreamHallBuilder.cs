using System.Linq;
using UnityEditor;
using UnityEngine;

// HellScape > Void: Dream Hall: builds Assets/Resources/DreamHall.prefab for EndingSequence.DreamWalk (the first death):
// the cathedral nave ("interior test"), scaled up and turned so the nave runs along +Z with the floor at y 0 and the
// axis on x 0, and the Demon King's throne with the King seated in it (Chair Sit Idle, a black silhouette), at the
// far end of the nave, facing -Z. The model has no floor and no end wall: the dream lays its own floor.
// Its east arm is a row of identical bays, so DreamHall.bay is exactly one of them (measured on the model: .043495 units).
public static class DreamHallBuilder
{
    const string Hall = "Assets/Models/interior test.fbx";
    const string Throne = "Assets/Models/demon-king-throne-dark-fantasy-gothic-chair/source/model.glb";
    const string KingDir = "Assets/Perishables/Avatar/Meshy_AI_Crimson_Hellscape_Dem_biped (2)/Demon King Seated/";
    const string KingSit = KingDir + "Meshy_AI_Crimson_Hellscape_Dem_biped_Animation_Chair_Sit_Idle_M_withSkin.fbx";
    const string Out = "Assets/Generated/Meshy/";

    public static float Scale = 140f;           // the model's units to metres: one bay 6.1 m, the nave 13.7 m wide, the vault 25 m
    const float BayUnits = .043495f, NaveAxisZ = .0492f;
    public static float ThroneScale = 5.5f, KingScale = 5.3f;   // the throne ~10 m tall, the King in it nearly as wide but clearly smaller than the chair
    public static float ThroneSeatY = .865f, KingHipsAboveSeat = .13f;   // in the model's own units
    public static float SeatFromEnd = 4.5f;     // metres from the end of the nave to the throne's middle

    [MenuItem("HellScape/Void: Dream Hall")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var hallAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Hall);
        if (hallAsset == null) return "Missing " + Hall;
        if (!AssetDatabase.IsValidFolder("Assets/Generated/Meshy")) AssetDatabase.CreateFolder("Assets/Generated", "Meshy");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        var hallMat = AssetDatabase.LoadAssetAtPath<Material>(Out + "DreamHall.mat");
        if (hallMat == null) { hallMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(hallMat, Out + "DreamHall.mat"); }
        hallMat.SetColor("_BaseColor", Color.white); hallMat.SetFloat("_Smoothness", .12f); hallMat.SetFloat("_Metallic", 0f);
        hallMat.EnableKeyword("_EMISSION"); hallMat.SetColor("_EmissionColor", new Color(.16f, .16f, .18f));
        EditorUtility.SetDirty(hallMat);
        var black = AssetDatabase.LoadAssetAtPath<Material>(Out + "DreamSilhouette.mat");
        if (black == null) { black = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(black, Out + "DreamSilhouette.mat"); }
        black.SetColor("_BaseColor", Color.black);
        EditorUtility.SetDirty(black);
        var iron = AssetDatabase.LoadAssetAtPath<Material>(Out + "DreamThrone.mat");   // the throne is a shade lighter than he is, so that he reads against it
        if (iron == null) { iron = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(iron, Out + "DreamThrone.mat"); }
        iron.SetColor("_BaseColor", new Color(.42f, .06f, .05f));
        EditorUtility.SetDirty(iron);

        var root = new GameObject("DreamHall");
        var dh = root.AddComponent<DreamHall>();

        // the nave: the model's own bounds say where its east end is; z 0 is seven bays before it
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(hallAsset);
        var mb = probe.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
        Object.DestroyImmediate(probe);
        float bay = BayUnits * Scale, endX = mb.max.x, zeroX = endX - 7f * BayUnits;
        var hall = (GameObject)PrefabUtility.InstantiatePrefab(hallAsset, root.transform);
        hall.name = "Cathedral";
        hall.transform.localScale = Vector3.one * Scale;
        hall.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);   // the model's +x (the nave) becomes +z
        hall.transform.localPosition = new Vector3(NaveAxisZ * Scale, 0f, -zeroX * Scale);
        var rs = hall.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rs) { r.sharedMaterial = hallMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        dh.hall = rs; dh.bay = bay; dh.armLength = 7f * bay; dh.length = mb.size.x * Scale;

        // the seat: the throne on the floor, the King in it; built facing +z, then turned to face the nave
        var seat = new GameObject("Seat").transform;
        seat.SetParent(root.transform, false);
        var throne = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Throne), seat);
        throne.name = "Demon king throne";
        throne.transform.localScale = Vector3.one * ThroneScale;
        throne.transform.localPosition = new Vector3(0f, .95f * ThroneScale, 0f);   // (its bounds are centred: 1.9 tall)
        foreach (var r in throne.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterials = Enumerable.Repeat(iron, r.sharedMaterials.Length).ToArray(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }

        MeshyCastBuilder.Loop(KingSit, true);
        var clip = MeshyCastBuilder.Clip(KingSit);
        var ctrl = MeshyCastBuilder.Controller("DemonKingSeated", ("Idle", clip));
        var king = MeshyCastBuilder.Man(KingSit, "Demon King (seated)", seat, 1f, black, ctrl);
        king.transform.localScale = Vector3.one * KingScale;
        foreach (var r in king.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        clip.SampleAnimation(king, 0f);
        var bones = king.GetComponentsInChildren<Transform>(true);
        Transform hips = bones.First(t => t.name == "mixamorig:Hips"), head = bones.First(t => t.name == "mixamorig:Head");
        Vector3 hipsAt = new Vector3(0f, ThroneSeatY * ThroneScale + KingHipsAboveSeat * KingScale, 0f);
        king.transform.position += seat.TransformPoint(hipsAt) - hips.position;   // (the seat is still at the origin, unturned)
        dh.kingHead = head;

        seat.localRotation = Quaternion.Euler(0f, 180f, 0f);
        seat.localPosition = new Vector3(0f, 0f, dh.armLength - SeatFromEnd);
        dh.seat = seat;
        var back = new GameObject("King back (glow)").transform;
        back.SetParent(root.transform, false);
        back.localPosition = new Vector3(0f, 6f, dh.armLength + 2.5f);
        dh.kingBack = back;
        dh.width = 5.5f;
        seat.gameObject.SetActive(false);

        // the stalkers: ten ordinary people from the PSX character pack, each a pure black silhouette (a shader without fog)
        var flat = AssetDatabase.LoadAssetAtPath<Material>(Out + "DreamPerson.mat");
        var flatShader = Shader.Find("HellScape/DreamSilhouette");
        if (flat == null) { flat = new Material(flatShader); AssetDatabase.CreateAsset(flat, Out + "DreamPerson.mat"); }
        flat.shader = flatShader; flat.color = Color.black;
        EditorUtility.SetDirty(flat);
        var eyes = AssetDatabase.LoadAssetAtPath<Material>(Out + "DreamEyes.mat");
        if (eyes == null) { eyes = new Material(flatShader); AssetDatabase.CreateAsset(eyes, Out + "DreamEyes.mat"); }
        eyes.shader = flatShader; eyes.SetColor("_Color", new Color(5f, .12f, .06f, 1f));
        EditorUtility.SetDirty(eyes);
        var walk = Resources.Load<RuntimeAnimatorController>("OfficeWalk");
        var rng = new System.Random(1304);
        var cast = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/characters-psx" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => (p.Contains("/Male/") || p.Contains("/Female/")) && !p.Contains("Police") && !p.Contains("Firefighter") && !p.Contains("Doctor") && !p.Contains("_HM"))
            .OrderBy(p => p).OrderBy(_ => rng.Next()).Take(10).ToList();
        var crowd = new GameObject("People").transform;
        crowd.SetParent(root.transform, false);
        var people = new System.Collections.Generic.List<GameObject>();
        foreach (var file in cast)
        {
            var person = new GameObject("Person " + System.IO.Path.GetFileNameWithoutExtension(file)).transform;
            person.SetParent(crowd, false);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(file), person);
            foreach (var r in body.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterials = Enumerable.Repeat(flat, r.sharedMaterials.Length).ToArray(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            var anim = body.GetComponent<Animator>() ?? body.AddComponent<Animator>();
            anim.runtimeAnimatorController = walk; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var b = MeshyCastBuilder.Bounds(body);
            body.transform.localScale *= (1.62f + (float)rng.NextDouble() * .3f) / Mathf.Max(.01f, b.size.y);   // 1.6 - 1.9 m
            b = MeshyCastBuilder.Bounds(body);
            body.transform.position += Vector3.up * (person.position.y - b.min.y) + new Vector3(person.position.x - b.center.x, 0f, person.position.z - b.center.z);   // feet on the origin
            // red eyes (glowing: the bloom makes them burn through the mist)
            var headBone = anim.GetBoneTransform(HumanBodyBones.Head) ?? body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLower().EndsWith("head"));
            if (headBone != null)
                foreach (float side in new[] { -1f, 1f })
                {
                    var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(eye.GetComponent<Collider>());
                    eye.name = "Red eye";
                    eye.GetComponent<Renderer>().sharedMaterial = eyes; eye.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    eye.transform.position = headBone.position + Vector3.up * .1f + body.transform.forward * .1f + body.transform.right * side * .032f;
                    eye.transform.localScale = Vector3.one * .035f;
                    eye.transform.SetParent(headBone, true);
                }
            person.gameObject.SetActive(false);
            people.Add(person.gameObject);
        }
        dh.people = people.ToArray();

        string path = "Assets/Resources/DreamHall.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return "Dream hall built: " + path + " (bay " + bay.ToString("F2") + " m, nave " + dh.armLength.ToString("F1") + " m). Scale " + Scale + ", model bounds " + mb;
    }
}
