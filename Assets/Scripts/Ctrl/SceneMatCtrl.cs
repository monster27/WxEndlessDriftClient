using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 场景材质控制 - 纯渲染控制器
/// 只负责 Shader 属性的设置和操作，不包含任何业务逻辑
/// </summary>
public class SceneMatCtrl : MonoBehaviour
{
    // ========== Inspector 显示参数 ==========
    [Header("=== 基础参数 ===")]
    [SerializeField] private SceneMatManager.ElementType elementId = SceneMatManager.ElementType.EnvBg;
    [SerializeField] private bool isSceneParameter = true;
    [SerializeField] private bool isLockFlip = false;
    [SerializeField] private bool isFlipped = false;
    [SerializeField] private SceneMatManager.RenderQueueLevel renderQueue = SceneMatManager.RenderQueueLevel.Environment;

    [Header("=== 纹理设置 ===")]
    [SerializeField] protected Texture2D mainTexture;
    [SerializeField] protected Color tintColor = Color.white;

    [Header("=== 闪烁设置 ===")]
    [SerializeField] protected Texture2D blinkTexture;
    [SerializeField] protected Color blinkColor = Color.white;
    [SerializeField] protected bool enableBlink = false;
    [SerializeField] protected float blinkInterval = 0.5f;
    [SerializeField] protected float blinkOffset = 0f;

    [Header("=== 运行时信息 ===")]
    [SerializeField] protected string sceneId = "";
    [SerializeField] protected string elementPath = "";

    // ========== 私有变量 ==========
    [SerializeField] protected Material material;
    [SerializeField] protected Renderer targetRenderer;
    protected bool isInitialized = false;
    protected Coroutine fadeCoroutine;
    protected Coroutine transitionCoroutine;

    // ========== 序列帧参数缓存 ==========
    private int cachedRows = 1;
    private int cachedColumns = 4;
    private float cachedSpeed = 15f;
    private bool cachedSpriteSheetEnabled = false;

    // ========== Shader属性ID ==========
    protected static readonly int MainTex = Shader.PropertyToID("_MainTex");
    protected static readonly int ColorProp = Shader.PropertyToID("_Color");
    protected static readonly int Flip = Shader.PropertyToID("_Flip");
    protected static readonly int LockFlip = Shader.PropertyToID("_LockFlip");
    protected static readonly int BlinkTex = Shader.PropertyToID("_BlinkTex");
    protected static readonly int BlinkColor = Shader.PropertyToID("_BlinkColor");
    protected static readonly int BlinkEnabled = Shader.PropertyToID("_BlinkEnabled");
    protected static readonly int BlinkInterval = Shader.PropertyToID("_BlinkInterval");
    protected static readonly int BlinkOffset = Shader.PropertyToID("_BlinkOffset");
    protected static readonly int Rows = Shader.PropertyToID("_Rows");
    protected static readonly int Columns = Shader.PropertyToID("_Columns");
    protected static readonly int Speed = Shader.PropertyToID("_Speed");
    protected static readonly int NextTex = Shader.PropertyToID("_NextTex");
    protected static readonly int Transition = Shader.PropertyToID("_Transition");
    protected static readonly int SpriteSheetEnabled = Shader.PropertyToID("_SpriteSheetEnabled");

    // ========== 日志标签 ==========
    private const string LOG_TAG = "SceneMat";

    // ========== 公共属性 ==========
    public SceneMatManager.ElementType ElementId => elementId;
    public bool IsSceneParameter => isSceneParameter;
    public bool IsFlipped => isFlipped;
    public bool IsLockFlip => isLockFlip;
    public SceneMatManager.RenderQueueLevel RenderQueue => renderQueue;
    public Texture2D MainTexture => mainTexture;
    public string SceneId => sceneId;
    public string ElementPath => elementPath;
    public bool IsBlinking => enableBlink;
    public Color CurrentBlinkColor => blinkColor;
    public Material Material => material;
    public Renderer Renderer => targetRenderer;
    public bool IsInitialized => isInitialized;

    // ========== 公共事件 ==========
    public event Action OnBlinkStart;
    public event Action OnBlinkStop;
    public event Action<Texture2D> OnMainTextureChanged;
    public event Action OnFadeComplete;

    // ========== 初始化 ==========
    protected virtual void Awake()
    {
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.Awake() - ElementId: {elementId}");
    }

    protected virtual void Start()
    {
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.Start() - ElementId: {elementId}, 开始初始化");
        Initialize();
    }

    public virtual void Initialize()
    {
        if (isInitialized)
        {
            Debug.Log($"[{LOG_TAG}] {gameObject.name}.Initialize() - 已初始化，跳过");
            return;
        }

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.Initialize() - 开始初始化, ElementId: {elementId}");

        // ===== 直接获取 Renderer =====
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer == null)
        {
            Debug.LogError($"[{LOG_TAG}] {gameObject.name}.Initialize() - 找不到Renderer！");
            return;
        }

        // ===== 关键修改：使用 material 而不是 sharedMaterial =====
        // 这样可以确保获取到的是当前正在使用的材质（包括实例）
        material = targetRenderer.material;
        if (material == null)
        {
            Debug.LogError($"[{LOG_TAG}] {gameObject.name}.Initialize() - 材质为空！");
            return;
        }

        if (material.shader == null || material.shader.name != "Custom/GameInSceneShader")
        {
            Debug.LogError($"[{LOG_TAG}] {gameObject.name}.Initialize() - Shader不是GameInSceneShader！当前: {material.shader?.name ?? "null"}");
            return;
        }

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.Initialize() - 获取材质成功: {material.name}");
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.Initialize() - 是否为实例材质: {material.name.Contains("(Instance)")}");

        // ===== 从材质读取现有属性 =====
        ReadMaterialProperties();

        // ===== 应用属性 =====
        ApplyAllProperties();
        UpdateRenderQueue();

        isInitialized = true;
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.Initialize() - 初始化完成");
    }

    /// <summary>
    /// 从材质读取属性
    /// </summary>
    private void ReadMaterialProperties()
    {
        if (material == null) return;

        try
        {
            if (material.HasProperty(MainTex))
            {
                Texture tex = material.GetTexture(MainTex);
                if (tex != null)
                {
                    mainTexture = tex as Texture2D;
                    Debug.Log($"[{LOG_TAG}] {gameObject.name}.ReadMaterialProperties() - 读取主纹理: {mainTexture?.name ?? "null"}");
                }
            }

            if (material.HasProperty(ColorProp))
            {
                tintColor = material.GetColor(ColorProp);
                Debug.Log($"[{LOG_TAG}] {gameObject.name}.ReadMaterialProperties() - 读取颜色: {tintColor}");
            }

            if (material.HasProperty(Flip))
            {
                isFlipped = material.GetFloat(Flip) > 0.5f;
            }

            if (material.HasProperty(BlinkTex))
            {
                Texture tex = material.GetTexture(BlinkTex);
                if (tex != null)
                {
                    blinkTexture = tex as Texture2D;
                }
            }

            if (material.HasProperty(BlinkColor))
            {
                blinkColor = material.GetColor(BlinkColor);
            }

            if (material.HasProperty(BlinkEnabled))
            {
                enableBlink = material.GetFloat(BlinkEnabled) > 0.5f;
            }
            if (material.HasProperty(BlinkInterval))
            {
                blinkInterval = material.GetFloat(BlinkInterval);
            }
            if (material.HasProperty(BlinkOffset))
            {
                blinkOffset = material.GetFloat(BlinkOffset);
            }

            if (material.HasProperty(SpriteSheetEnabled))
            {
                cachedSpriteSheetEnabled = material.GetFloat(SpriteSheetEnabled) > 0.5f;
            }
            if (material.HasProperty(Rows))
            {
                cachedRows = (int)material.GetFloat(Rows);
            }
            if (material.HasProperty(Columns))
            {
                cachedColumns = (int)material.GetFloat(Columns);
            }
            if (material.HasProperty(Speed))
            {
                cachedSpeed = material.GetFloat(Speed);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.ReadMaterialProperties() - 读取材质属性时出错: {e.Message}");
        }
    }

    // ========== 应用属性 ==========
    public virtual void ApplyAllProperties()
    {
        if (material == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 材质为空，跳过");
            return;
        }

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 开始应用材质属性");

        try
        {
            // 主纹理
            if (mainTexture != null)
            {
                material.SetTexture(MainTex, mainTexture);
                Debug.Log($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 设置主纹理: {mainTexture.name}");
            }

            // 颜色
            material.SetColor(ColorProp, tintColor);
            Debug.Log($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 设置颜色: {tintColor}");

            // 翻转
            material.SetFloat(Flip, isFlipped ? 1f : 0f);
            material.SetFloat(LockFlip, isLockFlip ? 1f : 0f);
            Debug.Log($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 设置翻转: {isFlipped}, 锁定: {isLockFlip}");

            // 闪烁
            material.SetTexture(BlinkTex, blinkTexture);
            material.SetColor(BlinkColor, blinkColor);
            bool hasTexture = blinkTexture != null;
            material.SetFloat(BlinkEnabled, (enableBlink && hasTexture) ? 1f : 0f);
            material.SetFloat(BlinkInterval, Mathf.Max(blinkInterval, 0.01f));
            material.SetFloat(BlinkOffset, blinkOffset);
            Debug.Log($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 设置闪烁: 启用={enableBlink}, 间隔={blinkInterval}, 偏移={blinkOffset}");

            // 序列帧
            if (cachedSpriteSheetEnabled)
            {
                material.SetFloat(Rows, cachedRows);
                material.SetFloat(Columns, cachedColumns);
                material.SetFloat(Speed, cachedSpeed);
                material.SetFloat(SpriteSheetEnabled, 1f);
                Debug.Log($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 恢复序列帧: Rows={cachedRows}, Columns={cachedColumns}, Speed={cachedSpeed}");
            }
            else
            {
                material.SetFloat(SpriteSheetEnabled, 0f);
                Debug.Log($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 序列帧已禁用");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{LOG_TAG}] {gameObject.name}.ApplyAllProperties() - 应用材质属性失败: {e.Message}");
        }
    }

    public virtual void UpdateRenderQueue()
    {
        if (material == null) return;

        int queueValue = GetRenderQueueValue(renderQueue);
        material.renderQueue = queueValue;
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.UpdateRenderQueue() - 渲染队列: {renderQueue} -> {queueValue}");
    }

    protected virtual int GetRenderQueueValue(SceneMatManager.RenderQueueLevel level)
    {
        switch (level)
        {
            case SceneMatManager.RenderQueueLevel.Background: return 1000;
            case SceneMatManager.RenderQueueLevel.Environment: return 2000;
            case SceneMatManager.RenderQueueLevel.Character: return 3000;
            case SceneMatManager.RenderQueueLevel.Foreground: return 4000;
            case SceneMatManager.RenderQueueLevel.UI: return 5000;
            default: return 2000;
        }
    }

    // ==========================================
    // 1. 主纹理功能
    // ==========================================

    public virtual void SetMainTexture(Texture2D texture)
    {
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.SetMainTexture() - 设置主纹理: {texture?.name ?? "null"}");

        if (texture == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.SetMainTexture() - 纹理为空！");
            return;
        }

        if (material == null)
        {
            Debug.LogError($"[{LOG_TAG}] {gameObject.name}.SetMainTexture() - 材质为空！");
            return;
        }

        mainTexture = texture;
        material.SetTexture(MainTex, texture);
        OnMainTextureChanged?.Invoke(texture);
    }

    public virtual void SetMainTextureByPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.SetMainTextureByPath() - 路径为空！");
            return;
        }

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogError($"[{LOG_TAG}] {gameObject.name}.SetMainTextureByPath() - 无法加载纹理: {path}");
            return;
        }

        elementPath = path;
        SetMainTexture(texture);
    }

    public virtual void SetMainTextureSmooth(Texture2D texture, float duration = 0.5f)
    {
        if (texture == null || material == null)
        {
            SetMainTexture(texture);
            return;
        }

        if (gameObject == null || !gameObject.activeInHierarchy)
        {
            SetMainTexture(texture);
            return;
        }

        StartCoroutine(SmoothTextureTransition(texture, duration));
    }

    protected virtual IEnumerator SmoothTextureTransition(Texture2D newTexture, float duration)
    {
        if (material == null || this == null || gameObject == null) yield break;

        Color originalColor = tintColor;
        Color startColor = tintColor;
        Color targetColor = tintColor;
        targetColor.a = 0f;

        float elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            Color currentColor = Color.Lerp(startColor, targetColor, t);
            material.SetColor(ColorProp, currentColor);
            yield return null;
        }

        mainTexture = newTexture;
        material.SetTexture(MainTex, newTexture);

        elapsed = 0f;
        startColor = targetColor;
        targetColor = originalColor;
        targetColor.a = 1f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            Color currentColor = Color.Lerp(startColor, targetColor, t);
            material.SetColor(ColorProp, currentColor);
            yield return null;
        }

        material.SetColor(ColorProp, originalColor);
        OnMainTextureChanged?.Invoke(newTexture);
    }

    // ==========================================
    // 2. 闪烁功能
    // ==========================================

    public virtual void SetBlinkColor(Color color)
    {
        blinkColor = color;
        if (material != null) material.SetColor(BlinkColor, color);
    }

    public virtual void SetBlinkEnabled(bool enabled)
    {
        enableBlink = enabled;
        if (material != null)
        {
            bool hasTexture = blinkTexture != null;
            material.SetFloat(BlinkEnabled, (enabled && hasTexture) ? 1f : 0f);
        }

        if (enabled) OnBlinkStart?.Invoke();
        else OnBlinkStop?.Invoke();
    }

    public virtual void SetBlinkTexture(Texture2D texture)
    {
        blinkTexture = texture;
        if (material != null)
        {
            material.SetTexture(BlinkTex, texture);
            bool hasTexture = texture != null;
            material.SetFloat(BlinkEnabled, (enableBlink && hasTexture) ? 1f : 0f);
        }
    }

    public virtual void SetBlinkInterval(float interval)
    {
        blinkInterval = Mathf.Max(interval, 0.01f);
        if (material != null) material.SetFloat(BlinkInterval, blinkInterval);
    }

    public virtual void SetBlinkOffset(float offset)
    {
        blinkOffset = offset;
        if (material != null) material.SetFloat(BlinkOffset, offset);
    }

    public virtual void StartBlink()
    {
        if (!isInitialized || blinkTexture == null) return;
        SetBlinkEnabled(true);
    }

    public virtual void StopBlink()
    {
        if (!isInitialized) return;
        SetBlinkEnabled(false);
    }

    // ==========================================
    // 3. 镜像功能
    // ==========================================

    public virtual void SetFlip(bool flip)
    {
        if (isLockFlip)
        {
            Debug.Log($"[{LOG_TAG}] {gameObject.name}.SetFlip() - {elementId} 已锁定镜像，无法改变");
            return;
        }

        isFlipped = flip;
        if (material != null) material.SetFloat(Flip, flip ? 1f : 0f);
    }

    public virtual void SetLockFlip(bool locked)
    {
        isLockFlip = locked;
        if (material != null) material.SetFloat(LockFlip, locked ? 1f : 0f);
    }

    public virtual void ToggleFlip()
    {
        if (isLockFlip) return;
        SetFlip(!isFlipped);
    }

    // ==========================================
    // 4. 渲染队列功能
    // ==========================================

    public virtual void SetRenderQueue(SceneMatManager.RenderQueueLevel level)
    {
        renderQueue = level;
        UpdateRenderQueue();
    }

    public virtual void SetRenderQueueValue(int queueValue)
    {
        if (material != null) material.renderQueue = queueValue;
    }

    // ==========================================
    // 5. 位置信息功能
    // ==========================================

    public virtual void SetTransformData(Vector3 position, Vector3? scale = null)
    {
        if (this == null || gameObject == null) return;
        transform.position = position;
        if (scale.HasValue) transform.localScale = scale.Value;
    }

    public virtual SceneMatManager.TransformData GetTransformData()
    {
        return new SceneMatManager.TransformData
        {
            position = transform.position,
            scale = transform.localScale
        };
    }

    // ==========================================
    // 6. 序列帧动画设置
    // ==========================================

    public virtual void SetSpriteSheetParams(int rows, int columns, float speed)
    {
        if (material == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.SetSpriteSheetParams() - 材质为空，跳过");
            return;
        }

        cachedRows = rows;
        cachedColumns = columns;
        cachedSpeed = speed;

        material.SetFloat(Rows, rows);
        material.SetFloat(Columns, columns);
        material.SetFloat(Speed, speed);
    }

    public virtual void SetSpriteSheetEnabled(bool enabled)
    {
        cachedSpriteSheetEnabled = enabled;
        if (material != null) material.SetFloat(SpriteSheetEnabled, enabled ? 1f : 0f);
    }

    public virtual bool IsSpriteSheetEnabled()
    {
        return cachedSpriteSheetEnabled;
    }

    // ==========================================
    // 7. 纹理过渡功能
    // ==========================================

    public virtual void SetNextTexture(Texture2D texture)
    {
        if (material != null && texture != null) material.SetTexture(NextTex, texture);
    }

    public virtual void SetTransition(float value)
    {
        if (material != null) material.SetFloat(Transition, Mathf.Clamp01(value));
    }

    public virtual void TransitionTo(Texture2D texture, float duration = 1f)
    {
        if (texture == null || material == null) return;
        SetNextTexture(texture);
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(SmoothTransition(duration));
    }

    protected virtual IEnumerator SmoothTransition(float duration)
    {
        if (material == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            material.SetFloat(Transition, Mathf.Lerp(0f, 1f, t));
            yield return null;
        }

        Texture2D nextTex = material.GetTexture(NextTex) as Texture2D;
        if (nextTex != null)
        {
            mainTexture = nextTex;
            material.SetTexture(MainTex, nextTex);
            material.SetFloat(Transition, 0f);
            OnMainTextureChanged?.Invoke(mainTexture);
        }

        transitionCoroutine = null;
        OnFadeComplete?.Invoke();
    }

    // ==========================================
    // 8. 淡入淡出功能
    // ==========================================

    public virtual void FadeTo(float targetAlpha, float duration = 0.5f, Action onComplete = null)
    {
        if (material == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (duration <= 0f)
        {
            Color color = material.GetColor(ColorProp);
            color.a = Mathf.Clamp01(targetAlpha);
            material.SetColor(ColorProp, color);
            tintColor = color;
            onComplete?.Invoke();
            return;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeToCoroutine(targetAlpha, duration, onComplete));
    }

    protected virtual IEnumerator FadeToCoroutine(float targetAlpha, float duration, Action onComplete)
    {
        if (material == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Color color = material.GetColor(ColorProp);
        float startAlpha = color.a;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            material.SetColor(ColorProp, color);
            tintColor = color;
            yield return null;
        }

        color.a = targetAlpha;
        material.SetColor(ColorProp, color);
        tintColor = color;

        fadeCoroutine = null;
        OnFadeComplete?.Invoke();
        onComplete?.Invoke();
    }

    public virtual void FadeIn(float duration = 0.5f, Action onComplete = null)
    {
        FadeTo(1f, duration, onComplete);
    }

    public virtual void FadeOut(float duration = 0.5f, Action onComplete = null)
    {
        FadeTo(0f, duration, onComplete);
    }

    public virtual void SetAlphaImmediate(float alpha)
    {
        if (material == null) return;

        Color color = material.GetColor(ColorProp);
        color.a = Mathf.Clamp01(alpha);
        material.SetColor(ColorProp, color);
        tintColor = color;
    }

    // ==========================================
    // 9. 场景参数加载
    // ==========================================

    public virtual void SetSceneId(string id)
    {
        sceneId = id;
    }

    public virtual void SetTintColor(Color color)
    {
        tintColor = color;
        if (material != null) material.SetColor(ColorProp, color);
    }

    public virtual void LoadFromData(SceneMatManager.SceneElementData data)
    {
        if (data == null) return;

        if (!string.IsNullOrEmpty(data.imagePath))
        {
            SetMainTextureByPath(data.imagePath);
        }

        SetTransformData(data.position, data.scale);

        if (Enum.TryParse<SceneMatManager.RenderQueueLevel>(data.renderLevel, out SceneMatManager.RenderQueueLevel level))
        {
            SetRenderQueue(level);
        }

        SetFlip(data.isFlipped);
        SetLockFlip(data.isLockFlip);
        sceneId = data.sceneId;
    }

    // ==========================================
    // Unity 生命周期
    // ==========================================

    protected virtual void OnDestroy()
    {
        if (fadeCoroutine != null)
        {
            try { StopCoroutine(fadeCoroutine); }
            catch { }
            fadeCoroutine = null;
        }

        if (transitionCoroutine != null)
        {
            try { StopCoroutine(transitionCoroutine); }
            catch { }
            transitionCoroutine = null;
        }

        // 注意：不要销毁材质，因为它是从 sharedMaterial 获取的
        // 如果销毁会导致材质丢失
        material = null;
    }

    protected virtual void OnEnable()
    {
        if (isInitialized && material != null)
        {
            try
            {
                ApplyAllProperties();
                UpdateRenderQueue();
            }
            catch { }
        }
    }

    protected virtual void OnDisable()
    {
        if (enableBlink && material != null)
        {
            try { material.SetFloat(BlinkEnabled, 0f); }
            catch { }
        }
    }
}