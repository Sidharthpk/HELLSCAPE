using System.Collections;
using UnityEngine;

// Second recovered recording. StabCutscene supplies the existing CCTV camera, tape look, fades and input lock.
// The cast and doorway are visual copies of the authored office, isolated from all gameplay interactions.
public sealed class OfficeEntryTape : MonoBehaviour
{
    public Animator mercer, door;
    public Transform[] route;
    public float walkSpeed = 1.15f;
    public float clipLength = 7.5f, openFrom = 1f, openTo = 3.1f, doorSpeed = 2.5f;
    public float openingPause = .65f, closingHold = 1.3f;
    public bool EnteredOffice { get; private set; }
    public bool WalkFinished { get; private set; }

    void Awake() => GetComponent<StabCutscene>().sequence = Beats;

    IEnumerator Beats()
    {
        EnteredOffice = WalkFinished = false;
        mercer.transform.SetPositionAndRotation(route[0].position, route[0].rotation);
        mercer.applyRootMotion = false; mercer.speed = 1f;
        mercer.Play("Walk", 0, 0f);
        PoseDoor(0f);
        yield return new WaitForSeconds(openingPause);
        yield return WalkTo(route[1].position);
        mercer.speed = 0f;
        if (door != null)
        {
            door.Play("Open", 0, openFrom / clipLength); door.speed = doorSpeed;
            yield return new WaitForSeconds(Mathf.Max(.1f, (openTo - openFrom) / doorSpeed));
            PoseDoor(openTo / clipLength);
        }
        mercer.speed = 1f;
        yield return WalkTo(route[2].position);
        EnteredOffice = true;
        for (int i = 3; i < route.Length; i++) yield return WalkTo(route[i].position);
        mercer.Play("Idle", 0, 0f); mercer.Update(0f); mercer.speed = 0f;
        WalkFinished = true;
        yield return new WaitForSeconds(closingHold);
    }
    IEnumerator WalkTo(Vector3 position)
    {
        while ((mercer.transform.position - position).sqrMagnitude > .0025f)
        {
            Vector3 delta = position - mercer.transform.position; delta.y = 0f;
            if (delta.sqrMagnitude > .001f)
                mercer.transform.rotation = Quaternion.Slerp(mercer.transform.rotation, Quaternion.LookRotation(delta), 8f * Time.deltaTime);
            mercer.transform.position = Vector3.MoveTowards(mercer.transform.position, position, walkSpeed * Time.deltaTime);
            yield return null;
        }
    }
    void PoseDoor(float normalized)
    {
        if (door == null) return;
        door.Play("Open", 0, normalized); door.speed = 0f; door.Update(0f);
    }
}
