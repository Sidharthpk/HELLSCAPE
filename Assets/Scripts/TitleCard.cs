using System.Collections;
using UnityEngine;

// "HELLSCAPE" title that slams in when the waves start. Call Show() from the deer-zombie StoryTrigger.
public class TitleCard : MonoBehaviour
{
    public CanvasGroup card;             // CanvasGroup on the title text, starts hidden
    public AudioSource sting;            // optional: low boom / distorted hit
    public float fadeIn = 0.4f;
    public float hold = 2.5f;
    public float fadeOut = 1.5f;
    public float startScale = 1.4f;      // shrinks to 1 while fading in
    public string lineAfter;             // dialogue said once the card has faded out ('|' splits lines)

    [Header("Flicker")]
    public float flickerTime = 0.6f;     // brief unstable flicker once it lands

    private bool shown = false;

    void Start()
    {
        card.alpha = 0f;
        card.blocksRaycasts = false;
    }

    public void Show()
    {
        if (shown) return;
        shown = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (sting != null) sting.Play();

        Transform tr = card.transform;
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            float k = t / fadeIn;
            card.alpha = k;
            tr.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, k);
            yield return null;
        }
        card.alpha = 1f;
        tr.localScale = Vector3.one;

        t = 0f;
        while (t < flickerTime)
        {
            t += Time.deltaTime;
            card.alpha = Random.value < 0.3f ? 0.3f : 1f;
            yield return null;
        }
        card.alpha = 1f;

        yield return new WaitForSeconds(hold);

        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            card.alpha = 1f - t / fadeOut;
            yield return null;
        }
        card.alpha = 0f;

        if (!string.IsNullOrEmpty(lineAfter) && DialogueBox.Instance != null)
            DialogueBox.Instance.Say(lineAfter);
    }
}
