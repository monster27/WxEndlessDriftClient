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

        // ✅ 常驻面板：在 Awake 注册，OnDestroy 注销
        CommunicateEvent.Register(PlayerDataManager.PetMessage.PetsUpdated.ToString(), OnPetsUpdated);
        CommunicateEvent.Register(PlayerDataManager.PetMessage.PetHungerChanged.ToString(), OnPetsUpdated);

        Z_Logger.Log("[UI_PetInfoPanel] Awake 完成，事件已注册");
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister(PlayerDataManager.PetMessage.PetsUpdated.ToString(), OnPetsUpdated);
        CommunicateEvent.Unregister(PlayerDataManager.PetMessage.PetHungerChanged.ToString(), OnPetsUpdated);

        Z_Logger.Log("[UI_PetInfoPanel] OnDestroy，事件已注销");
    }

    // ✅ Q5：不管面板是否 active，都刷新数据
    //        重资源（图标/稀有度背景）在 Refresh() 里根据 activeSelf 判断
    //        文本类字段（名字/饥饿度/等级/陪伴状态）无条件更新
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

        int instanceIdBefore = pet.petInstanceId;
        int hungerBefore = pet.hunger;

        // ✅ 从 PlayerDataManager 拿最新数据（_pets 是打开时的快照，
        //    PlayerDataManager.UpdatePlayerPets 会整个替换 List，
        //    导致快照里的对象引用和 DataManager 里的对象脱钩，
        //    饥饿度等字段会过期）
        bool gotFromManager = false;
        if (PlayerDataManager.Instance != null)
        {
            var latestPet = PlayerDataManager.Instance.GetPetByInstanceId(pet.petInstanceId);
            if (latestPet != null)
            {
                pet = latestPet;
                _pets[_currentIndex] = latestPet;
                gotFromManager = true;
            }
            else
            {
                Z_Logger.LogWarning($"[UI_PetInfoPanel] Refresh: PlayerDataManager 里找不到 instanceId={pet.petInstanceId}");
            }
        }
        else
        {
            Z_Logger.LogWarning("[UI_PetInfoPanel] Refresh: PlayerDataManager.Instance 为 null");
        }

        Z_Logger.Log($"[UI_PetInfoPanel] Refresh: instanceId={pet.petInstanceId}, hunger={pet.hunger}/{pet.maxHunger}, " +
                     $"name={pet.name}, level={pet.level}, isActive={pet.isActive}, " +
                     $"activeSelf={gameObject.activeSelf}, gotFromManager={gotFromManager}, " +
                     $"(before: hunger={hungerBefore})");

        // ============================================================
        // 重资源：只在面板 active 时加载（避免面板没打开时浪费资源加载）
        // ============================================================
        if (gameObject.activeSelf)
        {
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
        }

        // ============================================================
        // 文本类字段：无条件更新（面板没打开时也刷新，等打开就能看到最新值）
        // ============================================================

        // 名字
        string displayName = string.IsNullOrEmpty(pet.nickname) ? pet.name : pet.nickname;
        if (nameText != null) nameText.text = displayName;

        // 饥饿度
        if (hungerText != null)
        {
            string hungerStr = $"{pet.hunger}/{pet.maxHunger}";
            hungerText.text = hungerStr;
            Z_Logger.Log($"[UI_PetInfoPanel] 更新 hungerText = \"{hungerStr}\"");
        }
        else
        {
            Z_Logger.LogWarning("[UI_PetInfoPanel] hungerText 未绑定！");
        }

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
                // 喂食面板关闭后刷新
                Refresh();
            },
            onFed: () =>
            {
                Z_Logger.Log("[UI_PetInfoPanel] feedPanel onFed 回调（喂食成功）");
                // ✅ 喂食成功立即刷新（不等面板关闭）
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
