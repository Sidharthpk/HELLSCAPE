using System.Collections;
using UnityEngine;

// Short in-engine cutscene: lock the player, letterbox, turn the player camera toward a target
// and zoom in (FOV) while a line of dialogue plays, then ease back and hand control back.
public class GateCutscene : MonoBehaviour
{
    public Camera cam;
    public Transform lookTarget;
    public Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
    public float zoomFov = 14f;
    public float fogDensityDuring = 0.012f;  // thin the fog so the target reads at a distance

    [Header("Timing")]
    public float delay = 0.3f;
    public float moveIn = 1.6f;
    public float hold = 5f;
    public float moveOut = 1.2f;

    [Header("While playing")]
    public MonoBehaviour[] disableDuring;   // movement, punching
    public GameObject[] hideDuring;         // HUD, crosshair, arms
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;
    public float letterboxHeight = 130f;

    [TextArea] public string line = "Something's moving down there...|...wait. What is that?";

    private bool played;

    public void Play()
    {
        if (played) return;
        played = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        foreach (var b in disableDuring) if (b != null) b.enabled = false;
        foreach (var g in hideDuring) if (g != null) g.SetActive(false);

        Quaternion startRot = cam.transform.rotation;
        Quaternion startLocal = cam.transform.localRotation;
        float startFov = cam.fieldOfView;
        float startFog = RenderSettings.fogDensity;

        yield return new WaitForSeconds(delay);
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(line)) DialogueBox.Instance.Say(line);

        Quaternion targetRot = Quaternion.LookRotation(lookTarget.position + lookOffset - cam.transform.position);
        yield return Blend(moveIn, k =>
        {
            cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, k);
            cam.fieldOfView = Mathf.Lerp(startFov, zoomFov, k);
            RenderSettings.fogDensity = Mathf.Lerp(startFog, fogDensityDuring, k);
            SetLetterbox(k);
        });

        yield return new WaitForSeconds(hold);

        yield return Blend(moveOut, k =>
        {
            cam.transform.rotation = Quaternion.Slerp(targetRot, startRot, k);
            cam.fieldOfView = Mathf.Lerp(zoomFov, startFov, k);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensityDuring, startFog, k);
            SetLetterbox(1f - k);
        });

        cam.transform.localRotation = startLocal;
        cam.fieldOfView = startFov;
        RenderSettings.fogDensity = startFog;
        SetLetterbox(0f);
        foreach (var g in hideDuring) if (g != null) g.SetActive(true);
        foreach (var b in disableDuring) if (b != null) b.enabled = true;
    }

    IEnumerator Blend(float duration, System.Action<float> apply)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            apply(Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        apply(1f);
    }

    void SetLetterbox(float k)
    {
        float h = letterboxHeight * k;
        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, h);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, h);
    }
}
