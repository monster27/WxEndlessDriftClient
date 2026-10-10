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
    private bool _isInitialized = false;

    // ============================================================
    // 初始化 / 销毁（由 PetView 统一调度）
    // ============================================================

    /// <summary>
    /// 初始化：绑按钮 + 注册事件
    /// 由 PetView.BaseViewInit() 调用
    /// </summary>
    public void Init()
    {
        if (_isInitialized) return;

        if (feedBtn != null) feedBtn.onClick.AddListener(OnFeedClick);
        if (leftBtn != null) leftBtn.onClick.AddListener(OnLeftClick);
        if (rightBtn != null) rightBtn.onClick.AddListener(OnRightClick);
        if (companionBtn != null) companionBtn.onClick.AddListener(OnCompanionClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(Close);

        // ★ 修复：PetsUpdated 必须用常量，与 PlayerDataManager 广播一致
        CommunicateEvent.Register(CommunicateEvent.EVENT_PETS_UPDATED, OnPetsUpdated);
        CommunicateEvent.Register(PlayerDataManager.PetMessage.PetHungerChanged.ToString(), OnPetsUpdated);

        _isInitialized = true;
        Z_Logger.Log("[UI_PetInfoPanel] Init 完成，事件已注册");
    }

    /// <summary>
    /// 销毁：注销事件
    /// 由 PetView.OnDestroy() 调用
    /// </summary>
    public void Dispose()
    {
        if (!_isInitialized) return;

        // ★ 修复：注销和注册必须同名
        CommunicateEvent.Unregister(CommunicateEvent.EVENT_PETS_UPDATED, OnPetsUpdated);
        CommunicateEvent.Unregister(PlayerDataManager.PetMessage.PetHungerChanged.ToString(), OnPetsUpdated);

        _pets.Clear();
        _currentIndex = 0;
        _feedPanel = null;

        _isInitialized = false;
        Z_Logger.Log("[UI_PetInfoPanel] Dispose 完成，事件已注销");
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnPetsUpdated()
    {
        Z_Logger.Log($"[UI_PetInfoPanel] OnPetsUpdated 触发, activeSelf={gameObject.activeSelf}, _pets.Count={_pets.Count}, _currentIndex={_currentIndex}");
        Refresh();
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

        Z_Logger.Log($"[UI_PetInfoPanel] Open: pets.Count={pets.Count}, startIndex={startIndex}, _currentIndex={_currentIndex}");

        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        Z_Logger.Log("[UI_PetInfoPanel] Close");
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
        Z_Logger.Log($"[UI_PetInfoPanel] OnLeftClick -> _currentIndex={_currentIndex}");
        Refresh();
    }

    private void OnRightClick()
    {
        if (_pets.Count == 0) return;
        _currentIndex++;
        if (_currentIndex >= _pets.Count) _currentIndex = 0;
        Z_Logger.Log($"[UI_PetInfoPanel] OnRightClick -> _currentIndex={_currentIndex}");
        Refresh();
    }

    // ============================================================
    // 刷新
    // ============================================================

    private void Refresh()
    {
        if (_pets.Count == 0)
        {
            Z_Logger.Log("[UI_PetInfoPanel] Refresh: _pets 为空，跳过");
            return;
        }

        var pet = _pets[_currentIndex];
        if (pet == null)
        {
            Z_Logger.LogWarning($"[UI_PetInfoPanel] Refresh: _pets[{_currentIndex}] 为 null");
            return;
        }

        // 从 PlayerDataManager 拿最新数据（_pets 是打开时的快照，字段可能过期）
        if (PlayerDataManager.Instance != null)
        {
            var latestPet = PlayerDataManager.Instance.GetPetByInstanceId(pet.petInstanceId);
            if (latestPet != null)
            {
                pet = latestPet;
                _pets[_currentIndex] = latestPet;
            }
            else
            {
                Z_Logger.LogWarning($"[UI_PetInfoPanel] Refresh: PlayerDataManager 里找不到 instanceId={pet.petInstanceId}");
            }
        }

        // 重资源：只在面板 active 时加载
        if (gameObject.activeSelf)
        {
            if (icon != null)
            {
                string path = $"UI/Icon/PetIcons/{pet.petId}";
                AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
                {
                    if (icon != null) icon.sprite = sprite;
                });
            }

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
        }

        // 文本类字段：无条件更新
        string displayName = string.IsNullOrEmpty(pet.nickname) ? pet.name : pet.nickname;
        if (nameText != null) nameText.text = displayName;

        if (hungerText != null)
        {
            hungerText.text = $"{pet.hunger}/{pet.maxHunger}";
        }

        if (levelText != null) levelText.text = $"Lv.{pet.level}";

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

        Z_Logger.Log($"[UI_PetInfoPanel] OnCompanionClick: instanceId={pet.petInstanceId}");

        NetServerManager.Instance.SetActivePet(pet.petInstanceId, (success, message) =>
        {
            Z_Logger.Log($"[UI_PetInfoPanel] SetActivePet 回调: success={success}, message={message}");
            ShowTip(message);
            if (success)
            {
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

        Z_Logger.Log($"[UI_PetInfoPanel] OnFeedClick: 打开喂食面板, petInstanceId={pet.petInstanceId}");

        _feedPanel.Open(
            pet.petInstanceId,
            onClosed: () =>
            {
                Z_Logger.Log("[UI_PetInfoPanel] feedPanel onClosed 回调");
                Refresh();
            },
            onFed: () =>
            {
                Z_Logger.Log("[UI_PetInfoPanel] feedPanel onFed 回调（喂食成功）");
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
