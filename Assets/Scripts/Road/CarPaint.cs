using UnityEngine;

// A car that comes in several paint jobs (materials): picks one when it appears, so spawned wrecks aren't all the
// same colour.
public class CarPaint : MonoBehaviour
{
    public Material[] paints;

    void Awake()
    {
        if (paints == null || paints.Length == 0) return;
        var m = paints[Random.Range(0, paints.Length)];
        if (m == null) return;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }
}
