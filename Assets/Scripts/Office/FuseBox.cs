using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The office fuse box (the user's FuseBox model). Its loose fuses are modelled sitting in their own slots, so a
// fuse is "in" when its transform is back at `seatedPos` / `seatedRot`. Picking one up hides it; each fuse you put
// in slides out of the air in front of the box into its slot with a click, then counts toward the breaker.
public class FuseBox : MonoBehaviour
{
    public Transform[] fuses;               // the pickups, in slot order
    public Vector3 seatedPos;               // pose that seats every fuse in its own slot
    public Quaternion seatedRot;
    public Vector3 outward = Vector3.left;  // the box's front: fuses come in from this side
    public float slideTime = 0.7f;
    public PuzzleCounter counter;
    public AudioSource sfx;
    public AudioClip clunk;                 // procedural stand-in if empty

    private readonly Queue<int> picked = new Queue<int>();
    private int pending;
    private bool busy;

    public void PickUp(int i)
    {
        if (i < 0 || i >= fuses.Length || fuses[i] == null) return;
        fuses[i].gameObject.SetActive(false);
        picked.Enqueue(i);
    }

    // the breaker's Interactable has already taken a fuse from the inventory: queue it, so a quick second press
    // while one is still sliding in isn't lost
    public void InsertNext()
    {
        pending++;
        if (!busy) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        busy = true;
        clunk = Sounds.Clip("Office/Fuse clunk", clunk != null ? clunk : ProceduralAudio.Thud());
        while (pending > 0)
        {
            pending--;
            if (picked.Count > 0)
            {
                var f = fuses[picked.Dequeue()];
                Vector3 from = seatedPos + outward.normalized * 0.45f;
                f.SetPositionAndRotation(from, seatedRot);
                f.gameObject.SetActive(true);
                for (float t = 0f; t < slideTime; t += Time.deltaTime)
                {
                    f.position = Vector3.Lerp(from, seatedPos, Mathf.SmoothStep(0f, 1f, t / slideTime));
                    yield return null;
                }
                f.SetPositionAndRotation(seatedPos, seatedRot);
            }
            if (sfx != null) sfx.PlayOneShot(clunk, 0.7f * Sounds.Volume("Office/Fuse clunk"));
            if (counter != null) counter.Add();
            yield return new WaitForSeconds(0.15f);
        }
        busy = false;
    }
}
