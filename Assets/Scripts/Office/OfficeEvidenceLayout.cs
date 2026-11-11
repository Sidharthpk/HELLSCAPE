using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fits clue props to a real horizontal collider. Missing surfaces get a plain table,
// rather than leaving a floating clue. No story collider, event, or clue ID is replaced.
public class OfficeEvidenceLayout : MonoBehaviour
{
    public readonly List<Transform> Placed = new List<Transform>();
    Material wood;
    IEnumerator Start()
    {
        yield return null;
        Physics.SyncTransforms();
        foreach (var item in RevisedPlot.All<Interactable>(gameObject.scene))
        {
            bool small = item.name == "IncidentReport" || item.name == "KesslerLedger" || item.name == "CleaningRota" || item.name == "YourDesk_Note";
            bool box = item.name == "KEEP COLD - collection";
            bool phone = item.name == "Vane's Echo telephone";
            if (!small && !box && !phone) continue;
            if (item.name == "CleaningRota") { Placed.Add(item.transform); continue; }   // pinned to the wall: it needs no table under it
            Place(item.transform, box || phone);
        }
    }

    void Place(Transform item, bool needsTable)
    {
        var own = item.GetComponent<Collider>();
        if (own == null) return;
        Bounds bounds = item.TryGetComponent<Renderer>(out var visual) ? visual.bounds : own.bounds;
        if (needsTable && FindStand(item, bounds, out var stand))
        {
            item.position += stand - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Physics.SyncTransforms();
            bounds = visual != null ? visual.bounds : own.bounds;
        }
        Vector3 center = bounds.center;
        float top = bounds.min.y;
        if (needsTable)
        {
            // The container used to sit on the floor; the phone was suspended at desk height.
            top = bounds.min.y;
        }
        Vector3 half = bounds.extents;
        bool Supported(float height)
        {
            foreach (var offset in new[] { Vector3.zero, new Vector3(half.x * .8f, 0, half.z * .8f), new Vector3(-half.x * .8f, 0, -half.z * .8f) })
            {
                bool found = false;
                foreach (var hit in Physics.RaycastAll(new Vector3(center.x, height + .12f, center.z) + offset, Vector3.down, .55f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(item) || hit.collider.GetComponentInParent<Interactable>() != null || hit.normal.y < .95f) continue;
                    if (Mathf.Abs(hit.point.y - height) < .08f) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }
        if (!Supported(top))
        {
            // Search immediately below for an authored desk before adding a stand-in.
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(new Vector3(center.x, top + .15f, center.z), Vector3.down, .55f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(item) && hit.collider.GetComponentInParent<Interactable>() == null && hit.normal.y > .95f && hit.point.y > best)
                    best = hit.point.y;
            if (best > top - .35f && Supported(best)) top = best;
            else MakeTable(new Vector3(center.x, top, center.z), new Vector2(Mathf.Max(1.15f, bounds.size.x + .25f), Mathf.Max(.7f, bounds.size.z + .25f)));
        }
        item.position += Vector3.up * (top + .008f - bounds.min.y);
        Placed.Add(item);
        Physics.SyncTransforms();
    }

    bool FindStand(Transform item, Bounds bounds, out Vector3 stand)
    {
        stand = default;
        // Search free floor near the authored clue, leaving a full standing approach beside it.
        // pass 0: a spot against a wall with a wide clear margin (never in the walking path); pass 1: any clear spot
        for (int pass = 0; pass < 2; pass++) for (int ring = 1; ring <= 8; ring++) for (int angle = 0; angle < 16; angle++)
        {
            Vector3 offset = Quaternion.Euler(0f, angle * 22.5f, 0f) * Vector3.forward * (ring * .5f);
            Vector3 at = bounds.center + offset;
            float floor = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(item) && hit.normal.y > .95f && hit.point.y < bounds.min.y + .1f)
                    floor = Mathf.Min(floor, hit.point.y);
            if (float.IsInfinity(floor)) continue;
            Vector3 top = new Vector3(at.x, floor + .8f, at.z);
            Vector3 half = new Vector3(Mathf.Max(.6f, bounds.extents.x + .15f), .65f, Mathf.Max(.38f, bounds.extents.z + .15f));
            bool clear = true;
            if (pass == 0)
            {
                bool wall = false;
                foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    if (Physics.Raycast(top + Vector3.up * .4f, dir, out var w, Mathf.Max(half.x, half.z) + .35f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(w.normal.y) < .3f && !w.transform.IsChildOf(item)) { wall = true; break; }
                if (!wall) continue;
            }
            foreach (var c in Physics.OverlapBox(top + Vector3.up * .3f, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(item)) clear = false;
            foreach (var g in RevisedPlot.All<GhostNPC>(gameObject.scene))
                if (Vector2.Distance(new Vector2(top.x, top.z), new Vector2(g.transform.position.x, g.transform.position.z)) < 1.1f) clear = false;
            if (!clear) continue;
            Vector3 approach = top + offset.normalized * (half.z + .75f);
            approach.y = floor + 1f;
            if (Physics.CheckCapsule(approach - Vector3.up * .5f, approach + Vector3.up * .5f, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            stand = top; return true;
        }
        Debug.LogWarning("No clear evidence stand found for " + item.name);
        return false;
    }

    void MakeTable(Vector3 top, Vector2 size)
    {
        if (wood == null) { wood = new Material(Shader.Find("Universal Render Pipeline/Lit")); wood.color = new Color(.24f, .19f, .15f); }
        void Part(string name, Vector3 at, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(transform); go.transform.position = at; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = wood;
        }
        Part("Evidence table top", top - Vector3.up * .045f, new Vector3(size.x, .09f, size.y));
        // each leg runs down to the lowest upward-facing surface under its own corner (the floor), not whatever is under the middle
        foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
        {
            Vector3 corner = top + new Vector3(x * (size.x * .5f - .08f), 0f, z * (size.y * .5f - .08f));
            float floorY = top.y - .8f;   // (no floor found: a normal table height)
            float lowest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(corner - Vector3.up * .1f, Vector3.down, 1.8f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.normal.y > .7f && hit.point.y < top.y - .2f && hit.collider.GetComponentInParent<Interactable>() == null) lowest = Mathf.Min(lowest, hit.point.y);
            if (!float.IsInfinity(lowest)) floorY = lowest;
            float height = top.y - floorY + .02f;   // a hair into the floor so there's no gap
            Part("Evidence table leg", corner + Vector3.down * (height * .5f), new Vector3(.07f, height, .07f));
        }
    }
    void OnDestroy() { if (wood != null) Destroy(wood); }
}
