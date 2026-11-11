using System.Collections.Generic;
using UnityEngine;

// A lost soul hanging in the air over the hell city: frozen in a floating pose, bobbing, drifting slowly around
// the spot it was placed and turning in the wind, flickering like the office ghosts. Look at one for too long and
// it notices you: it comes down to your height, follows you and keeps asking questions until you lose it.
public class LostSoul : MonoBehaviour
{
    public float driftRadius = 7f;
    public float driftSpeed = 0.6f;
    public float bobHeight = 0.5f;
    public float bobSpeed = 0.5f;
    public Vector2 alphaRange = new Vector2(0.12f, 0.4f);

    [Header("Noticing you")]
    public float noticeAfter = 2.5f;        // seconds of being looked at
    public float followSpeed = 3.5f;        // slower than your walk (5): you can leave it behind
    public float keepAway = 2.5f;
    public float loseAfter = 35f;           // metres
    public Vector2 askEvery = new Vector2(6f, 10f);

    // HS-rows: 657-662
    static readonly int[] questions = { 657, 658, 659, 660, 661, 662 };
    const int V5Questions = 2;   // V5.1: "Can you tell me what happened?" (HS5_0086) and "My name. Please find it." (HS5_0168)
    static string Question(int q) => q < questions.Length ? HS.S(questions[q].ToString()) : HS5.Id(q == questions.Length ? 86 : 168);

    static readonly List<LostSoul> all = new List<LostSoul>();
    const float Apart = 2.2f;   // followers never settle closer than this, so they don't pile into one

    private float side, extra, lift;   // each follower's own angle, distance and height around you
    private Vector3 bearing;            // the direction from you it settles at (fixed when it starts following)
    private Vector3 home;
    private float seed, time, lookedAt, nextAsk;
    private int lastQuestion = -1;
    private bool following;
    private Transform player;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Start()
    {
        side = Random.Range(-75f, 75f); extra = Random.Range(0f, 1.5f); lift = Random.Range(0f, 1.2f);
        home = transform.position;
        seed = Random.value * 50f;
        renderers = GetComponentsInChildren<Renderer>();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (following) Follow(); else Drift();

        if (block == null) block = new MaterialPropertyBlock();
        float a = Mathf.Lerp(alphaRange.x, alphaRange.y, Mathf.PerlinNoise(seed, Time.time * 1.5f));
        foreach (var r in renderers)
        {
            r.GetPropertyBlock(block);
            var c = r.sharedMaterial.GetColor("_BaseColor");
            c.a = a;
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }

    void Drift()
    {
        time += Time.deltaTime * driftSpeed / Mathf.Max(driftRadius, 0.1f);
        Vector3 target = home + new Vector3(Mathf.PerlinNoise(seed, time) - 0.5f, 0f, Mathf.PerlinNoise(time, seed + 7f) - 0.5f) * 2f * driftRadius;
        target.y = home.y + Mathf.Sin((Time.time + seed) * bobSpeed) * bobHeight;
        Vector3 step = Vector3.MoveTowards(transform.position, target, driftSpeed * Time.deltaTime * 2f);
        Vector3 dir = step - transform.position; dir.y = 0f;
        transform.position = step;
        if (dir.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.6f * Time.deltaTime);

        if (!OnFoot || renderers.Length == 0) return;
        bool seen = ZombieAI.Watched(renderers[0].bounds.center, 40f, 8f, transform);
        lookedAt = seen ? lookedAt + Time.deltaTime : Mathf.Max(0f, lookedAt - Time.deltaTime * 0.5f);
        if (lookedAt >= noticeAfter) { following = true; nextAsk = Time.time + 1.5f; bearing = ToBearing(); }
    }

    void Follow()
    {
        Vector3 away = transform.position - player.position; away.y = 0f;
        if (!OnFoot || away.magnitude > loseAfter) { following = false; lookedAt = 0f; home = transform.position; return; }

        Vector3 spot = player.position + bearing * (keepAway + extra);
        spot.y = player.position.y + 0.3f + lift + Mathf.Sin((Time.time + seed) * bobSpeed) * 0.15f;
        foreach (var o in all)
        {
            if (o == this || !o.following) continue;
            Vector3 d = spot - o.transform.position; d.y = 0f;
            float m = d.magnitude;
            if (m < Apart) spot += (m > 0.01f ? d / m : transform.right) * (Apart - m);
        }
        transform.position = Vector3.MoveTowards(transform.position, spot, followSpeed * Time.deltaTime);
        Vector3 face = -away;
        if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 3f * Time.deltaTime);

        if (Time.time < nextAsk || DialogueBox.Instance == null || DialogueBox.Instance.Busy) return;
        nextAsk = Time.time + Random.Range(askEvery.x, askEvery.y);
        int q;
        do q = Random.Range(0, questions.Length + V5Questions); while (q == lastQuestion);
        lastQuestion = q;
        DialogueBox.Instance.Say(Question(q));
    }

    Vector3 ToBearing()
    {
        Vector3 away = transform.position - player.position; away.y = 0f;
        return Quaternion.Euler(0f, side, 0f) * (away.sqrMagnitude > 0.01f ? away.normalized : -player.forward);
    }

    // (in the van the player object is switched off: they lose you)
    bool OnFoot => player != null && player.gameObject.activeInHierarchy;
}
