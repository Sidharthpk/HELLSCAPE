using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// HellScape > Tests > Dialogue CSV: is the script whole, and is every row of it wired into the game?
// Rows count as wired when a script names them: HS.S("20-31"), HS.T(160), or a "// HS-rows: 160,164" note where the
// numbers are worked out in code. Result in Library/DialogueCsvCheck.result.
public static class DialogueCsvCheck
{
    const int Expected = 651;

    [MenuItem("HellScape/Tests/Dialogue CSV")]
    public static string Run()
    {
        var problems = new List<string>();
        var rows = HS.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/HellscapeDialogue.csv"));
        if (rows.Count != Expected) problems.Add("Expected " + Expected + " rows, found " + rows.Count);
        for (int id = 1; id <= Expected; id++) if (!rows.ContainsKey(id)) problems.Add("Missing HS_" + id.ToString("0000"));
        foreach (var r in rows)
        {
            if (r.Value.speaker.Length == 0) problems.Add("No speaker: HS_" + r.Key.ToString("0000"));
            if (r.Value.text.Contains("|") || r.Value.text.StartsWith("*")) problems.Add("Text would break the dialogue box: HS_" + r.Key.ToString("0000"));
            if (r.Value.text.Any(c => c > 126)) problems.Add("Character the pixel font may not have: HS_" + r.Key.ToString("0000"));
        }

        var used = new HashSet<int>();
        var spec = new Regex("HS\\.S\\(\\s*([^)]*)\\)|HS\\.T\\((\\d+)\\)|HS-rows:\\s*([\\d,\\- ]+)");
        var literal = new Regex("\"(\\d+(?:-\\d+)?(?:,\\d+(?:-\\d+)?)*)\"");
        foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith("DialogueCsvCheck.cs") || file.EndsWith("HS.cs")) continue;
            string src = File.ReadAllText(file);
            if (!src.Contains("HS.") && !src.Contains("HS-rows")) continue;
            foreach (Match m in spec.Matches(src))
            {
                if (m.Groups[2].Success) Add(used, m.Groups[2].Value, rows, problems, file);
                else if (m.Groups[3].Success) Add(used, m.Groups[3].Value.Replace(" ", ""), rows, problems, file);
            }
            foreach (Match m in literal.Matches(src)) Add(used, m.Groups[1].Value, rows, problems, file);   // "20-31" wherever it is passed on to HS.S
        }
        // (a row that is only a sound cue, like [gunshot], has no text to show: see HELLSCAPE_Missing_Assets.md)
        var unused = Enumerable.Range(1, Expected).Where(id => !used.Contains(id) && rows.TryGetValue(id, out var r) && r.text.Length > 0).ToList();
        if (unused.Count > 0) problems.Add(unused.Count + " rows are not wired to anything: " + string.Join(",", unused.Select(i => i.ToString())));

        string result = (problems.Count == 0 ? "PASS" : "FAIL") + ": " + rows.Count + " rows, " + used.Count + " wired\n" + string.Join("\n", problems);
        File.WriteAllText("Library/DialogueCsvCheck.result", result);
        if (problems.Count == 0) Debug.Log(result); else Debug.LogError(result);
        return result;
    }

    static void Add(HashSet<int> used, string spec, Dictionary<int, HS.Row> rows, List<string> problems, string file)
    {
        if (spec.Length == 0) return;
        foreach (int id in HS.Ids(spec.Trim(',', '-')))
        {
            if (!rows.ContainsKey(id)) problems.Add(Path.GetFileName(file) + " names HS_" + id.ToString("0000") + ", which is not in the CSV");
            used.Add(id);
        }
    }
}
