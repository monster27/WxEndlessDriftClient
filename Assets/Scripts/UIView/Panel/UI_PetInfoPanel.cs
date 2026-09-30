// ============================================================
// 文件: UI_PetInfoPanel.cs
// 说明: 宠物详情（左右切换，出战/陪伴）
// 路径: Assets/Scripts/UI/
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_PetInfoPanel : MonoBehaviour
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    public Image icon;
    public Image rarityBgImage;
    public Text nameText;
    public Text hungerText;
    public Text levelText;

    public Button feedBtn;
    public Button leftBtn;
    public Button companionBtn;
    public Text companionText;
    public Button rightBtn;
    public Button closeBtn;

    // ============================================================
    // 运行时数据
    // ============================================================

    private List<PlayerPetData> _pets = new List<PlayerPetData>();
    private int _currentIndex = 0;
    private UI_PetFeedPanel _feedPanel;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (feedBtn != null) feedBtn.onClick.AddListener(OnFeedClick);
        if (leftBtn != null) leftBtn.onClick.AddListener(OnLeftClick);
        if (rightBtn != null) rightBtn.onClick.AddListener(OnRightClick);
        if (companionBtn != null) companionBtn.onClick.AddListener(OnCompanionClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(Close);
    }

    // ============================================================
    // 打开
    // ============================================================

    public void Open(List<PlayerPetData> pets, int startIndex, UI_PetFeedPanel feedPanel)
    {
        if (pets == null || pets.Count == 0)
        {
            Z_Logger.LogWarning("[UI_PetInfoPanel] pets 为空");
            return;
        }

        _pets = new List<PlayerPetData>(pets);
        _currentIndex = Mathf.Clamp(startIndex, 0, _pets.Count - 1);
        _feedPanel = feedPanel;

        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // ============================================================
    // 切换
    // ============================================================

    private void OnLeftClick()
    {
        if (_pets.Count == 0) return;
        _currentIndex--;
        if (_currentIndex < 0) _currentIndex = _pets.Count - 1;
        Refresh();
    }

    private void OnRightClick()
    {
        if (_pets.Count == 0) return;
        _currentIndex++;
        if (_currentIndex >= _pets.Count) _currentIndex = 0;
        Refresh();
    }

    // ============================================================
    // 刷新
    // ============================================================

    private void Refresh()
    {
        if (_pets.Count == 0) return;

        var pet = _pets[_currentIndex];
        if (pet == null) return;

        // 图标
        if (icon != null)
        {
            string path = $"UI/Icon/PetIcons/{pet.petId}";
            AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
            {
                if (icon != null) icon.sprite = sprite;
            });
        }

        // 稀有度背景
        if (rarityBgImage != null)
        {
            string path = $"UI/Icon/RarityBackground/{pet.rarityId}";
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

        // 名字
        string displayName = string.IsNullOrEmpty(pet.nickname) ? pet.name : pet.nickname;
        if (nameText != null) nameText.text = displayName;

        // 饥饿度
        if (hungerText != null) hungerText.text = $"{pet.hunger}/{pet.maxHunger}";

        // 等级
        if (levelText != null) levelText.text = $"Lv.{pet.level}";

        // 陪伴按钮
        UpdateCompanionButton(pet);
    }

    private void UpdateCompanionButton(PlayerPetData pet)
    {
        bool isActive = pet.isActive;

        if (companionText != null)
        {
            companionText.text = isActive ? "已陪伴" : "陪伴";
        }

        if (companionBtn != null)
        {
            companionBtn.interactable = !isActive;
        }
    }

    // ============================================================
    // 陪伴
    // ============================================================

    private void OnCompanionClick()
    {
        if (_pets.Count == 0) return;
        var pet = _pets[_currentIndex];
        if (pet == null || pet.isActive) return;

        NetServerManager.Instance.SetActivePet(pet.petInstanceId, (success, message) =>
        {
            ShowTip(message);
            if (success)
            {
                // 刷新本地列表里的 isActive
                foreach (var p in _pets)
                {
                    p.isActive = (p.petInstanceId == pet.petInstanceId);
                }
                Refresh();
            }
        });
    }

    // ============================================================
    // 喂食
    // ============================================================

    private void OnFeedClick()
    {
        if (_feedPanel == null)
        {
            Z_Logger.LogWarning("[UI_PetInfoPanel] feedPanel 未绑定");
            return;
        }

        if (_pets.Count == 0) return;
        var pet = _pets[_currentIndex];
        if (pet == null) return;

        _feedPanel.Open(pet.petInstanceId, () =>
        {
            // 喂食完成后刷新
            Refresh();
        });
    }

    // ============================================================
    // 工具
    // ============================================================

    private void ShowTip(string msg)
    {
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, msg);
    }
}
