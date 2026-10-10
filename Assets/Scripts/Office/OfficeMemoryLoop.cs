using System.Collections.Generic;
using UnityEngine;

// One impossible return along a route the player has already walked. Never changes quest geometry.
public class OfficeMemoryLoop : MonoBehaviour
{
    OfficeHorrorDirector horror;
    RevisedPlotDirector plot;
    FirstPersonController mover;
    readonly Queue<Vector3> trail = new Queue<Vector3>();
    Vector3 last;
    float distance;
    bool used;
    public void Initialize(OfficeHorrorDirector h, RevisedPlotDirector p, FirstPersonController f)
    { horror = h; plot = p; mover = f; if (f != null) last = f.transform.position; }
    void Update()
    {
        if (used || mover == null || horror.Stage != 4 || !plot.Echo || !mover.enabled || PauseMenu.Paused
            || GhostNPC.Conversing || NoteViewer.Blocking || (DialogueBox.Instance != null && DialogueBox.Instance.Busy))
        { trail.Clear(); distance = 0f; if (mover != null) last = mover.transform.position; return; }
        Vector3 now = mover.transform.position;
        float step = Vector3.Distance(last, now);
        if (step < 1f) return;
        if (step > 3f || Mathf.Abs(now.y - last.y) > .3f) { trail.Clear(); distance = 0f; last = now; return; }
        trail.Enqueue(last); distance += step; last = now;
        if (distance < 12f || trail.Count < 6) return;
        Vector3 back = trail.Peek();
        foreach (var c in Physics.OverlapCapsule(back + Vector3.down * .5f, back + Vector3.up * .5f, .35f, ~0, QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(mover.transform)) { trail.Dequeue(); return; }
        used = true;
        mover.transform.position = back;
        if (mover.TryGetComponent<Rigidbody>(out var rb)) { rb.position = back; rb.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms();
        DialogueBox.Instance?.Say("I've already passed this door. The corridor is looping.");
    }
}
