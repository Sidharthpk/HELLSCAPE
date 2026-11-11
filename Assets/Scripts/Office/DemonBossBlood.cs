using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Vane's final form on the bridge (2026-10-29): four blood attacks, each changing with the phase.
//   SHARDS  (Particle Pack IceLance, dark blood red)  1: bursts of 3 aimed shards every 2 s, a red circle where each lands
//                                                    2: rings of shards fired out flat all round him, spreading apart as
//                                                       they go; red lines on the deck show their paths first
//                                                    3: rings and aimed bursts back to back
//   SMASH   (EarthShatter, red-orange)              1: a cone of spikes in front of him (the red cone outline first)
//                                                    2: two cones, front and back, then again where you've gone
//                                                    3: three cones all round, then turned to cover the gaps
//   CORRUPTED BLOOD (vomited out in gushes, pools that burn over time, toxic fumes off them)
//                                                    1: a line of pools sprayed out to you
//                                                    2: huge pools, one on you
//                                                    3: a ring of pools round him and a big one on you
//   CRAWL   (phase 2 on: down on all fours, side-to-side diagonal runs through you at 30 m/s, a red line first)
//   BOMBS   (the blood orb with severed parts stuck in it, red blast)
//                                                    1: three at you   2: a ring round you   3: each splits in three
// Sealed by the black hearts (phase 4) he floats in a shield of blood and body parts and keeps attacking from the air.
public partial class DemonBoss
{
    [Header("Blood attacks: shards, smash, corrupted blood, body-part bombs")]
    public GameObject iceLance;              // Particle Pack IceLance: the shard's mesh, its mist trail and the shatter
    public GameObject earthShatter;          // Particle Pack EarthShatter: the smash's spikes
    public GameObject poisonGas;             // Particle Pack PoisonGas: the fumes off the corrupted blood
    public GameObject goop;                  // Particle Pack GoopSprayEffect: its glob material is the vomit's
    public GameObject[] bodyParts;           // severed parts (Assets/Generated/BodyParts), 1 m long: in the bombs and the shield
    public Color shardColor = new Color(0.3f, 0.01f, 0.02f);
    public Color smashColor = new Color(1f, 0.2f, 0.04f);
    public Color bombColor = new Color(0.85f, 0.03f, 0.02f);
    public Color poolColor = new Color(0.09f, 0.005f, 0.012f);
    public Color fumeColor = new Color(0.42f, 0.48f, 0.16f);
    public Color vomitColor = new Color(0.3f, 0.025f, 0.015f);   // dark clotted red, gone brown at the edges
    public float attackGap = 2.2f;           // seconds between attacks (phase 3: x0.65)

    [Header("Shards")]
    public float shardEvery = 2f;            // between aimed bursts
    public int shardBurst = 3;               // bursts per attack
    public float shardFlight = 0.9f;
    public float shardDamage = 10f;
    public float shardHit = 1.8f;            // radius of the red circle it lands in
    public float shardLength = 2.4f;
    public int ringShards = 14;
    public float ringShardSpeed = 18f;
    public float ringShardReach = 40f;
    public float ringWarn = 0.9f;            // seconds the red lines show before a ring fires

    [Header("Smash (uses slamWindup / slamDamage / slamRecover)")]
    public float smashReach = 18f;
    public float smashAngle = 70f;

    [Header("Crawl (phase 2 on)")]
    public float crawlSpeed = 30f;           // m/s across the deck on all fours
    public int crawlRuns = 4;                // diagonal runs per attack (phase 3: +2)
    public float crawlWarn = 0.8f;           // seconds the red line shows before each run
    public float crawlHit = 7f;              // how close counts as run over (he's 24 m)
    public float crawlDamage = 20f;
    public float crawlAnimSpeed = 2.2f;      // the Crawl clip sped up to match

    [Header("Corrupted blood")]
    public float poolLife = 8f;
    public float poolDps = 10f;
    public float hugePool = 7f;

    [Header("Bombs")]
    public float bombSize = 1.1f;            // the ball's radius
    public float bombBlast = 4.5f;
    public float bombDamage = 22f;
    public float bombFlight = 1.4f;

    public string castState = "Hurt";        // arms up: the shard rings

    readonly List<GameObject> fx = new List<GameObject>();
    int lastAttack = -1, hoverPick;
    float poolHitAt;
    Mesh shardMesh; Material shardMat, poolMat; Quaternion shardAlign = Quaternion.identity; float shardScale = 1f;

    // ---------------------------------------------------------------- choosing

    IEnumerator Attack(IEnumerator a)
    {
        busy = true;
        Locomote(0f);
        yield return a;
        actTimer = attackGap * (phase >= 3 ? 0.65f : 1f);
        busy = false;
    }

    // never the same twice running; the smash only when you're near enough for it to reach; the crawl from phase 2
    IEnumerator PickAttack(float dist)
    {
        int pick;
        if (dist < smashReach * 0.6f && lastAttack != 3) pick = 3;
        else do pick = Random.Range(0, phase >= 2 ? 5 : 4); while (pick == lastAttack || (pick == 3 && dist >= smashReach));
        lastAttack = pick;
        return pick == 0 ? Shards() : pick == 1 ? Bombs() : pick == 2 ? Spray() : pick == 3 ? SmashAttack() : Crawl();
    }

    // ---------------------------------------------------------------- crawl

    // he drops onto all fours and scuttles across the deck, fast, on diagonals: side to side, each run cutting
    // through where you are; a red line shows each run first. Phase 3: more runs, less warning.
    IEnumerator Crawl()
    {
        Play(roarState);
        Roar(0.8f);
        for (float t = 0f; t < 0.7f; t += Time.deltaTime) { FaceToward(Tgt.position, turnRate * 3f); yield return null; }
        Play(stunState);   // (the Crawl clip)
        if (animator != null) animator.speed = crawlAnimSpeed;
        int runs = phase >= 3 ? crawlRuns + 2 : crawlRuns;
        float warn = phase >= 3 ? crawlWarn * 0.65f : crawlWarn;
        float side = arena != null && arena.InverseTransformPoint(transform.position).x > 0f ? -1f : 1f;   // first run to the far side
        for (int i = 0; i < runs && !defeated; i++, side = -side)
        {
            Vector3 to = transform.position;
            if (arena != null)
            {
                float z = arena.InverseTransformPoint(Tgt.position).z;
                float from = arena.InverseTransformPoint(transform.position).z;
                // the far end of the run sits past you along the deck, so the diagonal cuts through you
                z = Mathf.Clamp(z + (z - from) * 0.6f + Random.Range(-6f, 6f), -arenaHalfLength + 6f, arenaHalfLength - 6f);
                to = arena.TransformPoint(new Vector3(side * (arenaHalfWidth - 8f), 0f, z));
            }
            to.y = Ground(to);
            Vector3 start = transform.position, dir = Flat(to - start);
            if (dir.sqrMagnitude < 4f) continue;
            var line = Line(start, to);
            var lr = line.GetComponent<LineRenderer>();
            for (float t = 0f; t < warn; t += Time.deltaTime)
            {
                FaceToward(to, 10f);
                lr.widthMultiplier = (0.3f + 1.2f * t / warn) * crawlHit * 0.25f;
                yield return null;
            }
            marks.Remove(line); Destroy(line);
            transform.rotation = Quaternion.LookRotation(dir);
            bool hit = false;
            float len = dir.magnitude;
            for (float d = 0f; d < len && !defeated; d += crawlSpeed * Time.deltaTime)
            {
                MoveTo(start + dir.normalized * d);
                if (!hit && Near(transform.position, crawlHit))
                {
                    hit = true;
                    Hurt(crawlDamage);
                    Vector3 away = Flat(Tgt.position - transform.position);
                    Knock((Vector3.Cross(Vector3.up, dir.normalized) * Mathf.Sign(Vector3.Dot(away, Vector3.Cross(Vector3.up, dir))) * 9f) + Vector3.up * 4f);   // thrown out of his path
                }
                if (Random.value < 6f * Time.deltaTime) BloodFx.Spray(transform.position + Vector3.up * 0.3f, Vector3.up - dir.normalized, 8, 4f);
                yield return null;
            }
            Boom(transform.position, 0.8f);
            yield return new WaitForSeconds(0.25f);
        }
        if (animator != null) animator.speed = 1f;
        Play(roarState);
        yield return new WaitForSeconds(0.8f);
    }

    // sealed and floating: one at a time from the air
    IEnumerator HoverAttack()
    {
        switch (hoverPick++ % 3)
        {
            case 0: yield return AimedShards(1, 5); break;
            case 1: yield return Bombs(); break;
            default: yield return Spray(); break;
        }
    }

    IEnumerator Shards()
    {
        if (phase <= 1) { yield return AimedShards(shardBurst, 3); yield break; }
        if (phase == 2) { yield return RingShards(2, ringShards); yield break; }
        yield return RingShards(1, ringShards + 4);
        yield return AimedShards(1, 5);
        yield return RingShards(2, ringShards + 4);
    }

    IEnumerator SmashAttack()
    {
        if (phase <= 1) return Smash(1, smashAngle, 1, true, 0f);
        if (phase == 2) return Smash(2, smashAngle + 20f, 2, true, 0f);
        return Smash(3, smashAngle + 10f, 2, false, 60f);
    }

    // ---------------------------------------------------------------- shards

    IEnumerator AimedShards(int bursts, int perBurst)
    {
        for (int b = 0; b < bursts && !defeated; b++)
        {
            if (b > 0) for (float t = 0f; t < shardEvery; t += Time.deltaTime) { FaceToward(Tgt.position, turnRate * 2f); yield return null; }
            Play(throwState);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) { FaceToward(Tgt.position, turnRate * 3f); yield return null; }
            Vector3 lead = Lead() * shardFlight;
            for (int i = 0; i < perBurst; i++)
            {
                Vector3 aim = ClampArena(Tgt.position + lead + (i == 0 ? Vector3.zero : Flat(Random.insideUnitSphere).normalized * 3f));
                aim.y = Ground(aim);
                StartCoroutine(Shard(Hand(), aim));
                yield return new WaitForSeconds(0.22f);
            }
        }
    }

    IEnumerator Shard(Vector3 from, Vector3 to)
    {
        var mark = Circle(to, shardHit);
        var s = MakeShard(from, to - from);
        for (float t = 0f; t < shardFlight; t += Time.deltaTime)
        {
            float k = t / shardFlight;
            Vector3 p = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2f;
            if (s != null) s.transform.SetPositionAndRotation(p, Quaternion.LookRotation(to - p + Vector3.down * 0.01f));
            SetCircle(mark, shardHit * Mathf.Lerp(0.4f, 1f, k), 0.2f + 0.3f * k);
            yield return null;
        }
        marks.Remove(mark); Destroy(mark);
        EndShard(s, to);
        if (Near(to, shardHit)) { Hurt(shardDamage); Knock(Flat(Tgt.position - to).normalized * 5f + Vector3.up * 2f); }
    }

    // a ring of shards fired out flat along the deck: the gaps between them widen the further out they get
    IEnumerator RingShards(int rings, int count)
    {
        float spin = Random.value * 360f;
        for (int r = 0; r < rings && !defeated; r++)
        {
            Play(castState);
            Roar(0.5f);
            Vector3 o = Flat(transform.position) + Vector3.up * (Ground(transform.position) + 1.1f);
            var dirs = new Vector3[count];
            var lines = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                dirs[i] = Quaternion.Euler(0f, spin + (i + r * 0.5f) * 360f / count, 0f) * Vector3.forward;
                Vector3 a = o + dirs[i] * 3f, b = o + dirs[i] * ringShardReach;
                a.y = b.y = o.y - 1.05f;
                lines.Add(Line(a, b));
            }
            for (float t = 0f; t < ringWarn; t += Time.deltaTime)
            {
                foreach (var l in lines) if (l != null) l.GetComponent<LineRenderer>().widthMultiplier = 0.08f + 0.25f * t / ringWarn;
                yield return null;
            }
            foreach (var l in lines) { marks.Remove(l); Destroy(l); }
            for (int i = 0; i < count; i++) StartCoroutine(RingShard(o, dirs[i]));
            yield return new WaitForSeconds(1.1f);
        }
    }

    IEnumerator RingShard(Vector3 from, Vector3 dir)
    {
        var s = MakeShard(from, dir);
        Vector3 p = from;
        for (float d = 2f; d < ringShardReach; d += ringShardSpeed * Time.deltaTime)
        {
            p = from + dir * d;
            if (s != null) s.transform.SetPositionAndRotation(p, Quaternion.LookRotation(dir));
            if (Near(p, 1.1f) && Mathf.Abs(Tgt.position.y + 1f - p.y) < 2.2f)
            {
                Hurt(shardDamage);
                Knock(dir * 6f + Vector3.up * 2f);
                break;
            }
            yield return null;
        }
        EndShard(s, p);
    }

    Vector3 Hand() => Centre() + Vector3.up * 1.5f + transform.forward * 1.5f;

    Vector3 Lead()
    {
        if (InVan && carRb != null) return Flat(carRb.linearVelocity);
        var body = player != null ? player.GetComponent<Rigidbody>() : null;
        return body != null ? Flat(body.linearVelocity) : Vector3.zero;
    }

    // the IceLance's own shard mesh, in blood, with the pack's mist trail following it
    GameObject MakeShard(Vector3 at, Vector3 dir)
    {
        var g = new GameObject("BloodShard");
        g.transform.SetPositionAndRotation(at, Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : transform.forward));
        fx.Add(g);
        if (shardMesh == null && iceLance != null)
        {
            var psr = iceLance.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.mesh != null)
            {
                shardMesh = psr.mesh;
                shardMat = BloodTint.Mat(psr.sharedMaterial, shardColor);
                Vector3 s = shardMesh.bounds.size;
                Vector3 axis = s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
                shardAlign = Quaternion.FromToRotation(axis, Vector3.forward);
                shardScale = shardLength / Mathf.Max(0.01f, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
            }
        }
        var body = shardMesh != null ? new GameObject("Shard") : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        if (shardMesh != null)
        {
            body.AddComponent<MeshFilter>().sharedMesh = shardMesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = shardMat;
            body.transform.localScale = Vector3.one * shardScale;
            body.transform.SetParent(g.transform, false);
            body.transform.localRotation = shardAlign;
            body.transform.localPosition = -(shardAlign * shardMesh.bounds.center) * shardScale;
        }
        else
        {
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = boltMat;
            body.transform.SetParent(g.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.35f, shardLength * 0.5f, 0.35f);
        }
        var l = g.AddComponent<Light>(); l.color = new Color(1f, 0.1f, 0.05f); l.intensity = 3f; l.range = 5f;
        if (iceLance != null) LanceFx(at, g.transform.rotation).transform.SetParent(g.transform, true);
        return g;
    }

    // the pack's IceLance without its own lances: the burst of tiny shards and mist, and the trail while it moves
    GameObject LanceFx(Vector3 at, Quaternion rot)
    {
        var f = BloodTint.Spawn(iceLance, at, rot, shardColor, 1f, 0f);
        var root = f.GetComponent<ParticleSystem>();
        if (root != null) { var e = root.emission; e.enabled = false; }
        fx.Add(f);
        return f;
    }

    // it shatters where it stops; the trail is let go to fade on its own
    void EndShard(GameObject s, Vector3 at)
    {
        if (s == null) return;
        foreach (var ps in s.GetComponentsInChildren<ParticleSystem>())
            if (ps.transform.parent == s.transform) { ps.transform.SetParent(null, true); ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); Destroy(ps.gameObject, 2.5f); }
        Destroy(s);
        if (iceLance != null) Destroy(LanceFx(at, Quaternion.LookRotation(Vector3.up)), 3f);
        BloodFx.Spray(at + Vector3.up * 0.2f, Vector3.up, 14, 3f);
    }

    // ---------------------------------------------------------------- smash

    // `cones` cones evenly round him, the first at you; `waves` times, each re-aimed at you or turned by `turn`
    IEnumerator Smash(int cones, float angle, int waves, bool reaim, float turn)
    {
        float yaw = YawTo(Tgt.position);
        for (int w = 0; w < waves && !defeated; w++)
        {
            if (w > 0) yaw = reaim ? YawTo(Tgt.position) : yaw + turn;
            Play(slamState);
            Vector3 o = Flat(transform.position) + Vector3.up * Ground(transform.position);
            var cms = new List<GameObject>();
            for (int c = 0; c < cones; c++) cms.Add(ConeMark(o, yaw + c * 360f / cones, angle, smashReach));
            float windup = slamWindup * (phase >= 3 ? 0.75f : 1f) * (w > 0 ? 0.7f : 1f);
            Vector3 face = o + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 5f;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                FaceToward(face, 8f);
                foreach (var m in cms) if (m != null) m.GetComponent<LineRenderer>().widthMultiplier = 0.15f + 0.4f * t / windup;
                yield return null;
            }
            foreach (var m in cms) { marks.Remove(m); Destroy(m); }

            bool hit = false;
            for (int c = 0; c < cones; c++)
            {
                float cy = yaw + c * 360f / cones;
                int lanes = Mathf.Max(1, Mathf.RoundToInt(angle / 14f));
                for (int l = 0; l < lanes && earthShatter != null; l++)
                {
                    float a = cy + (lanes == 1 ? 0f : Mathf.Lerp(-angle * 0.42f, angle * 0.42f, l / (float)(lanes - 1)));
                    Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    var g = BloodTint.Spawn(earthShatter, o + d * smashReach * 0.5f, Quaternion.LookRotation(d), smashColor, smashReach / 9.4f, 4f);
                    fx.Add(g);
                    StartCoroutine(StopEmitting(g, 0.7f));
                }
                if (!hit && InCone(Tgt.position, o, cy, angle, smashReach) && Grounded())
                {
                    hit = true;
                    Hurt(slamDamage);
                    Knock(Flat(Tgt.position - o).normalized * 8f + Vector3.up * 4f);
                }
            }
            Boom(o + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 3f, 1.4f);
            yield return new WaitForSeconds(w < waves - 1 ? slamRecover * 0.4f : slamRecover);
        }
    }

    float YawTo(Vector3 p) { Vector3 d = Flat(p - transform.position); return d.sqrMagnitude < 0.01f ? transform.eulerAngles.y : Quaternion.LookRotation(d).eulerAngles.y; }

    bool InCone(Vector3 p, Vector3 o, float yaw, float angle, float reach)
    {
        Vector3 v = Flat(p - o);
        float margin = InVan ? 1.8f : 0f;
        if (v.magnitude > reach + margin) return false;
        if (v.magnitude < 2.5f + margin) return true;
        return Vector3.Angle(v, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) < angle * 0.5f + (InVan ? 6f : 0f);
    }

    GameObject ConeMark(Vector3 o, float yaw, float angle, float reach)
    {
        var g = Line(o, o);
        var lr = g.GetComponent<LineRenderer>();
        const int arc = 20;
        lr.positionCount = arc + 2;
        lr.loop = true;
        lr.SetPosition(0, o + Vector3.up * 0.08f);
        for (int i = 0; i <= arc; i++)
            lr.SetPosition(i + 1, o + Vector3.up * 0.08f + Quaternion.Euler(0f, yaw - angle * 0.5f + angle * i / arc, 0f) * Vector3.forward * reach);
        return g;
    }

    IEnumerator StopEmitting(GameObject g, float after)
    {
        yield return new WaitForSeconds(after);
        if (g == null) yield break;
        foreach (var ps in g.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    // ---------------------------------------------------------------- corrupted blood

    IEnumerator Spray()
    {
        Play(roarState);
        Roar(0.8f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { FaceToward(Tgt.position, turnRate * 3f); yield return null; }
        Vector3 me = Flat(transform.position), you = Flat(Tgt.position) + Lead() * 0.6f;
        Vector3 dir = Flat(you - me).sqrMagnitude > 0.01f ? Flat(you - me).normalized : Flat(transform.forward).normalized;
        var spots = new List<(Vector3 at, float r)>();
        if (phase <= 1)
            for (int i = 0; i < 5; i++) spots.Add((Vector3.Lerp(me + dir * 6f, you + dir * 4f, i / 4f), 2.8f));
        else if (phase == 2)
        {
            spots.Add((you, hugePool));
            for (int i = 0; i < 2; i++) spots.Add((you + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(10f, 15f), hugePool * 0.85f));
        }
        else
        {
            for (int i = 0; i < 8; i++) spots.Add((me + Quaternion.Euler(0f, i * 45f + 22.5f, 0f) * dir * 11f, 3.2f));
            spots.Add((you, hugePool * 0.8f));
        }
        Vector3 mouth = Mouth();
        foreach (var (at, r) in spots)
        {
            StartCoroutine(BloodBlob(mouth, at, r));
            yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(0.6f);
    }

    Transform headBone;
    Vector3 Mouth()
    {
        if (headBone == null && model != null) headBone = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.ToLower().Contains("head") && t.GetComponent<Renderer>() == null);
        return headBone != null ? headBone.position + transform.forward * 1.5f : Centre() + Vector3.up * 2f + transform.forward;
    }

    // he vomits it: a gush of clotted globs thrown so they come down on the spot as the pool spreads
    IEnumerator BloodBlob(Vector3 from, Vector3 at, float r)
    {
        at = ClampArena(at);
        at.y = Ground(at);
        var mark = Circle(at, r);
        const float flight = 1.1f;
        Vomit(from, at, flight, Mathf.Clamp(r * 0.3f, 0.6f, 2f));
        for (float t = 0f; t < flight; t += Time.deltaTime)
        {
            float k = t / flight;
            SetCircle(mark, r * Mathf.Lerp(0.5f, 1f, k), 0.25f + 0.35f * k);
            yield return null;
        }
        marks.Remove(mark); Destroy(mark);
        BloodFx.Spray(at + Vector3.up * 0.2f, Vector3.up, 30, 4f, VomitMat());
        StartCoroutine(Pool(at, r));
    }

    // a stream of globs for 0.35 s, launched on the arc that lands on `to` after `flight` seconds; they splat on what they hit
    void Vomit(Vector3 from, Vector3 to, float flight, float thick)
    {
        const float grav = 1.2f;
        Vector3 v = (to - from - 0.5f * Physics.gravity * grav * flight * flight) / flight;
        var g = new GameObject("Vomit");
        g.transform.SetPositionAndRotation(from, Quaternion.LookRotation(v));
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // (stopped before the duration can change)
        var m = ps.main;
        m.duration = 0.35f; m.loop = false;
        m.startLifetime = flight + 1.5f;
        m.startSpeed = new ParticleSystem.MinMaxCurve(v.magnitude * 0.93f, v.magnitude * 1.05f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.35f * thick, 1.1f * thick);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        m.startColor = new ParticleSystem.MinMaxGradient(vomitColor, new Color(vomitColor.r * 0.35f, vomitColor.g * 0.6f, vomitColor.b * 0.3f));
        m.gravityModifier = grav;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = 600;
        var e = ps.emission; e.rateOverTime = 400f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 4f; sh.radius = 0.3f * thick;
        var col = ps.collision;   // it lands and stays a moment instead of falling through the deck
        col.enabled = true; col.type = ParticleSystemCollisionType.World; col.dampen = 0.95f; col.bounce = 0f; col.lifetimeLoss = 0.5f;
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.3f;
        r.sharedMaterial = VomitMat();
        ps.Play();
        fx.Add(g);
        Destroy(g, flight + 2f);
    }

    Material vomitMat;
    Material VomitMat()
    {
        if (vomitMat != null) return vomitMat;
        var src = goop != null ? goop.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(x => x.sharedMaterial).FirstOrDefault(x => x != null) : null;
        Color hue = vomitColor / Mathf.Max(0.0001f, vomitColor.maxColorComponent); hue.a = 1f;   // (the particles carry the darkness)
        vomitMat = src != null ? BloodTint.Mat(src, hue) :new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        return vomitMat;
    }

    // a pool of corrupted blood: burns while you stand (or drive) in it, toxic fumes rising off it
    IEnumerator Pool(Vector3 at, float r)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(g.GetComponent<Collider>());
        g.name = "Corrupted blood";
        var rend = g.GetComponent<Renderer>();
        rend.sharedMaterial = PoolMat();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.position = at + Vector3.up * 0.03f;
        g.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
        fx.Add(g);
        var fumes = new List<GameObject>();
        if (poisonGas != null)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(r * r / 9f), 1, 6);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = at + (n == 1 ? Vector3.zero : Flat(Random.insideUnitSphere) * r * 0.6f);
                var f = BloodTint.Spawn(poisonGas, p, poisonGas.transform.rotation, fumeColor, Mathf.Clamp(r / 2.5f, 0.8f, 1.6f), 0f, false);
                fumes.Add(f); fx.Add(f);
            }
        }
        for (float t = 0f; t < poolLife && g != null; t += Time.deltaTime)
        {
            float grow = Mathf.Min(1f, t / 0.4f) * Mathf.Clamp01((poolLife - t) / 1.2f);
            g.transform.localScale = new Vector3(r * 2f * grow, 0.015f, r * 2f * grow);
            if (t > poolLife - 1.5f) foreach (var f in fumes) if (f != null) foreach (var ps in f.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (Time.time >= poolHitAt && grow > 0.5f && Near(at, r * 0.9f) && Grounded())
            {
                poolHitAt = Time.time + 0.5f;   // one tick for all the pools you're in
                Hurt(poolDps * 0.5f);
                if (!InVan) BloodFx.Spray(Tgt.position, Vector3.up, 4, 1.5f);
            }
            yield return null;
        }
        if (g != null) Destroy(g);
        foreach (var f in fumes) if (f != null) Destroy(f, 2f);
    }

    Material PoolMat()
    {
        if (poolMat != null) return poolMat;
        poolMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        poolMat.SetColor("_BaseColor", poolColor);
        poolMat.SetFloat("_Smoothness", 0.9f);   // wet
        poolMat.EnableKeyword("_EMISSION");
        poolMat.SetColor("_EmissionColor", new Color(0.25f, 0.01f, 0.02f));   // a dull glow: it reads in the dark
        return poolMat;
    }

    // ---------------------------------------------------------------- body-part bombs

    IEnumerator Bombs()
    {
        Play(throwState);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { FaceToward(Tgt.position, turnRate * 3f); yield return null; }
        Vector3 you = Tgt.position + Lead() * bombFlight * 0.8f;
        Vector3 side = Vector3.Cross(Vector3.up, Flat(you - transform.position).normalized);
        var aims = new List<Vector3>();
        bool split = phase >= 3;
        if (phase <= 1) { aims.Add(you); aims.Add(you + side * 4.5f); aims.Add(you - side * 4.5f); }
        else if (phase == 2) { aims.Add(you); for (int i = 0; i < 6; i++) aims.Add(you + Quaternion.Euler(0f, i * 60f, 0f) * side * 7f); }
        else { aims.Add(you); aims.Add(you + side * 6f); aims.Add(you - side * 6f); aims.Add(you + Flat(you - transform.position).normalized * 6f); }
        foreach (var a in aims)
        {
            StartCoroutine(Bomb(Hand(), a, bombSize, bombBlast, split));
            yield return new WaitForSeconds(0.15f);
        }
        yield return new WaitForSeconds(0.4f);
    }

    IEnumerator Bomb(Vector3 from, Vector3 to, float size, float blast, bool split)
    {
        to = ClampArena(to);
        to.y = Ground(to);
        var orb = MakeOrb(size);
        orb.transform.position = from;
        int n =bodyParts != null && bodyParts.Length > 0 ? Random.Range(3, 5) : 0;
        for (int i = 0; i < n; i++)
        {
            var src = bodyParts[Random.Range(0, bodyParts.Length)];
            if (src == null) continue;
            var p = Instantiate(src, from + Random.onUnitSphere * size * 0.8f, Random.rotation);
            p.name = "BombPart";
            p.transform.localScale = src.transform.localScale * size * Random.Range(0.8f, 1.2f);
            foreach (var c in p.GetComponentsInChildren<Collider>(true)) Destroy(c);
            p.transform.SetParent(orb.transform, true);
        }
        var mark = Circle(to, blast);
        float flight = bombFlight * (split ? 1f : Mathf.Lerp(0.7f, 1f, size / Mathf.Max(0.01f, bombSize)));
        for (float t = 0f; t < flight && orb != null; t += Time.deltaTime)
        {
            float k = t / flight;
            orb.transform.position = Vector3.Lerp(from, to + Vector3.up * size, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * (size < bombSize ? 6f : 13f);
            orb.transform.Rotate(220f * Time.deltaTime, 90f * Time.deltaTime, 0f);
            SetCircle(mark, blast * Mathf.Lerp(0.5f, 1f, k), 0.3f + 0.4f * k);
            yield return null;
        }
        marks.Remove(mark); Destroy(mark);
        Blast(to, blast, orb);
        if (split)
            for (int i = 0; i < 3; i++)
                StartCoroutine(Bomb(to + Vector3.up, to + Quaternion.Euler(0f, i * 120f + Random.Range(-20f, 20f), 0f) * Vector3.forward * 6f, size * 0.6f, blast * 0.65f, false));
    }

    // a red blast of blood: the parts in it fly out across the deck
    void Blast(Vector3 at, float r, GameObject orb)
    {
        if (bigBlast != null) fx.Add(BloodTint.Spawn(bigBlast, at + Vector3.up * 0.5f, Quaternion.identity, bombColor, r / 3f, 5f));
        BloodFx.Spray(at + Vector3.up * 0.5f, Vector3.up, Mathf.RoundToInt(20 * r), 2f * r);
        Boom(at, r / 4f);
        if (orb != null)
        {
            foreach (Transform c in orb.transform.Cast<Transform>().ToArray())
            {
                if (c.name != "BombPart") continue;
                c.SetParent(null, true);
                var rb = c.gameObject.AddComponent<Rigidbody>();
                rb.mass = 20f;
                rb.linearVelocity = (Flat(c.position - at).normalized + Vector3.up * 1.2f) * Random.Range(5f, 10f);
                rb.angularVelocity = Random.insideUnitSphere * 8f;
                c.gameObject.AddComponent<SphereCollider>().radius = 0.3f;
                fx.Add(c.gameObject);
                Destroy(c.gameObject, 6f);
            }
            orbs.Remove(orb); Destroy(orb);
        }
        if (Near(at, r)) { Hurt(bombDamage); Knock(Flat(Tgt.position - at).normalized * 7f + Vector3.up * 3f); }
        foreach (var d in drowned)
            if (d.z != null && !d.z.IsDead && Flat(d.t.position - at).magnitude < r) d.z.KillInstant(d.t.position, Vector3.up * 300f);
    }

    // ---------------------------------------------------------------- shared

    bool Near(Vector3 p, float r) => Tgt != null && Flat(Tgt.position - p).magnitude < r + (InVan ? 1.6f : 0f);

    Vector3 ClampArena(Vector3 p)
    {
        if (arena == null) return p;
        Vector3 l = arena.InverseTransformPoint(p);
        return arena.TransformPoint(new Vector3(Mathf.Clamp(l.x, -arenaHalfWidth + 2f, arenaHalfWidth - 2f), l.y, Mathf.Clamp(l.z, -arenaHalfLength + 2f, arenaHalfLength - 2f)));
    }

    GameObject Line(Vector3 a, Vector3 b)
    {
        var g = new GameObject("AttackMark");
        marks.Add(g);
        var lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = true; lr.positionCount = 2;
        lr.sharedMaterial = ringMat; lr.widthMultiplier = 0.1f;
        lr.SetPosition(0, a + Vector3.up * 0.08f); lr.SetPosition(1, b + Vector3.up * 0.08f);
        return g;
    }

    void ClearBlood()
    {
        foreach (var g in fx) if (g != null) Destroy(g);
        fx.Clear();
    }
}
