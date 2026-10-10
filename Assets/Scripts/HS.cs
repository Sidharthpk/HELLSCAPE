using System.Collections.Generic;
using System.Text;
using UnityEngine;

// The rewritten script (HELLSCAPE_Dialogue_Unity.csv, kept unchanged as Resources/HellscapeDialogue.csv), looked up
// by line number. The story code says WHEN a line is spoken; this says WHAT it is.
//   HS.S("20-31,54")         lines HS_0020..HS_0031 and HS_0054 in the DialogueBox's format ("Name: text", '|' between)
//   HS.S("195-199", "Priya") the same inside a talk with Priya: COLLEAGUE rows are hers, your own are marked "You:"
//                            (GhostNPC puts its own name on unmarked lines)
//   HS.T(160)                one line's bare text (a choice label, a testimony)
// Rows that are only a sound cue ([static], [gunshot]) are not subtitles and come back empty.
// Check: HellScape > Tests > Dialogue CSV.
public static class HS
{
    public struct Row { public string speaker, text, trigger, condition; }

    static Dictionary<int, Row> rows;
    public static Dictionary<int, Row> Rows => rows ?? (rows = Parse(Resources.Load<TextAsset>("HellscapeDialogue")));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => rows = null;

    // CSV speaker -> the name on the dialogue box's plate
    static readonly Dictionary<string, string> names = new Dictionary<string, string>
    {
        { "OKAFOR", "Mrs. Okafor" }, { "CLINIC EMPLOYEE", "Clinic employee" }, { "DEMON KING", "The Demon King" },
        { "TV NEWS", "News" }, { "DOOR SYSTEM", "Door" }, { "SECURITY SYSTEM", "Terminal" }, { "UNSEEN VOICE", "Unseen Voice" },
        { "VANE PHONE", "Vane" }, { "KESSLER PHONE", "Kessler" }, { "TOMAS CCTV", "Tomas" }, { "DEREK CCTV", "Derek" }, { "VANE CCTV", "Vane" },
        { "PA", "PA" },
    };

    public static string T(int id) => Rows.TryGetValue(id, out var r) ? r.text : Missing(id);

    public static string S(string spec, string who = null)
    {
        var sb = new StringBuilder();
        foreach (int id in Ids(spec))
        {
            if (!Rows.TryGetValue(id, out var r)) { Missing(id); continue; }
            if (r.text.Length == 0) continue;   // a sound cue
            if (sb.Length > 0) sb.Append('|');
            if (r.speaker == "DRIVER") sb.Append(who != null ? "You: " : "").Append(r.text);
            else sb.Append(r.speaker.EndsWith("COLLEAGUE") ? who ?? "Colleague" : Name(r.speaker)).Append(": ").Append(r.text);
        }
        return sb.ToString();
    }

    public static IEnumerable<int> Ids(string spec)
    {
        foreach (var part in spec.Split(','))
        {
            var ends = part.Split('-');
            int from = int.Parse(ends[0]), to = int.Parse(ends[ends.Length - 1]);
            for (int id = from; id <= to; id++) yield return id;
        }
    }

    static string Name(string speaker) => names.TryGetValue(speaker, out var n) ? n : speaker.Substring(0, 1) + speaker.Substring(1).ToLowerInvariant();

    static string Missing(int id) { Debug.LogError("HS_" + id.ToString("0000") + " is not in Resources/HellscapeDialogue.csv"); return ""; }

    // id,speaker,text,trigger,condition,effect with "quoted, fields" and "" for a quote
    public static Dictionary<int, Row> Parse(TextAsset csv)
    {
        var result = new Dictionary<int, Row>();
        if (csv == null) { Debug.LogError("Resources/HellscapeDialogue.csv is missing: no rewritten dialogue."); return result; }
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
                if (fields.Count >= 3 && fields[0].StartsWith("HS_") && int.TryParse(fields[0].Substring(3), out int id))
                    result[id] = new Row { speaker = fields[1].Trim(), text = Clean(fields[2]), trigger = fields.Count > 3 ? fields[3] : "", condition = fields.Count > 4 ? fields[4] : "" };
                fields.Clear();
            }
            else if (c != '\r' && c != '﻿') field.Append(c);
        }
        return result;
    }

    // The pixel font has no curly quotes; "[hit] Ah...!" keeps its words; "[static]" is a sound, not a subtitle.
    static string Clean(string text)
    {
        text = text.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Replace("…", "...").Trim();
        if (text.StartsWith("["))
        {
            int end = text.IndexOf(']');
            if (end >= 0) text = text.Substring(end + 1).Trim();
        }
        return text;
    }
}
