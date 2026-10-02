using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Elias's fight on the silo floor (user, 2026-10-02): he dodges when you shoot at him, charges (miss and he runs into
// the wall and is stunned), throws knives that stick in the wall, and you can pull those out and throw them back to
// stun him. The moves are the user's Mixamo clips in Assets/Enemies/Elias Boss, retargeted to his humanoid rig as
// extra states on Killer.controller that KillerBoss cross-fades to by name; the knife is the model in the same folder.
// HellScape > Silo > Elias Fight Moves. (MainGameScene open; OfficeActBuilder calls AddStates when it rebuilds the controller.)
public static class EliasFightBuilder
{
    const string Dir = "Assets/Enemies/Elias Boss/";
    const string ControllerPath = "Assets/Materials/OfficeAct/Anim/Killer.controller";
    const string KnifeFbx = Dir + "household-knife-low-poly/source/knife_LP.fbx";
    const string KnifeMat = Dir + "household-knife-low-poly/textures/knife.mat";
    const float FightHealth = 1300f;

    // state on the controller, the clip's file, does it loop, how fast it plays (KillerBoss times its moves to these)
    public static readonly (string state, string file, bool loop, float speed)[] Moves =
    {
        ("DodgeLeft", "Dodging left", false, 1.9f),
        ("DodgeRight", "Dodging Right", false, 1.9f),
        ("DodgeBack", "Dodging", false, 2.4f),
        ("Corkscrew", "Corkscrew Evade", false, 2.4f),
        ("RollToRun", "Quick Roll To Run", false, 1.7f),
        ("Charge", "Injured Run", true, 1.3f),
        ("Throw", "Throw Object", false, 1f),
        ("Stunned", "Stunned", false, 1f),       // (he staggers, goes down flat, and stays there)
        ("GetUp", "Standing Up", false, 2.2f),
    };

    [MenuItem("HellScape/Silo/Elias Fight Moves")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ac == null) return "No Killer.controller (run Build Office Act first).";
        AddStates(ac);

        var boss = Object.FindFirstObjectByType<KillerBoss>(FindObjectsInactive.Include);
        if (boss == null) return "Moves added to the controller; no KillerBoss in the open scenes to hand the knife to.";
        boss.knifeModel = AssetDatabase.LoadAssetAtPath<GameObject>(KnifeFbx);
        boss.knifeMaterial = AssetDatabase.LoadAssetAtPath<Material>(KnifeMat);
        // most shots miss now, and a stun is a whole magazine's worth of free hits (30 x 14 = his old 420 health):
        // enough health for three or four stuns
        if (boss.maxHealth < FightHealth) boss.maxHealth = FightHealth;
        // walking, he always dodges (to the side away from the shot); running at you, stunned or throwing, he can be shot
        boss.dodgeChance = 1f;
        boss.dodgeCooldown = 0f;
        boss.chargeDamageTaken = 1f;
        // health orbs: the same glowing pickup as the bridge fight
        var bridge = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if (bridge != null && bridge.healthPickup != null) boss.healthPickup = bridge.healthPickup;
        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        AssetDatabase.SaveAssets();
        return $"Elias: {Moves.Length} moves on Killer.controller, knife {(boss.knifeModel != null ? "wired" : "MISSING")}, health {boss.maxHealth:0}, health orbs {(boss.healthPickup != null ? "wired" : "MISSING")}.";
    }

    public static AnimationClip Clip(string file, string name, bool loop)
    {
        string path = Dir + file + ".fbx";
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) return null;
        var have = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
        if (have == null || mi.animationType != ModelImporterAnimationType.Human || have.isLooping != loop)
        {
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importCameras = false;
            mi.importLights = false;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = name;
                c.loopTime = loop;
                c.lockRootRotation = true;      // he faces where the script points him (a spin shows in the pose)
                c.lockRootHeightY = true;       // rolls and falls go down, and come back up, in the pose
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
            }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
    }

    // the moves as plain states (no transitions: the script cross-fades in and out of them)
    public static void AddStates(AnimatorController ac)
    {
        var sm = ac.layers[0].stateMachine;
        int row = 0;
        foreach (var m in Moves)
        {
            var clip = Clip(m.file, "Elias" + m.state, m.loop);
            if (clip == null) { Debug.LogWarning("Elias move missing: " + m.file); continue; }
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == m.state) ?? sm.AddState(m.state, new Vector3(560f, 60f * row, 0f));
            st.motion = clip;
            st.speed = m.speed;
            row++;
        }
        EditorUtility.SetDirty(ac);
    }
}
