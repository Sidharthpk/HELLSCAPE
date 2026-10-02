using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Swap Car For Van: the Kessler & Vane delivery van (the user's PixelDucatoJacex, placed in the scene)
// becomes the drivable car. The old car object (Prometheus) keeps its controller, physics, CarInteract, CarHealth,
// CarZombieHit, camera and every story hook pointing at it; only its looks are swapped:
//   - the old body and wheel meshes are switched off, the van is parented in their place (where the user put it)
//   - each van wheel gets an axle-aligned pivot the controller spins/steers (the rig's wheel meshes sit at odd
//     local rotations, so they can't take the WheelCollider's pose directly)
//   - wheel colliders, skid/smoke effects, the body box collider and centre of mass fitted to the van
//   - a van's handling (heavier, lower top speed) and a higher, further chase camera
public static class VanBuilder
{
    static readonly string[] WheelNames = { "Wheel.Ft.L", "Wheel.Ft.R", "Wheel.Bk.L", "Wheel.Bk.R" };

    [MenuItem("HellScape/Swap Car For Van")]
    public static void Swap()
    {
        var car = GameObject.Find("Prometheus");
        var van = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "PixelDucatoJacex");
        if (car == null || van == null) { Debug.LogWarning("Need both Prometheus and PixelDucatoJacex in the scene."); return; }
        var pc = car.GetComponent<PrometeoCarController>();
        var rb = car.GetComponent<Rigidbody>();
        Undo.RegisterFullObjectHierarchyUndo(car, "Van");
        Undo.RegisterFullObjectHierarchyUndo(van.gameObject, "Van");
        // the wheels get re-parented onto pivots: not possible inside an FBX prefab instance
        if (PrefabUtility.IsPartOfPrefabInstance(van.gameObject))
            PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(van.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // the van's wheels (mesh renderers under the rig's DEF bones)
        // (on a re-run they already sit under the car's wheel pivots, not inside the van)
        var wheels = WheelNames.Select(n => van.GetComponentsInChildren<Transform>(true).Concat(car.GetComponentsInChildren<Transform>(true))
                                              .First(t => t.name == n && t.GetComponent<Renderer>() != null)).ToArray();
        Vector3 Centre(Transform w) => w.GetComponent<Renderer>().bounds.center;
        float radius = wheels.Average(w => w.GetComponent<Renderer>().bounds.extents.y);

        // put the car where the van stands: between its wheels, on the ground, facing the way the van faces
        if (van.parent != car.transform)
        {
            Vector3 front = (Centre(wheels[0]) + Centre(wheels[1])) * 0.5f, back = (Centre(wheels[2]) + Centre(wheels[3])) * 0.5f;
            Vector3 fwd = front - back; fwd.y = 0f;
            Vector3 mid = (front + back) * 0.5f;
            Vector3 vanPos = van.position; Quaternion vanRot = van.rotation;
            car.transform.SetPositionAndRotation(new Vector3(mid.x, mid.y - radius, mid.z), Quaternion.LookRotation(fwd.normalized));
            van.SetPositionAndRotation(vanPos, vanRot);   // (not a child yet: stays exactly where the user put it)
            van.SetParent(car.transform, true);
        }
        van.name = "PixelDucatoJacex";
        var anim = van.GetComponent<Animator>();
        if (anim != null) Object.DestroyImmediate(anim);   // the rig demo clip would fight the wheels

        // old looks off (Body carries the old mesh collider too)
        var body = car.transform.Find("Body"); if (body != null) body.gameObject.SetActive(false);
        var oldMeshes = car.transform.Find("Wheels/Meshes"); if (oldMeshes != null) oldMeshes.gameObject.SetActive(false);

        // an axle-aligned pivot per wheel; the controller drives these
        var pivots = car.transform.Find("Wheels/Van wheel pivots");
        if (pivots == null) { pivots = new GameObject("Van wheel pivots").transform; pivots.SetParent(car.transform.Find("Wheels"), false); }
        var cols = new[] { pc.frontLeftCollider, pc.frontRightCollider, pc.rearLeftCollider, pc.rearRightCollider };
        var pivotT = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var p = pivots.Find(WheelNames[i]);
            if (p == null) { p = new GameObject(WheelNames[i]).transform; p.SetParent(pivots, false); }
            p.SetPositionAndRotation(Centre(wheels[i]), car.transform.rotation);
            wheels[i].SetParent(p, true);
            pivotT[i] = p;
            // the wheel collider sits where the wheel is (same rest pose as the old car: centre +0.15, travel 0.3)
            cols[i].transform.localPosition = car.transform.InverseTransformPoint(p.position);
            cols[i].radius = radius;
        }
        pc.frontLeftMesh = pivotT[0].gameObject; pc.frontRightMesh = pivotT[1].gameObject;
        pc.rearLeftMesh = pivotT[2].gameObject; pc.rearRightMesh = pivotT[3].gameObject;

        // skid marks and tyre smoke at the rear wheels
        var fx = car.transform.Find("Effects");
        if (fx != null)
            foreach (Transform e in fx)
            {
                var w = e.name.StartsWith("Left") ? pivotT[2] : pivotT[3];
                Vector3 l = car.transform.InverseTransformPoint(w.position);
                e.localPosition = new Vector3(l.x, e.localPosition.y, l.z);
            }

        // body collider: the van's shell, measured unrotated, lifted clear of the road
        Quaternion rot = car.transform.rotation;
        car.transform.rotation = Quaternion.identity;
        var shell = van.GetComponentsInChildren<Renderer>(true).Where(r => !WheelNames.Contains(r.name) && r.name != "Mirror")
                       .Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        Vector3 min = car.transform.InverseTransformPoint(shell.min), max = car.transform.InverseTransformPoint(shell.max);
        car.transform.rotation = rot;
        min.y = Mathf.Max(min.y, radius * 1.1f);
        Vector3 shellCentre = (min + max) * 0.5f, shellSize = new Vector3(max.x - min.x, max.y - min.y, max.z - min.z);
        // the solid body (the old car's was the mesh collider under Body, now switched off with it)
        var solidT = car.transform.Find("Van body collider");
        if (solidT == null) { solidT = new GameObject("Van body collider").transform; solidT.SetParent(car.transform, false); }
        var solid = solidT.GetComponent<BoxCollider>();
        if (solid == null) solid = solidT.gameObject.AddComponent<BoxCollider>();
        solid.center = shellCentre;
        solid.size = shellSize;
        // the root box is CarInteract's get-in trigger: the van plus a step all round
        var box = car.GetComponent<BoxCollider>();
        box.center = shellCentre;
        box.size = shellSize + new Vector3(2.4f, 0.4f, 2.4f);

        // a van: heavier, a bit slower, lower centre of mass so it doesn't roll in the corners
        rb.mass = 1700f;
        pc.bodyMassCenter = new Vector3(0f, 0.45f, 0.1f);
        pc.maxSpeed = 150;
        pc.accelerationMultiplier = 8;
        VanHandling.Apply(pc);   // brakes, tyre grip, steering lock

        // exit on the driver's side, just clear of the van
        var exit = car.transform.Find("Exit");
        if (exit != null) exit.localPosition = new Vector3(min.x - 1.1f, 1.1f, max.z - 1.8f);

        // the chase camera higher and further back: the van is 2.4 m tall
        var ci = car.GetComponent<CarInteract>();
        var follow = ci != null && ci.carCamera != null ? ci.carCamera.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            Undo.RecordObject(follow, "Van cam");
            follow.baseOffset = new Vector3(0f, 2.9f, -7.2f);
            follow.lookHeightOffset = 1.5f;
            EditorUtility.SetDirty(follow);
        }

        EditorUtility.SetDirty(pc); EditorUtility.SetDirty(rb); EditorUtility.SetDirty(box); EditorUtility.SetDirty(solid);
        EditorSceneManager.MarkSceneDirty(car.scene);
        Debug.Log($"Van swapped in: wheel radius {radius:0.00}, body box {box.size}.");
    }
}
