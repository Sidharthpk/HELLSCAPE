using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The Damned inside the office. Each point wakes once: when the act has reached its stage (OfficeHorrorDirector:
// 1-3 fuses, 4 keypad), you are close, and you can't see the spot. Never more than maxAlive at a time. They stand
// frozen while you talk to a witness or read a note, are gone in the Echo (the clean memory), and every one of
// them is taken away when Vane's turn begins. Move, add or remove points in the scene: `points` is the list.
public class OfficeDamned : MonoBehaviour
{
    [System.Serializable]
    public class Point { public Transform at; public int stage = 2; [System.NonSerialized] public bool used; }

    public GameObject prefab;               // PrimeZombie: walks the office's own navmesh
    public Transform player;
    public Point[] points;
    public int maxAlive = 3;
    public float wakeDistance = 13f;        // a point wakes when you are this close...
    public float minDistance = 6f;          // ...but never right on top of you
    public Collider doorBlocker;            // Vane's door: while it is shut, the navmesh is cut there too
    public NavMeshObstacle doorCarve;

    readonly List<GameObject> alive = new List<GameObject>();
    OfficeHorrorDirector horror;
    KesslerConfrontation conf;
    FirstPersonController fpc;
    Camera cam;
    bool over;

    void Update()
    {
        if (doorCarve != null && doorBlocker != null) doorCarve.enabled = doorBlocker.enabled;
        if (over || player == null || prefab == null || points == null) return;
        if (horror == null)
        {
            horror = FindFirstObjectByType<OfficeHorrorDirector>();   // (added at runtime by RevisedPlotDirector)
            if (horror == null) return;
            conf = FindFirstObjectByType<KesslerConfrontation>();
            fpc = player.GetComponent<FirstPersonController>();
            cam = player.GetComponentInChildren<Camera>();
        }
        alive.RemoveAll(g => g == null || (g.TryGetComponent(out Zombie z) && z.IsDead));
        if (conf != null && (conf.PreparingAmbush || conf.Ambushing || conf.Armed || conf.Done))
        {
            foreach (var g in alive) Destroy(g);
            alive.Clear(); over = true;
            return;
        }

        bool echo = RevisedPlotDirector.Instance != null && RevisedPlotDirector.Instance.Echo;
        bool hold = GhostNPC.Conversing || NoteViewer.Blocking || (fpc != null && !fpc.enabled)
            || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open);
        foreach (var g in alive)
        {
            if (g.activeSelf == echo) g.SetActive(!echo);
            if (echo || !g.TryGetComponent(out ZombieAI ai)) continue;
            bool live = !hold && !PalmLight.Stunned(ai);   // (a flash holds them too: PalmLight lets them go)
            if (ai.enabled == live) continue;
            ai.enabled = live;
            if (ai.agent != null && ai.agent.isOnNavMesh) ai.agent.isStopped = !live;
        }
        if (echo || hold || cam == null) return;

        int count = alive.Count;
        foreach (var p in points)
        {
            if (count >= maxAlive) break;
            if (p.used || p.at == null || horror.Stage < p.stage) continue;
            float dist = Vector3.Distance(p.at.position, player.position);
            if (dist > wakeDistance || dist < minDistance || Seen(p.at.position)) continue;
            if (!NavMesh.SamplePosition(p.at.position, out var hit, 2f, NavMesh.AllAreas)) continue;
            p.used = true;
            var z = Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            if (z.TryGetComponent(out ZombieAI ai)) ai.player = player;
            alive.Add(z); count++;
        }
    }

    // in front of you with nothing in the way: it must not appear out of thin air
    bool Seen(Vector3 p)
    {
        Vector3 eye = cam.transform.position, to = p + Vector3.up - eye;
        if (Vector3.Angle(cam.transform.forward, to) > 65f) return false;
        foreach (var h in Physics.RaycastAll(eye, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(player.root)) return false;
        return true;
    }

    void OnDrawGizmos()
    {
        if (points == null) return;
        foreach (var p in points)
        {
            if (p == null || p.at == null) continue;
            Gizmos.color = Color.Lerp(new Color(1f, .8f, .2f, .8f), new Color(1f, .1f, .1f, .8f), (p.stage - 1) / 3f);
            Gizmos.DrawWireSphere(p.at.position + Vector3.up, .5f);
        }
    }
}
