using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IconShinyEffect : MonoBehaviour
{
    private Image targetImage;
    private Coroutine shinyPulseCoroutine = null;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (targetImage != null && gameObject.activeSelf)
        {
            StartShinyPulse();
        }
    }

    private void OnDisable()
    {
        StopShinyPulse();
    }

    private void OnDestroy()
    {
        StopShinyPulse();
    }

    private void StartShinyPulse()
    {
        if (targetImage == null) return;

        if (shinyPulseCoroutine != null)
        {
            StopCoroutine(shinyPulseCoroutine);
            shinyPulseCoroutine = null;
        }

        Color c = targetImage.color;
        c.a = 1f;
        targetImage.color = c;
        shinyPulseCoroutine = StartCoroutine(ShinyPulseCoroutine());
    }

    private void StopShinyPulse()
    {
        if (shinyPulseCoroutine != null)
        {
            StopCoroutine(shinyPulseCoroutine);
            shinyPulseCoroutine = null;
        }

        if (targetImage != null)
        {
            Color c = targetImage.color;
            c.a = 1f;
            targetImage.color = c;
        }
    }

    private IEnumerator ShinyPulseCoroutine()
    {
        if (targetImage == null) yield break;

        float speed = 2.5f;
        float minAlpha = 0.2f;
        float maxAlpha = 1f;

        while (true)
        {
            float t = Mathf.PingPong(Time.time * speed, 1f);
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

            Color c = targetImage.color;
            c.a = alpha;
            targetImage.color = c;

            yield return null;
        }
    }
}
