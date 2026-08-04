// ==================== ClickableObject.cs ====================
using UnityEngine;
using System;

/// <summary>
/// 挂载到Quad物体上，用于标识可点击
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ClickableObject : MonoBehaviour
{
    [Header("点击配置")]
    public ClickableType objectType;
    public string objData;

    [Header("点击效果")]
    [SerializeField] private float defaultScaleMin = 0.9f;
    [SerializeField] private float defaultDuration = 0.2f;

    private Vector3 originalScale;
    private bool isPlaying = false;
    private float clickTime = 0f;
    private float currentScaleMin = 0.9f;
    private float currentDuration = 0.3f;
    private Action onComplete;

    void Start()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {
        UpdateClickEffect();
    }

    private void OnMouseUp()
    {
        PlayClickEffect(defaultScaleMin, defaultDuration, () =>
        {
            ClickManager.Instance.OnObjectClicked(this);
        });
    }

    private void UpdateClickEffect()
    {
        if (!isPlaying) return;

        float progress = Mathf.Clamp01((Time.time - clickTime) / currentDuration);
        float elastic = Mathf.Sin(progress * Mathf.PI) * (1f - progress * 0.3f);
        float scale = 1f - (1f - currentScaleMin) * Mathf.Max(0, elastic);

        transform.localScale = originalScale * scale;

        if (progress >= 1f)
        {
            isPlaying = false;
            transform.localScale = originalScale;
            onComplete?.Invoke();
            onComplete = null;
        }
    }

    public void PlayClickEffect(float scaleMin = 0.9f, float duration = 0.3f, Action onComplete = null)
    {
        currentScaleMin = Mathf.Clamp(scaleMin, 0.5f, 1f);
        currentDuration = Mathf.Max(duration, 0.05f);
        this.onComplete = onComplete;
        clickTime = Time.time;
        isPlaying = true;
    }
}
