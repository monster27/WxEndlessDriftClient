// 路径：Assets/Scripts/UIView/Prefab/UI_FishTankDec.cs
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 鱼缸内摆放的装饰实例（挂在 decPrefab 上）
/// </summary>
public class UI_FishTankDec : MonoBehaviour
{
    [Header("===== 显示组件 =====")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Button clickBtn;

    private int _recordId;
    private int _category;
    private int _tankId;
    private int _decorationId;
    private Action<UI_FishTankDec> _onClick;

    public int RecordId => _recordId;
    public int Category => _category;
    public int TankId => _tankId;
    public int DecorationId => _decorationId;

    public RectTransform Rect
    {
        get
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) rect = gameObject.AddComponent<RectTransform>();
            return rect;
        }
    }

    public void Init(int recordId, int category, int tankId, int decorationId, Action<UI_FishTankDec> onClick)
    {
        _recordId = recordId;
        _category = category;
        _tankId = tankId;
        _decorationId = decorationId;
        _onClick = onClick;

        // 兜底：自动查找组件
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (iconImage == null) iconImage = GetComponentInChildren<Image>();
        if (iconImage != null) iconImage.raycastTarget = true;

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

        LoadIcon();
    }

    /// <summary>
    /// 设置装饰是否可交互
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (clickBtn != null)
        {
            clickBtn.interactable = interactable;
        }
    }

    public void SetFlip(bool flipped)
    {
        var rect = Rect;
        var scale = rect.localScale;
        scale.x = Mathf.Abs(scale.x) * (flipped ? -1f : 1f);
        rect.localScale = scale;
    }

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

    private void OnClick()
    {
        _onClick?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (clickBtn != null) clickBtn.onClick.RemoveAllListeners();
    }
}
