// ============================================================
// 文件: NetServerManager.Egg.cs
// 说明: 蛋系统网络请求（孵化槽位、开始孵化、领取宠物、蛋升级、跳过孵化、提升稀有度）
// 路径: Assets/Scripts/Network/
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;

public partial class NetServerManager
{
    // ============================================================
    // 缓存数据
    // ============================================================

    /// <summary>当前缓存的孵化槽位列表</summary>
    private List<EggHatchSlotData> hatchSlotsCache = new List<EggHatchSlotData>();

    /// <summary>公开访问器</summary>
    public List<EggHatchSlotData> HatchSlots => hatchSlotsCache;

    // ============================================================
    // 查询孵化槽位
    // ============================================================

    public void FetchEggHatchSlots(Action<bool, List<EggHatchSlotData>> onComplete = null)
    {
        StartCoroutine(FetchEggHatchSlotsCoroutine(onComplete));
    }

    private IEnumerator FetchEggHatchSlotsCoroutine(Action<bool, List<EggHatchSlotData>> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/egg/slots";
        Z_Logger.Log($"[NetServerManager] 获取孵化槽位: {url}");

        yield return FetchGetJson<EggSlotsResponse>(url, data =>
        {
            if (data != null && data.success)
            {
                hatchSlotsCache = data.slots ?? new List<EggHatchSlotData>();
                Z_Logger.Log($"[NetServerManager] 孵化槽位加载完成，共 {hatchSlotsCache.Count} 个");

                // 同步到 PlayerDataManager（由它广播事件）
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdateEggHatchSlots(hatchSlotsCache);
                }

                onComplete?.Invoke(true, hatchSlotsCache);
            }
            else
            {
                Z_Logger.LogWarning("[NetServerManager] 获取孵化槽位失败");
                onComplete?.Invoke(false, null);
            }
        }, "孵化槽位");
    }

    // ============================================================
    // 开始孵化
    // ============================================================

    public void StartEggHatch(int slotIndex, int eggId, Action<bool, string, EggHatchSlotData> onComplete = null)
    {
        StartCoroutine(StartEggHatchCoroutine(slotIndex, eggId, onComplete));
    }

    private IEnumerator StartEggHatchCoroutine(int slotIndex, int eggId, Action<bool, string, EggHatchSlotData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/egg/hatch";
        var requestData = new Dictionary<string, object>
        {
            { "slotIndex", slotIndex },
            { "eggId", eggId }
        };

        Z_Logger.Log($"[NetServerManager] 开始孵化: slotIndex={slotIndex}, eggId={eggId}");

        yield return SendRequest<EggHatchResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                Z_Logger.Log($"[NetServerManager] 开始孵化成功: slotIndex={slotIndex}");

                UpdateSlotCache(resp.slot);

                if (PlayerDataManager.Instance != null && resp.slot != null)
                {
                    PlayerDataManager.Instance.UpdateSingleHatchSlot(resp.slot);
                }

                // 扣了蛋，刷新背包
                StartCoroutine(FetchPlayerInventory());

                onComplete?.Invoke(true, resp.message, resp.slot);
            }
            else
            {
                Z_Logger.LogWarning($"[NetServerManager] 开始孵化失败: {resp?.message}");
                onComplete?.Invoke(false, resp?.message ?? "孵化失败", null);
            }
        }, err =>
        {
            Z_Logger.LogError($"[NetServerManager] 开始孵化请求失败: {err}");
            onComplete?.Invoke(false, "网络请求失败", null);
        }, forcePost: true);
    }

    // ============================================================
    // 领取孵化完成的宠物
    // ============================================================

    public void ClaimEggHatch(int slotIndex, Action<bool, string, PlayerPetData> onComplete = null)
    {
        StartCoroutine(ClaimEggHatchCoroutine(slotIndex, onComplete));
    }

    private IEnumerator ClaimEggHatchCoroutine(int slotIndex, Action<bool, string, PlayerPetData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/egg/claim";
        var requestData = new Dictionary<string, object>
        {
            { "slotIndex", slotIndex }
        };

        Z_Logger.Log($"[NetServerManager] 领取孵化宠物: slotIndex={slotIndex}");

        yield return SendRequest<EggClaimResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                Z_Logger.Log($"[NetServerManager] 领取孵化宠物成功: petInstanceId={resp.pet?.petInstanceId}");

                ClearSlotCache(slotIndex);

                if (PlayerDataManager.Instance != null)
                {
                    // 1. 更新槽位（清空）
                    var emptySlot = hatchSlotsCache.Find(s => s.slotIndex == slotIndex);
                    if (emptySlot != null)
                        PlayerDataManager.Instance.UpdateSingleHatchSlot(emptySlot);

                    // 2. 添加宠物
                    if (resp.pet != null)
                        PlayerDataManager.Instance.AddPet(resp.pet);

                    // 3. 宠物栏已用 +1
                    PlayerDataManager.Instance.LocalIncreasePetStorageUsed();
                }

                onComplete?.Invoke(true, resp.message, resp.pet);
            }
            else
            {
                Z_Logger.LogWarning($"[NetServerManager] 领取孵化宠物失败: {resp?.message}");
                onComplete?.Invoke(false, resp?.message ?? "领取失败", null);
            }
        }, err =>
        {
            Z_Logger.LogError($"[NetServerManager] 领取孵化宠物请求失败: {err}");
            onComplete?.Invoke(false, "网络请求失败", null);
        }, forcePost: true);
    }

    // ============================================================
    // 卖蛋
    // ============================================================

    public void SellEgg(int eggId, int count = 1, Action<bool, string, int> onComplete = null)
    {
        StartCoroutine(SellEggCoroutine(eggId, count, onComplete));
    }

    private IEnumerator SellEggCoroutine(int eggId, int count, Action<bool, string, int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/egg/sell";
        var requestData = new Dictionary<string, object>
        {
            { "eggId", eggId },
            { "count", count }
        };

        Z_Logger.Log($"[NetServerManager] 卖蛋: eggId={eggId}, count={count}");

        yield return SendRequest<SellEggResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                Z_Logger.Log($"[NetServerManager] 卖蛋成功，获得 {resp.goldEarned} 金币");

                // 扣了蛋，刷新背包 + 金币
                StartCoroutine(FetchPlayerGold());
                StartCoroutine(FetchPlayerInventory());

                onComplete?.Invoke(true, resp.message, resp.goldEarned);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "出售失败", 0);
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败", 0);
        }, forcePost: true);
    }



    // ============================================================
    // 蛋升级
    // ============================================================

    public void UpgradeEgg(int fromEggId, bool adWatched, Action<bool, string, bool, int> onComplete = null)
    {
        StartCoroutine(UpgradeEggCoroutine(fromEggId, adWatched, onComplete));
    }

    private IEnumerator UpgradeEggCoroutine(int fromEggId, bool adWatched, Action<bool, string, bool, int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", false, 0);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/egg/upgrade";
        var requestData = new Dictionary<string, object>
        {
            { "fromEggId", fromEggId },
            { "adWatched", adWatched }
        };

        Z_Logger.Log($"[NetServerManager] 蛋升级: fromEggId={fromEggId}, adWatched={adWatched}");

        yield return SendRequest<EggUpgradeResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                Z_Logger.Log($"[NetServerManager] 蛋升级成功: toEggId={resp.toEggId}");

                // 扣了金币（或看了广告），刷新金币 + 背包
                StartCoroutine(FetchPlayerGold());
                StartCoroutine(FetchPlayerInventory());

                onComplete?.Invoke(true, resp.message, false, resp.toEggId);
            }
            else
            {
                Z_Logger.LogWarning($"[NetServerManager] 蛋升级失败: {resp?.message}, needAd={resp?.needAd}");
                onComplete?.Invoke(false, resp?.message ?? "升级失败", resp?.needAd ?? false, 0);
            }
        }, err =>
        {
            Z_Logger.LogError($"[NetServerManager] 蛋升级请求失败: {err}");
            onComplete?.Invoke(false, "网络请求失败", false, 0);
        }, forcePost: true);
    }

    // ============================================================
    // 跳过孵化（本轮新增）
    // ============================================================

    public void SkipEggHatch(int slotIndex, bool adWatched,
        Action<bool, string, bool, EggHatchSlotData> onComplete = null)
    {
        StartCoroutine(SkipEggHatchCoroutine(slotIndex, adWatched, onComplete));
    }

    private IEnumerator SkipEggHatchCoroutine(int slotIndex, bool adWatched,
        Action<bool, string, bool, EggHatchSlotData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/egg/skip";
        var requestData = new Dictionary<string, object>
        {
            { "slotIndex", slotIndex },
            { "adWatched", adWatched }
        };

        Z_Logger.Log($"[NetServerManager] 跳过孵化: slotIndex={slotIndex}, adWatched={adWatched}");

        yield return SendRequest<EggSkipResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                Z_Logger.Log($"[NetServerManager] 跳过孵化成功: slotIndex={slotIndex}");

                UpdateSlotCache(resp.slot);

                if (PlayerDataManager.Instance != null && resp.slot != null)
                {
                    PlayerDataManager.Instance.UpdateSingleHatchSlot(resp.slot);
                }

                // 扣了金币或看了广告，刷新金币
                StartCoroutine(FetchPlayerGold());

                onComplete?.Invoke(true, resp.message, false, resp.slot);
            }
            else
            {
                Z_Logger.LogWarning($"[NetServerManager] 跳过孵化失败: {resp?.message}, needAd={resp?.needAd}");
                onComplete?.Invoke(false, resp?.message ?? "跳过失败", resp?.needAd ?? false, null);
            }
        }, err =>
        {
            Z_Logger.LogError($"[NetServerManager] 跳过孵化请求失败: {err}");
            onComplete?.Invoke(false, "网络请求失败", false, null);
        }, forcePost: true);
    }

    // ============================================================
    // 提升孵化稀有度（本轮新增）
    // ============================================================

    public void UpgradeHatchedPetRarity(int slotIndex, bool adWatched,
        Action<bool, string, bool, PlayerPetData> onComplete = null)
    {
        StartCoroutine(UpgradeHatchedPetRarityCoroutine(slotIndex, adWatched, onComplete));
    }

    private IEnumerator UpgradeHatchedPetRarityCoroutine(int slotIndex, bool adWatched,
        Action<bool, string, bool, PlayerPetData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/egg/upgrade-pet-rarity";
        var requestData = new Dictionary<string, object>
        {
            { "slotIndex", slotIndex },
            { "adWatched", adWatched }
        };

        Z_Logger.Log($"[NetServerManager] 提升孵化稀有度: slotIndex={slotIndex}, adWatched={adWatched}");

        yield return SendRequest<EggUpgradeRarityResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                Z_Logger.Log($"[NetServerManager] 提升孵化稀有度成功: petInstanceId={resp.pet?.petInstanceId}");

                ClearSlotCache(slotIndex);

                if (PlayerDataManager.Instance != null)
                {
                    // 1. 更新槽位（清空）
                    var emptySlot = hatchSlotsCache.Find(s => s.slotIndex == slotIndex);
                    if (emptySlot != null)
                        PlayerDataManager.Instance.UpdateSingleHatchSlot(emptySlot);

                    // 2. 添加宠物
                    if (resp.pet != null)
                        PlayerDataManager.Instance.AddPet(resp.pet);

                    // 3. 宠物栏已用 +1
                    PlayerDataManager.Instance.LocalIncreasePetStorageUsed();
                }

                // 扣了金币或看了广告，刷新金币
                StartCoroutine(FetchPlayerGold());

                onComplete?.Invoke(true, resp.message, false, resp.pet);
            }
            else
            {
                Z_Logger.LogWarning($"[NetServerManager] 提升孵化稀有度失败: {resp?.message}, needAd={resp?.needAd}");
                onComplete?.Invoke(false, resp?.message ?? "提升失败", resp?.needAd ?? false, null);
            }
        }, err =>
        {
            Z_Logger.LogError($"[NetServerManager] 提升孵化稀有度请求失败: {err}");
            onComplete?.Invoke(false, "网络请求失败", false, null);
        }, forcePost: true);
    }

    // ============================================================
    // 内部辅助
    // ============================================================

    private void UpdateSlotCache(EggHatchSlotData slot)
    {
        if (slot == null) return;
        int idx = hatchSlotsCache.FindIndex(s => s.slotIndex == slot.slotIndex);
        if (idx >= 0) hatchSlotsCache[idx] = slot;
        else hatchSlotsCache.Add(slot);
    }

    private void ClearSlotCache(int slotIndex)
    {
        int idx = hatchSlotsCache.FindIndex(s => s.slotIndex == slotIndex);
        if (idx >= 0)
        {
            hatchSlotsCache[idx] = new EggHatchSlotData
            {
                slotIndex = slotIndex,
                isOccupied = false,
                eggId = 0,
                rarityId = 0,
                remainingSeconds = -1
            };
        }
    }

    // ============================================================
    // 响应数据类
    // ============================================================

    [Serializable]
    private class EggSlotsResponse
    {
        public bool success;
        public List<EggHatchSlotData> slots;
    }

    [Serializable]
    private class EggHatchResponse
    {
        public bool success;
        public string message;
        public EggHatchSlotData slot;
    }

    [Serializable]
    private class EggClaimResponse
    {
        public bool success;
        public string message;
        public PlayerPetData pet;
    }

    [Serializable]
    private class SellEggResponse
    {
        public bool success;
        public string message;
        public int goldEarned;
    }
    [Serializable]
    private class EggUpgradeResponse
    {
        public bool success;
        public string message;
        public bool needAd;
        public int toEggId;
    }

    [Serializable]
    private class EggSkipResponse
    {
        public bool success;
        public string message;
        public bool needAd;
        public EggHatchSlotData slot;
    }

    [Serializable]
    private class EggUpgradeRarityResponse
    {
        public bool success;
        public string message;
        public bool needAd;
        public PlayerPetData pet;
    }
}
