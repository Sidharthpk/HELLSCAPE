using UnityEngine;

// Separate material assets keep the prologue and the kitchen memory clean.
public class BurntHomeAppearance : MonoBehaviour
{
    [System.Serializable]
    public struct Surface
    {
        public Renderer renderer;
        public Material[] clean, burnt;
    }
    public Surface[] surfaces;
    public void ShowClean(bool clean)
    {
        if (surfaces == null) return;
        foreach (var s in surfaces)
            if (s.renderer != null) s.renderer.sharedMaterials = clean ? s.clean : s.burnt;
    }
}
