using UnityEngine;

// The visible beam under a ceiling spotlight (made by HellScape > Office: Spot Lights + Shafts): follows the lamp's
// colour and brightness, so it flickers and goes out with it.
[ExecuteAlways, RequireComponent(typeof(MeshRenderer))]
public class LightShaft : MonoBehaviour
{
    public Light lamp;
    public float strength = .25f;
    MeshRenderer mr; MaterialPropertyBlock block; float full;

    void Awake() { mr = GetComponent<MeshRenderer>(); block = new MaterialPropertyBlock(); }

    void LateUpdate()
    {
        if (lamp == null) { mr.enabled = false; return; }
        full = Mathf.Max(full, lamp.intensity);
        bool on = lamp.enabled && lamp.gameObject.activeInHierarchy && lamp.intensity > .02f;
        if (mr.enabled != on) mr.enabled = on;
        if (!on) return;
        Color c = lamp.color; c.a = strength * Mathf.Clamp01(lamp.intensity / Mathf.Max(.01f, full));
        mr.GetPropertyBlock(block);
        block.SetColor("_Color", c);
        mr.SetPropertyBlock(block);
    }
}
