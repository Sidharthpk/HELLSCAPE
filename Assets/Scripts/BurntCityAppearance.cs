using UnityEngine;

// The shared city stays clean for the title/day; only HellWorld swaps its exterior surfaces.
public class BurntCityAppearance : MonoBehaviour
{
    public BurntHomeAppearance.Surface[] surfaces;

    public void ShowBurnt(bool burnt)
    {
        if (surfaces == null) return;
        foreach (var surface in surfaces)
            if (surface.renderer != null)
                surface.renderer.sharedMaterials = burnt ? surface.burnt : surface.clean;
    }

    void OnEnable() => ShowBurnt(true);
    void OnDisable() => ShowBurnt(false);
}
