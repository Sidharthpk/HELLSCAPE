using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit art pass; importing this file does not alter the scene. Request file supports editor automation.
[InitializeOnLoad]
public static class RedCityArtPass
{
    const string Folder = "Assets/Generated/BurntCity", Request = "Library/RedCityArtPass.request";
    const string Result = "Library/RedCityArtPass.result", ReviewKey = "HellScape.RedCityArtReview";
    static IEnumerator review;
    static readonly List<string> notes = new List<string>();
    static double deadline;

    static RedCityArtPass()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ReviewKey, false)) return;
            SessionState.EraseBool(ReviewKey);
            notes.Clear();
            if (File.Exists(Result)) notes.AddRange(File.ReadAllLines(Result));
            SleepSequence.WakeInHell = true;
            Teleporter.ArriveAt = "CityReturnPoint";
            SceneManager.LoadScene("MainGameScene");
            deadline = EditorApplication.timeSinceStartup + 120;
            review = Review();
        };
    }

    static void Poll()
    {
        if (review != null)
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Visual review timed out");
                if (review.MoveNext()) return;
                notes.Add("PASS: Red city visual review");
            }
            catch (Exception error) { notes.Add("FAIL: " + error); }
            review = null;
            File.WriteAllLines(Result, notes);
            EditorApplication.isPlaying = false;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Apply(); SessionState.SetBool(ReviewKey, true); EditorApplication.isPlaying = true; }
        catch (Exception error) { File.WriteAllText(Result, "FAIL: " + error); }
    }

    [MenuItem("HellScape/Hell City/Burn exteriors, weather roads and alternate trees")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before applying the art pass");
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "MainGameScene")
        {
            if (scene.isDirty) throw new Exception("Save the current scene before opening MainGameScene");
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity");
        }
        Directory.CreateDirectory("Backups/RedCityArt-20261023");
        string backup = "Backups/RedCityArt-20261023/MainGameScene.unity";
        if (!File.Exists(backup)) File.Copy(scene.path, backup);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var hell = scene.GetRootGameObjects().First(g => g.name == "HellWorld");
        var appearance = hell.GetComponent<BurntCityAppearance>();
        if (appearance != null) appearance.ShowBurnt(false);
        else appearance = hell.AddComponent<BurntCityAppearance>();
        var burntShader = Shader.Find("HellScape/Burnt City");
        var roadShader = Shader.Find("HellScape/Worn Road");
        if (burntShader == null || roadShader == null) throw new Exception("City surface shaders were not imported");
        var surfaces = new List<BurntHomeAppearance.Surface>();
        int roads = 0, buildings = 0;
        var clones = new Dictionary<Material, Material>();
        foreach (var renderer in RevisedPlot.All<MeshRenderer>(scene))
        {
            var clean = renderer.sharedMaterials;
            var burnt = clean.ToArray(); bool changed = false;
            for (int slot = 0; slot < clean.Length; slot++)
            {
                var source = clean[slot]; if (source == null) continue;
                string n = source.name.ToLowerInvariant();
                bool road = n.StartsWith("asphalt_") || n.StartsWith("sideway_tile");
                bool building = new[] { "building_bases", "building_facades", "building_interior", "building_windows", "highrise_facades", "highway_wall" }.Any(n.StartsWith);
                if (!road && !building) continue;
                if (!clones.TryGetValue(source, out var copy))
                {
                    string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                    string path = Folder + "/" + source.name + "_" + id.Substring(0, 8) + ".mat";
                    copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (copy == null) { copy = new Material(source); AssetDatabase.CreateAsset(copy, path); }
                    else copy.CopyPropertiesFromMaterial(source);
                    copy.name = source.name + (road ? " (worn red city)" : " (charred red city)");
                    copy.shader = road ? roadShader : burntShader;
                    copy.SetFloat("_Smoothness", road ? .035f : .06f); copy.SetFloat("_Metallic", 0);
                    copy.SetColor("_EmissionColor", Color.black); copy.DisableKeyword("_EMISSION");
                    EditorUtility.SetDirty(copy); clones[source] = copy;
                }
                burnt[slot] = copy; changed = true;
                if (road) roads++; else buildings++;
            }
            if (changed) surfaces.Add(new BurntHomeAppearance.Surface { renderer = renderer, clean = clean, burnt = burnt });
        }
        appearance.surfaces = surfaces.ToArray(); EditorUtility.SetDirty(appearance);

        var trees = RevisedPlot.All<HellTrees>(scene).First();
        int kept = Alternate(trees);
        string originalProfilePath = "Assets/Settings/HellVolumeProfile.asset";
        var originalProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(originalProfilePath);
        string profilePath = Folder + "/RedCityAtmosphere.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null) { AssetDatabase.CopyAsset(originalProfilePath, profilePath); profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath); }
        if (!profile.TryGet<Bloom>(out var bloom)) { bloom = profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom, profile); }
        bloom.intensity.Override(.85f); bloom.scatter.Override(.65f);
        EditorUtility.SetDirty(bloom); EditorUtility.SetDirty(profile);
        foreach (var volume in RevisedPlot.All<Volume>(scene))
            if (volume.sharedProfile == originalProfile || volume.sharedProfile == profile) { volume.sharedProfile = profile; EditorUtility.SetDirty(volume); }
        // The saved scene keeps the clean shared city; HellWorld applies the damage on enable.
        appearance.ShowBurnt(hell.activeSelf);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        notes.Clear();
        notes.Add("Building material slots: " + buildings + "; road/pavement slots: " + roads + "; mapped renderers: " + surfaces.Count);
        notes.Add("Burning trees: " + trees.lights.Length + " -> " + kept + "; red city bloom: 1.10 -> 0.85");
        File.WriteAllLines(Result, notes);
    }

    static int Alternate(HellTrees trees)
    {
        var all = trees.lights.Where(l => l != null).Select(l => l.transform.parent).Distinct().ToArray();
        foreach (var tree in all) tree.gameObject.SetActive(true);
        var assigned = new HashSet<Transform>();
        var rows = new List<(Transform[] trees, int phase, bool alongZ)>();
        for (int axis = 0; axis < 2; axis++)
        {
            bool alongZ = axis == 0;
            var groups = all.GroupBy(t => Mathf.RoundToInt((alongZ ? t.position.x : t.position.z) / 3f)).OrderBy(g => g.Key).ToArray();
            for (int row = 0; row < groups.Length; row++)
                if (groups[row].Count() >= 3) rows.Add((groups[row].ToArray(), row % 2, alongZ));
        }
        foreach (var row in rows.OrderByDescending(r => r.trees.Length))
        {
            var remaining = row.trees.Where(t => !assigned.Contains(t)).OrderBy(t => row.alongZ ? t.position.z : t.position.x).ToArray();
            if (remaining.Length < 3) continue;
            for (int index = 0; index < remaining.Length; index++)
            {
                remaining[index].gameObject.SetActive((index + row.phase) % 2 == 0);
                assigned.Add(remaining[index]);
            }
        }
        var loose = all.Where(t => !assigned.Contains(t)).OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToArray();
        for (int i = 0; i < loose.Length; i++) loose[i].gameObject.SetActive(i % 2 == 0);
        return all.Count(t => t.gameObject.activeSelf);
    }

    static IEnumerator Review()
    {
        while (Teleporter.ArriveAt != null || Time.timeSinceLevelLoad < 2f) yield return null;
        var scene = SceneManager.GetActiveScene();
        var appearance = RevisedPlot.All<BurntCityAppearance>(scene).First();
        Check(appearance.surfaces.Length > 0 && appearance.surfaces.All(s => s.renderer.sharedMaterials.SequenceEqual(s.burnt)), "Red city applies burnt building and worn road materials");
        Check(!ShaderUtil.ShaderHasError(Shader.Find("HellScape/Burnt City")) && !ShaderUtil.ShaderHasError(Shader.Find("HellScape/Worn Road")), "Both URP surface shaders compile");
        var trees = RevisedPlot.All<HellTrees>(scene).First();
        int kept = trees.lights.Count(l => l != null && l.transform.parent.gameObject.activeSelf);
        Check(kept > trees.lights.Length * .3f && kept < trees.lights.Length * .65f, "Tree rows are thinned to roughly half (" + kept + "/" + trees.lights.Length + ")");
        var player = RevisedPlot.All<FirstPersonController>(scene).First(); player.enabled = false;
        player.GetComponent<Rigidbody>().isKinematic = true;
        var sleep = RevisedPlot.All<SleepSequence>(scene).First();
        var look = typeof(SleepSequence).GetMethod("ApplyLook", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        // Use the actual street geometry: the old city's hard-coded review positions now sit inside buildings.
        var streets = appearance.surfaces.Where(s => s.clean.Any(m => m != null && m.name.StartsWith("asphalt_")))
            .Select(s => s.renderer.bounds).Where(b => b.size.x > 6 && b.size.z > 6)
            .OrderBy(b => (b.center - player.transform.position).sqrMagnitude).ToArray();
        var street = streets.First();
        var junction = streets.FirstOrDefault(b => Vector3.Distance(b.center, street.center) > 35f);
        if (junction.size.sqrMagnitude < 1f) junction = street;
        Vector3 direction = street.size.x > street.size.z ? Vector3.right : Vector3.forward;
        Vector3 cross = junction.size.x > junction.size.z ? Vector3.right : Vector3.forward;
        var shots = new[] {
            ("street", street.center + Vector3.up*2.2f, street.center + direction*24 + Vector3.up*1.5f),
            ("junction", junction.center + Vector3.up*2.2f, junction.center + cross*22 + Vector3.up*1.5f),
            ("overview", street.center + Vector3.up*22, street.center + direction*28 + Vector3.up*2),
            ("road-detail", street.center + Vector3.up*2.8f, street.center + direction*3) };
        foreach (var shot in shots)
        {
            player.transform.position = shot.Item2;
            float until = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Capture("red-" + shot.Item1, shot.Item2, shot.Item3);
        }
        look.Invoke(sleep, new object[] { false });
        Check(appearance.surfaces.All(s => s.renderer.sharedMaterials.SequenceEqual(s.clean)), "Day appearance restores all original exterior materials");
        Capture("day-comparison", shots[0].Item2, shots[0].Item3);
        look.Invoke(sleep, new object[] { true });
        Check(appearance.surfaces.All(s => s.renderer.sharedMaterials.SequenceEqual(s.burnt)), "Returning to red reapplies damage");
        yield return null;
    }

    static void Check(bool ok, string note) { if (!ok) throw new Exception(note); notes.Add("PASS: " + note); }
    static void Capture(string name, Vector3 position, Vector3 target)
    {
        var go = new GameObject("Red city art review camera"); var camera = go.AddComponent<Camera>();
        camera.enabled = false; camera.transform.position = position; camera.transform.LookAt(target);
        camera.fieldOfView = 65; camera.farClipPlane = 1200; camera.nearClipPlane = .1f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var rt = new RenderTexture(1280, 800, 24); var previous = RenderTexture.active; Texture2D pixels = null;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            pixels = new Texture2D(1280,800,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1280,800),0,0); pixels.Apply();
            Directory.CreateDirectory("Library/RedCityArtPass"); File.WriteAllBytes("Library/RedCityArtPass/"+name+".png", pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); if (pixels != null) Object.DestroyImmediate(pixels); Object.DestroyImmediate(go); }
    }
}
