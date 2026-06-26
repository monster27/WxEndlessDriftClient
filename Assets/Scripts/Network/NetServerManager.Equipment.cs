using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager 
{
    // 装备数据
    private int equippedRodId = 3001;
    private int equippedLineId = 3101;
    private int equippedHookId = 3201;
    private int equippedSkill1Id = 0;
    private int equippedSkill2Id = 0;
    private int equippedCharacterId = 3401;
    private int equippedBaitId = 0;
    private int characterLevel = 1;
    private int currentCharacterExp = 0;

    private int GetEquippedItem(EquipmentSlotType slotType)
    {
        EquipmentSlotType slot = slotType;
        switch (slot)
        {
            case EquipmentSlotType.FishingRod:
                return equippedRodId;
            case EquipmentSlotType.FishingLine:
                return equippedLineId;
            case EquipmentSlotType.FishingHook:
                return equippedHookId;
            case EquipmentSlotType.Skill1:
                return equippedSkill1Id;
            case EquipmentSlotType.Skill2:
                return equippedSkill2Id;
            case EquipmentSlotType.Character:
                return equippedCharacterId;
            case EquipmentSlotType.Bait:
                return equippedBaitId;
            default:
                return 0;
        }
    }

    private int GetCharacterLevel()
    {
        return characterLevel;
    }

    private PlayerNetworkData GetPlayerData()
    {
        return new PlayerNetworkData
        {
            playerId = _currentPlayerId,
            nickname = "Player",
            gold = playerGold,
            level = 1,
            experience = 0,
            currentSceneId = 1,
            maxFishBagCapacity = fishBagCapacity
        };
    }

    private PlayerCharacterData GetPlayerCharacterData()
    {
        return new PlayerCharacterData
        {
            equippedCharacterId = equippedCharacterId,
            isEquipped = equippedCharacterId > 0,
            currentLevel = characterLevel,
            currentExp = currentCharacterExp
        };
    }

    private int GetExpToNextLevel()
    {
        int level = characterLevel;
        if (level >= 1 && level <= 10) return 10;
        if (level >= 11 && level <= 20) return 20;
        if (level >= 21 && level <= 30) return 30;
        if (level >= 31 && level <= 40) return 40;
        if (level >= 41 && level <= 50) return 50;
        if (level >= 51 && level <= 60) return 60;
        if (level >= 61 && level <= 70) return 70;
        if (level >= 71 && level <= 80) return 80;
        if (level >= 81 && level <= 90) return 90;
        if (level >= 91 && level <= 99) return 100;
        return 100;
    }

    // ========== 装备操作 ==========

    private void OnEquipItem((EquipmentSlotType slotType, int itemId) data)
    {
        if (!CheckNetworkConnection())
            return;

        var (slotType, itemId) = data;
        Logger.Log($"[NetServerManager] 处理装备请求: slotType={slotType}, itemId={itemId}");

        UpdateLocalEquippedItem(slotType, itemId);

        int slotTypeInt = (int)slotType;
        StartCoroutine(SendEquipRequest(slotTypeInt, itemId));
    }

    private void OnEquipBait(int itemId)
    {
        if (!CheckNetworkConnection())
            return;

        Logger.Log($"[NetServerManager] 处理装备鱼饵请求: itemId={itemId}");

        UpdateLocalEquippedItem(EquipmentSlotType.Bait, itemId);

        StartCoroutine(SendEquipRequest((int)EquipmentSlotType.Bait, itemId));

        PlayerDataManager.Instance?.SyncInventoryFromServer();

        CommunicateEvent.Modify("Bag_RefreshItems");

        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, ((int)EquipmentSlotType.Bait, itemId));
    }

    private void UpdateLocalEquippedItem(EquipmentSlotType slotType, int itemId)
    {
        switch (slotType)
        {
            case EquipmentSlotType.FishingRod:
                equippedRodId = itemId;
                break;
            case EquipmentSlotType.FishingLine:
                equippedLineId = itemId;
                break;
            case EquipmentSlotType.FishingHook:
                equippedHookId = itemId;
                break;
            case EquipmentSlotType.Skill1:
                equippedSkill1Id = itemId;
                break;
            case EquipmentSlotType.Skill2:
                equippedSkill2Id = itemId;
                break;
            case EquipmentSlotType.Character:
                equippedCharacterId = itemId;
                break;
            case EquipmentSlotType.Bait:
                equippedBaitId = itemId;
                break;
        }
        Logger.Log($"[NetServerManager] 本地装备数据已更新: {slotType} = {itemId}");
    }

    private IEnumerator SendEquipRequest(int slotType, int itemId)
    {
        string url = $"/api/player/equipment/{_currentPlayerId}/{slotType}/equip/{itemId}";
        Logger.Log($"[NetServerManager] 发送装备请求: {url}");

        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(serverUrl + url, ""))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<EquipResponse>(json);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 装备成功: slotType={slotType}, itemId={itemId}");

                        if (PlayerDataManager.Instance != null)
                        {
                            PlayerDataManager.Instance.SyncInventoryFromServer();
                        }

                        if (slotType == (int)EquipmentSlotType.Character)
                        {
                            Logger.Log($"[NetServerManager] 检测到人物装备，立即同步人物数据...");
                            StartCoroutine(SyncCharacterDataAfterEquip(itemId));
                        }

                        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, (slotType, itemId));
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 装备失败: {response?.message ?? "未知错误"}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析装备响应失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 装备请求失败: {request.error}");
            }
        }
    }

    // ========== 人物数据同步 ==========

    public void SyncCharacterDataFromServer()
    {
        if (!_isEnabled || _currentPlayerId <= 0)
            return;

        string url = $"/api/player/character/{_currentPlayerId}";
        StartCoroutine(SendRequest<CharacterSyncResponse>(url, null,
            (response) =>
            {
                if (response != null)
                {
                    equippedCharacterId = response.characterId;
                    characterLevel = response.level;
                    currentCharacterExp = response.exp;
                    Logger.Log($"[NetServerManager] 人物数据同步完成: CharacterId={equippedCharacterId}, Level={characterLevel}, Exp={currentCharacterExp}");

                    int requiredExp = GetExpToNextLevel();

                    CommunicateEvent.Modify<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, (characterLevel, currentCharacterExp, requiredExp));
                }
            },
            (error) =>
            {
                Logger.LogError($"[NetServerManager] 人物数据同步失败: {error}");
            }));
    }

    private IEnumerator SyncCharacterDataAfterEquip(int expectedCharacterId)
    {
        yield return new WaitForSeconds(0.3f);

        string url = $"/api/player/character/{_currentPlayerId}";
        Logger.Log($"[NetServerManager] 正在从服务器获取人物数据: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log($"[NetServerManager] 人物数据响应: {json}");
                    var response = JsonUtility.FromJson<CharacterSyncResponse>(json);

                    if (response != null)
                    {
                        equippedCharacterId = response.characterId;
                        characterLevel = response.level > 0 ? response.level : 1;
                        currentCharacterExp = response.exp;

                        Logger.Log($"[NetServerManager] 装备后人物数据同步完成: CharacterId={equippedCharacterId}, Level={characterLevel}, Exp={currentCharacterExp}");

                        int requiredExp = GetExpToNextLevel();

                        CommunicateEvent.Modify<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, (characterLevel, currentCharacterExp, requiredExp));

                        if (response.characterId != expectedCharacterId)
                        {
                            Logger.LogWarning($"[NetServerManager] 警告：获取到的人物ID({response.characterId})与预期({expectedCharacterId})不符！");
                        }

                        if (PlayerAniManager.Instance != null && equippedCharacterId > 0)
                        {
                            Logger.Log($"[NetServerManager] 切换人物动画: characterId={equippedCharacterId}");
                            PlayerAniManager.Instance.SwitchCharacter(equippedCharacterId);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析人物数据失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 获取人物数据失败: {request.error}");
            }
        }
    }

    // ========== 装备解锁 ==========

    public void OnUnlockEquipment(int equipId)
    {
        if (!CheckNetworkConnection())
            return;

        Logger.Log($"[NetServerManager] 处理装备解锁请求: equipId={equipId}");

        string equipmentType = GetEquipmentType(equipId);
        if (string.IsNullOrEmpty(equipmentType))
        {
            Logger.LogWarning($"[NetServerManager] 无法确定装备类型: equipId={equipId}");
            GameUIManager.Instance?.ShowTip("装备类型错误");
            return;
        }

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "equipmentId", equipId },
            { "equipmentType", equipmentType }
        };

        StartCoroutine(SendRequest<object>("/api/equipment/unlock", requestData,
            (response) =>
            {
                Logger.Log($"[NetServerManager] 装备解锁成功: equipId={equipId}");

                StartCoroutine(FetchPlayerDataAfterUnlock());
            },
            (error) =>
            {
                Logger.LogWarning($"[NetServerManager] 装备解锁失败: {error}");
                GameUIManager.Instance?.ShowTip("解锁失败，请重试");
            }));
    }

    private string GetEquipmentType(int equipId)
    {
        if (equipId >= 3001 && equipId <= 3099) return "Rod";
        else if (equipId >= 3101 && equipId <= 3199) return "Line";
        else if (equipId >= 3201 && equipId <= 3299) return "Hook";
        else if (equipId >= 3401 && equipId <= 3499) return "Character";
        else if (equipId >= 3301 && equipId <= 3399) return "Skill";
        else return null;
    }

    private System.Collections.IEnumerator FetchPlayerDataAfterUnlock()
    {
        yield return null;

        yield return StartCoroutine(FetchPlayerData());

        CommunicateEvent.Modify("Equipment_Refresh");
        CommunicateEvent.Modify("Character_Refresh");
        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, (0, 0));

        GameUIManager.Instance?.ShowTip("解锁成功！");

        Logger.Log("[NetServerManager] 装备解锁后已刷新玩家数据");
    }

    public void UnlockEquipment(int playerId, int equipmentId, string equipmentType, System.Action<bool, string> onComplete)
    {
        Logger.LogColor($"[NetServerManager] UnlockEquipment: PlayerId={playerId}, EquipmentId={equipmentId}, Type={equipmentType}", "cyan");

        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            return;
        }

        var requestData = new Dictionary<string, object>
        {
            { "playerId", playerId },
            { "equipmentId", equipmentId },
            { "equipmentType", equipmentType }
        };

        StartCoroutine(SendRequest<UnlockEquipmentResponse>(
            "/api/fishing/unlock-equipment",
            requestData,
            (response) =>
            {
                if (response.success)
                {
                    Logger.LogColor($"[NetServerManager] 装备解锁成功: {response.message}", "green");
                    onComplete?.Invoke(true, response.message);
                }
                else
                {
                    Logger.LogError($"[NetServerManager] 装备解锁失败: {response.message}");
                    onComplete?.Invoke(false, response.message);
                }
            },
            (error) =>
            {
                Logger.LogError($"[NetServerManager] 装备解锁请求失败: {error}");
                onComplete?.Invoke(false, error);
            },
            forcePost: true
        ));
    }

    // ========== 辅助数据类 ==========

    private class EquipResponse
    {
        public bool success;
        public string message;
    }

    [System.Serializable]
    private class EquipmentResponse
    {
        public int rodId;
        public int lineId;
        public int hookId;
        public int skill1Id;
        public int skill2Id;
        public int characterId;
        public int baitId;
        public int baitLevel;
        public int characterLevel;
    }

    [System.Serializable]
    private class CharacterSyncResponse
    {
        public int characterId;
        public int level;
        public int exp;
    }

    private class UnlockEquipmentResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
    }
}