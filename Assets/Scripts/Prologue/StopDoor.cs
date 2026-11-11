using System.Collections;
using UnityEngine;

// A door in the building behind a delivery customer (DeliveryRun builds one per stop): a frame, a wooden leaf on a
// hinge, and - behind it, on the wall - a black rectangle for the dark inside. After the hand-over the customer
// walks back to it, opens it, goes in and shuts it behind them, instead of the narration doing it for them.
// The door has no colliders: it is only ever walked through by the customer, in the cutscene.
public class StopDoor : MonoBehaviour
{
    public Transform leaf;                  // pivots on its hinge edge
    public float openAngle = 105f;          // outwards, into the street

    // local space: +z out into the street, +x to the door's right (as it faces the street); the hinge is on -x
    public Vector3 Stand => transform.TransformPoint(new Vector3(0.85f, 0f, 0.75f));    // where they wait while it's opened (clear of the swing)
    public Vector3 Doorway => transform.TransformPoint(new Vector3(0f, 0f, 0.7f));      // in front of the opening
    public Vector3 Inside => transform.TransformPoint(new Vector3(0f, 0f, -0.35f));     // in through the doorway

    AudioClip creak, thud;

    // finds a flat stretch of building wall behind `customer` (away from the bay) and puts a door on it; null if there isn't one
    public static StopDoor Build(Transform customer, Vector3 bay, bool cosyInside = false)
    {
        Vector3 origin = customer.position + Vector3.up * 1.2f;
        Vector3 back = customer.position - bay; back.y = 0f; back.Normalize();
        float bestDist = float.MaxValue; Vector3 bestPoint = default, bestNormal = default;
        for (int a = -70; a <= 70; a += 5)
        {
            Vector3 dir = Quaternion.AngleAxis(a, Vector3.up) * back;
            if (!Physics.Raycast(origin, dir, out var hit, 22f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (hit.collider.transform.IsChildOf(customer)) continue;
            Vector3 n = hit.normal; n.y = 0f;
            if (n.sqrMagnitude < 0.5f) continue;
            n.Normalize();
            if (Vector3.Dot(n, -dir) < 0.85f) continue;                 // a wall facing us, not a corner or a post
            // flat and wide enough for a door: two more rays a metre either side land at about the same distance
            Vector3 side = Vector3.Cross(Vector3.up, n);
            bool wide = true;
            foreach (float s in new[] { -0.7f, 0.7f })
            {
                Vector3 from = hit.point + n * 0.6f + side * s;
                wide &= Physics.Raycast(from, -n, out var h2, 1.2f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(Vector3.Dot(h2.point - hit.point, n)) < 0.3f;
            }
            if (!wide) continue;
            if (hit.distance < bestDist) { bestDist = hit.distance; bestPoint = hit.point; bestNormal = n; }
        }
        if (bestDist == float.MaxValue) return null;

        // the ground in front of it
        Vector3 foot = bestPoint + bestNormal * 0.8f;
        float y = customer.position.y;
        if (Physics.Raycast(foot + Vector3.up * 2f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) y = g.point.y;

        var root = new GameObject("Door (" + customer.name + ")");
        root.transform.SetPositionAndRotation(new Vector3(bestPoint.x, y, bestPoint.z), Quaternion.LookRotation(bestNormal));
        var door = root.AddComponent<StopDoor>();
        door.Make(cosyInside);
        return door;
    }

    void Make(bool cosyInside)
    {
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Material M(Shader sh, Color c) { var m = new Material(sh); m.SetColor("_BaseColor", c); return m; }
        Transform Part(string name, PrimitiveType type, Vector3 pos, Vector3 size, Material m, Transform parent = null)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent != null ? parent : transform, false);
            p.transform.localPosition = pos; p.transform.localScale = size;
            var r = p.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p.transform;
        }

        // the inside: black, or (Ms Okafor only) the cosy Scandi room, on a rectangle in the wall bigger than the door
        // so no wall shows round it
        var inside = M(unlit, Color.black);
        var room = cosyInside ? Resources.Load<Texture2D>("ScandiRoom") : null;
        if (room != null)
        {
            inside.SetColor("_BaseColor", Color.white);
            inside.SetTexture("_BaseMap", room);
            // centre-crop the picture to the door's shape instead of squashing it
            float k = (1.15f / 2.25f) / ((float)room.width / room.height);
            inside.SetTextureScale("_BaseMap", new Vector2(k, 1f));
            inside.SetTextureOffset("_BaseMap", new Vector2((1f - k) * 0.5f, 0f));
        }
        var quad = Part("Inside", PrimitiveType.Quad, new Vector3(0f, 1.1f, 0.03f), new Vector3(1.15f, 2.25f, 1f), inside);
        quad.localRotation = Quaternion.Euler(0f, 180f, 0f);   // a quad faces -z: turn it out into the street

        var frame = M(lit, new Color(0.17f, 0.12f, 0.09f));
        Part("Frame left", PrimitiveType.Cube, new Vector3(-0.6f, 1.1f, 0.06f), new Vector3(0.1f, 2.3f, 0.12f), frame);
        Part("Frame right", PrimitiveType.Cube, new Vector3(0.6f, 1.1f, 0.06f), new Vector3(0.1f, 2.3f, 0.12f), frame);
        Part("Frame top", PrimitiveType.Cube, new Vector3(0f, 2.25f, 0.06f), new Vector3(1.3f, 0.12f, 0.12f), frame);
        Part("Step", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.2f), new Vector3(1.3f, 0.1f, 0.4f), M(lit, new Color(0.3f, 0.29f, 0.28f)));

        // the leaf: hinged on its -x edge, so its pivot sits there and it hangs out to +x
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(transform, false);
        hinge.localPosition = new Vector3(-0.52f, 0f, 0.1f);
        leaf = hinge;
        var wood = M(lit, new Color(0.38f, 0.2f, 0.12f));
        Part("Leaf", PrimitiveType.Cube, new Vector3(0.52f, 1.08f, 0f), new Vector3(1.04f, 2.15f, 0.06f), wood, hinge);
        Part("Knob", PrimitiveType.Sphere, new Vector3(0.9f, 1.05f, 0.05f), Vector3.one * 0.08f, M(lit, new Color(0.7f, 0.55f, 0.2f)), hinge);
    }

    void Awake()
    {
        creak = Sounds.Clip("Prologue/Door creak", ProceduralAudio.Creak(0.9f));
        thud = Sounds.Clip("Prologue/Door flung open", ProceduralAudio.Thud());
    }

    public IEnumerator Open(float time = 0.9f)
    {
        if (creak != null) AudioSource.PlayClipAtPoint(creak, transform.position + Vector3.up, 0.5f);
        yield return Swing(0f, -openAngle, time);
    }

    public IEnumerator Close(float time = 0.7f)
    {
        yield return Swing(-openAngle, 0f, time);
        if (thud != null) AudioSource.PlayClipAtPoint(thud, transform.position + Vector3.up, 0.35f);
    }

    IEnumerator Swing(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            leaf.localRotation = Quaternion.Euler(0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time)), 0f);
            yield return null;
        }
        leaf.localRotation = Quaternion.Euler(0f, to, 0f);
    }
}
