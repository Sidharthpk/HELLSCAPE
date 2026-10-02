using UnityEngine;

// The office model's ceiling light panels: dead until the breaker has its fuses back, then lit.
public class PanelLights : MonoBehaviour
{
    public Renderer[] renderers;
    public Material glowing;                // the model's own emissive panel material
    public Material off;

    void Start() => SetPowered(false);

    public void SetPowered(bool on)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == glowing || mats[i] == off) mats[i] = on ? glowing : off;
            r.sharedMaterials = mats;
        }
    }
}
