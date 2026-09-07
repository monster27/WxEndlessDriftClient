// ============================================================
// 文件: UI_FishTankDecPrefab.cs
// 说明: 鱼缸装饰预制体逻辑
// 路径: Assets/Scripts/UIView/Prefab/
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System;

public class UI_FishTankDecPrefab : MonoBehaviour
{
    [Header("===== 显示组件 =====")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private GameObject equippedMark;        // 已装备标记（仅对唯一品类）
    [SerializeField] private Text equippedCountText;        // 已装备数量（仅对 80/81）
    [SerializeField] private Text unEquippedCountText;      // 未装备数量（背包剩余）
    [SerializeField] private Button clickBtn;

    private FishTankDecData _config;
    private bool _owned;
    private bool _equipped;
    private int _equippedCount;
    private int _unEquippedCount;
    private int _currentTankId;
    private Action<UI_FishTankDecPrefab> _onClick;

    public FishTankDecData Config => _config;

    private void Awake()
    {
        if (clickBtn != null)
            clickBtn.onClick.AddListener(OnClick);
    }

    /// <summary>
    /// 初始化预制体
    /// </summary>
    public void Init(FishTankDecData config, bool owned, bool equipped, int equippedCount, int unEquippedCount,
        int tankId, Action<UI_FishTankDecPrefab> onClick)
    {
        _config = config;
        _owned = owned;
        _equipped = equipped;
        _equippedCount = equippedCount;
        _unEquippedCount = unEquippedCount;
        _currentTankId = tankId;
        _onClick = onClick;

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_config == null) return;

        // 名称
        if (nameText != null)
            nameText.text = _config.name;

        // 图标（从 Addressables 加载）
        LoadIcon();

        // 判断品类
        bool isMovable = (_config.categoryId == 80 || _config.categoryId == 81);

        if (isMovable)
        {
            // 80/81：不唯一，不显示装备标记，显示两个数字
            if (equippedMark != null) equippedMark.SetActive(false);
            if (equippedCountText != null)
            {
                equippedCountText.text = _equippedCount.ToString();
                equippedCountText.gameObject.SetActive(true);
            }
            if (unEquippedCountText != null)
            {
                unEquippedCountText.text = _unEquippedCount.ToString();
                unEquippedCountText.gameObject.SetActive(true);
            }
        }
        else
        {
            // 82-84：唯一，显示装备标记，隐藏装备数量文本，显示未装备数量（1或0）
            if (equippedMark != null)
                equippedMark.SetActive(_equipped);
            if (equippedCountText != null)
                equippedCountText.gameObject.SetActive(false);
            if (unEquippedCountText != null)
            {
                unEquippedCountText.text = _unEquippedCount > 0 ? "1" : "0";
                unEquippedCountText.gameObject.SetActive(true);
            }
        }

        // 按钮交互：如果未拥有或未解锁，禁用点击
        if (clickBtn != null)
            clickBtn.interactable = _owned;
    }

    private void LoadIcon()
    {
        if (iconImage == null) return;
        string iconPath = null;
        // 从 ItemData 获取图标路径
        var itemData = LoadDataManager.Instance?.GetItemById(_config.id);
        if (itemData != null && !string.IsNullOrEmpty(itemData.iconPath))
        {
            iconPath = itemData.iconPath;
        }
        else
        {
            // 降级：使用默认路径
            iconPath = $"UI/Icon/FishTankDecIcons/{_config.id}";
        }
        AssetManager.LoadFromAddressables<Sprite>(iconPath, (sprite, handle) =>
        {
            if (sprite != null)
                iconImage.sprite = sprite;
            else
                iconImage.gameObject.SetActive(false);
        });
    }

    private void OnClick()
    {
        _onClick?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (clickBtn != null)
            clickBtn.onClick.RemoveAllListeners();
    }
}
