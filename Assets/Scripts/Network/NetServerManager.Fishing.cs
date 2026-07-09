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
    // 钓鱼状态
    private bool isAutoFishing = false;
    private bool isPaused = false;
    private float timeUntilNextFishing = 0f;
    private int trashStreak = 0;
    private bool isFishBagFull = false;
    private FishingMode currentFishingMode = FishingMode.Normal;

    private bool isPlayingReelAnimation = false;
    private float struggleStartTime = 0f;
    private float currentStruggleTime = 0f;

    private int lastCatchFishId = -1;
    private LastCatchInfo pendingCatchInfo = null;

    public bool IsPaused => isPaused;
    public bool IsPlayingReelAnimation => isPlayingReelAnimation;

    private int GetCurrentSceneId() => EnvManager.Instance?.currentSceneId ?? 1;

    // ========== 钓鱼操作 ==========

    public void DoFishing(int baitId = 0)
    {
        if (!CheckNetworkConnection()) return;
        int actualBaitId = baitId == 0 && equippedBaitId != 0 ? equippedBaitId : baitId;
        int sceneId = GetCurrentSceneId();

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId }, { "sceneId", sceneId }, { "baitId", actualBaitId }
        };
        NetUtils.LogRequest("DoFishing", requestData);
        StartCoroutine(DoFishingCoroutine(ServerUrls.Fishing.Catch, requestData));
    }

    private IEnumerator DoFishingCoroutine(string url, Dictionary<string, object> requestData)
    {
        if (!isConnected) { Logger.LogWarning("[NetServerManager] 未连接服务器，无法钓鱼"); yield break; }

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (var request = new UnityWebRequest(serverUrl + url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Logger.LogError("[NetServerManager] 钓鱼请求失败: " + request.error);
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<FishingCatchResponse>(request.downloadHandler.text);
                if (response == null || !response.success)
                {
                    Logger.LogWarning("[NetServerManager] 钓鱼失败: " + (response?.message ?? "未知错误"));
                    yield break;
                }

                Logger.Log($"[NetServerManager] 钓鱼成功: {response.fishName} ({response.weight}kg)");
                playerGold = response.goldBalance;

                if (response.isTrash)
                {
                    trashStreak = response.trashStreak;
                    StartCoroutine(FetchFishInventoryFromServer());
                }
                else
                {
                    float struggleTime = response.struggleTime > 0 ? response.struggleTime : 2f;
                    NotifyPlayReelAnimation(struggleTime, () =>
                    {
                        trashStreak = 0;
                        StartCoroutine(FetchFishInventoryFromServer());
                    });
                }

                isFishBagFull = GetTotalFishCount() >= fishBagCapacity;
                if (isFishBagFull && !isPlayingReelAnimation) NotifyPlayLazyAnimation();
            }
            catch (Exception ex)
            {
                Logger.LogError("[NetServerManager] 解析钓鱼响应失败: " + ex.Message);
            }
        }
    }

    public void StartAutoFishing(int baitId = 0)
    {
        if (!CheckNetworkConnection()) return;
        if (isFishBagFull) { NotifyPlayLazyAnimation(); return; }

        int actualBaitId = baitId == 0 && equippedBaitId != 0 ? equippedBaitId : baitId;
        int sceneId = GetCurrentSceneId();

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId }, { "sceneId", sceneId }, { "baitId", actualBaitId }
        };
        StartCoroutine(SendRequest<AutoFishingResponse>(ServerUrls.Fishing.AutoStart, requestData, resp =>
        {
            if (resp != null && resp.success) { isAutoFishing = true; Logger.Log("[NetServerManager] 自动钓鱼已启动"); }
            else Logger.LogWarning("[NetServerManager] 启动自动钓鱼失败: " + (resp?.message ?? "未知错误"));
        }));
    }

    public void StopAutoFishing()
    {
        if (!CheckNetworkConnection()) return;
        var requestData = new Dictionary<string, object> { { "playerId", _currentPlayerId } };
        StartCoroutine(SendRequest<AutoFishingResponse>(ServerUrls.Fishing.AutoStop, requestData, resp =>
        {
            if (resp != null && resp.success) { isAutoFishing = false; Logger.Log("[NetServerManager] 自动钓鱼已停止"); }
        }));
    }

    private void AutoStartFishing()
    {
        if (isAutoFishing) return;
        int baitId = equippedBaitId > 0 ? equippedBaitId : 0;
        StartAutoFishing(baitId);
    }

    // ========== 轮询钓鱼状态 ==========

    private IEnumerator PollFishingStatus()
    {
        int lastCatchId = -1;
        bool isFirstRequest = true;

        while (isConnected && this != null && gameObject != null)
        {
            if (!isFirstRequest) yield return new WaitForSeconds(2f);
            isFirstRequest = false;

            // ⭐ 检查是否已销毁或断开
            if (!isConnected || this == null || gameObject == null)
            {
                Logger.Log("[NetServerManager] PollFishingStatus 退出 - 连接断开或对象销毁");
                yield break;
            }

            using (var request = UnityWebRequest.Get(serverUrl + ServerUrls.Fishing.StatusByPlayerId(_currentPlayerId)))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogWarning("[NetServerManager] 获取钓鱼状态失败: " + request.error);
                    continue;
                }

                try
                {
                    var response = JsonUtility.FromJson<FishingStatusResponse>(request.downloadHandler.text);
                    if (response == null || !response.success) continue;

                    // ⭐ 打印关键信息，方便调试
                    Logger.Log($"[NetServerManager] 轮询状态: auto={response.isAutoFishing}, paused={response.isPaused}, nextTime={response.nextFishingTime}, hasCatch={response.lastCatch != null}");

                    bool wasPaused = isPaused, wasFull = isFishBagFull;
                    isAutoFishing = response.isAutoFishing;
                    isPaused = response.isPaused;
                    trashStreak = response.trashStreak;

                    ProcessContinuousModeFromPoll(response);
                    ProcessNewCatch(response.lastCatch, ref lastCatchId);
                    ProcessReelAnimationRecovery(response);
                    UpdateAnimationState(wasPaused, wasFull);
                    ProcessWeatherAndTimeSync(response);

                    string display = GetNextFishingDisplay(response);
                    Logger.Log($"[NetServerManager] 钓鱼状态: auto={isAutoFishing}, paused={isPaused}, full={isFishBagFull}, trash={trashStreak}, fish={GetTotalFishCount()}, next={display}");
                }
                catch (Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析钓鱼状态失败: " + ex.Message);
                }
            }
        }
    }

    private void ProcessContinuousModeFromPoll(FishingStatusResponse response)
    {
        if (response.continuousModeRemainingTime > 0)
        {
            continuousModeRemainingTime = response.continuousModeRemainingTime;
            isInContinuousMode = true;
            baitEndTimeIsSeconds = true;
        }
        else
        {
            if (continuousModeRemainingTime > 0)
            {
                continuousModeRemainingTime -= 2f;
                if (continuousModeRemainingTime <= 0) { continuousModeRemainingTime = 0; isInContinuousMode = false; baitEndTimeIsSeconds = false; }
            }
            else { isInContinuousMode = false; baitEndTimeIsSeconds = false; }
        }

        currentFishingMode = Enum.IsDefined(typeof(FishingMode), response.fishingMode)
            ? (FishingMode)response.fishingMode
            : (continuousModeRemainingTime > 0 ? FishingMode.Continuous : FishingMode.Normal);

        timeUntilNextFishing = response.nextFishingTime > 0 ? response.nextFishingTime : 0;
        isFishBagFull = GetTotalFishCount() >= fishBagCapacity;
    }

    private void ProcessNewCatch(LastCatchInfo lastCatch, ref int lastCatchId)
    {
        if (lastCatch == null || lastCatch.fishId <= 0 || lastCatch.fishId == lastCatchId) return;
        lastCatchId = lastCatch.fishId;

        float struggleTime = lastCatch.struggleTime > 0 ? lastCatch.struggleTime : 1.5f;
        Logger.Log($"[NetServerManager] 检测到新钓获: {lastCatch.fishName} (ID:{lastCatch.fishId}), {lastCatch.weight}kg, 挣扎{struggleTime}秒");

        if (isPlayingReelAnimation || isFishBagFull) return;

        pendingCatchInfo = lastCatch;
        if (lastCatch.goldEarned > 0) playerGold += lastCatch.goldEarned;

        // ⭐ 获取鱼类稀有度颜色并设置到鱼饵提示动画
        SetFishTipColorByFishId(lastCatch.fishId, struggleTime);

        NotifyPlayReelAnimation(struggleTime, () =>
        {
            if (pendingCatchInfo != null) { ShowCatchResultFromServer(pendingCatchInfo); pendingCatchInfo = null; }
            StartCoroutine(FetchFishInventoryFromServer());
        });
    }

    /// <summary>
    /// 根据鱼类ID获取稀有度颜色，并设置到鱼饵提示动画
    /// </summary>
    private void SetFishTipColorByFishId(int fishId,float struggleTime)
    {
        try
        {
            // 1. 从 LoadDataManager 获取鱼类数据
            if (LoadDataManager.Instance == null)
            {
                Logger.LogWarning("[NetServerManager] LoadDataManager 未初始化，无法设置鱼饵提示颜色");
                return;
            }

            FishData fishData = LoadDataManager.Instance.GetFishById(fishId);
            if (fishData == null)
            {
                Logger.LogWarning($"[NetServerManager] 未找到鱼类数据: fishId={fishId}");
                return;
            }

            // 2. 获取稀有度ID
            int rarityId = fishData.rarityId;
            if (rarityId <= 0)
            {
                Logger.LogWarning($"[NetServerManager] 鱼类 {fishId} 的稀有度ID无效: {rarityId}");
                return;
            }

            // 3. 获取稀有度数据
            RarityData rarityData = LoadDataManager.Instance.GetRarityById(rarityId);
            if (rarityData == null)
            {
                Logger.LogWarning($"[NetServerManager] 未找到稀有度数据: rarityId={rarityId}");
                return;
            }

            // 4. 解析颜色
            if (string.IsNullOrEmpty(rarityData.colorCode))
            {
                Logger.LogWarning($"[NetServerManager] 稀有度 {rarityId} 的颜色代码为空");
                return;
            }

            if (!ColorUtility.TryParseHtmlString(rarityData.colorCode, out Color color))
            {
                Logger.LogWarning($"[NetServerManager] 解析颜色失败: colorCode={rarityData.colorCode}");
                return;
            }

            // 5. 设置到鱼饵提示动画
            if (PlayerAniManager.Instance != null)
            {
                PlayerAniManager.Instance.SetFishTip(color, struggleTime);
                Logger.Log($"[NetServerManager] 设置鱼饵提示颜色: fishId={fishId}, rarityId={rarityId}, color={rarityData.colorCode} , struggleTime-{struggleTime}");
            }
            else
            {
                Logger.LogWarning("[NetServerManager] PlayerAniManager 未初始化，无法设置鱼饵提示颜色");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[NetServerManager] SetFishTipColorByFishId 异常: {ex.Message}");
        }
    }

    private void ProcessReelAnimationRecovery(FishingStatusResponse response)
    {
        if (!isPaused || isPlayingReelAnimation || response.lastCatch == null || response.lastCatch.struggleTime <= 0) return;

        struggleStartTime = Time.time;
        currentStruggleTime = response.lastCatch.struggleTime;
        isPlayingReelAnimation = true;

        PlayerAniManager.Instance?.PlayReelAnimation(currentStruggleTime, () =>
        {
            isPlayingReelAnimation = false;
            struggleStartTime = 0f;
            currentStruggleTime = 0f;
            if (pendingCatchInfo != null) { ShowCatchResultFromServer(pendingCatchInfo); pendingCatchInfo = null; }
            StartCoroutine(FetchFishInventoryFromServer());
            NotifySyncInventoryFromServer();
            if (isFishBagFull || isPaused) NotifyPlayLazyAnimation(); else NotifyPlayIdleAnimation();
        });
    }

    private void UpdateAnimationState(bool wasPaused, bool wasFull)
    {
        if (isPlayingReelAnimation) return;
        if (isFishBagFull && !wasFull) NotifyPlayLazyAnimation();
        else if (!isFishBagFull && (wasFull || wasPaused)) NotifyPlayIdleAnimation();
    }

    private void ProcessWeatherAndTimeSync(FishingStatusResponse response)
    {
        Debug.Log($"[NetServerManager] ProcessWeatherAndTimeSync - currentWeatherId={response.currentWeatherId}, timeSlotId={response.timeSlotId}, timeStatus={response.timeStatus}");
        
        if (response.currentWeatherId > 0)
        {
            currentWeatherId = response.currentWeatherId;
            currentWeatherName = GetWeatherNameById(response.currentWeatherId);
            Debug.Log($"[NetServerManager] 触发天气变化事件: weatherId={currentWeatherId}, weatherName={currentWeatherName}");
            CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_WEATHER_CHANGED, new Dictionary<string, object>
            {
                { "weatherId", currentWeatherId }, { "weatherName", currentWeatherName }
            });
        }
        else
        {
            Debug.LogWarning($"[NetServerManager] 天气ID无效: {response.currentWeatherId}");
        }
        
        if (response.timeSlotId > 0)
        {
            currentTimeSlotId = response.timeSlotId;
            currentTimeSlotName = GetTimeSlotNameById(response.timeSlotId);
            currentTimeStatus = (TimeStatus)response.timeStatus;
            Debug.Log($"[NetServerManager] 触发时段变化事件: timeSlotId={currentTimeSlotId}, timeSlotName={currentTimeSlotName}, timeStatus={(int)currentTimeStatus}");
            CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_TIME_SLOT_CHANGED, new Dictionary<string, object>
            {
                { "timeSlotId", currentTimeSlotId }, { "timeSlotName", currentTimeSlotName },
                { "timeStatus", (int)currentTimeStatus }, { "weatherId", currentWeatherId }
            });
        }
        else
        {
            Debug.LogWarning($"[NetServerManager] 时段ID无效: {response.timeSlotId}");
        }
    }

    private string GetNextFishingDisplay(FishingStatusResponse response)
    {
        if (isFishBagFull) return "鱼篓已满";
        if (isPaused)
        {
            if (isPlayingReelAnimation && currentStruggleTime > 0)
                return $"收竿中 {Mathf.Max(0, currentStruggleTime - (Time.time - struggleStartTime)):F1}秒";
            if (response.lastCatch?.struggleTime > 0) return $"收竿中 {response.lastCatch.struggleTime:F1}秒";
            return "收竿中";
        }
        return response.nextFishingTime > 0 ? $"{response.nextFishingTime:F1}秒" : "等待中";
    }

    // ========== 动画控制 ==========

    public void NotifyPlayIdleAnimation()
    {
        if (isPlayingReelAnimation) return;
        PlayerAniManager.Instance?.PlayIdleAnimation();
    }

    public void NotifyPlayLazyAnimation()
    {
        if (isPlayingReelAnimation) return;
        PlayerAniManager.Instance?.PlayLazyAnimation();
    }

    public void NotifyPlayReelAnimation(float struggleTime, Action onComplete)
    {
        if (isPlayingReelAnimation) { onComplete?.Invoke(); return; }
        isPlayingReelAnimation = true;
        struggleStartTime = Time.time;
        currentStruggleTime = struggleTime;

        PlayerAniManager.Instance?.PlayReelAnimation(struggleTime, () =>
        {
            isPlayingReelAnimation = false;
            onComplete?.Invoke();
            struggleStartTime = 0f;
            currentStruggleTime = 0f;
            NotifySyncInventoryFromServer();
            if (GameUIManager.Instance?.fishBagView != null && GameUIManager.Instance.fishBagView.gameObject.activeSelf)
                GameUIManager.Instance.fishBagView.RefreshItems();
            if (isFishBagFull || isPaused) NotifyPlayLazyAnimation(); else NotifyPlayIdleAnimation();
        });
    }

    // ========== 钓获显示 ==========

    private void ShowCatchResultFromServer(LastCatchInfo catchInfo)
    {
        if (catchInfo == null) return;
        Sprite icon = GetItemIcon(catchInfo.fishId);
        // ✅ 传递星级ID
        GameUIManager.Instance?.ShowCatchResult(catchInfo.fishName, catchInfo.weight, icon, catchInfo.starRatingId);
        SyncCharacterDataFromServer();
    }

    private Sprite GetItemIcon(int itemId)
    {
        if (LoadDataManager.Instance?.items == null) return null;
        foreach (ItemData item in LoadDataManager.Instance.items)
            if (item.id == itemId && !string.IsNullOrEmpty(item.iconPath))
                return Resources.Load<Sprite>(item.iconPath);
        return null;
    }

    public void OnServerFishingResult(FishingResult result) { }

    // ========== 鱼操作 ==========

    public void OnSellFishItems((List<int>, int) data)
    {
        if (!CheckNetworkConnection()) return;
        var (itemIds, totalPrice) = data;

        var fishCountMap = new Dictionary<int, int>();
        foreach (var fishId in itemIds)
            fishCountMap[fishId] = fishCountMap.TryGetValue(fishId, out int c) ? c + 1 : 1;

        var sellItems = fishCountMap.Select(kvp => new Dictionary<string, object>
        {
            { "fishId", kvp.Key }, { "quantity", kvp.Value }
        }).ToList();

        Logger.Log($"[NetServerManager] 售卖鱼: {fishCountMap.Count}种, {itemIds.Count}条, 总价{totalPrice}");

        var requestData = new Dictionary<string, object> { { "items", sellItems }, { "totalPrice", totalPrice } };

        // 【调试】打印实际发送的JSON
        string jsonToSend = NetUtils.SerializeToJson(requestData);
        Logger.Log($"[NetServerManager] 发送JSON: {jsonToSend}");

        StartCoroutine(SendRequest<object>(ServerUrls.Player.SellFish(_currentPlayerId), requestData,
            _ => { Logger.Log("[NetServerManager] 售卖鱼成功"); StartCoroutine(FetchPlayerDataAfterSell(itemIds, totalPrice)); },
            error => { Logger.LogWarning("[NetServerManager] 售卖鱼失败: " + error); GameUIManager.Instance?.ShowTip("售卖失败，请重试"); }));
    }

    public void NotifyAddFish(int fishId, int quantity) { }
    public void NotifyRefreshUI() => PlayerDataManager.Instance?.RefreshUI();
    public void NotifyShowCatchResult(string itemName, float weight, Sprite icon) => GameUIManager.Instance?.ShowCatchResult(itemName, weight, icon);

    public void NotifySyncInventoryFromServer()
    {
        if (PlayerDataManager.Instance == null) return;
        PlayerDataManager.Instance.SyncInventoryFromServer();
        if (GameUIManager.Instance?.fishBagView != null && GameUIManager.Instance.fishBagView.gameObject.activeSelf)
            PlayerAniManager.Instance.StartCoroutine(DelayedRefreshFishBag());
        GameUIManager.Instance?.UpdateGoldDisplay(playerGold);
    }

    private IEnumerator DelayedRefreshFishBag()
    {
        yield return null;
        GameUIManager.Instance?.fishBagView?.RefreshItems();
    }

    // ========== 辅助数据类 ==========

    [Serializable] private class FishingCatchResponse { public bool success; public string message; public string fishName; public float weight; public int goldBalance; public bool isTrash; public int trashStreak; public float struggleTime; public bool isShiny; }
    [Serializable] private class AutoFishingResponse { public bool success; public string message; }
    [Serializable] private class FishingStatusResponse { public bool success; public bool isAutoFishing; public bool isPaused; public int trashStreak; public float nextFishingTime; public float continuousModeRemainingTime; public int fishingMode; public int currentWeatherId; public int timeSlotId; public int timeStatus; public LastCatchInfo lastCatch; }

    // Shared/SharedModels/NetworkData.cs

    [Serializable]
    public class LastCatchInfo
    {
        public int fishId;
        public string fishName;
        public float weight;
        public int goldEarned;
        public int expEarned;
        public bool isTrash;
        public float struggleTime;
        public int starRatingId;      // ✅ 新增：星级ID
        public long caughtTimestamp;  // ✅ 新增：捕获时间戳
        public bool isShiny;          // ✅ 新增：是否闪光鱼
    }
}