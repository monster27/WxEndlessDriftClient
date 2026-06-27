using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager 
{
    // 玩家数据
    private Dictionary<int, int> playerInventory = new Dictionary<int, int>();
    private Dictionary<int, int> fishInventory = new Dictionary<int, int>();
    private int fishBagCapacity = 20;
    private int playerGold = 0;

    private HashSet<int> unlockedCharacters = new HashSet<int>();
    private HashSet<int> unlockedEquipment = new HashSet<int>();

    public event Action OnAllDataLoaded;

    private bool isDataLoading = false;

    // ========== 通用网络请求辅助 ==========

    /// <summary>发送 GET 请求并解析 JSON 响应，自动处理错误日志</summary>
    private IEnumerator FetchGetJson<T>(string url, Action<T> onSuccess, string errorLabel = null)
    {
        using (var request = UnityWebRequest.Get(serverUrl + url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<T>(json);
                    onSuccess?.Invoke(data);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析{errorLabel ?? url}失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 获取{errorLabel ?? url}失败: {request.error}");
            }
        }
    }

    /// <summary>发送 GET 请求，返回原始 JSON 字符串</summary>
    private IEnumerator FetchGetJson(string url, Action<string> onSuccess, string errorLabel = null)
    {
        using (var request = UnityWebRequest.Get(serverUrl + url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                Logger.LogError($"[NetServerManager] 获取{errorLabel ?? url}失败: {request.error}");
            }
        }
    }

    /// <summary>发送 POST 请求</summary>
    private IEnumerator FetchPostJson(string url, string jsonBody, Action<string> onSuccess, string errorLabel = null)
    {
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        using (var request = new UnityWebRequest(serverUrl + url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                Logger.LogError($"[NetServerManager] {errorLabel ?? url} 请求失败: {request.error}");
            }
        }
    }

    // ========== 数据查询 ==========

    private Dictionary<int, int> GetPlayerInventory() => playerInventory;
    private Dictionary<int, int> GetPlayerFishInventory() => fishInventory;
    private int GetFishBagCapacity() => fishBagCapacity;
    public int GetPlayerGold() => playerGold;

    private bool IsCharacterObtained(int characterId)
    {
        if (characterId == 3401) return true;
        if (unlockedCharacters.Contains(characterId)) return true;
        return playerInventory != null && playerInventory.TryGetValue(characterId, out int count) && count > 0;
    }

    private bool IsSkillObtained(int skillId)
    {
        return playerInventory != null && playerInventory.TryGetValue(skillId, out int count) && count > 0;
    }

    private bool IsItemEquipped(int itemId)
    {
        return equippedRodId == itemId || equippedLineId == itemId || equippedHookId == itemId
            || equippedSkill1Id == itemId || equippedSkill2Id == itemId
            || equippedCharacterId == itemId || equippedBaitId == itemId;
    }

    public bool IsEquipmentUnlocked(int equipmentId)
    {
        if (equipmentId == 3401) return true;
        if (unlockedEquipment.Contains(equipmentId)) return true;
        return playerInventory != null && playerInventory.TryGetValue(equipmentId, out int count) && count > 0;
    }


    private int GetTotalFishCount()
    {
        int total = 0;
        foreach (var list in fishDetailData.Values)
        {
            total += list.Count;
        }
        return total;
    }

    // ========== 数据加载主流程（兼容旧接口）==========

    /// <summary>
    /// 兼容旧接口：启动数据加载（建议使用 StartInitialization）
    /// </summary>
    public void StartDataLoading()
    {
        if (isDataLoading)
        {
            Logger.LogWarning("[NetServerManager] 数据正在加载中，请勿重复调用");
            return;
        }
        isDataLoading = true;
        Logger.Log("[NetServerManager] 开始加载所有玩家数据...");
        StartCoroutine(FetchAllPlayerData());
    }

    private IEnumerator FetchAllPlayerData()
    {
        var steps = new (string label, IEnumerator coroutine)[]
        {
            ("加载背包数据", FetchPlayerInventoryCoroutine()),
            ("加载装备数据", FetchPlayerEquipmentCoroutine()),
            ("加载鱼篓数据", FetchPlayerFishInventoryCoroutine()),
            ("加载人物数据", FetchPlayerCharacterDataCoroutine()),
        };

        foreach (var (label, coroutine) in steps)
        {
            CommunicateEvent.Modify("NetworkLoadingTask", label);
            yield return StartCoroutine(coroutine);
            CommunicateEvent.Modify("NetworkLoadingComplete", label);
        }

        CommunicateEvent.Modify("NetworkLoadingTask", "加载商城数据...");
        SyncMallItemsFromServer();
        CommunicateEvent.Modify("NetworkLoadingComplete", "加载商城数据...");

        yield return null;

        OnAllDataLoaded?.Invoke();
        Logger.Log("[NetServerManager] 所有玩家数据加载完成！");
        CommunicateEvent.Modify("NetworkLoadingComplete", "所有数据加载完成！");
        isDataLoading = false;
    }

    // ========== 各模块数据加载 Coroutine ==========

    // 在 NetServerManager.PlayerData.cs 的 FetchPlayerInventoryCoroutine 中
    private IEnumerator FetchPlayerInventoryCoroutine()
    {
        yield return FetchGetJson<InventoryResponse>("/api/player/inventory/" + _currentPlayerId, data =>
        {
            if (data?.items == null) return;
            playerInventory.Clear();
            foreach (var item in data.items)
            {
                playerInventory[item.key] = item.value;
            }
            Logger.Log($"[NetServerManager] 背包数据加载完成: {playerInventory.Count} 件物品");

            // ✅ 添加这行
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.SyncInventoryFromServer();
            }
        }, "背包数据");
    }

    private IEnumerator FetchPlayerEquipmentCoroutine()
    {
        yield return FetchGetJson<EquipmentResponse>("/api/player/equipment/" + _currentPlayerId, data =>
        {
            if (data == null) return;
            equippedRodId = data.rodId > 0 ? data.rodId : 3001;
            equippedLineId = data.lineId > 0 ? data.lineId : 3101;
            equippedHookId = data.hookId > 0 ? data.hookId : 3201;
            equippedSkill1Id = data.skill1Id;
            equippedSkill2Id = data.skill2Id;
            equippedCharacterId = data.characterId > 0 ? data.characterId : 3401;
            equippedBaitId = data.baitId;
            characterLevel = data.characterLevel > 0 ? data.characterLevel : 1;

            Logger.Log($"[NetServerManager] 装备数据加载完成: Rod={equippedRodId}, Char={equippedCharacterId}, Bait={equippedBaitId}");
            CommunicateEvent.Modify<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, (EquipmentSlotType.Bait, equippedBaitId));
        }, "装备数据");
    }

    private IEnumerator FetchPlayerFishInventoryCoroutine()
    {
        yield return FetchGetJson<InventoryResponse>("/api/player/fish-bag/" + _currentPlayerId, data =>
        {
            if (data?.items == null) return;

            // 清空旧数据
            fishInventory.Clear();
            fishDetailData.Clear();

            foreach (var item in data.items)
            {
                // item.key = fishId, item.value = 1 (因为每条鱼是独立的)
                // 但这里我们不使用 fishInventory 作为显示数据源
                if (!fishInventory.ContainsKey(item.key))
                    fishInventory[item.key] = 0;
                fishInventory[item.key] += item.value; // 这行保留用于统计总数，但主要显示靠 detailData

                // 存储每条鱼的详细信息
                if (!fishDetailData.ContainsKey(item.key))
                {
                    fishDetailData[item.key] = new List<FishDetailData>();
                }
                fishDetailData[item.key].Add(new FishDetailData
                {
                    fishId = item.key,
                    weight = item.weight,
                    starRatingId = item.starRatingId,
                    calculatedPrice = 0,
                    caughtTimestamp = item.caughtTimestamp,
                    isShiny = item.isShiny
                });
            }

            int total = GetTotalFishCount();
            isFishBagFull = total >= fishBagCapacity;
            PlayerDataManager.Instance?.UpdateFishDetailData(fishDetailData);
            Logger.Log("[NetServerManager] 鱼篓数据加载完成: " + fishInventory.Count + " 种鱼，总数量: " + total + "，详细数据: " + fishDetailData.Count + " 条");
        }, "鱼篓数据");
    }


    private IEnumerator FetchPlayerCharacterDataCoroutine()
    {
        yield return FetchGetJson("/api/player/character/" + _currentPlayerId, json =>
        {
            Logger.Log("[NetServerManager] 人物数据加载完成: " + json);
        }, "人物数据");
    }

    // ========== FetchPlayerData：登录后全量拉取（拆分为多个小方法）==========

    private IEnumerator FetchPlayerData()
    {
        if (!isConnected) yield break;

        yield return FetchPlayerGold();
        yield return FetchPlayerInventory();
        yield return FetchUnlockedCharacters();
        yield return FetchPlayerFishBag();
        yield return FetchFishBagCapacity();
        yield return FetchPlayerEquipment();
        yield return FetchPlayerCharacter();

        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(EnsureBasicCharacter());

        NotifyPlayerDataSyncedInternal();
        SyncUnlockedEquipmentFromServer();
        SyncMallItemsFromServer();

        if (PlayerAniManager.Instance != null && equippedCharacterId > 0)
            PlayerAniManager.Instance.SwitchCharacter(equippedCharacterId);

        if (isFishBagFull) NotifyPlayLazyAnimation();
        else AutoStartFishing();
    }

    private IEnumerator FetchPlayerGold()
    {
        yield return FetchGetJson<GoldResponse>("/api/player/gold/" + _currentPlayerId, data =>
        {
            if (data != null) playerGold = data.gold;
            Logger.Log("[NetServerManager] 更新玩家金币: " + playerGold);
        }, "金币数据");
    }

    private IEnumerator FetchPlayerInventory()
    {
        yield return FetchGetJson<InventoryResponse>("/api/player/inventory/" + _currentPlayerId, data =>
        {
            if (data?.items == null) return;
            playerInventory.Clear();
            foreach (var item in data.items) playerInventory[item.key] = item.value;
            Logger.Log("[NetServerManager] 更新玩家背包: " + playerInventory.Count + " 件物品");

            if (playerInventory.ContainsKey(2001)) CommunicateEvent.Modify("BaitDataUpdated");
            if (playerInventory.ContainsKey(2501)) CommunicateEvent.Modify("BaitCountChanged");
        }, "背包数据");
    }

    private IEnumerator FetchUnlockedCharacters()
    {
        yield return FetchGetJson("/api/player/characters/" + _currentPlayerId, json =>
        {
            unlockedCharacters.Clear();
            try
            {
                var chars = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CharacterData>>(json);
                if (chars != null)
                {
                    foreach (var c in chars)
                        unlockedCharacters.Add(c.characterId);
                }
                else
                {
                    var listResp = JsonUtility.FromJson<CharacterListResponse>(json);
                    if (listResp?.characters != null)
                    {
                        foreach (var c in listResp.characters)
                            unlockedCharacters.Add(c.characterId);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[NetServerManager] 解析人物列表失败: {ex.Message}");
            }
            unlockedCharacters.Add(3401);
            Logger.Log($"[NetServerManager] 同步人物列表完成，共 {unlockedCharacters.Count} 个已解锁人物");
        }, "人物列表");
    }

    private IEnumerator FetchPlayerFishBag()
    {
        yield return FetchGetJson<InventoryResponse>("/api/player/fish-bag/" + _currentPlayerId, data =>
        {
            if (data?.items == null) return;
            fishInventory.Clear();
            fishDetailData.Clear();
            foreach (var item in data.items)
            {
                if (!fishInventory.ContainsKey(item.key))
                    fishInventory[item.key] = 0;
                fishInventory[item.key] += item.value;

                if (!fishDetailData.ContainsKey(item.key))
                {
                    fishDetailData[item.key] = new List<FishDetailData>();
                }
                fishDetailData[item.key].Add(new FishDetailData
                {
                    fishId = item.key,
                    weight = item.weight,
                    starRatingId = item.starRatingId,
                    calculatedPrice = 0,
                    caughtTimestamp = item.caughtTimestamp,
                    isShiny = item.isShiny
                });
            }
            int total = GetTotalFishCount();
            isFishBagFull = total >= fishBagCapacity;
            PlayerDataManager.Instance?.UpdateFishDetailData(fishDetailData);
            Logger.Log("[NetServerManager] 更新玩家鱼篓: " + fishInventory.Count + " 种鱼，总数量: " + total + "，详细数据: " + fishDetailData.Count + " 种");
        }, "鱼篓数据");
    }

    private IEnumerator FetchFishBagCapacity()
    {
        yield return FetchGetJson<CapacityResponse>("/api/inventory/fish/" + _currentPlayerId + "/capacity", data =>
        {
            if (data != null) fishBagCapacity = data.capacity;
            Logger.Log("[NetServerManager] 更新鱼篓容量: " + fishBagCapacity);
        }, "鱼篓容量");
    }

    private IEnumerator FetchPlayerEquipment()
    {
        yield return FetchGetJson<EquipmentResponse>("/api/player/equipment/" + _currentPlayerId, data =>
        {
            if (data == null) return;
            equippedRodId = data.rodId > 0 ? data.rodId : 3001;
            equippedLineId = data.lineId > 0 ? data.lineId : 3101;
            equippedHookId = data.hookId > 0 ? data.hookId : 3201;
            equippedSkill1Id = data.skill1Id;
            equippedSkill2Id = data.skill2Id;
            equippedCharacterId = data.characterId > 0 ? data.characterId : 3401;
            equippedBaitId = data.baitId;
            characterLevel = data.characterLevel > 0 ? data.characterLevel : 1;
            Logger.Log($"[NetServerManager] 登录后更新装备: Char={equippedCharacterId}, Bait={equippedBaitId}, Level={characterLevel}");
            CommunicateEvent.Modify("Bag_RefreshItems");
        }, "装备数据");
    }

    private IEnumerator FetchPlayerCharacter()
    {
        yield return FetchGetJson<CharacterSyncResponse>("/api/player/character/" + _currentPlayerId, data =>
        {
            if (data == null) return;
            equippedCharacterId = data.characterId > 0 ? data.characterId : 3401;
            characterLevel = data.level > 0 ? data.level : 1;
            currentCharacterExp = data.exp;
            Logger.Log($"[NetServerManager] 更新人物: CharId={equippedCharacterId}, Lv={characterLevel}, Exp={currentCharacterExp}");
        }, "人物数据");
    }

    /// <summary>
    /// 内部通知数据同步完成（重命名避免二义性）
    /// </summary>
    /// <summary>
    private void NotifyPlayerDataSyncedInternal()
    {
        if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsReady)
        {
            PlayerDataManager.Instance.SyncInventoryFromServer();
            PlayerDataManager.Instance.SyncGoldFromServer();
        }
        else
        {
            Logger.LogWarning("[NetServerManager] PlayerDataManager 尚未就绪，跳过数据同步");
        }

        // 【修复】使用 CommunicateEvent.EVENT_EQUIP_CHANGED 而不是 S2C_EVENT_EQUIP_CHANGED
        if (unlockedCharacters.Count > 0)
        {
            CommunicateEvent.Modify<List<int>>("SyncUnlockedCharacters", new List<int>(unlockedCharacters));
        }
    }
    // ========== 数据同步 ==========

    public void SyncUnlockedCharactersFromServer() => StartCoroutine(SyncUnlockedCharactersCoroutine());

    private IEnumerator SyncUnlockedCharactersCoroutine()
    {
        yield return FetchGetJson("/api/player/characters/" + _currentPlayerId, json =>
        {
            unlockedCharacters.Clear();
            var listResp = JsonUtility.FromJson<CharacterListResponse>(json);
            if (listResp?.characters != null)
            {
                foreach (var c in listResp.characters) unlockedCharacters.Add(c.characterId);
            }
            else
            {
                var chars = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CharacterData>>(json);
                if (chars != null)
                    foreach (var c in chars) unlockedCharacters.Add(c.characterId);
            }
            unlockedCharacters.Add(3401);
            Logger.Log($"[NetServerManager] 同步人物列表完成，共 {unlockedCharacters.Count} 个");
        }, "人物列表");
    }

    public void SyncUnlockedEquipmentFromServer() => StartCoroutine(SyncUnlockedEquipmentCoroutine());

    private IEnumerator SyncUnlockedEquipmentCoroutine()
    {
        yield return FetchGetJson<UnlockedEquipmentResponse>("/api/player/" + _currentPlayerId + "/unlocked-equipment", resp =>
        {
            if (resp == null || !resp.success || resp.unlockedEquipment == null) return;
            unlockedEquipment.Clear();
            foreach (var id in resp.unlockedEquipment) unlockedEquipment.Add(id);
            Logger.Log($"[NetServerManager] 同步已解锁装备列表完成，共 {unlockedEquipment.Count} 个");
            CommunicateEvent.Modify<List<int>>("SyncUnlockedEquipment", new List<int>(unlockedEquipment));
        }, "已解锁装备");
    }

    // ========== 数据更新 ==========

    private IEnumerator FetchPlayerDataAfterSell(List<int> itemIds, int totalPrice)
    {
        yield return null;
        yield return FetchPlayerFishBag();  // ✅ 使用修复后的 FetchPlayerFishBag
        yield return FetchPlayerGold();

        isFishBagFull = GetTotalFishCount() >= fishBagCapacity;
        if (!isFishBagFull && !isPlayingReelAnimation) NotifyPlayIdleAnimation();
        if (!isFishBagFull && !isAutoFishing) AutoStartFishing();

        PlayerDataManager.Instance?.SyncInventoryFromServer();
        PlayerDataManager.Instance?.SyncGoldFromServer();
    }

    private IEnumerator FetchFishInventoryFromServer()
    {
        yield return null;
        yield return FetchPlayerFishBag();  // ✅ 使用修复后的 FetchPlayerFishBag
        yield return StartCoroutine(FetchPlayerInventoryFromServer());
        PlayerDataManager.Instance?.SyncInventoryFromServer();
    }

    private IEnumerator FetchPlayerInventoryFromServer()
    {
        yield return FetchGetJson<InventoryResponse>("/api/player/inventory/" + _currentPlayerId, data =>
        {
            if (data?.items == null) return;
            playerInventory.Clear();
            foreach (var item in data.items) playerInventory[item.key] = item.value;
            Logger.Log("[NetServerManager] 普通背包数据已更新: " + playerInventory.Count + " 件物品");
            CommunicateEvent.Modify<Dictionary<int, int>>("BagDataUpdated", playerInventory);
        }, "背包数据");
    }

    // ========== 基础人物管理 ==========

    private IEnumerator EnsureBasicCharacter()
    {
        if (playerInventory == null || !playerInventory.ContainsKey(3401))
        {
            Logger.LogWarning("[NetServerManager] 玩家未拥有基础人物3401，正在添加...");
            yield return StartCoroutine(AddCharacterToInventory(3401));
        }
        yield return StartCoroutine(AddCharacterToPlayerCharacter(3401));

        if (equippedCharacterId < 3401 || equippedCharacterId > 3500)
        {
            Logger.LogWarning($"[NetServerManager] 当前装备的人物ID({equippedCharacterId})无效，装备基础人物3401");
            yield return StartCoroutine(SendEquipRequest((int)EquipmentSlotType.Character, 3401));
        }
    }

    private IEnumerator AddCharacterToInventory(int characterId)
    {
        string json = $"{{\"playerId\":{_currentPlayerId},\"itemId\":{characterId},\"quantity\":1}}";
        yield return FetchPostJson("/api/player/inventory/add", json, responseText =>
        {
            var resp = JsonUtility.FromJson<AddItemResponse>(responseText);
            if (resp != null && resp.success)
            {
                playerInventory[characterId] = playerInventory.TryGetValue(characterId, out int c) ? c + 1 : 1;
                PlayerDataManager.Instance?.SyncInventoryFromServer();
            }
        }, "添加人物到背包");
    }

    private IEnumerator AddCharacterToPlayerCharacter(int characterId)
    {
        string json = $"{{\"playerId\":{_currentPlayerId},\"characterId\":{characterId}}}";
        yield return FetchPostJson("/api/player/character/add", json, responseText =>
        {
            var resp = JsonUtility.FromJson<AddItemResponse>(responseText);
            if (resp != null && resp.success)
                Logger.Log($"[NetServerManager] 成功添加人物 {characterId} 到PlayerCharacter表");
        }, "添加人物到PlayerCharacter");
    }

    public void UnlockCharacter(int characterId, Action<bool> callback)
    {
        StartCoroutine(UnlockCharacterCoroutine(characterId, callback));
    }

    private IEnumerator UnlockCharacterCoroutine(int characterId, Action<bool> callback)
    {
        string json = $"{{\"playerId\":{_currentPlayerId},\"characterId\":{characterId}}}";
        yield return FetchPostJson("/api/player/character/add", json, responseText =>
        {
            var resp = JsonUtility.FromJson<AddItemResponse>(responseText);
            if (resp != null && resp.success)
            {
                Logger.Log($"[NetServerManager] 成功解锁人物 {characterId}");
                callback?.Invoke(true);
                PlayerDataManager.Instance?.SyncInventoryFromServer();
                SyncUnlockedCharactersFromServer();
                SyncUnlockedEquipmentFromServer();
            }
            else
            {
                callback?.Invoke(false);
            }
        }, "解锁人物");
    }

    // ========== 辅助数据类 ==========

    [Serializable] private class CharacterListResponse { public List<CharacterData> characters; }
    [Serializable] private class CharacterData { public int characterId; public int level; public int exp; public bool isActive; }
    [Serializable] private class UnlockedEquipmentResponse { public bool success; public List<int> unlockedEquipment; }
    [Serializable] private class AddItemResponse { public bool success; public string message; }
    [Serializable] private class InventoryResponse { public List<ItemKV> items; }
    [Serializable]
    private class ItemKV
    {
        public int key;
        public int value;
        public float weight;
        public int starRatingId;
        public long caughtTimestamp;
        public bool isShiny;  // 是否闪光鱼
    }
    [Serializable] private class GoldResponse { public int gold; }
    [Serializable] private class CapacityResponse { public int capacity; }

    private Dictionary<int, List<FishDetailData>> fishDetailData = new Dictionary<int, List<FishDetailData>>();

    public Dictionary<int, List<FishDetailData>> GetFishDetailData() => fishDetailData;
}