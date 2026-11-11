using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Office: Meshy Colleagues
// Priya, Ben, Nadia and Omar (the dead office workers, "Ghost - <name>" in OfficeSilo) get the user's Meshy models
// (Assets/Perishables/Avatar), standing still in their idle clip; Ben sits on the pantry floor, Priya's idle is her
// sitting one. Same rig and bone paths per character, so a model plays the clips of its other files (Generic rigs).
// The old humanoid body stays on the ghost, inactive, as "Body template": OfficeBodies / the Auditor copy it.
// GhostNPC.skin is the model's own material, which WitnessLook soots (burnt) instead of the old Burnt materials.
public static class ColleagueSwap
{
    const string Dir = "Assets/Perishables/Avatar/";
    const string Suffix = " (Meshy)";

    // name, the idle FBX, a standing clip of the same character (to size the sitters), height
    static readonly (string who, string idle, string stand, float height)[] Cast =
    {
        ("Priya", "priya_idle Sit/Meshy_AI_priya_ps2_refined_rig_biped/Meshy_AI_priya_ps2_refined_rig_biped_Animation_Sit_Cross_Legged_withSkin.fbx",
                  "Priya_walk/Meshy_AI_priya_ps2_refined_rig_biped/Meshy_AI_priya_ps2_refined_rig_biped_Animation_Walking_Woman_withSkin.fbx", 1.66f),
        ("Ben",   "Ben_sit idle/Meshy_AI_Ben_PS2_Rigged_biped/Meshy_AI_Ben_PS2_Rigged_biped_Animation_Sit_Cross_Legged_on_Floor_withSkin.fbx",
                  "Ben_Idle Stance/Meshy_AI_Ben_PS2_Rigged_biped/Meshy_AI_Ben_PS2_Rigged_biped_Animation_Long_Breathe_and_Look_Around_withSkin.fbx", 1.78f),
        ("Nadia", "Nadia_Idle/Meshy_AI_Nadia_PS2_Rigged_biped/Meshy_AI_Nadia_PS2_Rigged_biped_Animation_Idle_7_withSkin.fbx", null, 1.68f),
        ("Omar",  "Omar_Idle/Meshy_AI_Omar_PS2_Rigged_biped/Meshy_AI_Omar_PS2_Rigged_biped_Animation_Idle_11_withSkin.fbx", null, 1.76f),
    };

    [MenuItem("HellScape/Office: Meshy Colleagues")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var scene = SceneManager.GetSceneByName("OfficeSilo");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/OfficeSilo.unity", OpenSceneMode.Additive);
        var ghosts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GhostNPC>(true)).ToList();
        var log = new System.Text.StringBuilder();
        foreach (var (who, idle, stand, height) in Cast)
        {
            var g = ghosts.FirstOrDefault(x => x.ghostName == who);
            if (g == null) { log.Append(who + ": no ghost; "); continue; }
            string pose = Dir + idle, folder = System.IO.Path.GetDirectoryName(pose).Replace('\\', '/') + "/";
            string tex = System.IO.Directory.GetFiles(folder, "*_texture_0.png").First().Replace('\\', '/');
            MeshyCastBuilder.Loop(pose, true);

            // the body that is there now: the old humanoid one the first time, our own on a re-run
            var mine = g.transform.Find(who + Suffix);
            if (mine != null) Object.DestroyImmediate(mine.gameObject);
            var old = g.transform.Find("Body template") ?? g.body;
            if (old == null) { log.Append(who + ": no body; "); continue; }
            var ghostMat = old.GetComponentInChildren<Renderer>(true).sharedMaterial;
            var oldLocalPos = old.localPosition; var oldLocalRot = old.localRotation;
            old.name = "Body template";
            old.gameObject.SetActive(false);

            var skin = MeshyCastBuilder.Mat("Colleague" + who, tex);
            var ctrl = MeshyCastBuilder.Controller("Colleague" + who, ("Idle", MeshyCastBuilder.Clip(pose)));
            if (!ctrl.parameters.Any(p => p.name == "Speed")) ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);   // (GhostNPC sets it every frame)
            EditorUtility.SetDirty(ctrl);
            var man = MeshyCastBuilder.Man(pose, who + Suffix, g.transform, height, skin, ctrl);
            if (stand != null) man.transform.localScale = Vector3.one * (height / Standing(Dir + stand));   // a sitter is sized by the same man standing
            man.transform.localPosition = oldLocalPos; man.transform.localRotation = oldLocalRot;
            // some sit clips put the root above the floor-sitting pelvis: raise him until his lowest foot/hip bone is on the floor
            float low = man.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("Foot") || t.name.EndsWith("ToeBase") || t.name.EndsWith("Hips")).Min(t => t.position.y);
            if (low < g.transform.position.y) man.transform.position += Vector3.up * (g.transform.position.y + .03f - low);
            foreach (var r in man.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(ghostMat, r.sharedMaterials.Length).ToArray();   // the ghost look, until WitnessLook burns it
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Undo.RecordObject(g, "Colleague");
            g.body = man.transform;
            g.animator = man.GetComponent<Animator>();
            g.skin = skin;
            g.driftRadius = 0f;   // idle clip only: they stand (or sit) where they are
            EditorUtility.SetDirty(g);
            log.Append(who + " ok; ");
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Meshy colleagues: " + log;
    }

    // the character's height in its first frame at scale 1
    static float Standing(string fbx)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        MeshyCastBuilder.Clip(fbx).SampleAnimation(g, 0f);
        float h = MeshyCastBuilder.Bounds(g).size.y;
        Object.DestroyImmediate(g);
        return h;
    }
}
