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
        Set(from, softnessAtStart);
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
            if (playing || done) { playing = false; done = false; Set(from, softnessAtStart); }
            return;
        }
        if (!playing && !done) { playing = true; t = -delay; }
        if (!playing) return;

        t += Time.unscaledDeltaTime;       // the death screen freezes time
        if (t < 0f) { Set(from, softnessAtStart); return; }
        float k = Mathf.Clamp01(t / duration);
        float ease = 1f - Mathf.Pow(1f - k, 3f);
        float jitter = (Mathf.PerlinNoise(Time.unscaledTime * 14f, 0.3f) - 0.5f) * 2f * shudder * (1f - k);
        Set(Mathf.Lerp(from, to, ease) + jitter, Mathf.Lerp(softnessAtStart, 0f, ease));
        if (k >= 1f) { playing = false; done = true; Set(to, 0f); }
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
