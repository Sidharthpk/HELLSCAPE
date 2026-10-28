using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// The V5.1 script (Resources/HellscapeDialogueV5.csv, ids HS5_0001..), looked up by trigger key.
//   HS5.S("P04_derek_hit")        every spoken row of that trigger in the DialogueBox's format ("Name: text", '|' between)
//   HS5.S("P01_priya_talk", "Priya")  the same inside a talk: the Driver's rows are marked "You:"
// SFX rows (gunshots, crashes) are not subtitles and are skipped; HS5.Sfx(trigger) returns their descriptions.
// Check: HellScape > Tests > Dialogue CSV.
public static class HS5
{
    public struct Row { public string id, scene, trigger, speaker, text, kind; }

    static List<Row> rows;
    public static List<Row> Rows => rows ?? (rows = Parse(Resources.Load<TextAsset>("HellscapeDialogueV5")));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => rows = null;

    static readonly Dictionary<string, string> names = new Dictionary<string, string>
    {
        { "KING", "The Demon King" }, { "TV NEWS", "News" }, { "CAFE WORKER", "Cafe worker" }, { "CLINIC WORKER", "Clinic worker" },
        { "WHISPERS", "Whispers" }, { "VOICES", "Voices" }, { "SYSTEM", "Terminal" },
    };

    public static string S(string trigger, string who = null)
    {
        var sb = new StringBuilder();
        foreach (var r in Rows)
        {
            if (r.trigger != trigger || r.kind == "sfx") continue;
            if (sb.Length > 0) sb.Append('|');
            if (r.speaker == "DRIVER") sb.Append(who != null ? "You: " : "").Append(r.text);
            else sb.Append(Name(r.speaker)).Append(": ").Append(r.text);
        }
        if (sb.Length == 0) Debug.LogError("HS5: no spoken rows for trigger " + trigger);
        return sb.ToString();
    }

    // one row by its number: HS5.Id(92) = HS5_0092 as "Name: text" (rows are numbered in file order, no gaps)
    public static string Id(int n)
    {
        if (n < 1 || n > Rows.Count) { Debug.LogError("HS5_" + n.ToString("0000") + " is not in the V5 script"); return ""; }
        var r = Rows[n - 1];
        return r.speaker == "DRIVER" ? r.text : Name(r.speaker) + ": " + r.text;
    }

    public static IEnumerable<string> Sfx(string trigger) => Rows.Where(r => r.trigger == trigger && r.kind == "sfx").Select(r => r.text);

    static string Name(string speaker)
    {
        if (names.TryGetValue(speaker, out var n)) return n;
        int paren = speaker.IndexOf(" (");
        string baseName = paren >= 0 ? speaker.Substring(0, paren) : speaker;
        return baseName.Substring(0, 1) + baseName.Substring(1).ToLowerInvariant();
    }

    // id,scene,trigger,speaker,text,kind,condition,action (same quoting as HS.Parse)
    public static List<Row> Parse(TextAsset csv)
    {
        var result = new List<Row>();
        if (csv == null) { Debug.LogError("Resources/HellscapeDialogueV5.csv is missing."); return result; }
        var fields = new List<string>(); var field = new StringBuilder(); bool quoted = false;
        string src = csv.text + "\n";
        for (int i = 0; i < src.Length; i++)
        {
            char c = src[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < src.Length && src[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Length = 0; }
            else if (c == '\n')
            {
                fields.Add(field.ToString()); field.Length = 0;
                if (fields.Count >= 6 && fields[0].StartsWith("HS5_"))
                    result.Add(new Row { id = fields[0], scene = fields[1], trigger = fields[2], speaker = fields[3].Trim(), text = fields[4].Trim(), kind = fields[5] });
                fields.Clear();
            }
            else if (c != '\r' && c != '﻿') field.Append(c);
        }
        return result;
    }
}
