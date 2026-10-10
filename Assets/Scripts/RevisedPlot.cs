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
        if (scene.name != "Prologue" && scene.name != "MainGameScene") return;
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
        return value.Replace("Kessler & Vane", "{COMPANY}")
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
        foreach (var d in All<DeliveryRun>(scene))
        {
            d.startLines = "*6:38 AM.*|" + HS.S("20");
            d.clockOutCard = "7:00 AM";
            d.depotLines = HS.S("60-66");
            if (d.stops.Length > 0) d.stops[0].handOverLines = HS.S("32-42");
            if (d.stops.Length > 1)
            {
                d.stops[1].objective = "Collect the KEEP COLD container - Harlow Clinic";
                d.stops[1].arriveLine = "*6:50 AM. Harlow Clinic, back door.*";
                d.stops[1].handOverLines = HS.S("43-53");
                if (d.stops[1].customer != null) d.stops[1].customer.prompt = "Collect the sealed container";
            }
        }
        foreach (var d in All<PrologueDirector>(scene))
        {
            d.timeCard = "SATURDAY - 6:38 AM";
            d.arriveLines = "*7:00 AM.*";
            d.homeLines = "Flat 2B. Second floor.";
            d.doorOpenLines = HS.S("67");
            d.callOut = HS.S("68");
            d.noAnswer = HS.S("69");
            d.settleLines = "";
            d.newsLines = HS.S("70-73");
            d.suspicionLines = HS.S("74");
            d.bodyLines = HS.S("75-77");
            d.letterBefore = HS.S("78");
            d.letterBody = "I opened truck four after Tomas did. It wasn't equipment.\n\nVane told me I could save both of us if I said it was a fall. I thought signing it would end it.\n\nThe Saturday box from Harlow is Tomas. If you brought it back, you have to understand what that makes us.\n\nLook in the ledger. Don't trust Kessler.\n\nI tried to tell you. <color=#6a0000>I kept deciding to do it tomorrow.</color>";
            d.letterAfter = HS.S("79-81");
            d.stalkLines = HS.S("82-86");
            d.stabLine = HS.S("87");
            d.woundedLines = HS.S("88-89");
            d.upLines = HS.S("90");
            d.topLines = HS.S("91");
            d.confrontLines = d.roofLines = HS.S("92-96");
            d.grabLines = HS.S("97");
        }
        foreach (var s in All<SleepSequence>(scene)) s.fallWakeLine = HS.S("126-128");
        foreach (var car in All<CarInteract>(scene)) car.noKeysLine = HS.S("146");

        foreach (var g in All<GhostNPC>(scene)) ConfigureGhost(g);
        foreach (var n in All<ReadableNote>(scene)) ConfigureNote(n);
        foreach (var c in All<KesslerConfrontation>(scene))
        {
            c.confrontNodes = new[] {
                N(HS.T(344),
                    C(HS.T(345), HS.S("346", "Vane"), 1),
                    C(HS.T(347), HS.S("348", "Vane"), 1),
                    C(HS.T(349), HS.S("350", "Vane"), 1, -1),
                    C(HS.T(351), HS.S("352", "Vane"), 1)),
                N(HS.T(353),
                    C(HS.T(354), HS.S("355", "Vane")),
                    C(HS.T(356), HS.S("357", "Vane")),
                    C(HS.T(358), HS.S("359", "Vane"))) };
            c.scareLine = HS.S("340-343");
            c.turnLine = HS.S("360-364");
            c.keyLine = HS.S("365");
            c.afterTurnLine = HS.S("366");
        }
        foreach (var c in All<KesslerChase>(scene))
        {
            c.knockLine = HS.S("388,395");
            c.screamLine = HS.S("396");
            c.runLine = HS.S("397");
            c.hitLines = HS.S("398-399");
            c.tomasLine = HS.S("389");
            c.tomasHoldLine = HS.S("390-393");
            c.tomasLostLine = HS.S("394");
            c.insideLine = HS.S("400");
            c.quietLine = HS.S("401-402");
        }
        foreach (var k in All<KillerCutscenes>(scene))
        {
            k.scareLine = HS.S("403");
            k.getUpLine = HS.S("404-408");
            k.ambushWarn = "";
            k.ambushLine = HS.S("413-415");
        }
        foreach (var e in All<Elevator>(scene)) { e.notInsideLine = HS.S("409"); e.openLine = HS.S("410-411"); }
        foreach (var s in All<SiloEncounter>(scene))
        {
            s.pushLine = HS.S("403");
            s.leapLine = HS.S("412,416");
            s.landLine = HS.S("417-418");
            s.derekDeathLine = HS.S("419-421");
            s.throwLine = HS.S("422");
            s.gunLine = HS.S("423-425");
            s.outLine = HS.S("426");
            s.defeatedLine = HS.S("427-429");
            s.killTalkLine = HS.S("430-434");
            s.killLine = HS.S("436-437");
            s.killMayaLine = HS.S("449,451-453");   // (her own rows from the spare route: the ones that don't answer Elias)
            s.spareLine = HS.S("441-446");
            s.mayaSnapLine = HS.S("447-451");
            s.mayaRealiseLine = HS.S("452-453");
            s.mayaTurnedLine = HS.S("454-455");
            s.eliasSaveLine = HS.S("456");
            s.eliasKeysLine = HS.S("457-459");
            s.keysLine = "";
            s.confessionLine = HS.S("438-440");
            s.twistFloodLine = s.floodLine = HS.S("460-461");
        }
        foreach (var f in All<BloodFlood>(scene))
        {
            f.firstLine = HS.S("462");
            f.clingLine = HS.S("463-465");
            f.clingLineSam = HS.S("464-465");
            f.lowLine = HS.S("466-469,471-472");
            f.lowLineSam = HS.S("466-467,470-472");
            f.mayaDrownLine = HS.S("477-478");
            f.samDrownLine = HS.S("488-489");
            f.lostLine = HS.S("491");
            f.damnedLine = HS.S("482");
        }
        foreach (var t in All<TurningCutscene>(scene))
        {
            t.damnedSpeech = HS.S("479-481");
            t.damnedAfter = HS.S("482");
            t.lostSpeech = HS.S("490,492");
        }
        foreach (var s in All<Survivor>(scene)) s.joinLine = s.displayName == "Maya" ? HS.S("473-476") : HS.S("483-487");
        foreach (var h in All<HordeChase>(scene))
        {
            h.introLine = HS.S("496-497");
            h.clingLine = HS.S("498-500");
            h.clingLineAlone = HS.S("499-500");
            h.stopLine = HS.S("502-503");
            h.driveLine = "Go.";
        }
        foreach (var b in All<DemonBoss>(scene))
        {
            b.displayName = "TOMAS - THE AMALGAMATION";
            b.burstLines = "*Tomas walks out of the office, dragging Vane and Maya.*|" + HS.S("506-509");
            b.driveLine = "Go.";
            b.transformLine = "*The three bodies pull together into one.*";
            b.tunnelInLine = HS.S("501");
            b.tunnelOutLine = HS.S("527");
            b.arriveLines = HS.S("504-505");
            b.nowhereLine = HS.S("510-517");
            b.riseLines = HS.S("518-522");
            b.phase2Lines = HS.S("530-531");
            b.phase3Lines = HS.S("523-524");
            b.sealedName = "TOMAS - BOUND BY THREE HEARTS";
            b.heartsLines = HS.S("532-535");
            b.heartsHint = "*Break the three hearts.*";
            b.crashLines = "";
            b.deathLines = "";   // (what he says as he comes apart is RevisedPlotDirector.AmalgamationAccounts)
            b.afterLines = HS.S("577-580");
        }
        foreach (var e in All<EndingSequence>(scene))
        {
            e.arriveLine = HS.S("581");
            e.brandLine = "";
            e.boundLine = HS.S("639-641");
            e.deathLines = HS.S("108-121");
            e.deathBrandLine = HS.S("123-125");
        }
        foreach (var c in All<StabCutscene>(scene))
            if (c.gameObject.name.Contains("Derek kills Tomas")) c.afterLine = HS.S("336-337");
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
                g.nodes = new[] { N(HS.T(288),
                    C(HS.T(289), HS.S("290-292", "Vane")),
                    C(HS.T(293), HS.S("294", "Vane")),
                    C(HS.T(295), HS.S("296", "Vane"))) };
                g.afterLine = HS.T(288);
                break;
            case "Tomas":
                // (the first answer leads into "What do you want from Vane?", whichever it was)
                g.nodes = new[] {
                    N(HS.T(241),
                        C(HS.T(250), HS.S("251,254", "Tomas"), 1, 1),
                        C(HS.T(252), HS.S("253,254", "Tomas"), 1),
                        C(HS.T(242), HS.S("243-249,254", "Tomas"), 1)),
                    N(HS.T(255),
                        C(HS.T(258), HS.S("259", "Tomas")),
                        C(HS.T(260), HS.S("261", "Tomas")),
                        C(HS.T(256), HS.S("257", "Tomas"))) };
                g.afterLine = HS.T(262);
                break;
        }
    }

    static void ConfigureNote(ReadableNote n)
    {
        switch (n.gameObject.name)
        {
            case "Derek's phone": n.title = "3 missed calls from Vane"; n.subtitle = "Vane"; n.afterLine = "Three missed calls from Vane. All before three in the morning."; break;
            case "IncidentReport":
                n.title = "SHIFT WITNESS STATEMENT";
                n.body = "13 APRIL - SILO 2\n\nOfficial cause: accidental fall while unsupervised.\n\nSignatures affirm witness to this account, not attendance.";
                n.footer = "Signed: Maya / Sam / Derek / [your signature]\nFiled by Vane.";
                n.afterLine = HS.S("307-308");
                break;
            case "KesslerLedger":
                n.title = "VANE - NIGHT ACCOUNTS";
                n.body = "City morgue: unclaimed intake -> Silo 2.\nMaterial removed -> Harlow Clinic.\nRemainder -> disposal.\n\nT. Rourke: disposal.\nDriver bonuses included.";
                n.footer = "Cleaner opened bags. Derek identified.\nResolve it. - V.";
                n.beforeLine = "";
                n.afterLine = HS.S("309-311");
                break;
            case "CleaningRota": n.scrawl = "DID NOT FINISH"; n.afterLine = HS.S("312-313"); break;   // (the times stay out of order: putting them in order is the puzzle)
            case "Calendar": n.afterLine = HS.S("279-281"); break;
            case "StickyNote": n.body = "THE DAY.\n\nDAY FIRST,\nMONTH SECOND.\n\nDO NOT WRITE DOWN\nTHE CODE."; n.footer = "- K."; n.afterLine = ""; break;
            case "YourDesk_Note":
                n.beforeLine = HS.S("275-276");
                n.body = "AFTER THE FALL\n\nYou'll look for keys first.\nYou always did.\nI've got them.\n\nSILO 2.\nCome find the last name\non the paper.\n- Elias";
                n.afterLine = HS.S("277-278");
                break;
        }
    }
}
