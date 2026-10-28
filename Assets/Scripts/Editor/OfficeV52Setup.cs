using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit migration of OfficeSilo only. No level builder, prefab or project setting is touched.
public static class OfficeV52Setup
{
    [MenuItem("HellScape/Office/Apply V5.2 progression")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "OfficeSilo") throw new InvalidOperationException("Open OfficeSilo in Edit Mode.");
        if (RevisedPlot.All<OfficeV52Sequence>(scene).Length > 0) { Debug.Log("Office V5.2 already configured."); return; }
        var office = GameObject.Find("OfficeAct").transform.Find("Office");
        var root = new GameObject("Office V5.2 encounters");
        Undo.RegisterCreatedObjectUndo(root, "Office V5.2 progression");
        root.transform.SetParent(office, false);
        var seq = root.AddComponent<OfficeV52Sequence>();
        seq.player = RevisedPlot.All<FirstPersonController>(scene).First().transform;
        seq.fader = RevisedPlot.All<ScreenFader>(scene).First();
        seq.legacyEncounter = RevisedPlot.All<KesslerConfrontation>(scene).First();
        if (seq.legacyEncounter.damned != null && seq.legacyEncounter.damnedAnim != null)
        {
            Undo.RecordObject(seq.legacyEncounter.damned, "Office V5.2 active form animator");
            seq.legacyEncounter.damned.animator = seq.legacyEncounter.damnedAnim;
        }
        seq.cctv = RevisedPlot.All<CctvDesk>(scene).First();
        Undo.RecordObject(seq.cctv, "Office V5.2 CCTV");
        seq.cctv.officeSequence = seq;
        var film = seq.cctv.fireTape != null ? seq.cctv.fireTape.GetComponent<FireTape>() : null;
        if (film != null)
        {
            Undo.RecordObject(film, "Office V5.2 recording");
            film.personalWeaponLines = "A3V52_weapon_order";
            film.fireLines = "A3V52_fire_order"; film.executionLines = "A3V52_execution";
        }
        seq.officeDoor = RevisedPlot.All<KesslerDoor>(scene).OrderBy(d => Vector3.Distance(d.transform.position, new Vector3(1003.1f, 3.4f, 888.58f))).First();
        Undo.RecordObject(seq.officeDoor, "Office V5.2 main door");
        seq.officeDoor.echoOnly = true;
        foreach (var it in seq.officeDoor.GetComponentsInChildren<Interactable>(true))
            Gate(it, seq, OfficeProgressGate.Rule.KesslerDoor, "I need to finish helping the workers.");
        seq.concealedDoor = office.Find("Armory door").GetComponent<Interactable>();
        Undo.RecordObject(seq.concealedDoor, "Office V5.2 concealed entrance");
        // Replace only the obsolete instant-hide callback with the completed-pry action.
        for (int i = seq.concealedDoor.onUse.GetPersistentEventCount() - 1; i >= 0; i--)
            if (seq.concealedDoor.onUse.GetPersistentTarget(i) == seq.concealedDoor.gameObject
                && seq.concealedDoor.onUse.GetPersistentMethodName(i) == "SetActive")
                UnityEventTools.RemovePersistentListener(seq.concealedDoor.onUse, i);
        seq.concealedDoor.once = false; seq.concealedDoor.line = "";
        seq.concealedDoor.requiredItem = "Crowbar"; seq.concealedDoor.consumeItem = false;
        UnityEventTools.AddPersistentListener(seq.concealedDoor.onUse, seq.Pry);
        Gate(seq.concealedDoor, seq, OfficeProgressGate.Rule.Armory, "I should check the security maintenance records before opening this.");

        seq.redPose = Marker(root.transform, "Vane red silhouette", new Vector3(1005.1f, 3.4f, 906.4f));
        seq.transformationPose = Marker(root.transform, "Vane confrontation", new Vector3(998.3f, 3.4f, 890f));
        seq.breachPose = Marker(root.transform, "Vane at Kessler door", new Vector3(1001.8f, 3.4f, 888.58f));
        seq.alternateExit = Area(root.transform, "Security alternate exit", new Vector3(990f, 4.5f, 906.4f), new Vector3(1.5f, 3f, 1.5f));
        seq.confrontationArea = Area(root.transform, "Worker confrontation area", new Vector3(998f, 4.5f, 895f), new Vector3(4f, 3f, 3f));
        seq.officeInterior = Area(root.transform, "Kessler interior", new Vector3(1007.8f, 4.5f, 887.2f), new Vector3(7.5f, 3f, 6.8f));
        var blocked = Area(root.transform, "Vane blocked security exit", new Vector3(1003.7f, 4.9f, 906.4f), new Vector3(.3f, 3f, 1.2f));
        blocked.isTrigger = false; blocked.enabled = false; seq.blockedExit = blocked;
        var red = new GameObject("Vane red backlight").AddComponent<Light>();
        red.transform.SetParent(root.transform, false); red.transform.position = new Vector3(1006f, 5.2f, 906.4f);
        red.type = LightType.Point; red.color = Color.red; red.range = 5f; red.intensity = 7f; red.enabled = false;
        seq.redBacklight = red;
        seq.doorAudio = root.AddComponent<AudioSource>(); seq.doorAudio.playOnAwake = false;
        seq.doorAudio.spatialBlend = 1f;

        var safes = RevisedPlot.All<Renderer>(scene).Where(r => r.name.StartsWith("Secure safe ")).OrderBy(r => r.name).ToArray();
        if (safes.Length != 3) throw new InvalidOperationException("Expected the three existing secure-storage safes.");
        for (int i = 0; i < 2; i++)
        {
            var use = safes[i].gameObject.GetOrAdd<Interactable>(); use.once = false; use.prompt = "Open locker 0" + (i + 1);
            var note = safes[i].gameObject.GetOrAdd<ReadableNote>();
            note.title = i == 0 ? "EXECUTIVE STORAGE 01" : "KESSLER'S PERSONAL EFFECTS";
            note.body = i == 0 ? "Security duplicates. Witness statements were retained in executive storage." : "A personal holster. A return label: A. KESSLER. The pistol was kept here.";
        }
        Bounds b = safes[2].bounds;
        seq.lockerLid = safes[2].gameObject;
        var panel = Box(root.transform, "Locker 03 rotary lock", b.center + new Vector3(0f, 0f, b.extents.z + .025f), new Vector3(b.size.x, b.size.y, .035f), safes[2].sharedMaterial);
        seq.locker = panel.AddComponent<VaultDial>(); seq.locker.code = new[] { 18, 7, 31 };
        seq.locker.requireDirections = true; seq.locker.directions = new[] { 1, -1, 1 };
        seq.locker.lockedText = "Locker 03. [J] step away to review the maintenance note.";
        seq.locker.openText = "The bolts slide back.";
        var dialUse = panel.AddComponent<Interactable>(); dialUse.once = false; dialUse.prompt = "Turn locker 03 dial";
        UnityEventTools.AddPersistentListener(dialUse.onUse, seq.locker.Use);
        Gate(dialUse, seq, OfficeProgressGate.Rule.Locker, "Open the concealed armory entrance first.");
        seq.lockerContents = new GameObject("Locker 03 contents"); seq.lockerContents.transform.SetParent(root.transform, false);
        // The original one-piece safe is the closed model. A shallow interior remains when it opens.
        Box(root.transform, "Locker 03 back", b.center + Vector3.back * b.extents.z, new Vector3(b.size.x, b.size.y, .025f), safes[2].sharedMaterial);
        Box(root.transform, "Locker 03 floor", b.center + Vector3.down * b.extents.y, new Vector3(b.size.x, .025f, b.size.z), safes[2].sharedMaterial);
        foreach (int side in new[] { -1, 1 })
            Box(root.transform, "Locker 03 side " + side, b.center + Vector3.right * b.extents.x * side, new Vector3(.025f, b.size.y, b.size.z), safes[2].sharedMaterial);
        seq.pistol = office.Find("Armory pistol").GetComponent<Interactable>();
        Undo.RecordObject(seq.pistol.transform, "Office V5.2 pistol placement");
        seq.pistol.transform.SetParent(seq.lockerContents.transform, true);
        seq.pistol.transform.position = b.center + new Vector3(0f, .12f, .05f);
        Gate(seq.pistol, seq, OfficeProgressGate.Rule.Pistol, "Locker 03 is locked.");
        seq.keys = seq.legacyEncounter.keyPickup.GetComponent<Interactable>();
        Undo.RecordObject(seq.keys.transform, "Office V5.2 key placement");
        seq.keys.transform.SetParent(seq.lockerContents.transform, true);
        seq.keys.transform.position = b.center + new Vector3(0f, -.15f, .12f);
        seq.keys.prompt = "Take the Silo Two keys"; seq.keys.line = ""; seq.keys.gameObject.SetActive(true);
        Gate(seq.keys, seq, OfficeProgressGate.Rule.Keys, "Locker 03 is locked.");
        var ammunition = Box(seq.lockerContents.transform, "Spare pistol magazine", b.center + new Vector3(.18f, -.12f, .10f), new Vector3(.07f, .12f, .04f), safes[2].sharedMaterial);
        var ammo = ammunition.AddComponent<Interactable>(); ammo.prompt = "Take spare ammunition"; ammo.hideOnUse = true;
        var gun = RevisedPlot.All<GunController>(scene).First();
        UnityEventTools.AddPersistentListener(ammo.onUse, gun.AddMag);
        seq.lockerContents.SetActive(false);

        var maintenance = Box(root.transform, "Executive storage maintenance note", new Vector3(1002.8f, 4.05f, 902.5f), new Vector3(.24f, .015f, .32f), safes[0].sharedMaterial);
        var read = maintenance.AddComponent<ReadableNote>(); read.title = "SECURITY MAINTENANCE";
        read.body = "Executive storage. Locker 03. Rotary mechanism serviced. Sequence L18, R07, L31.";
        var readUse = maintenance.AddComponent<Interactable>(); readUse.once = false; readUse.prompt = "Read security maintenance note";
        UnityEventTools.AddPersistentListener(readUse.onUse, seq.DiscoverCombination);
        seq.silo = office.Find("SiloDoor").GetComponent<Teleporter>();
        Gate(office.Find("ExitDoor").GetComponent<Interactable>(), seq, OfficeProgressGate.Rule.Street,
            "Vane is between me and the street. I have to take the silo route.");
        var siloUse = seq.silo.GetComponent<Interactable>();
        Gate(siloUse, seq, OfficeProgressGate.Rule.Silo, "I need Kessler's pistol and the Silo Two keys from the armory.");
        siloUse.lockedLine = "SILO TWO. Kessler's access keys are in executive storage.";
        var checkpoints = GameObject.Find("OfficeAct").transform.Find("Checkpoints");
        var checkpoint = new GameObject("Checkpoint - Office armory"); checkpoint.transform.SetParent(checkpoints, false);
        seq.armoryCheckpoint = checkpoint.AddComponent<Checkpoint>();
        seq.armoryCheckpoint.title = "Kessler's armory";
        seq.armoryCheckpoint.spawn = Marker(root.transform, "Armory retry position", new Vector3(1007.5f, 4.25f, 885f));
        seq.armoryCheckpoint.player = seq.player; seq.armoryCheckpoint.fader = seq.fader;
        seq.armoryCheckpoint.clearZombiesRadius = 0f; seq.armoryCheckpoint.restorePoses = Array.Empty<Transform>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Office V5.2 configured. Run OfficeV52Validation for reference and play-mode checks.");
    }
    static void Gate(Interactable item, OfficeV52Sequence seq, OfficeProgressGate.Rule rule, string text)
    {
        var gate = item.gameObject.GetOrAdd<OfficeProgressGate>(); gate.sequence = seq; gate.rule = rule; gate.unavailable = text;
    }
    static Transform Marker(Transform parent, string name, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position; return go.transform;
    }
    static BoxCollider Area(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var c = Marker(parent, name, pos).gameObject.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = size; return c;
    }
    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = pos; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat; return go;
    }
}
