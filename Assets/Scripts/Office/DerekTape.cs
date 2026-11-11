using System.Collections;
using UnityEngine;

// CCTV: Derek in the security room, the night of the fire. He walks in, stands at the desk watching the screens,
// takes the file and runs out. Two Dereks (his walk and his run are different rigs): the runner is swapped in to leave.
// The player stands at this very desk while it plays, so the player's own renderers are hidden for the tape.
public class DerekTape : MonoBehaviour
{
    public Animator walker, runner;
    public Transform[] walkIn;      // door -> desk
    public Transform[] runOut;      // desk -> door -> away
    public GameObject file;         // the folder on the desk; rides his hand once he takes it
    public Transform player;
    public float walkSpeed = 1.2f, runSpeed = 4.2f, watch = 3.5f;
    public string seen = "A3V6_tape_derek", after = "A3V6_derek_after";

    Vector3 fileAt; Quaternion fileRot; Transform fileParent;

    void Awake()
    {
        GetComponent<StabCutscene>().sequence = Beats;
        if (file != null) { fileParent = file.transform.parent; fileAt = file.transform.position; fileRot = file.transform.rotation; }
    }

    IEnumerator Beats()
    {
        var hidden = new System.Collections.Generic.List<Renderer>();
        if (player != null) foreach (var r in player.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
        if (file != null) { file.transform.SetParent(fileParent, true); file.transform.SetPositionAndRotation(fileAt, fileRot); file.SetActive(true); }
        runner.gameObject.SetActive(false);
        walker.gameObject.SetActive(true);
        walker.transform.position = walkIn[0].position;
        walker.speed = 1f; walker.Play("Walk", 0, 0f);
        for (int i = 1; i < walkIn.Length; i++) yield return Move(walker.transform, walkIn[i].position, walkSpeed);
        walker.speed = 0f;   // stood at the desk, looking at the screens
        Vector3 desk = fileAt - walker.transform.position; desk.y = 0f;
        if (desk.sqrMagnitude > .01f) walker.transform.rotation = Quaternion.LookRotation(desk);
        DialogueBox.Instance?.Say(HS5.S(seen));
        yield return new WaitForSeconds(watch);
        // he takes the file and runs
        runner.transform.SetPositionAndRotation(walker.transform.position, walker.transform.rotation);
        walker.gameObject.SetActive(false);
        runner.gameObject.SetActive(true);
        runner.speed = 1f; runner.Play("Run", 0, 0f);
        if (file != null)
        {
            var hand = runner.isHuman ? runner.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null) foreach (var t in runner.GetComponentsInChildren<Transform>()) if (t.name.EndsWith("RightHand")) { hand = t; break; }
            if (hand != null) { file.transform.SetParent(hand, true); file.transform.position = hand.position; }
            else file.SetActive(false);
        }
        foreach (var p in runOut) yield return Move(runner.transform, p.position, runSpeed);
        runner.gameObject.SetActive(false);
        yield return new WaitForSeconds(.6f);
        DialogueBox.Instance?.Say(HS5.S(after));
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
        if (file != null) { file.transform.SetParent(fileParent, true); file.SetActive(false); }
        foreach (var r in hidden) if (r != null) r.enabled = true;
    }

    static IEnumerator Move(Transform t, Vector3 to, float speed)
    {
        to.y = t.position.y;
        while ((t.position - to).sqrMagnitude > .01f)
        {
            Vector3 d = to - t.position;
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(d), 10f * Time.deltaTime);
            t.position = Vector3.MoveTowards(t.position, to, speed * Time.deltaTime);
            yield return null;
        }
    }
}
