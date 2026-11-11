using UnityEngine;

// The screen-space outline while the red city is on (sits under HellWorld): edges draw the surface's own colour pushed
// into shadow, with a dark-red border tint. The pass lives on the shared renderer; the material's values are put back
// afterwards so the office and the prologue keep theirs.
public class HellOutline : MonoBehaviour
{
    public Material outline;
    public float strength = 0.7f;
    public float surfaceDarkening = 0.3f;   // how far the surface colour goes into shadow on an edge
    public Color border = new Color(0.14f, 0.01f, 0.01f);
    public Vector2 fade = new Vector2(20f, 70f);

    float oldStrength, oldShadow, oldStart, oldEnd;
    Color oldBorder;

    void OnEnable()
    {
        if (outline == null) return;
        oldStrength = outline.GetFloat("_Strength"); oldShadow = outline.GetFloat("_Shadow");
        oldStart = outline.GetFloat("_FadeStart"); oldEnd = outline.GetFloat("_FadeEnd"); oldBorder = outline.GetColor("_BorderTint");
        outline.SetFloat("_Strength", strength); outline.SetFloat("_Shadow", surfaceDarkening);
        outline.SetFloat("_FadeStart", fade.x); outline.SetFloat("_FadeEnd", fade.y); outline.SetColor("_BorderTint", border);
    }

    void OnDisable()
    {
        if (outline == null) return;
        outline.SetFloat("_Strength", oldStrength); outline.SetFloat("_Shadow", oldShadow);
        outline.SetFloat("_FadeStart", oldStart); outline.SetFloat("_FadeEnd", oldEnd); outline.SetColor("_BorderTint", oldBorder);
    }
}
