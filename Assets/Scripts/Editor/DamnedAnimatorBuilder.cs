using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// HellScape > Build Damned Animator: the Scary Zombie Pack clips made to play in place, and one controller with
// every state the street zombies use. ZombieAI drives it by state name (CrossFade), the blend trees by "Speed"
// (actual metres per second, so each clip's threshold is the speed its feet really cover).
static class DamnedAnimatorBuilder
{
    const string Dir = "Assets/Enemies/Scary Zombie Pack/";
    const string ControllerPath = Dir + "DamnedController.controller";
    const string Hips = "mixamorig5:Hips";
    const float Scale = 1.16f;              // PrimeZombie's root scale: clip travel x this = world speed

    // travel measured off the hips curves (m per loop / loop length), before scale
    public const float WalkSpeed = 0.35f * Scale, RunSpeed = 3.16f * Scale, CrawlSpeed = 0.45f * Scale, CrawlRunSpeed = 2.3f * Scale;

    [MenuItem("HellScape/Build Damned Animator")]
    static void Build()
    {
        // crawls (and the feeding/grab clips) had their forward travel left in the hips: extract it like the walk
        foreach (var f in new[] { "zombie crawl", "running crawl", "zombie biting", "zombie biting (2)", "zombie neck bite" })
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Dir + f + ".fbx");
            if (imp.motionNodeName == Hips) continue;
            imp.motionNodeName = Hips;
            imp.SaveAndReimport();
        }

        AnimationClip Clip(string file) =>
            AssetDatabase.LoadAllAssetsAtPath(Dir + file + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

        AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);   // kept for old callers (Kessler, the ending)
        var sm = ac.layers[0].stateMachine;

        AnimatorState Tree(string name, Vector3 pos, params (AnimationClip clip, float at)[] kids)
        {
            var st = ac.CreateBlendTreeInController(name, out BlendTree t, 0);
            t.blendParameter = "Speed";
            t.useAutomaticThresholds = false;
            foreach (var (clip, at) in kids) t.AddChild(clip, at);
            var states = sm.states;            // (CreateBlendTreeInController drops it at the origin)
            for (int i = 0; i < states.Length; i++) if (states[i].state == st) states[i].position = pos;
            sm.states = states;
            return st;
        }

        var upright = Tree("Upright", new Vector3(300, 0, 0), (Clip("zombie idle"), 0f), (Clip("zombie walk"), WalkSpeed), (Clip("zombie run"), RunSpeed));
        var crawl = Tree("Crawl", new Vector3(300, 120, 0), (Clip("zombie crawl"), 0f), (Clip("zombie crawl"), CrawlSpeed), (Clip("running crawl"), CrawlRunSpeed));
        sm.defaultState = upright;

        AnimatorState One(string name, AnimationClip clip, Vector3 pos)
        {
            var st = sm.AddState(name, pos);
            st.motion = clip;
            return st;
        }
        One("Feed", Clip("zombie biting (2)"), new Vector3(550, -120, 0));            // kneeling over a body, eating
        One("FeedDown", Clip("zombie biting"), new Vector3(550, -60, 0));             // stoops down onto it, then eats
        One("Scream", Clip("zombie scream"), new Vector3(550, 0, 0));
        var attack = One("Attack", Clip("zombie attack"), new Vector3(550, 60, 0));
        One("NeckBite", Clip("zombie neck bite"), new Vector3(550, 120, 0));
        One("CrawlBite", Clip("zombie biting (2)"), new Vector3(550, 180, 0));

        // the old "Attack" trigger still works for anything that sets it
        var any = sm.AddAnyStateTransition(attack);
        any.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        any.duration = 0.1f;
        var back = attack.AddTransition(upright);
        back.hasExitTime = true;
        back.exitTime = 0.9f;
        back.duration = 0.25f;

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();

        // the street zombies use it
        var prefabPath = Dir + "PrimeZombie.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        root.GetComponentInChildren<Animator>().runtimeAnimatorController = ac;
        var ai = root.GetComponent<ZombieAI>();
        ai.walkClipSpeed = WalkSpeed;
        ai.runClipSpeed = RunSpeed;
        ai.crawlClipSpeed = CrawlSpeed;
        ai.crawlRunClipSpeed = CrawlRunSpeed;
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("HellScape: DamnedController built and set on PrimeZombie.");
    }
}
