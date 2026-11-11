using UnityEngine;

// One of the black hearts that seal Kessler in his last phase (DemonBoss.HeartPhase). It stands on the deck, tied to
// him by a writhing tether; while any is left he takes no damage. The guns hit it through Hit().
public class BossHeart : MonoBehaviour
{
    public DemonBoss boss;
    public float health = 210f;
    public System.Func<Vector3> anchor;      // where on the boss the tether ties on
    public Material tetherMat;
    public float height = 2.8f;

    public bool Dead { get; private set; }

    LineRenderer tether;
    Light glow;
    float flash, born;
    Vector3 scale0;
    static Material black;

    public static Material BlackMat()
    {
        if (black != null) return black;
        black = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        black.SetColor("_BaseColor", new Color(0.07f, 0.0f, 0.13f));
        black.SetFloat("_Smoothness", 0.35f);
        black.SetFloat("_SpecularHighlights", 0f);
        black.EnableKeyword("_EMISSION");
        black.SetColor("_EmissionColor", new Color(0.3f, 0f, 0.55f));
        return black;
    }

    void Start()
    {
        born = Time.time;
        scale0 = transform.localScale;
        glow = gameObject.AddComponent<Light>();
        glow.color = new Color(0.55f, 0.1f, 1f); glow.range = 9f; glow.intensity = 2f;
        var t = new GameObject("Tether").AddComponent<LineRenderer>();
        t.transform.SetParent(transform, false);
        t.useWorldSpace = true; t.positionCount = 16; t.widthMultiplier = 0.32f;
        t.sharedMaterial = tetherMat;
        tether = t;
    }

    void Update()
    {
        float rise = Mathf.SmoothStep(0f, 1f, (Time.time - born) / 1.2f);   // pushes up out of the deck
        transform.localScale = scale0 * Mathf.Max(0.01f, rise);
        float beat = 0.75f + 0.25f * Mathf.Sin(Time.time * 4.4f);
        flash = Mathf.Max(0f, flash - Time.deltaTime * 6f);
        if (glow != null) glow.intensity = (0.6f + flash * 5f) * beat;
        BlackMat().SetColor("_EmissionColor", new Color(0.3f, 0f, 0.55f) * (0.5f + beat + flash * 4f));

        if (tether == null || anchor == null) return;
        Vector3 a = transform.position + Vector3.up * height * 0.4f, b = anchor();
        Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized);
        for (int i = 0; i < tether.positionCount; i++)
        {
            float k = i / (float)(tether.positionCount - 1);
            float sway = Mathf.Sin(k * Mathf.PI) * Mathf.Sin(Time.time * 3f + k * 9f) * 0.7f;
            tether.SetPosition(i, Vector3.Lerp(a, b, k) + side * sway);
        }
    }

    public void Hit(float dmg, Vector3 point, Vector3 dir)
    {
        if (Dead) return;
        health -= dmg;
        flash = 1f;
        BloodFx.Spray(point, -dir, 10, 3f);
        if (health > 0f) return;
        Dead = true;
        if (boss != null) boss.HeartBroken(this);
        Destroy(gameObject);
    }
}
