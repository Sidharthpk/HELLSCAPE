using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Prologue > Build Extra Deliveries: two ordinary parcel drops, one by fence_part51 and one by fence_part13, before the KEEP COLD
// box to Harlow Clinic (the last delivery). Each is a bay in the south lane and a customer (a copy of a street pedestrian)
// waiting on the south pavement, like the clinic's, who carries a cardboard box off (no door); they go first in DeliveryRun.stops. The lines are plain strings here
// (RevisedPlot.ApplyText finds the Harlow and cafe stops by their customer, so the order doesn't matter to it).
public static class ExtraDeliveries
{
    [MenuItem("HellScape/Prologue/Build Extra Deliveries")]
    public static void Build()
    {
        var sc = EditorSceneManager.GetSceneByName("Prologue");
        if (!sc.isLoaded) sc = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);
        var run = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DeliveryRun>(true)).FirstOrDefault();
        if (run == null) { Debug.LogError("Extra deliveries: no DeliveryRun in Prologue"); return; }
        // rebuilt from scratch each time (the old drops are dropped from the run first)
        foreach (var old in run.stops.Where(s => s.customer != null && s.customer.name.StartsWith("Parcel customer"))) { if (old.customer != null) Object.DestroyImmediate(old.customer.gameObject); if (old.bay != null) Object.DestroyImmediate(old.bay.gameObject); }
        run.stops = run.stops.Where(s => s.customer != null).ToArray();

        var oldRoot = run.transform.Find("Extra deliveries"); if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);
        var root = new GameObject("Extra deliveries").transform;
        root.SetParent(run.transform, false);
        var walkers = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PavementWalker>(true)).Select(w => w.gameObject).ToArray();
        Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject.scene == sc && h.normal.y > .85f && !h.collider.isTrigger).OrderByDescending(h => h.point.y).ToArray();
            return hits.Length > 0 ? hits[0].point : p;
        }

        // bay: the south lane of an east-running road; at: where the customer waits on the pavement beside it
        DeliveryRun.Stop Make(string name, Vector3 bayAt, Vector3 at, int pick, string objective, string arrive, string lines)
        {
            var bay = new GameObject(name + " (bay)").transform;
            bay.SetParent(root, false);
            bay.SetPositionAndRotation(Ground(bayAt), Quaternion.LookRotation(Vector3.right));
            var g = Object.Instantiate(walkers[pick % walkers.Length], root);
            g.name = "Parcel customer - " + name;
            foreach (var w in g.GetComponentsInChildren<PavementWalker>(true)) Object.DestroyImmediate(w);
            g.transform.SetPositionAndRotation(Ground(at), Quaternion.LookRotation(Vector3.forward));   // facing the road
            var cap = g.GetComponent<CapsuleCollider>();
            if (cap == null) cap = g.AddComponent<CapsuleCollider>();   // (not ??: Unity objects)
            cap.height = 1.8f; cap.radius = .4f; cap.center = new Vector3(0f, .9f, 0f);
            var it = g.GetComponent<Interactable>();
            if (it == null) it = g.AddComponent<Interactable>();
            it.prompt = "Hand over the parcel"; it.once = true; it.lockedLine = "";
            return new DeliveryRun.Stop { objective = objective, bay = bay, bayRadius = 13f, customer = it, customerAnimator = g.GetComponentInChildren<Animator>(), arriveLine = arrive, handOverLines = lines, carryBox = true, exitSpeed = 1.2f };
        }

        var first = Make("Corner shop", new Vector3(-185f, 7.6f, -179.2f), new Vector3(-184f, 7.6f, -182.6f), 2, "Deliver the parcel - the corner shop", "*First drop. A parcel for the corner shop.*",
            "Shopkeeper: Early again.|Kessler's schedule, not mine.|Shopkeeper: Sign here. Mind the step.");
        var second = Make("Number 31", new Vector3(-208f, 6f, -4.6f), new Vector3(-206.5f, 6f, -9.6f), 3, "Deliver the parcel - No. 31", "*Number 31. Already waiting at the kerb.*",
            "Neighbour: Is that mine? Finally.|It was on the sheet.|Neighbour: Sign here. Long shift?|Since five.");
        run.stops = new[] { first, second }.Concat(run.stops).ToArray();
        EditorUtility.SetDirty(run);
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        Debug.Log("Extra deliveries built: bays at " + first.bay.position + " and " + second.bay.position + ", Prologue saved");
    }
}
