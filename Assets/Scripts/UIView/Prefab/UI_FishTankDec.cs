using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 鱼缸内摆放的装饰实例（挂在 decPrefab 上）
/// 结构：
///   UI_FishTankDec (根，RectTransform + Button)
///     ├── Icon  (Image，显示贴图)
///     └── Frame (Image，白色透明，仅用于编辑器预览显示边界)
/// 尺寸规则：
///   摆设(80): Frame = (jsonW, 20)      Icon = (jsonW, jsonH)   pivot = (0.5, 0)
///   挂饰(81): Frame = (jsonW, jsonH)   Icon = (jsonW, jsonH)   pivot = (0.5, 0.5)
/// 预制体必须绑定 Icon / Frame，缺失直接报错
/// </summary>
public class UI_FishTankDec : MonoBehaviour
{
    /// <summary>摆设 Frame 高度写死</summary>
    public const float DISPLAY_FRAME_HEIGHT = 20f;

    [Header("===== 显示组件 =====")]
    public Image iconImage;
    public Image frameImage;
    [SerializeField] private Button clickBtn;

    private int _recordId;
    private int _category;
    private int _tankId;
    private int _decorationId;
    private Action<UI_FishTankDec> _onClick;

    private int _width = 100;
    private int _height = 100;

    public int RecordId => _recordId;
    public int Category => _category;
    public int TankId => _tankId;
    public int DecorationId => _decorationId;
    public int Width => _width;
    public int Height => _height;

    public Image IconImage => iconImage;
    public Image FrameImage => frameImage;

    public RectTransform Rect
    {
        get
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) rect = gameObject.AddComponent<RectTransform>();
            return rect;
        }
    }

    public RectTransform IconRect => iconImage != null ? iconImage.rectTransform : null;
    public RectTransform FrameRect => frameImage != null ? frameImage.rectTransform : null;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void Init(int recordId, int category, int tankId, int decorationId, Action<UI_FishTankDec> onClick)
    {
        Init(recordId, category, tankId, decorationId, 100, 100, onClick);
    }

    public void Init(int recordId, int category, int tankId, int decorationId,
                     int width, int height, Action<UI_FishTankDec> onClick)
    {
        _recordId = recordId;
        _category = category;
        _tankId = tankId;
        _decorationId = decorationId;
        _onClick = onClick;
        _width = width <= 0 ? 100 : width;
        _height = height <= 0 ? 100 : height;

        if (iconImage == null)
        {
            Z_Logger.LogError($"[UI_FishTankDec] id={decorationId} 缺少 Icon Image 引用，请检查预制体");
            return;
        }
        if ((category == 80 || category == 81) && frameImage == null)
        {
            Z_Logger.LogError($"[UI_FishTankDec] id={decorationId} 缺少 Frame Image 引用，请检查预制体");
            return;
        }

        iconImage.raycastTarget = true;

        if (clickBtn == null) clickBtn = GetComponent<Button>();
        if (clickBtn == null) clickBtn = GetComponentInChildren<Button>();
        if (clickBtn == null) clickBtn = gameObject.AddComponent<Button>();

        if (clickBtn != null)
        {
            if (clickBtn.targetGraphic == null && iconImage != null)
                clickBtn.targetGraphic = iconImage;
            clickBtn.onClick.RemoveAllListeners();
            clickBtn.onClick.AddListener(OnClick);
        }

        SetFrameVisible(false);
        ApplySize();
        LoadIcon();
    }

    /// <summary>
    /// 设置装饰宽高（会按 category 分派到 Frame / Icon）
    /// </summary>
    public void SetDecorationSize(int width, int height, int category)
    {
        _width = width <= 0 ? 100 : width;
        _height = height <= 0 ? 100 : height;
        _category = category;
        ApplySize();
    }

    /// <summary>
    /// 应用尺寸到 Frame / Icon / 根
    /// </summary>
    private void ApplySize()
    {
        var root = Rect;
        var iconRect = IconRect;
        var frameRect = FrameRect;

        float frameW, frameH;
        Vector2 pivot;

        if (_category == 80)          // 摆设：底对齐
        {
            frameW = _width;
            frameH = DISPLAY_FRAME_HEIGHT;
            pivot = new Vector2(0.5f, 0f);

            if (iconRect != null)
            {
                iconRect.pivot = pivot;
                iconRect.sizeDelta = new Vector2(_width, _height);
            }
        }
        else if (_category == 81)     // 挂饰：中心对齐
        {
            frameW = _width;
            frameH = _height;
            pivot = new Vector2(0.5f, 0.5f);

            if (iconRect != null)
            {
                iconRect.pivot = pivot;
                iconRect.sizeDelta = new Vector2(_width, _height);
            }
        }
        else
        {
            return; // 其他品类不处理尺寸
        }

        if (frameRect != null)
        {
            frameRect.pivot = pivot;
            frameRect.sizeDelta = new Vector2(frameW, frameH);
        }

        root.pivot = pivot;
        root.sizeDelta = new Vector2(frameW, frameH);
    }

    /// <summary>
    /// 设置 Frame 可见性（运行时透明；编辑器预览白半透明）
    /// </summary>
    public void SetFrameVisible(bool visible)
    {
        if (frameImage == null) return;
        frameImage.color = visible
            ? new Color(1f, 1f, 1f, 0.35f)
            : new Color(1f, 1f, 1f, 0f);
        frameImage.raycastTarget = false;
    }

    /// <summary>
    /// 编辑器预览：Frame 显示在 Icon 之上
    /// </summary>
    public void SetFrameOnTopInEditor()
    {
        if (frameImage == null || iconImage == null) return;
        frameImage.transform.SetAsLastSibling();
    }

    public void SetInteractable(bool interactable)
    {
        if (clickBtn != null) clickBtn.interactable = interactable;
    }

    public void SetFlip(bool flipped)
    {
        var rect = Rect;
        var scale = rect.localScale;
        scale.x = Mathf.Abs(scale.x) * (flipped ? -1f : 1f);
        rect.localScale = scale;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void LoadIcon()
    {
        if (iconImage == null) return;

        var itemData = LoadDataManager.Instance?.GetItemById(_decorationId);
        if (itemData != null && !string.IsNullOrEmpty(itemData.iconPath))
        {
            AssetManager.LoadFromAddressables<Sprite>(itemData.iconPath, (sprite, handle) =>
            {
                if (sprite != null && iconImage != null) iconImage.sprite = sprite;
            });
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器预览专用：直接设置尺寸 / 贴图 / Frame 显示
    /// （不依赖 LoadDataManager）
    /// </summary>
    public void EditorApplySize(int category, int width, int height, Sprite iconSprite)
    {
        _category = category;
        _width = width <= 0 ? 100 : width;
        _height = height <= 0 ? 100 : height;

        if (iconImage == null || ((category == 80 || category == 81) && frameImage == null))
        {
            Z_Logger.LogError("[UI_FishTankDec] EditorApplySize 缺 Icon/Frame 组件");
            return;
        }

        if (iconSprite != null) iconImage.sprite = iconSprite;

        ApplySize();

        SetFrameVisible(true);
        SetFrameOnTopInEditor();
    }
#endif
    private void OnClick()
    {
        _onClick?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (clickBtn != null) clickBtn.onClick.RemoveAllListeners();
    }
}
