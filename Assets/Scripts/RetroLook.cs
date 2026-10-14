using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The game's whole retro look, no PS1 shader: material textures are point-filtered at half size (slightly pixelated)
// and a thin film grain sits over the picture. Depth in the office is InteriorDarkness' fog.
public class RetroLook : MonoBehaviour
{
    const int MipDrop = 1;            // 1 = half-size textures, 2 = quarter: chunkier
    const float Grain = 0.35f;

    static readonly HashSet<Object> done = new HashSet<Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        QualitySettings.globalTextureMipmapLimit = MipDrop;
        var go = new GameObject("Retro look");
        DontDestroyOnLoad(go);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true; v.priority = -10f;
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        var g = p.Add<FilmGrain>(true);
        g.type.Override(FilmGrainLookup.Thin1);
        g.intensity.Override(Grain);
        g.response.Override(0.8f);
        v.sharedProfile = p;
        go.AddComponent<RetroLook>();
    }

    IEnumerator Start()
    {
        // scenes build parts of themselves after load: sweep a few times
        foreach (float wait in new[] { 0f, 2f, 8f })
        {
            yield return new WaitForSeconds(wait);
            Sweep();
        }
    }

    static void Sweep()
    {
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !done.Add(m) || m.shader.name.Contains("TextMeshPro")) continue;   // not font atlases
                foreach (int id in m.GetTexturePropertyNameIDs())
                    if (m.GetTexture(id) is Texture2D t && done.Add(t)) t.filterMode = FilterMode.Point;
            }
    }
}
