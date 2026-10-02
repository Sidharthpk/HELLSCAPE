using System.Collections;
using UnityEngine;

// Full-screen black Image with a CanvasGroup, used for sleep and ending transitions.
public class ScreenFader : MonoBehaviour
{
    public CanvasGroup fade;

    void Start()
    {
        fade.alpha = 0f;
        fade.blocksRaycasts = false;
    }

    public IEnumerator FadeTo(float target, float duration)
    {
        float start = fade.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        fade.alpha = target;
    }
}
