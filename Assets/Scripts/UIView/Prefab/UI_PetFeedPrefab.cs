// ============================================================
// 文件: UI_PetFeedPrefab.cs
// 说明: 单条鱼按钮（prefab）
// 路径: Assets/Scripts/UI/
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_PetFeedPrefab : MonoBehaviour
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    public Button button;
    public Image icon;
    public Image rarityBgImage;
    public GameObject selectedObj;

    public Text nameText;
    public Text feedAmountText;

    // ============================================================
    // 运行时数据
    // ============================================================

    private FishDetailData _fish;
    private ItemData _itemData;   // ✅ 从 items.json 拿 iconPath / name
    private bool _isSelected = false;
    private Action<UI_PetFeedPrefab> _onSelected;

    public int FishDetailId => _fish?.id ?? 0;
    public int FishId => _fish?.fishId ?? 0;
    public bool IsSelected => _isSelected;
    public FishDetailData Fish => _fish;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(FishDetailData fish, Action<UI_PetFeedPrefab> onSelected)
    {
        _fish = fish;
        _onSelected = onSelected;
        _isSelected = false;

        // ✅ 从 items.json 拿 ItemData（iconPath / name 都从这里来）
        _itemData = LoadDataManager.Instance != null
            ? LoadDataManager.Instance.GetItemById(fish.fishId)
            : null;

        RefreshDisplay();
        UpdateSelectedVisual();
    }

    // ============================================================
    // 显示
    // ============================================================

    private void RefreshDisplay()
    {
        if (_fish == null) return;

        // 名字：优先 ItemData.name（items.json），兜底 FishData.name
        if (nameText != null)
        {
            string displayName = _itemData?.name;
            if (string.IsNullOrEmpty(displayName))
            {
                var fishData = LoadDataManager.Instance != null
                    ? LoadDataManager.Instance.GetFishById(_fish.fishId) : null;
                displayName = fishData?.name ?? $"鱼{_fish.fishId}";
            }
            nameText.text = displayName;
        }

        // 喂食量：直接用服务器返回的 feedRestored
        if (feedAmountText != null)
        {
            int restored = _fish.feedRestored;
            if (restored <= 0)
            {
                // 兜底：如果服务器没返回，客户端自己算
                var fishData = LoadDataManager.Instance != null
                    ? LoadDataManager.Instance.GetFishById(_fish.fishId) : null;
                restored = CalcFeedRestored(fishData, _fish.weight);
            }
            feedAmountText.text = $"+{restored}";
        }

        // 图标
        LoadIcon();

        // 稀有度背景
        LoadRarityBg();
    }

    // ============================================================
    // 图标
    // ============================================================

    private void LoadIcon()
    {
        if (icon == null) return;

        // ✅ 从 ItemData 拿 iconPath
        if (_itemData == null || string.IsNullOrEmpty(_itemData.iconPath))
        {
            Z_Logger.LogWarning($"[UI_PetFeedPrefab] iconPath 为空: fishId={_fish?.fishId}");
            icon.sprite = null;
            icon.color = Color.gray;
            return;
        }

        string basePath = _itemData.iconPath;
        string loadPath = (_fish != null && _fish.isShiny) ? basePath + "_s" : basePath;

        AssetManager.LoadFromAddressables<Sprite>(loadPath, (sprite, handle) =>
        {
            if (sprite != null)
            {
                if (icon != null)
                {
                    icon.sprite = sprite;
                    icon.color = Color.white;
                }
            }
            else
            {
                // 闪光图标加载失败 → 回退到普通图标
                if (_fish != null && _fish.isShiny)
                {
                    AssetManager.LoadFromAddressables<Sprite>(basePath, (s2, h2) =>
                    {
                        if (icon != null)
                        {
                            icon.sprite = s2;
                            icon.color = s2 != null ? Color.white : Color.gray;
                        }
                    });
                }
                else
                {
                    if (icon != null)
                    {
                        icon.sprite = null;
                        icon.color = Color.gray;
                    }
                }
            }
        });
    }

    // ============================================================
    // 稀有度背景
    // ============================================================

    private void LoadRarityBg()
    {
        if (rarityBgImage == null || _fish == null) return;

        int rarityId = 0;
        var fishData = LoadDataManager.Instance != null
            ? LoadDataManager.Instance.GetFishById(_fish.fishId) : null;
        if (fishData != null) rarityId = fishData.rarityId;

        string path = $"UI/Icon/RarityBackground/{rarityId}";
        AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
        {
            if (sprite != null)
            {
                rarityBgImage.sprite = sprite;
                rarityBgImage.gameObject.SetActive(true);
            }
            else
            {
                // 兜底：加载默认背景
                AssetManager.LoadFromAddressables<Sprite>("UI/Icon/RarityBackground/0", (def, h) =>
                {
                    if (def != null)
                    {
                        rarityBgImage.sprite = def;
                        rarityBgImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        rarityBgImage.gameObject.SetActive(false);
                    }
                });
            }
        });
    }

    // ============================================================
    // 选择
    // ============================================================

    public void SetSelection(bool selected)
    {
        _isSelected = selected;
        UpdateSelectedVisual();
    }

    private void UpdateSelectedVisual()
    {
        if (selectedObj != null) selectedObj.SetActive(_isSelected);
    }

    private void OnClick()
    {
        _isSelected = !_isSelected;
        UpdateSelectedVisual();
        _onSelected?.Invoke(this);
    }

    // ============================================================
    // 兜底：客户端算喂食量（服务器已算，这里是保险）
    // ============================================================

    private int CalcFeedRestored(FishData fishData, float weight)
    {
        if (fishData == null || fishData.feedAmount <= 0) return 0;
        if (fishData.baseWeight <= 0) return fishData.feedAmount;

        int restored = (int)Math.Floor(fishData.feedAmount * (weight / fishData.baseWeight));
        return restored > 0 ? restored : 1;
    }
}
