using System.Collections;
using UnityEngine;

// Someone you let drown in the red city who had owned up to something rotten, back as one of the Damned: it drags
// itself out of the blood onto the stairs behind you, screams, and comes up after you. No navmesh in the silo:
// it walks straight at you and steps up the stairs by itself (like Kessler did in the office).
public class RisenDamned : MonoBehaviour
{
    public Zombie damned;                   // a zombie with its NavMesh AI stripped, inactive until Rise()
    public Animator anim;
    public Transform player;
    public float crawlOutTime = 2.2f;
    public float speed = 2.2f;
    public float attackRange = 1.6f;
    public float attackCooldown = 1.6f;
    public int attackDamage = 12;
    public int health = 60;                 // four punches

    private bool hunting;
    private float attackTimer;
    private string state;

    void Start() { if (damned != null && !hunting) damned.gameObject.SetActive(false); }

    public void Rise(Vector3 at)
    {
        if (damned == null) return;
        StopAllCoroutines();
        Vector3 d = player.position - at; d.y = 0f;
        damned.transform.SetPositionAndRotation(at, Quaternion.LookRotation(d.sqrMagnitude > 0.01f ? d : Vector3.forward));
        damned.health = health;
        damned.gameObject.SetActive(true);
        StartCoroutine(CrawlOut());
    }

    // the cutscene's version: it's already standing there (she burned into it), and it screams in your face
    public void Appear(Vector3 at)
    {
        if (damned == null) return;
        StopAllCoroutines();
        Vector3 d = player.position - at; d.y = 0f;
        damned.transform.SetPositionAndRotation(at, Quaternion.LookRotation(d.sqrMagnitude > 0.01f ? d : Vector3.forward));
        damned.health = health;
        damned.gameObject.SetActive(true);
        StartCoroutine(ScreamThenHunt());
    }

    IEnumerator ScreamThenHunt()
    {
        hunting = false;
        state = null;
        Go("Scream", 0f);
        damned.Scream();
        yield return new WaitForSeconds(2.2f);
        Go("Upright", 0.35f);
        hunting = true;
    }

    public void ResetRisen()
    {
        StopAllCoroutines();
        hunting = false;
        state = null;
        if (damned != null) damned.gameObject.SetActive(false);
    }

    IEnumerator CrawlOut()
    {
        hunting = false;
        Go("Crawl", 0f);
        anim.SetFloat("Speed", 0.5f);
        Vector3 end = damned.transform.position;
        Vector3 start = end - Vector3.up * 1.2f;   // out of the blood, onto the step
        for (float t = 0f; t < crawlOutTime; t += Time.deltaTime)
        {
            damned.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / crawlOutTime));
            yield return null;
        }
        yield return ScreamThenHunt();
    }

    void Update()
    {
        if (!hunting || damned == null || !damned.gameObject.activeInHierarchy) return;
        if (damned.IsDead) { hunting = false; return; }
        var ph = player.GetComponentInParent<PlayerHealth>();
        if (ph != null && ph.IsDead) { anim.SetFloat("Speed", 0f); return; }

        Transform z = damned.transform;
        Vector3 to = player.position - z.position;
        Vector3 flat = to; flat.y = 0f;
        attackTimer -= Time.deltaTime;
        if (flat.magnitude <= attackRange && Mathf.Abs(to.y) < 2f)
        {
            anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
            z.rotation = Quaternion.Slerp(z.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);
            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                anim.SetTrigger("Attack");
                state = null;
                StartCoroutine(HitSoon());
            }
            return;
        }

        Go("Upright", 0.3f);
        Vector3 dir = flat.normalized;
        z.position += dir * speed * Time.deltaTime;
        z.rotation = Quaternion.Slerp(z.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        Ground(z);
        anim.SetFloat("Speed", speed, 0.15f, Time.deltaTime);
    }

    // up (or down) the steps: the nearest surface under it within a stride; never the shaft floor far below
    void Ground(Transform z)
    {
        float feet = z.position.y, best = float.MaxValue, y = feet;
        foreach (var h in Physics.RaycastAll(z.position + Vector3.up * 0.7f, Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(z) || h.collider.transform.IsChildOf(player)) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; }
        }
        if (best < float.MaxValue) z.position = new Vector3(z.position.x, Mathf.MoveTowards(feet, y, 6f * Time.deltaTime), z.position.z);
    }

    IEnumerator HitSoon()
    {
        yield return new WaitForSeconds(0.45f);
        if (damned == null || damned.IsDead) yield break;
        Vector3 d = player.position - damned.transform.position; d.y = 0f;
        if (d.magnitude > attackRange + 0.4f) yield break;
        var ph = player.GetComponentInParent<PlayerHealth>();
        if (ph != null) ph.TakeDamage(attackDamage);
    }

    void Go(string next, float fade)
    {
        if (anim == null || state == next) return;
        state = next;
        anim.CrossFadeInFixedTime(next, fade, 0);
    }
}
