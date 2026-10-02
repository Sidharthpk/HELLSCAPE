using UnityEngine;

public class PunchController : MonoBehaviour
{
    public Animator armsAnimator;
    public Transform cam;
    private bool useLeftNext = true;

    void Update()
    {
        if (GunController.Equipped) return;
        if (Input.GetMouseButtonDown(0))
        {
            Punch();
        }
    }

    // fists down, out of the way (lying in the street after the fall): the first frame of the draw, held
    public void HoldDown()
    {
        if (armsAnimator == null) return;
        armsAnimator.Play("GuardDraw", 0, 0f);
        armsAnimator.Update(0f);
        armsAnimator.speed = 0f;
    }

    // back on your feet: fists come up into the guard
    public void Draw()
    {
        if (armsAnimator == null) return;
        armsAnimator.speed = 1f;
        armsAnimator.Play("GuardDraw", 0, 0f);
    }

    void Punch()
    {
        // one trigger at a time: a leftover one would fire a second jab on its own
        armsAnimator.ResetTrigger(useLeftNext ? "JabR" : "JabL");
        armsAnimator.SetTrigger(useLeftNext ? "JabL" : "JabR");
        useLeftNext = !useLeftNext;

        // nearest hit that isn't our own body (the camera sits at the edge of the player's capsule)
        var hits = Physics.SphereCastAll(cam.position, 0.4f, cam.forward, 1.5f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(cam.root)) continue;

            GateButton button = hit.collider.GetComponentInParent<GateButton>();
            if (button != null)
            {
                button.OnPunched();
                return;
            }

            DemonBoss demon = hit.collider.GetComponentInParent<DemonBoss>();
            if (demon != null)
            {
                demon.TakeDamage(15f, hit.point, cam.forward, hit.collider);
                return;
            }

            Zombie z = hit.collider.GetComponentInParent<Zombie>();
            if (z != null)
                z.TakeDamage(15, hit.point, cam.forward * 300f);
            return;
        }
    }
}
