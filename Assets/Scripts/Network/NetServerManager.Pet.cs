// ============================================================
// 文件: NetServerManager.Pet.cs
// 说明: 宠物系统网络请求（列表、详情、出战、改名、喂食、抓昆虫、卖、锁、宠物栏、自动喂食）
// 路径: Assets/Scripts/Network/
// ============================================================

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

public partial class NetServerManager
{
    // ============================================================
    // 缓存数据
    // ============================================================

    /// <summary>当前缓存的宠物列表</summary>
    private List<PlayerPetData> playerPetsCache = new List<PlayerPetData>();

    /// <summary>公开访问器</summary>
    public List<PlayerPetData> PlayerPets => playerPetsCache;

    /// <summary>当前出战宠物（可能为 null）</summary>
    public PlayerPetData GetActivePet()
    {
        return playerPetsCache.Find(p => p.isActive);
    }

    /// <summary>根据 instanceId 获取宠物</summary>
    public PlayerPetData GetPetByInstanceId(int instanceId)
    {
        return playerPetsCache.Find(p => p.petInstanceId == instanceId);
    }

    // ============================================================
    // 宠物列表 / 详情
    // ============================================================

    public void FetchPlayerPets(Action<bool, List<PlayerPetData>> onComplete = null)
    {
        StartCoroutine(FetchPlayerPetsCoroutine(onComplete));
    }

    private IEnumerator FetchPlayerPetsCoroutine(Action<bool, List<PlayerPetData>> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets";
        Z_Logger.Log($"[NetServerManager] 获取宠物列表: {url}");

        yield return FetchGetJson<PetsListResponse>(url, data =>
        {
            if (data != null && data.success)
            {
                playerPetsCache = data.pets ?? new List<PlayerPetData>();
                Z_Logger.Log($"[NetServerManager] 宠物列表加载完成，共 {playerPetsCache.Count} 只");

                // 同步到 PlayerDataManager（由它广播事件）
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdatePlayerPets(playerPetsCache);
                }

                onComplete?.Invoke(true, playerPetsCache);
            }
            else
            {
                Z_Logger.LogWarning("[NetServerManager] 获取宠物列表失败");
                onComplete?.Invoke(false, null);
            }
        }, "宠物列表");
    }

    public void FetchPlayerPet(int instanceId, Action<bool, PlayerPetData> onComplete = null)
    {
        StartCoroutine(FetchPlayerPetCoroutine(instanceId, onComplete));
    }

    private IEnumerator FetchPlayerPetCoroutine(int instanceId, Action<bool, PlayerPetData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/{instanceId}";
        yield return FetchGetJson<PetDetailResponse>(url, data =>
        {
            if (data != null && data.success)
            {
                onComplete?.Invoke(true, data.pet);
            }
            else
            {
                onComplete?.Invoke(false, null);
            }
        }, "宠物详情");
    }

    // ============================================================
    // 设置出战
    // ============================================================

    public void SetActivePet(int instanceId, Action<bool, string> onComplete = null)
    {
        StartCoroutine(SetActivePetCoroutine(instanceId, onComplete));
    }

    private IEnumerator SetActivePetCoroutine(int instanceId, Action<bool, string> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/{instanceId}/active";
        Z_Logger.Log($"[NetServerManager] 设置出战宠物: instanceId={instanceId}");

        yield return SendRequest<PetOperationResponse>(url, null, resp =>
        {
            if (resp != null && resp.success)
            {
                // 本地缓存：把目标设为出战，其他设为非出战
                foreach (var p in playerPetsCache)
                    p.isActive = (p.petInstanceId == instanceId);

                // 同步到 PlayerDataManager（由它广播事件）
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.SetLocalActivePet(instanceId);
                }

                Z_Logger.Log($"[NetServerManager] 设置出战宠物成功: instanceId={instanceId}");
                onComplete?.Invoke(true, resp.message);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "设置失败");
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败");
        }, forcePost: true);
    }

    // ============================================================
    // 改名
    // ============================================================

    public void RenamePet(int instanceId, string nickname, Action<bool, string> onComplete = null)
    {
        StartCoroutine(RenamePetCoroutine(instanceId, nickname, onComplete));
    }

    private IEnumerator RenamePetCoroutine(int instanceId, string nickname, Action<bool, string> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/{instanceId}/rename";
        var requestData = new Dictionary<string, object>
        {
            { "nickname", nickname }
        };

        yield return SendRequest<PetOperationResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                var pet = playerPetsCache.Find(p => p.petInstanceId == instanceId);
                if (pet != null) pet.nickname = nickname;

                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdateLocalPetNickname(instanceId, nickname);
                }

                onComplete?.Invoke(true, resp.message);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "改名失败");
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败");
        }, forcePost: true);
    }

    // ============================================================
    // 喂食（用鱼喂）
    // ============================================================

    public void FeedPet(int instanceId, int fishInstanceId,
        Action<bool, string, int, int> onComplete = null)
    {
        StartCoroutine(FeedPetCoroutine(instanceId, fishInstanceId, onComplete));
    }

    private IEnumerator FeedPetCoroutine(int instanceId, int fishInstanceId,
        Action<bool, string, int, int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0, 0);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/{instanceId}/feed";
        var requestData = new Dictionary<string, object>
        {
            { "fishInstanceId", fishInstanceId }
        };

        Z_Logger.Log($"[NetServerManager] 喂食宠物: instanceId={instanceId}, fishInstanceId={fishInstanceId}");

        yield return SendRequest<FeedPetResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                // 更新本地宠物饥饿度
                var pet = playerPetsCache.Find(p => p.petInstanceId == instanceId);
                if (pet != null) pet.hunger = resp.currentHunger;

                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdateLocalPetHunger(instanceId, resp.currentHunger);
                }

                Z_Logger.Log($"[NetServerManager] 喂食成功，恢复 {resp.restored} 点");

                // 鱼被消耗了，刷新鱼篓
                FetchPlayerFishBag();

                onComplete?.Invoke(true, resp.message, resp.restored, resp.currentHunger);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "喂食失败", 0, 0);
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败", 0, 0);
        }, forcePost: true);
    }

    // ============================================================
    // 抓昆虫（手动触发）
    // ============================================================

    public void CatchInsect(Action<InsectCatchResult> onComplete = null)
    {
        StartCoroutine(CatchInsectCoroutine(onComplete));
    }

    private IEnumerator CatchInsectCoroutine(Action<InsectCatchResult> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(new InsectCatchResult { success = false, message = "网络未连接" });
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/catch-insect";
        Z_Logger.Log($"[NetServerManager] 抓昆虫请求");

        yield return SendRequest<InsectCatchResult>(url, null, resp =>
        {
            if (resp != null && resp.success)
            {
                Z_Logger.Log($"[NetServerManager] 抓昆虫成功: {resp.insectName}({resp.insectId})");

                // 更新出战宠物冷却
                var activePet = GetActivePet();
                if (activePet != null)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    activePet.lastInsectCatchTime = now;
                    activePet.nextInsectCatchTime = now + resp.nextCooldownSeconds;
                }

                // 抓到的昆虫加入本地列表
                if (PlayerDataManager.Instance != null)
                {
                    var insectData = new PlayerInsectData
                    {
                        insectInstanceId = resp.insectInstanceId,
                        insectId = resp.insectId,
                        name = resp.insectName,
                        rarityId = resp.rarityId,
                        caughtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        caughtByPetInstanceId = activePet?.petInstanceId ?? 0
                    };
                    PlayerDataManager.Instance.AddInsect(insectData);
                }

                // 触发抓昆虫成功事件（UI 弹窗用）
                CommunicateEvent.Modify<InsectCatchResult>("Insect_Caught", resp);

                onComplete?.Invoke(resp);
            }
            else
            {
                Z_Logger.LogWarning($"[NetServerManager] 抓昆虫失败: {resp?.message}");
                onComplete?.Invoke(resp ?? new InsectCatchResult { success = false, message = "抓取失败" });
            }
        }, err =>
        {
            onComplete?.Invoke(new InsectCatchResult { success = false, message = "网络请求失败" });
        }, forcePost: true);
    }

    /// <summary>
    /// 查询出战宠物的抓昆虫冷却剩余
    /// </summary>
    public void FetchInsectCooldown(Action<int> onComplete = null)
    {
        StartCoroutine(FetchInsectCooldownCoroutine(onComplete));
    }

    private IEnumerator FetchInsectCooldownCoroutine(Action<int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(-1);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/insect-cooldown";
        yield return FetchGetJson<InsectCooldownResponse>(url, data =>
        {
            if (data != null && data.success)
            {
                onComplete?.Invoke(data.remainingSeconds);
            }
            else
            {
                onComplete?.Invoke(-1);
            }
        }, "抓昆虫冷却");
    }
    // ============================================================
    // 直接购买宠物（测试用）
    // ============================================================

    public void BuyPet(int petId, Action<bool, string, PlayerPetData> onComplete = null)
    {
        StartCoroutine(BuyPetCoroutine(petId, onComplete));
    }

    private IEnumerator BuyPetCoroutine(int petId, Action<bool, string, PlayerPetData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/buy";
        var requestData = new Dictionary<string, object>
        {
            { "petId", petId }
        };

        Z_Logger.Log($"[NetServerManager] 直接购买宠物: petId={petId}");

        yield return SendRequest<BuyPetResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                Z_Logger.Log($"[NetServerManager] 购买宠物成功: petInstanceId={resp.pet?.petInstanceId}");

                // 1. 加入本地缓存
                if (resp.pet != null)
                {
                    playerPetsCache.Add(resp.pet);

                    // 2. 同步到 PlayerDataManager
                    if (PlayerDataManager.Instance != null)
                    {
                        PlayerDataManager.Instance.AddPet(resp.pet);
                        PlayerDataManager.Instance.LocalIncreasePetStorageUsed();
                    }
                }

                // 3. 刷新金币
                StartCoroutine(FetchPlayerGold());

                onComplete?.Invoke(true, resp.message, resp.pet);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "购买失败", null);
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败", null);
        }, forcePost: true);
    }

    // ============================================================
    // 卖宠物
    // ============================================================

    public void SellPet(int instanceId, Action<bool, string, int> onComplete = null)
    {
        StartCoroutine(SellPetCoroutine(instanceId, onComplete));
    }

    private IEnumerator SellPetCoroutine(int instanceId, Action<bool, string, int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/{instanceId}/sell";
        Z_Logger.Log($"[NetServerManager] 卖宠物: instanceId={instanceId}");

        yield return SendRequest<SellPetResponse>(url, null, resp =>
        {
            if (resp != null && resp.success)
            {
                playerPetsCache.RemoveAll(p => p.petInstanceId == instanceId);

                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.RemovePet(instanceId);
                }

                Z_Logger.Log($"[NetServerManager] 卖宠物成功，获得 {resp.goldEarned} 金币");
                StartCoroutine(FetchPlayerGold());

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

    public void SellPetsBatch(List<int> instanceIds, Action<bool, string, int> onComplete = null)
    {
        StartCoroutine(SellPetsBatchCoroutine(instanceIds, onComplete));
    }

    private IEnumerator SellPetsBatchCoroutine(List<int> instanceIds, Action<bool, string, int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0);
            yield break;
        }

        if (instanceIds == null || instanceIds.Count == 0)
        {
            onComplete?.Invoke(false, "请选择要出售的宠物", 0);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/sell-batch";
        var requestData = new Dictionary<string, object>
        {
            { "instanceIds", instanceIds }
        };

        Z_Logger.Log($"[NetServerManager] 批量卖宠物: count={instanceIds.Count}");

        yield return SendRequest<SellPetResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                playerPetsCache.RemoveAll(p => instanceIds.Contains(p.petInstanceId));

                if (PlayerDataManager.Instance != null)
                {
                    foreach (var id in instanceIds)
                    {
                        PlayerDataManager.Instance.RemovePet(id);
                    }
                }

                Z_Logger.Log($"[NetServerManager] 批量卖宠物成功，获得 {resp.goldEarned} 金币");
                StartCoroutine(FetchPlayerGold());

                onComplete?.Invoke(true, resp.message, resp.goldEarned);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "批量出售失败", 0);
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败", 0);
        }, forcePost: true);
    }

    // ============================================================
    // 锁定 / 解锁宠物
    // ============================================================

    public void SetPetLocked(int instanceId, bool isLocked, Action<bool, string> onComplete = null)
    {
        StartCoroutine(SetPetLockedCoroutine(instanceId, isLocked, onComplete));
    }

    private IEnumerator SetPetLockedCoroutine(int instanceId, bool isLocked, Action<bool, string> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/{instanceId}/lock";
        var requestData = new Dictionary<string, object>
        {
            { "isLocked", isLocked }
        };

        yield return SendRequest<PetOperationResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                // ✅ 同步更新 NetServerManager 自己的缓存
                var pet = playerPetsCache.Find(p => p.petInstanceId == instanceId);
                if (pet != null) pet.isLocked = isLocked;

                // ✅ 同步到 PlayerDataManager
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdateLocalPetLocked(instanceId, isLocked);
                }

                onComplete?.Invoke(true, resp.message);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "操作失败");
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败");
        }, forcePost: true);
    }

    public void SetPetsLockedBatch(List<int> instanceIds, bool isLocked, Action<bool, string> onComplete = null)
    {
        StartCoroutine(SetPetsLockedBatchCoroutine(instanceIds, isLocked, onComplete));
    }

    private IEnumerator SetPetsLockedBatchCoroutine(List<int> instanceIds, bool isLocked, Action<bool, string> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        if (instanceIds == null || instanceIds.Count == 0)
        {
            onComplete?.Invoke(false, "请选择要操作的宠物");
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pets/lock-batch";
        var requestData = new Dictionary<string, object>
        {
            { "instanceIds", instanceIds },
            { "isLocked", isLocked }
        };

        yield return SendRequest<PetOperationResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                // ✅ 同步更新 NetServerManager 自己的缓存
                foreach (var id in instanceIds)
                {
                    var pet = playerPetsCache.Find(p => p.petInstanceId == id);
                    if (pet != null) pet.isLocked = isLocked;
                }

                // ✅ 同步到 PlayerDataManager
                if (PlayerDataManager.Instance != null)
                {
                    foreach (var id in instanceIds)
                    {
                        PlayerDataManager.Instance.UpdateLocalPetLocked(id, isLocked);
                    }
                }

                onComplete?.Invoke(true, resp.message);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "批量操作失败");
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败");
        }, forcePost: true);
    }

    // ============================================================
    // 宠物栏（储存）
    // ============================================================

    public void FetchPetStorageStatus(Action<bool, PetStorageStatusData> onComplete = null)
    {
        StartCoroutine(FetchPetStorageStatusCoroutine(onComplete));
    }

    private IEnumerator FetchPetStorageStatusCoroutine(Action<bool, PetStorageStatusData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pet-storage";
        yield return FetchGetJson<PetStorageStatusData>(url, data =>
        {
            if (data != null && data.success)
            {
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdatePetStorageStatus(data);
                }
                onComplete?.Invoke(true, data);
            }
            else
            {
                onComplete?.Invoke(false, data);
            }
        }, "宠物栏状态");
    }

    public void UpgradePetStorage(Action<bool, string, PetStorageStatusData> onComplete = null)
    {
        StartCoroutine(UpgradePetStorageCoroutine(onComplete));
    }

    private IEnumerator UpgradePetStorageCoroutine(Action<bool, string, PetStorageStatusData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pet-storage/upgrade";

        yield return SendRequest<UpgradePetStorageResponse>(url, null, resp =>
        {
            if (resp != null && resp.success)
            {
                if (resp.data != null && PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdatePetStorageStatus(resp.data);
                }
                StartCoroutine(FetchPlayerGold());
                onComplete?.Invoke(true, resp.message, resp.data);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "升级失败", null);
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败", null);
        }, forcePost: true);
    }

    public void UpgradePetStorageByAd(Action<bool, string, PetStorageStatusData> onComplete = null)
    {
        StartCoroutine(UpgradePetStorageByAdCoroutine(onComplete));
    }

    private IEnumerator UpgradePetStorageByAdCoroutine(Action<bool, string, PetStorageStatusData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pet-storage/upgrade-by-ad";

        yield return SendRequest<UpgradePetStorageResponse>(url, null, resp =>
        {
            if (resp != null && resp.success)
            {
                if (resp.data != null && PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdatePetStorageStatus(resp.data);
                }
                onComplete?.Invoke(true, resp.message, resp.data);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "广告升级失败", null);
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败", null);
        }, forcePost: true);
    }

    // ============================================================
    // 自动喂食
    // ============================================================

    public void FetchAutoFeedStatus(Action<bool, AutoFeedStatusData> onComplete = null)
    {
        StartCoroutine(FetchAutoFeedStatusCoroutine(onComplete));
    }

    private IEnumerator FetchAutoFeedStatusCoroutine(Action<bool, AutoFeedStatusData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pet/auto-feed/status";
        yield return FetchGetJson<AutoFeedStatusData>(url, data =>
        {
            if (data != null && data.success)
            {
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdateAutoFeedStatus(data);
                }
                onComplete?.Invoke(true, data);
            }
            else
            {
                onComplete?.Invoke(false, data);
            }
        }, "自动喂食状态");
    }

    public void ToggleAutoFeed(bool enable, Action<bool, string> onComplete = null)
    {
        StartCoroutine(ToggleAutoFeedCoroutine(enable, onComplete));
    }

    private IEnumerator ToggleAutoFeedCoroutine(bool enable, Action<bool, string> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pet/auto-feed/toggle";
        var requestData = new Dictionary<string, object>
        {
            { "enable", enable }
        };

        yield return SendRequest<PetOperationResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdateLocalAutoFeedEnabled(enable);
                }
                onComplete?.Invoke(true, resp.message);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "切换失败");
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败");
        }, forcePost: true);
    }

    public void FetchAutoFeedFilterConfig(Action<bool, AutoFeedFilterData> onComplete = null)
    {
        StartCoroutine(FetchAutoFeedFilterConfigCoroutine(onComplete));
    }

    private IEnumerator FetchAutoFeedFilterConfigCoroutine(Action<bool, AutoFeedFilterData> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pet/auto-feed/filter-config";
        yield return FetchGetJson<AutoFeedFilterData>(url, data =>
        {
            if (data != null && data.success)
            {
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdateAutoFeedFilter(data);
                }
                onComplete?.Invoke(true, data);
            }
            else
            {
                onComplete?.Invoke(false, data);
            }
        }, "自动喂食过滤配置");
    }

    public void SaveAutoFeedFilterConfig(AutoFeedFilterData filter, Action<bool, string> onComplete = null)
    {
        StartCoroutine(SaveAutoFeedFilterConfigCoroutine(filter, onComplete));
    }

    private IEnumerator SaveAutoFeedFilterConfigCoroutine(AutoFeedFilterData filter, Action<bool, string> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        if (filter == null)
        {
            onComplete?.Invoke(false, "过滤配置为空");
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/pet/auto-feed/filter-config";
        var requestData = new Dictionary<string, object>
        {
            { "rarity201", filter.rarity201 },
            { "rarity202", filter.rarity202 },
            { "rarity203", filter.rarity203 },
            { "rarity204", filter.rarity204 },
            { "rarity205", filter.rarity205 },
            { "rarity206", filter.rarity206 },
            { "starRate501", filter.starRate501 },
            { "starRate502", filter.starRate502 },
            { "starRate503", filter.starRate503 },
            { "starRate504", filter.starRate504 },
            { "notShine", filter.notShine },
            { "isShine", filter.isShine },
            { "skipSelected", filter.skipSelected }
        };

        yield return SendRequest<PetOperationResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdateAutoFeedFilter(filter);
                }
                onComplete?.Invoke(true, resp.message);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "保存失败");
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败");
        }, forcePost: true);
    }

    // ============================================================
    // 响应数据类
    // ============================================================

    [Serializable]
    private class PetsListResponse
    {
        public bool success;
        public List<PlayerPetData> pets;
    }

    [Serializable]
    private class PetDetailResponse
    {
        public bool success;
        public PlayerPetData pet;
    }

    [Serializable]
    private class PetOperationResponse
    {
        public bool success;
        public string message;
    }

    [Serializable]
    private class FeedPetResponse
    {
        public bool success;
        public string message;
        public int restored;
        public int currentHunger;
    }

    [Serializable]
    private class InsectCooldownResponse
    {
        public bool success;
        public int remainingSeconds;
    }

    [Serializable]
    private class BuyPetResponse
    {
        public bool success;
        public string message;
        public PlayerPetData pet;
    }

    [Serializable]
    private class SellPetResponse
    {
        public bool success;
        public string message;
        public int goldEarned;
    }

    [Serializable]
    private class UpgradePetStorageResponse
    {
        public bool success;
        public string message;
        public PetStorageStatusData data;
    }
}

// ============================================================
// 自动喂食 DTO（客户端 & 服务器共用字段名）
// ============================================================

/// <summary>
/// 自动喂食状态（服务器 → 客户端）
/// </summary>
[Serializable]
public class AutoFeedStatusData
{
    public bool success;
    public string message = string.Empty;

    /// <summary>当前等级 1~10</summary>
    public int level;

    /// <summary>是否开启</summary>
    public bool enabled;

    /// <summary>该等级的喂食间隔（秒）</summary>
    public int intervalSeconds;

    /// <summary>最大等级</summary>
    public int maxLevel;

    /// <summary>距离下次喂食剩余秒数</summary>
    public int remainingSeconds;
}

/// <summary>
/// 自动喂食过滤配置（客户端 ↔ 服务器）
/// 语义：勾选的是"保留"的，不喂
/// </summary>
[Serializable]
public class AutoFeedFilterData
{
    public bool success;

    // 稀有度 201~206
    public bool rarity201;
    public bool rarity202;
    public bool rarity203;
    public bool rarity204;
    public bool rarity205;
    public bool rarity206;

    // 星级 501~504
    public bool starRate501;
    public bool starRate502;
    public bool starRate503;
    public bool starRate504;

    // 闪光
    public bool notShine;
    public bool isShine;

    // 跳过已选中（自动喂食里不生效，保留字段兼容）
    public bool skipSelected;
}
