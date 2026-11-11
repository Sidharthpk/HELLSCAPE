using System.Collections;
using UnityEngine;

// CCTV: Kessler and Mercer on the silo stairs after Tomas (StabCutscene runs this in place of its timer).
// Mercer stands on the landing facing the lens; Kessler is just out of shot below the camera, only his voice.
// "Do we delete the footage?" - "No need. Burn this whole place down and come with me. Make sure no one has seen it."
public class StairsTape : MonoBehaviour
{
    public Animator mercer;
    public string state = "Idle";
    public string lines = "A3V6_tape_stairs";
    public float holdAfter = 1.2f;

    Vector3 at; Quaternion rot;

    void Awake()
    {
        GetComponent<StabCutscene>().sequence = Beats;
        if (mercer != null) { at = mercer.transform.position; rot = mercer.transform.rotation; }
    }

    IEnumerator Beats()
    {
        if (mercer != null)
        {
            mercer.transform.SetPositionAndRotation(at, rot);
            mercer.gameObject.SetActive(true);
            mercer.Play(state, 0, 0f); mercer.Update(0f); mercer.speed = 0f;   // (his only "idle" is the first frame of the aim)
        }
        yield return new WaitForSeconds(.8f);
        DialogueBox.Instance?.Say(HS5.S(lines));
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
        yield return new WaitForSeconds(holdAfter);
    }
}
