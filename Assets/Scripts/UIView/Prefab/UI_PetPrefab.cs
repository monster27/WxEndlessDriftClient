// ============================================================
// 文件: UI_PetPrefab.cs
// 说明: 单个宠物按钮（prefab）
//       仿 UI_FishBagPrefab 风格
// 路径: Assets/Scripts/UI/
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_PetPrefab : MonoBehaviour
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    public Button button;
    public Image bg;
    public Image icon;
    public Image rarityBgImage;

    public GameObject selectedObj;
    public GameObject lockObj;
    public GameObject activeObj;      // 出战标记（可选）
    public GameObject defaultObj;     // 默认宠物标记（可选）

    public Text nameText;
    public Text priceText;

    // ============================================================
    // 运行时数据
    // ============================================================

    private PlayerPetData _pet;
    private bool _isSelected = false;
    private Action<UI_PetPrefab> _onSelectionChanged;

    public int PetInstanceId => _pet?.petInstanceId ?? 0;
    public int PetId => _pet?.petId ?? 0;
    public bool IsSelected => _isSelected;
    public bool IsLocked => _pet?.isLocked ?? false;
    public bool IsActive => _pet?.isActive ?? false;
    public bool IsDefault => _pet?.isDefault ?? false;
    public PlayerPetData PetData => _pet;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClick);
        }
    }

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(PlayerPetData pet, Action<UI_PetPrefab> onSelectionChanged)
    {
        _pet = pet;
        _onSelectionChanged = onSelectionChanged;
        _isSelected = false;

        RefreshDisplay();
        UpdateSelectedVisual();
    }

    // ============================================================
    // 显示刷新
    // ============================================================

    private void RefreshDisplay()
    {
        if (_pet == null) return;

        // 图标
        LoadIcon();

        // 稀有度背景
        LoadRarityBg();

        // 名字（优先昵称）
        string displayName = string.IsNullOrEmpty(_pet.nickname) ? _pet.name : _pet.nickname;
        if (nameText != null) nameText.text = displayName;

        // 售价
        if (priceText != null)
        {
            int price = GetPetSellPrice(_pet.petId);
            priceText.text = price > 0 ? $"{price}" : "--";
        }

        // 锁定 / 出战 / 默认
        if (lockObj != null) lockObj.SetActive(_pet.isLocked);
        if (activeObj != null) activeObj.SetActive(_pet.isActive);
        if (defaultObj != null) defaultObj.SetActive(_pet.isDefault);
    }

    // ============================================================
    // 选择状态
    // ============================================================

    public void SetSelection(bool selected)
    {
        _isSelected = selected;
        UpdateSelectedVisual();
        _onSelectionChanged?.Invoke(this);
    }

    public void SetLocked(bool locked)
    {
        if (_pet != null) _pet.isLocked = locked;
        if (lockObj != null) lockObj.SetActive(locked);
    }

    private void UpdateSelectedVisual()
    {
        if (selectedObj != null) selectedObj.SetActive(_isSelected);
    }

    // ============================================================
    // 点击
    // ============================================================

    private void OnButtonClick()
    {
        // 默认宠物 / 出战宠物不能选中出售（可选）
        // 这里允许选中，由 PetView 在出售/锁定时过滤

        _isSelected = !_isSelected;
        UpdateSelectedVisual();
        _onSelectionChanged?.Invoke(this);
    }

    // ============================================================
    // 图标 / 背景
    // ============================================================

    private void LoadIcon()
    {
        if (icon == null || _pet == null) return;

        string path = $"UI/Icon/PetIcons/{_pet.petId}";

        AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
        {
            if (icon != null) icon.sprite = sprite;
        });
    }

    private void LoadRarityBg()
    {
        if (rarityBgImage == null || _pet == null) return;

        string path = $"UI/Icon/RarityBackground/{_pet.rarityId}";

        AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
        {
            if (sprite != null)
            {
                rarityBgImage.sprite = sprite;
                rarityBgImage.gameObject.SetActive(true);
            }
            else
            {
                rarityBgImage.gameObject.SetActive(false);
            }
        });
    }

    // ============================================================
    // 工具
    // ============================================================

    private int GetPetSellPrice(int petId)
    {
        var item = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetItemById(petId) : null;
        return item?.sellPrice ?? 0;
    }
}
