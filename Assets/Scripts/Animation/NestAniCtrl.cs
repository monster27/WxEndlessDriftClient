using UnityEngine;
using System.Collections;

public class NestAniCtrl : MonoBehaviour
{
    [Header("序列帧动画设置")]
    [SerializeField] private Texture2D baitSheet;        // 窝料序列帧图片
    [SerializeField] private int columns = 4;             // 列数
    [SerializeField] private int rows = 1;                // 行数
    [SerializeField] private float frameSpeed = 20f;      // 帧播放速度

    [Header("动画行为设置")]
    [SerializeField] private float displayDuration = 2f;  // 显示时长（秒）
    [SerializeField] private float fadeDuration = 0.5f;   // 淡出时长（秒）
    [SerializeField] private bool autoPlayOnStart = false; // 是否自动播放

    private Material baitMaterial;
    private Renderer baitRenderer;
    private Coroutine currentAnimationCoroutine;
    private bool isPlaying = false;

    // 颜色属性ID（缓存以提升性能）
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int Rows1 = Shader.PropertyToID("_Rows1");
    private static readonly int Columns1 = Shader.PropertyToID("_Columns1");
    private static readonly int Speed1 = Shader.PropertyToID("_Speed1");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    private void Start()
    {
        InitializeRenderer();

        if (autoPlayOnStart && baitSheet != null)
        {
            PlayBaitAnimation();
        }
    }

    /// <summary>
    /// 初始化渲染器组件
    /// </summary>
    private void InitializeRenderer()
    {
        if (baitRenderer == null)
        {
            baitRenderer = GetComponent<Renderer>();
        }

        if (baitRenderer != null)
        {
            baitMaterial = baitRenderer.material;

            // 默认设置为透明
            if (baitMaterial != null)
            {
                Color color = baitMaterial.color;
                color.a = 0f;
                baitMaterial.color = color;
            }
        }
        else
        {
            Debug.LogWarning("[BaitAnimationController] 未找到Renderer组件！");
        }
    }

    /// <summary>
    /// 播放窝料动画（外部调用入口）
    /// </summary>
    public void PlayBaitAnimation()
    {
        if (baitSheet == null)
        {
            Debug.LogWarning("[BaitAnimationController] 窝料序列帧图片未设置！");
            return;
        }

        if (baitMaterial == null)
        {
            InitializeRenderer();
            if (baitMaterial == null)
            {
                Debug.LogWarning("[BaitAnimationController] 材质初始化失败！");
                return;
            }
        }

        // 如果正在播放，先停止
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        // 开始新的动画协程
        currentAnimationCoroutine = StartCoroutine(PlayAnimationSequence());
    }

    /// <summary>
    /// 播放动画序列的协程
    /// </summary>
    private IEnumerator PlayAnimationSequence()
    {
        isPlaying = true;

        // 1. 设置序列帧纹理和参数
        baitMaterial.SetTexture(MainTex, baitSheet);
        baitMaterial.SetFloat(Rows1, rows);
        baitMaterial.SetFloat(Columns1, columns);
        baitMaterial.SetFloat(Speed1, frameSpeed);

        // 2. 淡入（快速出现）
        yield return StartCoroutine(FadeTo(1f, 0.2f));

        // 3. 播放指定时长
        float elapsedTime = 0f;
        while (elapsedTime < displayDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 4. 淡出并隐藏
        yield return StartCoroutine(FadeTo(0f, fadeDuration));

        // 5. 清理状态
        isPlaying = false;
        currentAnimationCoroutine = null;
    }

    /// <summary>
    /// 淡入淡出协程
    /// </summary>
    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (baitMaterial == null)
        {
            yield break;
        }

        Color color = baitMaterial.color;
        float startAlpha = color.a;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            color.a = currentAlpha;
            baitMaterial.color = color;

            yield return null;
        }

        // 确保最终值准确
        color.a = targetAlpha;
        baitMaterial.color = color;
    }

    /// <summary>
    /// 立即停止动画并隐藏
    /// </summary>
    public void StopAnimation()
    {
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        isPlaying = false;

        if (baitMaterial != null)
        {
            Color color = baitMaterial.color;
            color.a = 0f;
            baitMaterial.color = color;
        }
    }

    /// <summary>
    /// 设置窝料序列帧图片
    /// </summary>
    public void SetBaitSheet(Texture2D sheet)
    {
        baitSheet = sheet;
    }

    /// <summary>
    /// 设置序列帧行列数
    /// </summary>
    public void SetSpriteGrid(int rowCount, int colCount)
    {
        rows = rowCount;
        columns = colCount;
    }

    /// <summary>
    /// 设置播放速度
    /// </summary>
    public void SetFrameSpeed(float speed)
    {
        frameSpeed = speed;
    }

    /// <summary>
    /// 设置显示时长
    /// </summary>
    public void SetDisplayDuration(float duration)
    {
        displayDuration = duration;
    }

    /// <summary>
    /// 获取当前是否正在播放
    /// </summary>
    public bool IsPlaying()
    {
        return isPlaying;
    }

    private void OnDestroy()
    {
        // 清理材质，避免内存泄漏
        if (baitMaterial != null)
        {
            Destroy(baitMaterial);
        }
    }
}