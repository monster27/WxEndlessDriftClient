using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 钓鱼提示动画控制器 - 精简版
/// 适用于 Quad + MeshRenderer + 材质
/// </summary>
public class FishingTipAniCtrl : MonoBehaviour
{
    [Header("闪烁叠加图片")]
    [SerializeField] private Texture2D blinkTexture;
    [SerializeField] private Color blinkColor = Color.white;
    [SerializeField] private bool enableBlink = false;
    [SerializeField] private float blinkInterval = 0.5f;
    [SerializeField] private float blinkOffset = 0f;

    private Material material;
    private bool isInitialized = false;
    private Renderer targetRenderer;
    private Texture2D mainTexture;

    // 稀有度颜色缓存（从 LoadDataManager 加载）
    private static Dictionary<int, Color> rarityColorCache = new Dictionary<int, Color>();
    private static bool isRarityDataLoaded = false;

    // Shader属性ID
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int BlinkTex = Shader.PropertyToID("_BlinkTex");
    private static readonly int BlinkColor = Shader.PropertyToID("_BlinkColor");
    private static readonly int BlinkEnabled = Shader.PropertyToID("_BlinkEnabled");
    private static readonly int BlinkInterval = Shader.PropertyToID("_BlinkInterval");
    private static readonly int BlinkOffset = Shader.PropertyToID("_BlinkOffset");

    // ========== 公共属性 ==========
    public bool IsBlinking => enableBlink;
    public Color CurrentColor => blinkColor;

    // ========== 公共事件 ==========
    public event Action OnBlinkStart;
    public event Action OnBlinkStop;

    private void Awake()
    {
        LoadRarityData();
        Initialize();
    }

    // ========== 稀有度数据加载（从 LoadDataManager 获取） ==========

    private void LoadRarityData()
    {
        if (isRarityDataLoaded) return;

        try
        {
            // ⭐ 从 LoadDataManager 获取稀有度数据
            if (LoadDataManager.Instance == null)
            {
                Debug.LogWarning("[FishingTipAniCtrl] LoadDataManager 未初始化，无法加载稀有度数据");
                return;
            }

            List<RarityData> rarities = LoadDataManager.Instance.rarities;
            if (rarities == null || rarities.Count == 0)
            {
                Debug.LogWarning("[FishingTipAniCtrl] LoadDataManager 中没有稀有度数据");
                return;
            }

            rarityColorCache.Clear();
            foreach (var rarity in rarities)
            {
                if (!string.IsNullOrEmpty(rarity.colorCode) && ColorUtility.TryParseHtmlString(rarity.colorCode, out Color color))
                {
                    rarityColorCache[rarity.id] = color;
                }
                else
                {
                    // 备用：如果 colorCode 无效，尝试使用 color 字段
                    Debug.LogWarning($"[FishingTipAniCtrl] 稀有度 {rarity.id} 的颜色代码无效: {rarity.colorCode}");
                }
            }

            isRarityDataLoaded = true;
            Debug.Log($"[FishingTipAniCtrl] 从 LoadDataManager 加载稀有度数据完成，共 {rarityColorCache.Count} 个");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FishingTipAniCtrl] 加载稀有度数据异常: {e.Message}");
        }
    }

    private Color GetRarityColor(int rarityId)
    {
        if (rarityColorCache.TryGetValue(rarityId, out Color color))
        {
            return color;
        }

        // 尝试从 LoadDataManager 实时获取
        if (LoadDataManager.Instance != null)
        {
            RarityData rarity = LoadDataManager.Instance.GetRarityById(rarityId);
            if (rarity != null && !string.IsNullOrEmpty(rarity.colorCode) && ColorUtility.TryParseHtmlString(rarity.colorCode, out Color newColor))
            {
                rarityColorCache[rarityId] = newColor;
                return newColor;
            }
        }

        Debug.LogWarning($"[FishingTipAniCtrl] 未找到稀有度ID: {rarityId}，使用白色");
        return Color.white;
    }

    // ========== 初始化 ==========

    private void Initialize()
    {
        if (isInitialized) return;

        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer == null)
        {
            Debug.LogError($"[FishingTipAniCtrl] 找不到 Renderer！物体: {gameObject.name}");
            return;
        }

        Shader shader = Shader.Find("Custom/DefaultSprite");
        if (shader == null)
        {
            Debug.LogError("[FishingTipAniCtrl] 找不到 Custom/DefaultSprite Shader！");
            return;
        }

        Material originalMat = targetRenderer.sharedMaterial;
        if (originalMat != null && originalMat.HasProperty(MainTex))
        {
            Texture tex = originalMat.GetTexture(MainTex);
            if (tex != null)
            {
                mainTexture = tex as Texture2D;
            }
        }

        if (mainTexture == null)
        {
            Debug.LogWarning($"[FishingTipAniCtrl] 材质中没有主纹理！物体: {gameObject.name}");
        }

        material = new Material(shader);

        if (mainTexture != null)
        {
            material.SetTexture(MainTex, mainTexture);
        }

        if (originalMat != null && originalMat.HasProperty(ColorProp))
        {
            material.SetColor(ColorProp, originalMat.GetColor(ColorProp));
        }

        targetRenderer.material = material;
        ApplyMaterialProperties();

        isInitialized = true;
    }

    private void ApplyMaterialProperties()
    {
        if (material == null) return;

        if (mainTexture != null)
        {
            material.SetTexture(MainTex, mainTexture);
        }

        material.SetTexture(BlinkTex, blinkTexture);
        material.SetColor(BlinkColor, blinkColor);

        bool hasTexture = blinkTexture != null;
        material.SetFloat(BlinkEnabled, (enableBlink && hasTexture) ? 1f : 0f);
        material.SetFloat(BlinkInterval, Mathf.Max(blinkInterval, 0.01f));
        material.SetFloat(BlinkOffset, blinkOffset);
    }

    // ========== 核心方法 ==========

    /// <summary>
    /// 设置主纹理（换图）
    /// </summary>
    public void SetMainTexture(Texture2D texture)
    {
        if (texture == null)
        {
            Debug.LogWarning($"[FishingTipAniCtrl] 设置的主纹理为空！物体: {gameObject.name}");
            return;
        }

        mainTexture = texture;
        if (material != null)
        {
            material.SetTexture(MainTex, texture);
        }
    }

    /// <summary>
    /// 开启闪烁
    /// </summary>
    public void StartBlink()
    {
        if (!isInitialized) return;
        if (blinkTexture == null)
        {
            Debug.LogWarning($"[FishingTipAniCtrl] 闪烁纹理为空，无法开启闪烁！");
            return;
        }
        enableBlink = true;
        ApplyMaterialProperties();
        OnBlinkStart?.Invoke();
    }

    /// <summary>
    /// 关闭闪烁
    /// </summary>
    public void StopBlink()
    {
        if (!isInitialized) return;
        enableBlink = false;
        ApplyMaterialProperties();
        OnBlinkStop?.Invoke();
    }

    /// <summary>
    /// 设置闪烁叠加图片
    /// </summary>
    public void SetBlinkTexture(Texture2D texture)
    {
        blinkTexture = texture;
        ApplyMaterialProperties();
    }

    /// <summary>
    /// 设置闪烁颜色
    /// </summary>
    public void SetBlinkColor(Color color)
    {
        blinkColor = color;
        ApplyMaterialProperties();
    }

    /// <summary>
    /// 设置闪烁间隔
    /// </summary>
    public void SetBlinkInterval(float interval)
    {
        blinkInterval = Mathf.Max(interval, 0.01f);
        ApplyMaterialProperties();
    }

    /// <summary>
    /// 设置闪烁偏移
    /// </summary>
    public void SetBlinkOffset(float offset)
    {
        blinkOffset = offset;
        ApplyMaterialProperties();
    }

    /// <summary>
    /// 设置闪烁状态（指定颜色），闪烁2秒后自动关闭
    /// </summary>
    public void SetBlinkState(Color color,float struggleTime = 2, float interval = 0.5f)
    {
        if (!isInitialized) return;

        enableBlink = true;
        blinkColor = color;
        blinkInterval = Mathf.Max(interval, 0.01f);
        ApplyMaterialProperties();
        OnBlinkStart?.Invoke();

        StopCoroutine(nameof(AutoStopBlinkCoroutine));
        StartCoroutine(AutoStopBlinkCoroutine(struggleTime));
    }

    /// <summary>
    /// 设置闪烁状态（指定稀有度ID），自动获取颜色，闪烁2秒后自动关闭
    /// </summary>
    public void SetBlinkState(int rarityId, float interval = 0.5f)
    {
        Color color = GetRarityColor(rarityId);
        SetBlinkState(color, interval);
    }

    /// <summary>
    /// 关闭闪烁（重载，用于统一调用）
    /// </summary>
    public void SetBlinkState(bool enabled)
    {
        if (enabled)
        {
            StartBlink();
        }
        else
        {
            StopBlink();
        }
    }

    private System.Collections.IEnumerator AutoStopBlinkCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (enableBlink)
        {
            enableBlink = false;
            ApplyMaterialProperties();
            OnBlinkStop?.Invoke();
        }
    }

    /// <summary>
    /// 闪烁一次（指定颜色），持续指定时间后自动停止
    /// </summary>
    public void BlinkOnce(Color color, float duration = 0.5f, float interval = 0.5f)
    {
        SetBlinkColor(color);
        SetBlinkInterval(interval);
        StartBlink();
        StartCoroutine(StopBlinkAfterDelay(duration));
    }

    /// <summary>
    /// 闪烁一次（指定稀有度ID），自动获取颜色，持续指定时间后自动停止
    /// </summary>
    public void BlinkOnce(int rarityId, float duration = 0.5f, float interval = 0.5f)
    {
        Color color = GetRarityColor(rarityId);
        BlinkOnce(color, duration, interval);
    }

    private System.Collections.IEnumerator StopBlinkAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StopBlink();
    }

    // ========== Unity 生命周期 ==========

    private void OnEnable()
    {
        if (isInitialized && material != null)
        {
            ApplyMaterialProperties();
        }
    }

    private void OnDisable()
    {
        if (enableBlink && material != null)
        {
            material.SetFloat(BlinkEnabled, 0f);
        }
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            DestroyImmediate(material);
            material = null;
        }
    }

    // ========== 功能测试 ==========
    [Header("测试")]
    [SerializeField] private bool enableTest = true;

    private void Update()
    {
        if (!enableTest || !isInitialized) return;

        if (Input.GetKeyDown(KeyCode.Keypad1)) { SetBlinkState(201); Debug.Log("[测试] 普通"); }
        if (Input.GetKeyDown(KeyCode.Keypad2)) { SetBlinkState(202); Debug.Log("[测试] 罕见"); }
        if (Input.GetKeyDown(KeyCode.Keypad3)) { SetBlinkState(203); Debug.Log("[测试] 稀有"); }
        if (Input.GetKeyDown(KeyCode.Keypad4)) { SetBlinkState(204); Debug.Log("[测试] 史诗"); }
        if (Input.GetKeyDown(KeyCode.Keypad5)) { SetBlinkState(205); Debug.Log("[测试] 传说"); }
        if (Input.GetKeyDown(KeyCode.Keypad6)) { SetBlinkState(206); Debug.Log("[测试] 幻想"); }
        if (Input.GetKeyDown(KeyCode.Keypad0)) { StopBlink(); Debug.Log("[测试] 关闭闪烁"); }
    }
}