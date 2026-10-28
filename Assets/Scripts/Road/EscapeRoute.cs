using System.Collections.Generic;
using UnityEngine;

// The drive from the office to the bridge, marked: a small glowing post and ring on the road at each of the route's
// points (only the next few stand up), collected as the van goes through them, and the round map with the stops on it
// (DeliveryHud: the next one big and gold with its distance over it in the world, the ones after it as dots). Runs
// while Kessler is chasing (DemonBoss.IsChasing: from the doors bursting open to the bridge trigger).
public class EscapeRoute : MonoBehaviour
{
    public Transform[] road;            // in driving order; the last one is the bridge
    public DemonBoss boss;
    public CarInteract car;
    public Transform player;
    public Material glow;               // unlit, additive gold
    public float reach = 16f;           // the van this close to a stop collects it
    public int shown = 4;               // how many stand up ahead at once

    GameObject[] marks;
    int next;
    bool on;
    DeliveryHud hud;

    void Start()
    {
        marks = new GameObject[road.Length];
        for (int i = 0; i < road.Length; i++)
        {
            if (road[i] == null) continue;
            var g = new GameObject("Stop " + i);
            g.transform.SetParent(transform, false);
            g.transform.position = road[i].position;
            float k = i == road.Length - 1 ? 1.8f : 1f;
            Part(g.transform, PrimitiveType.Cylinder, new Vector3(0f, 3.6f * k, 0f), new Vector3(0.35f * k, 3.6f * k, 0.35f * k));   // the post
            Part(g.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(3.8f * k, 0.02f, 3.8f * k));            // the ring on the road
            g.SetActive(false);
            marks[i] = g;
        }
    }

    void Part(Transform parent, PrimitiveType type, Vector3 at, Vector3 scale)
    {
        var p = GameObject.CreatePrimitive(type);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = at; p.transform.localScale = scale;
        var r = p.GetComponent<Renderer>();
        r.sharedMaterial = glow; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    Vector3 Where => car != null && car.InCar ? car.transform.position : player.position;
    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

    void Update()
    {
        bool escaping = boss != null && boss.IsChasing;
        if (escaping && !on) Begin();
        else if (!escaping && on) End();
        if (!on) return;

        // the stops the van has gone through; if it cut a corner past some, the ones it skipped go too
        Vector3 v = Where;
        for (int i = next; i < marks.Length; i++)
        {
            if (marks[i] == null || Flat(v - marks[i].transform.position) > reach * (i == next ? 1f : 1.6f)) continue;
            next = i + 1;
        }
        for (int i = 0; i < marks.Length; i++)
            if (marks[i] != null) marks[i].SetActive(i >= next && i < next + shown);
        DeliveryHud.Target = next < marks.Length && marks[next] != null ? marks[next].transform : null;
    }

    void Begin()
    {
        on = true;
        // where the van is on the route: the first stop still ahead of it
        Vector3 v = Where;
        int near = 0; float bd = float.MaxValue;
        for (int i = 0; i < road.Length; i++)
        {
            float d = Flat(v - road[i].position);
            if (d < bd) { bd = d; near = i; }
        }
        next = bd < reach ? near + 1 : near;
        if (hud == null)
        {
            var go = new GameObject("Escape HUD");
            go.transform.SetParent(transform, false);
            hud = go.AddComponent<DeliveryHud>();
            hud.topRight = true;
            hud.Me = () => car != null && car.InCar ? car.transform : player;
            hud.Driving = () => car != null && car.InCar;
            hud.Cam = () => car != null && car.InCar ? car.carCamera : car != null ? car.playerCamera : null;
            hud.Show = () => on && !CutsceneHUD.Hidden && !TitleScreen.Showing && !GameOverScreen.Open && !PauseMenu.Paused;
            hud.Further = Further;
        }
    }

    void End()
    {
        on = false;
        DeliveryHud.Target = null;
        foreach (var m in marks) if (m != null) m.SetActive(false);
    }

    IEnumerable<Vector3> Further()
    {
        for (int i = next + 1; i < marks.Length; i++)
            if (marks[i] != null) yield return marks[i].transform.position;
    }
}
