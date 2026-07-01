using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 场景材质控制 - 纯渲染控制器
/// 只负责 Shader 属性的设置和操作，不包含任何业务逻辑
/// </summary>
public class SceneMatCtrl : MonoBehaviour
{
    // ========== 枚举定义 ==========
    public enum ParameterType
    {
        SceneParameter,    // 场景参数（由场景切换控制）
        StaticParameter,   // 静态参数（固定不变）
        PlayerParameter    // 玩家参数（由玩家控制）
    }

    // ========== Inspector 显示参数 ==========
    [Header("=== 基础参数 ===")]
    [SerializeField] private SceneMatManager.ElementType elementId = SceneMatManager.ElementType.EnvBg;
    [SerializeField] private ParameterType parameterType = ParameterType.SceneParameter;
    [SerializeField] private bool isCanFlip = true;
    [SerializeField] private bool isFlipped = false;
    [SerializeField] private SceneMatManager.RenderQueueLevel renderQueue = SceneMatManager.RenderQueueLevel.GameLayer;

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
    public ParameterType ParamType => parameterType;
    public bool IsCanFlip => isCanFlip;
    public bool IsFlipped => isFlipped;
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

        material = targetRenderer.sharedMaterial;
        if (material == null)
        {
            Debug.LogError($"[{LOG_TAG}] {gameObject.name}.Initialize() - 材质为空！");
            return;
        }

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.Initialize() - 获取材质引用成功: {material.name} (实例ID: {material.GetInstanceID()})");

        ReadMaterialProperties();
        ApplyAllProperties();

        SceneMatManager manager = FindObjectOfType<SceneMatManager>();
        if (manager != null)
        {
            manager.RegisterController(this);
            Debug.Log($"[{LOG_TAG}] {gameObject.name}.Initialize() - 已注册到SceneMatManager");
        }
        else
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.Initialize() - 找不到SceneMatManager");
        }

        UpdateRenderQueue();

        isInitialized = true;
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.Initialize() - ✅ 初始化完成");
    }

    private void ReadMaterialProperties()
    {
        if (material == null) return;

        try
        {
            if (material.HasProperty(MainTex))
            {
                Texture tex = material.GetTexture(MainTex);
                if (tex != null) mainTexture = tex as Texture2D;
            }
            if (material.HasProperty(ColorProp)) tintColor = material.GetColor(ColorProp);
            if (material.HasProperty(Flip)) isFlipped = material.GetFloat(Flip) > 0.5f;
            if (material.HasProperty(BlinkTex))
            {
                Texture tex = material.GetTexture(BlinkTex);
                if (tex != null) blinkTexture = tex as Texture2D;
            }
            if (material.HasProperty(BlinkColor)) blinkColor = material.GetColor(BlinkColor);
            if (material.HasProperty(BlinkEnabled)) enableBlink = material.GetFloat(BlinkEnabled) > 0.5f;
            if (material.HasProperty(BlinkInterval)) blinkInterval = material.GetFloat(BlinkInterval);
            if (material.HasProperty(BlinkOffset)) blinkOffset = material.GetFloat(BlinkOffset);
            if (material.HasProperty(SpriteSheetEnabled)) cachedSpriteSheetEnabled = material.GetFloat(SpriteSheetEnabled) > 0.5f;
            if (material.HasProperty(Rows)) cachedRows = (int)material.GetFloat(Rows);
            if (material.HasProperty(Columns)) cachedColumns = (int)material.GetFloat(Columns);
            if (material.HasProperty(Speed)) cachedSpeed = material.GetFloat(Speed);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.ReadMaterialProperties() - 读取材质属性时出错: {e.Message}");
        }
    }

    // ========== 应用属性 ==========
    public virtual void ApplyAllProperties()
    {
        if (material == null) return;

        try
        {
            if (mainTexture != null) material.SetTexture(MainTex, mainTexture);
            material.SetColor(ColorProp, tintColor);
            material.SetFloat(Flip, isFlipped ? 1f : 0f);
            material.SetFloat(LockFlip, isCanFlip ? 0f : 1f);

            bool hasTexture = blinkTexture != null;
            material.SetTexture(BlinkTex, blinkTexture);
            material.SetColor(BlinkColor, blinkColor);
            material.SetFloat(BlinkEnabled, (enableBlink && hasTexture) ? 1f : 0f);
            material.SetFloat(BlinkInterval, Mathf.Max(blinkInterval, 0.01f));
            material.SetFloat(BlinkOffset, blinkOffset);

            if (cachedSpriteSheetEnabled)
            {
                material.SetFloat(Rows, cachedRows);
                material.SetFloat(Columns, cachedColumns);
                material.SetFloat(Speed, cachedSpeed);
                material.SetFloat(SpriteSheetEnabled, 1f);
            }
            else
            {
                material.SetFloat(SpriteSheetEnabled, 0f);
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

        SceneMatManager manager = FindObjectOfType<SceneMatManager>();
        if (manager != null)
        {
            int queueValue = manager.GetRenderQueueValue(renderQueue);
            if (material.renderQueue != queueValue)
            {
                Debug.Log($"[{LOG_TAG}] {gameObject.name}.UpdateRenderQueue() - 📊 设置渲染队列: {renderQueue} -> {queueValue}");
                material.renderQueue = queueValue;
            }
        }
        else
        {
            if (material.renderQueue != 3000)
            {
                Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.UpdateRenderQueue() - ⚠️ 找不到SceneMatManager，使用默认队列: 3000");
                material.renderQueue = 3000;
            }
        }
    }

    // ==========================================
    // 1. 主纹理功能
    // ==========================================

    public virtual void SetMainTexture(Texture2D texture)
    {
        if (texture == null || material == null) return;

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.SetMainTexture() - 🖼️ 设置主纹理: {texture.name}");
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

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.SetMainTextureByPath() - 📂 加载纹理: {path}");

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogError($"[{LOG_TAG}] {gameObject.name}.SetMainTextureByPath() - ❌ 无法加载纹理: {path}");
            return;
        }

        elementPath = path;
        SetMainTexture(texture);
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.SetMainTextureByPath() - ✅ 纹理加载成功: {path}");
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

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.SetMainTextureSmooth() - 🎬 开始平滑切换纹理: {texture.name}, 时长: {duration}秒");
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
            material.SetColor(ColorProp, Color.Lerp(startColor, targetColor, t));
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
            material.SetColor(ColorProp, Color.Lerp(startColor, targetColor, t));
            yield return null;
        }

        material.SetColor(ColorProp, originalColor);
        OnMainTextureChanged?.Invoke(newTexture);
        Debug.Log($"[{LOG_TAG}] {gameObject.name}.SmoothTextureTransition() - ✅ 平滑切换完成: {newTexture.name}");
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
        if (!isCanFlip)
        {
            Debug.Log($"[{LOG_TAG}] {gameObject.name}.SetFlip() - ⛔ {elementId} 不允许镜像翻转");
            return;
        }

        isFlipped = flip;
        if (material != null) material.SetFloat(Flip, flip ? 1f : 0f);
    }

    public virtual void ToggleFlip()
    {
        if (!isCanFlip) return;
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
        if (material != null && material.renderQueue != queueValue)
        {
            material.renderQueue = queueValue;
        }
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

    /// <summary>
    /// 获取变换数据 - 使用 JsonDataStructures 中的 TransformData
    /// </summary>
    public SceneElementTransformData GetTransformData()
    {
        return new SceneElementTransformData
        {
            position = SerializableVector3.FromUnityVector(transform.position.x, transform.position.y, transform.position.z),
            scale = SerializableVector3.FromUnityVector(transform.localScale.x, transform.localScale.y, transform.localScale.z)
        };
    }

    // ==========================================
    // 6. 序列帧动画设置
    // ==========================================

    public virtual void SetSpriteSheetParams(int rows, int columns, float speed)
    {
        if (material == null) return;

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
        if (texture == null || material == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.TransitionTo() - 纹理或材质为空，跳过");
            return;
        }

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
            material.SetFloat(Transition, Mathf.Lerp(0f, 1f, elapsed / duration));
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
        if (material == null) { onComplete?.Invoke(); return; }

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
        if (material == null) { onComplete?.Invoke(); yield break; }

        Color color = material.GetColor(ColorProp);
        float startAlpha = color.a;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
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
        if (sceneId != id) sceneId = id;
    }

    public virtual void SetTintColor(Color color)
    {
        tintColor = color;
        if (material != null) material.SetColor(ColorProp, color);
    }

    /// <summary>
    /// 从数据加载 - 使用 JsonDataStructures 中的 SceneElementData
    /// </summary>
    public virtual void LoadFromData(SceneElementData data)
    {
        if (data == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.LoadFromData() - 数据为空，跳过");
            return;
        }

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.LoadFromData() - 📥 从数据加载: ID={data.id}, Name={data.name}");

        if (data.transform != null)
        {
            Vector3 position = ToUnityVector(data.transform.position);
            Vector3 scale = ToUnityVector(data.transform.scale);
            Debug.Log($"[{LOG_TAG}] {gameObject.name}.LoadFromData() - 📍 位置: ({position.x:F2}, {position.y:F2}, {position.z:F2}), 大小: ({scale.x:F2}, {scale.y:F2}, {scale.z:F2})");
            SetTransformData(position, scale);
        }
        else
        {
            Debug.LogWarning($"[{LOG_TAG}] {gameObject.name}.LoadFromData() - transform数据为空");
        }

        Debug.Log($"[{LOG_TAG}] {gameObject.name}.LoadFromData() - ✅ 数据加载完成");
    }

    /// <summary>
    /// 转换为Unity Vector3
    /// </summary>
    public UnityEngine.Vector3 ToUnityVector(SerializableVector3 v)
    {
        return new UnityEngine.Vector3(v.x, v.y,v.z);
    }

    // ==========================================
    // Unity 生命周期
    // ==========================================

    protected virtual void OnDestroy()
    {
        SceneMatManager manager = FindObjectOfType<SceneMatManager>();
        if (manager != null) manager.UnregisterController(this);

        if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }
        if (transitionCoroutine != null) { StopCoroutine(transitionCoroutine); transitionCoroutine = null; }
        material = null;
    }

    protected virtual void OnEnable()
    {
        if (isInitialized && material != null)
        {
            ApplyAllProperties();
            UpdateRenderQueue();
        }
    }

    protected virtual void OnDisable()
    {
        if (enableBlink && material != null)
        {
            material.SetFloat(BlinkEnabled, 0f);
        }
    }
}