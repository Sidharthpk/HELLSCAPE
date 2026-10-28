using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Canon: HellScape Revised Plot.docx (Sid, 3 October 2026).
// Old component/object names remain stable to preserve scene references and gameplay wiring.
// Apply before Start, including scene reloads and scenes produced by the existing editor builders.
public static class RevisedPlot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
        Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Prologue" && scene.name != "MainGameScene" && scene.name != "OfficeSilo") return;
        ReadableSetup.Apply();
        ApplyText(scene);
        if (All<RevisedPlotDirector>(scene).Length == 0)
        {
            var go = new GameObject("Revised Plot Director");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<RevisedPlotDirector>();
        }
    }

    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

    // The component, added if it is not there. (Not `GetComponent<T>() ?? AddComponent<T>()`: in the editor a missing
    // component comes back as a placeholder that is not a C# null, so `??` keeps it and the next line throws.)
    public static T GetOrAdd<T>(this GameObject go) where T : Component => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
    public static T GetOrAdd<T>(this Component on) where T : Component => on.gameObject.GetOrAdd<T>();

    // Used for old serialized dialogue and UnityEvent string arguments, not IDs, paths or audio keys.
    public static string Migrate(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value
            .Replace("Kessler & Vane", "{COMPANY}")
            .Replace("KESSLER & VANE", "{COMPANY_UPPER}")
            .Replace("Arthur Kessler", "Vane").Replace("Kessler", "Vane").Replace("KESSLER", "VANE")
            .Replace("{COMPANY}", "Kessler & Vane").Replace("{COMPANY_UPPER}", "KESSLER & VANE");
    }

    static bool Narrative(FieldInfo f) => f.FieldType == typeof(string) &&
        (f.Name.IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0 ||
         new[] { "text", "reply", "prompt", "objective", "depotObjective", "displayName", "ghostName", "title", "subtitle", "body", "footer", "scrawl", "dayNote" }.Contains(f.Name));

    static void MigrateFields(object obj, int depth = 0)
    {
        if (obj == null || depth > 3) return;
        foreach (var f in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Narrative(f)) f.SetValue(obj, Migrate((string)f.GetValue(obj)));
            else if (f.FieldType == typeof(GhostNode[]) || f.FieldType == typeof(GhostChoice[]) || f.FieldType == typeof(DeliveryRun.Stop[]))
                foreach (var child in (IEnumerable)(f.GetValue(obj) ?? Array.CreateInstance(f.FieldType.GetElementType(), 0))) MigrateFields(child, depth + 1);
        }
    }

    public static void ApplyText(Scene scene)
    {
        foreach (var c in All<MonoBehaviour>(scene))
            if (c != null && c.GetType().Assembly == typeof(RevisedPlot).Assembly) MigrateFields(c);
        foreach (var t in All<TextMeshProUGUI>(scene)) t.text = Migrate(t.text);

        // Every line below is a row of Resources/HellscapeDialogue.csv (HS.S). The few plain strings left are things
        // the script has no row for and the player needs: time cards, key prompts, what a cutscene shows.
        // Story V4 (.novel-os/novel/hellscape-story-v4.md): one drop (the KEEP COLD box to Harlow), Maya and Mercer at
        // the depot, Derek's call, the flat, Mercer in the bedroom, and you die in the living room (no roof).
        foreach (var d in All<DeliveryRun>(scene))
        {
            d.startLines = "";
            d.clockOutCard = "7:00 AM";
            d.depotLines = "";
            // (stops are found by their customer: two ordinary parcels come first - HellScape > Prologue > Build Extra Deliveries -
            // their own lines are set there; then Harlow, the last delivery; then the cafe, Derek's coffee)
            foreach (var s in d.stops)
            {
                string who = s.customer != null ? s.customer.name : "";
                if (who.Contains("Harlow"))
                {
                    s.objective = "Deliver the KEEP COLD box - Harlow Clinic";
                    s.arriveLine = "*6:40 AM. Harlow Clinic, front door.*";
                    s.handOverLines = HS5.S("P03_clinic_signed") + "|*Your phone rings: DEREK.*|" + HS5.S("P03_derek_call");
                    s.customer.prompt = "Hand over the KEEP COLD box";
                }
                else if (who.Contains("Cafe"))
                {
                    s.objective = "Pick up Derek's coffee - the cafe";
                    s.arriveLine = "";
                    s.handOverLines = HS5.S("P03_cafe_pickup");
                    s.customer.prompt = "Collect Derek's order";
                }
            }
        }
        foreach (var d in All<PrologueDirector>(scene))
        {
            d.timeCard = "SATURDAY - 6:00 AM";
            d.arriveLines = "*7:00 AM.*";
            d.homeLines = "Flat 2B. Second floor.";
            d.doorOpenLines = "";
            d.callOut = HS.S("45");
            d.noAnswer = "";
            d.settleLines = "";
            d.newsLines = HS.S("47-49");
            d.suspicionLines = "";
            d.noiseLines = HS.S("50");
            d.witnessLine = HS.S("51-52");
            d.caughtLine = HS.S("53-55");
            d.bathroomLine = HS.S("46");
            d.mugLine = HS.S("63");
            d.photoLine = HS.S("44");
            d.chainLine = HS.S("56");
            d.deathLines = HS.S("57-62");
            d.bedroomObjective = "Check the noise - the bedroom";
            d.runObjective = "RUN - the front door!";
        }
        foreach (var s in All<SleepSequence>(scene)) s.fallWakeLine = HS5.Id(78);
        foreach (var car in All<CarInteract>(scene)) car.noKeysLine = HS.S("146");

        foreach (var g in All<GhostNPC>(scene)) ConfigureGhost(g);
        foreach (var n in All<ReadableNote>(scene)) ConfigureNote(n);
        foreach (var c in All<KesslerConfrontation>(scene))
        {
            // V5.1 A3A: he accuses you (Mercer told him you talked), sees your hands, and comes apart
            var talk = HS5.S("A3A_a3a_start").Split('|');
            c.confrontNodes = new[] { N(talk[talk.Length - 1]) };
            c.scareLine = string.Join("|", talk, 0, talk.Length - 1);
            c.turnLine = "*Something tears open under his skin.*";
            c.keyLine = "*The city is taking him. A key falls from his coat: the silo.*|*[Q] raises your hands. Flash him when he gets close.*";
            c.afterTurnLine = "*Run.*";
            c.defeatedLine = HS5.S("A3A_flash_tutorial");
        }
        foreach (var c in All<KesslerChase>(scene))
        {
            c.knockLine = HS5.S("A04_a04_start");
            c.screamLine = HS.S("396");
            c.runLine = HS.S("397");
            c.hitLines = HS.S("398-399");
            c.tomasLine = HS.S("389");
            c.tomasHoldLine = HS.S("390-393");
            c.tomasLostLine = HS.S("394");
            c.insideLine = HS5.S("A04_blast_gate");
            c.quietLine = HS.S("401-402");
        }
        foreach (var k in All<KillerCutscenes>(scene))
        {
            k.scareLine = HS.S("403");
            k.getUpLine = HS.S("404-408");
            k.ambushWarn = "";
            k.ambushLine = HS5.S("A04_vane_taunt");   // V5.1 (was: Derek begged)
        }
        foreach (var e in All<Elevator>(scene)) { e.notInsideLine = HS.S("409"); e.openLine = HS.S("410-411"); }
        foreach (var s in All<SiloEncounter>(scene))
        {
            s.pushLine = HS.S("403");
            s.leapLine = HS.S("412,416");
            s.landLine = HS.S("417-418");
            s.rifleLine = HS.S("419");
            s.jumpLine = HS5.S("A05_boss_confront");   // V5.1: Vane in the silo, "I moved paperwork"
            s.fightLine = HS.S("422");
            s.gunLine = HS.S("423");
            s.defeatedLine = HS.S("428-429");
            s.keysLine = HS5.S("A05_boss_defeat");
            s.kingLine = HS5.S("A06_flood_king") + "|" + HS5.S("A06_flood_voices") + "|" + HS5.S("A06_van_found");
            s.floodLine = HS5.S("A06_a06_start");
        }
        // Vane in the silo (the KillerBoss body): his barks during the fight
        foreach (var k in All<KillerBoss>(scene))
        {
            k.halfLine = HS.S("424-425");
            k.staggerLine = HS.S("426");
            k.rushLine = HS.S("427");
        }
        foreach (var f in All<BloodFlood>(scene))
        {
            // nobody to pull up any more: only you, and the voices of the dead in the blood
            f.firstLine = HS.S("462");
            f.clingLine = f.clingLineSam = HS.S("463-465");
            f.lowLine = f.lowLineSam = HS.S("466-472");
            f.mayaDrownLine = f.samDrownLine = f.lostLine = f.damnedLine = "";
        }
        foreach (var t in All<TurningCutscene>(scene)) { t.damnedSpeech = t.damnedAfter = t.lostSpeech = ""; }
        foreach (var sv in All<Survivor>(scene)) sv.joinLine = "";
        foreach (var h in All<HordeChase>(scene))
        {
            h.introLine = HS5.S("A07_van_start");
            h.clingLine = h.clingLineAlone = HS.S("498-500");
            h.stopLine = HS.S("502-503");
            h.driveLine = "Go.";
        }
        foreach (var b in All<DemonBoss>(scene))
        {
            b.displayName = "GRAHAM VANE";
            b.burstLines = "*Vane drags himself out of the office.*|" + HS.S("506-508");   // (509: Derek did it)
            b.driveLine = "Go.";
            b.transformLine = "*Every lie tears him open wider. Something else climbs out.*";
            b.tunnelInLine = HS.S("501");
            b.tunnelOutLine = HS.S("527");
            b.arriveLines = HS.S("504-505");
            b.nowhereLine = HS.S("510-517");
            b.riseLines = HS.S("518-522");
            b.phase2Lines = HS.S("530-531");
            b.phase3Lines = HS.S("523-524");
            b.sealedName = "VANE - THREE HEARTS, THREE LIES";
            b.heartsLines = HS.S("532-533") + "|" + HS5.Id(205);   // V5.1: three hearts, the King says name them
            b.heartNameLines = new[] { HS5.Id(206) + "|" + HS5.Id(207), HS5.Id(208) + "|" + HS5.Id(209), HS5.Id(210) + "|" + HS5.Id(211) };
            b.heartsHint = "*Break the three hearts.*";
            b.crashLines = "";
            b.deathLines = "";   // (what he says as he comes apart is RevisedPlotDirector.AmalgamationAccounts)
            b.afterLines = HS.S("577-580");
        }
        foreach (var e in All<EndingSequence>(scene))
        {
            e.arriveLine = HS.S("581");
            e.brandLine = "";
            e.boundLine = HS5.S("A08_wake_ditch");
            e.deathLines = HS5.S("A01_brand") + "|" + HS5.S("A01_brand_hands");   // the void (A01)
            e.deathBrandLine = "";
        }
        foreach (var c in All<StabCutscene>(scene))
            if (c.gameObject.name.Contains("Derek kills Tomas")) c.afterLine = "";   // V5.1: Mercer kills Tomas (the object keeps its old name)
        foreach (var t in All<TomasTape>(scene))
        {
            t.talk = "A05_tomas_tape"; t.shot = ""; t.after = ""; t.leaving = ""; t.end = "";   // V5.1: Tomas on the phone, Mercer: put it down
        }
        DialogueRewrite.Apply(scene);
    }

    static GhostChoice C(string text, string reply, int next = -1, int karma = 0, int fate = 0) => new GhostChoice { text = text, reply = reply, next = next, karma = karma, fate = fate };
    static GhostNode N(string line, params GhostChoice[] choices) => new GhostNode { line = line, choices = choices };

    // (Priya, Ben, Nadia and Omar get their conversations from OfficeWitness.)
    static void ConfigureGhost(GhostNPC g)
    {
        switch (g.ghostName)
        {
            case "Vane":
                g.nodes = new[] { N(HS5.Id(92)) };   // V5.1: Mercer said you gave us up
                g.afterLine = HS5.Id(92);
                break;
            case "Tomas":
                g.nodes = new[] { N(HS5.S("A05_tomas_echo", "Tomas")) };   // V5.1: Mercer found him, Vane covered it up
                g.afterLine = HS5.Id(176);
                break;
        }
    }

    static void ConfigureNote(ReadableNote n)
    {
        switch (n.gameObject.name)
        {
            case "Derek's phone": n.title = "Last call: Derek"; n.subtitle = "Derek"; n.afterLine = HS5.Id(85); break;
            case "IncidentReport":
                n.title = "SHIFT WITNESS STATEMENT";
                n.body = "13 APRIL - SILO 2\nT. ROURKE\n\nOFFICIAL ACCOUNT\nAccidental fall while unsupervised.\n\nSigning confirms that I witnessed this account.\nThis is not an attendance sheet.";
                n.footer = "Signed: Derek / [your signature]\nFiled by Vane.";
                n.afterLine = HS.S("307-308");
                break;
            case "KesslerLedger":
                n.title = "VANE - NIGHT ACCOUNTS";
                n.body = "City morgue: unclaimed intake -> Silo 2.\nMaterial removed -> Harlow Clinic.\nRemainder -> disposal.\n\nT. Rourke: disposal.\nDriver bonuses included.";
                n.footer = "Cleaner opened bags. He called the police.\nResolve it. - V.";
                n.beforeLine = "";
                n.afterLine = HS.S("309-311");
                break;
            case "CleaningRota": n.scrawl = "DID NOT FINISH"; n.afterLine = HS.S("312-313"); break;   // (the times stay out of order: putting them in order is the puzzle)
            case "Calendar":
                n.dayNote = "Tomas accident";
                n.scrawl = "";
                n.beforeLine = HS.S("279");
                n.afterLine = HS.S("281");
                break;
            case "StickyNote": n.body = "THE DAY.\n\nDAY FIRST,\nMONTH SECOND.\n\nDO NOT WRITE DOWN\nTHE CODE."; n.footer = "- K."; n.afterLine = ""; break;
            case "YourDesk_Note":
                n.beforeLine = HS.S("275-276");
                n.body = "YOUR KEYS ARE ON MY BELT.\nSILO 2.\n\nCOME AND CLOCK OUT.";
                n.footer = "- G.V.";
                n.afterLine = HS.S("277-278");
                break;
        }
    }
}
