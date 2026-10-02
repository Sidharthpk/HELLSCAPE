using UnityEngine;

// Gives each wrecked car a random paint colour without duplicating materials.
public class RandomTint : MonoBehaviour
{
    public Color[] palette =
    {
        new Color(0.85f, 0.25f, 0.2f),   // faded red
        new Color(0.35f, 0.5f, 0.8f),    // dull blue
        new Color(0.45f, 0.6f, 0.35f),   // army green
        new Color(0.9f, 0.75f, 0.35f),   // sun-bleached yellow
        new Color(0.55f, 0.55f, 0.55f),  // grey
        new Color(0.25f, 0.22f, 0.22f),  // burnt black
        new Color(0.7f, 0.45f, 0.3f),    // rust
    };

    void Awake()
    {
        if (palette.Length == 0) return;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", palette[Random.Range(0, palette.Length)]);
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.SetPropertyBlock(block);
    }
}
