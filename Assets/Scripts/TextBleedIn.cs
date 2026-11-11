using TMPro;
using UnityEngine;

// Big horror text that doesn't just appear: the letters start eaten away to nothing (TMP face dilate -1) and swell
// out to full thickness with a shudder, like ink bleeding through paper. Plays each time the screen it's on becomes
// visible (its CanvasGroups fading in), and resets when that screen is hidden again. On the HELLSCAPE title and YOU DIED.
[RequireComponent(typeof(TMP_Text))]
public class TextBleedIn : MonoBehaviour
{
    public float from = -1f;              // face dilate at the start: -1 = nothing left of the letters
    public float to = 0f;                 // where it settles (0 = the font as designed)
    public float delay = 0.15f;
    public float duration = 2.2f;
    public float shudder = 0.06f;         // a nervous flicker in the dilate while it grows
    public float softnessAtStart = 0.35f; // blurry edges early on, sharpening as it comes in
    public float perLetter = 0f;          // > 0: no bleed, the letters come in one by one, this many seconds apart (H, E, L, ...)
    public float letterFade = 0.9f;       // ...each one fading up over this long

    static readonly int FaceDilate = Shader.PropertyToID("_FaceDilate");
    static readonly int OutlineSoftness = Shader.PropertyToID("_OutlineSoftness");

    TMP_Text text;
    Material mat;
    CanvasGroup[] groups;
    bool playing, done;
    float t;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        mat = text.fontMaterial;           // this text's own copy: the font asset's material is left alone
        groups = GetComponentsInParent<CanvasGroup>(true);
        Set(perLetter > 0f ? to : from, perLetter > 0f ? 0f : softnessAtStart);
    }

    float Visible()
    {
        float a = 1f;
        foreach (var g in groups) if (g != null) a *= g.alpha;
        return a;
    }

    void Update()
    {
        float vis = Visible();
        if (vis < 0.01f)
        {
            // hidden again: ready to bleed in next time
            if (playing || done) { playing = false; done = false; Set(perLetter > 0f ? to : from, perLetter > 0f ? 0f : softnessAtStart); }
            return;
        }
        if (!playing && !done) { playing = true; t = -delay; }
        if (!playing) return;

        t += Time.unscaledDeltaTime;       // the death screen freezes time
        if (perLetter > 0f)
        {
            int n = text.GetParsedText().Length;
            if (t > (n - 1) * perLetter + letterFade) { playing = false; done = true; text.ForceMeshUpdate(); return; }   // all in: plain text again
            Letters(t);
            return;
        }
        if (t < 0f) { Set(from, softnessAtStart); return; }
        float k = Mathf.Clamp01(t / duration);
        float ease = 1f - Mathf.Pow(1f - k, 3f);
        float jitter = (Mathf.PerlinNoise(Time.unscaledTime * 14f, 0.3f) - 0.5f) * 2f * shudder * (1f - k);
        Set(Mathf.Lerp(from, to, ease) + jitter, Mathf.Lerp(softnessAtStart, 0f, ease));
        if (k >= 1f) { playing = false; done = true; Set(to, 0f); }
    }

    // each letter's alpha follows its own clock, so the word is written out one letter at a time, softly
    void Letters(float time)
    {
        text.ForceMeshUpdate();
        var ti = text.textInfo;
        for (int i = 0; i < ti.characterCount; i++)
        {
            var ci = ti.characterInfo[i];
            if (!ci.isVisible) continue;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - i * perLetter) / letterFade));
            var cols = ti.meshInfo[ci.materialReferenceIndex].colors32;
            for (int v = 0; v < 4; v++) cols[ci.vertexIndex + v].a = (byte)(cols[ci.vertexIndex + v].a * a);
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    void Set(float dilate, float softness)
    {
        if (mat == null) return;
        mat.SetFloat(FaceDilate, dilate);
        if (mat.HasProperty(OutlineSoftness)) mat.SetFloat(OutlineSoftness, softness);
        text.UpdateMeshPadding();
    }

    void OnDestroy()
    {
        if (mat != null && Application.isPlaying) Destroy(mat);
    }
}
