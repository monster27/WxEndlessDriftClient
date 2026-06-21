using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager : SingletonMono<NetServerManager>
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
        StartCoroutine(DoFishingCoroutine("/api/fishing/catch", requestData));
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

                isFishBagFull = fishInventory.Values.Sum() >= fishBagCapacity;
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
        StartCoroutine(SendRequest<AutoFishingResponse>("/api/fishing/auto/start", requestData, resp =>
        {
            if (resp != null && resp.success) { isAutoFishing = true; Logger.Log("[NetServerManager] 自动钓鱼已启动"); }
            else Logger.LogWarning("[NetServerManager] 启动自动钓鱼失败: " + (resp?.message ?? "未知错误"));
        }));
    }

    public void StopAutoFishing()
    {
        if (!CheckNetworkConnection()) return;
        var requestData = new Dictionary<string, object> { { "playerId", _currentPlayerId } };
        StartCoroutine(SendRequest<AutoFishingResponse>("/api/fishing/auto/stop", requestData, resp =>
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
            if (!isConnected || this == null || gameObject == null) yield break;

            using (var request = UnityWebRequest.Get(serverUrl + "/api/fishing/status?playerId=" + _currentPlayerId))
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

        NotifyPlayReelAnimation(struggleTime, () =>
        {
            if (pendingCatchInfo != null) { ShowCatchResultFromServer(pendingCatchInfo); pendingCatchInfo = null; }
            StartCoroutine(FetchFishInventoryFromServer());
        });
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
        if (response.currentWeatherId > 0)
        {
            currentWeatherId = response.currentWeatherId;
            currentWeatherName = GetWeatherNameById(response.currentWeatherId);
            CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_WEATHER_CHANGED, new Dictionary<string, object>
            {
                { "weatherId", currentWeatherId }, { "weatherName", currentWeatherName }
            });
        }
        if (response.timeSlotId > 0)
        {
            currentTimeSlotId = response.timeSlotId;
            currentTimeSlotName = GetTimeSlotNameById(response.timeSlotId);
            currentTimeStatus = (TimeStatus)response.timeStatus;
            CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_TIME_SLOT_CHANGED, new Dictionary<string, object>
            {
                { "timeSlotId", currentTimeSlotId }, { "timeSlotName", currentTimeSlotName },
                { "timeStatus", (int)currentTimeStatus }, { "weatherId", currentWeatherId }
            });
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
            if (UIManager.Instance?.fishBagView != null && UIManager.Instance.fishBagView.gameObject.activeSelf)
                UIManager.Instance.fishBagView.RefreshItems();
            if (isFishBagFull || isPaused) NotifyPlayLazyAnimation(); else NotifyPlayIdleAnimation();
        });
    }

    // ========== 钓获显示 ==========

    private void ShowCatchResultFromServer(LastCatchInfo catchInfo)
    {
        if (catchInfo == null) return;
        Sprite icon = GetItemIcon(catchInfo.fishId);
        UIManager.Instance?.ShowCatchResult(catchInfo.fishName, catchInfo.weight, icon);
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

        StartCoroutine(SendRequest<object>($"/api/player/fish-bag/{_currentPlayerId}/sell", requestData,
            _ => { Logger.Log("[NetServerManager] 售卖鱼成功"); StartCoroutine(FetchPlayerDataAfterSell(itemIds, totalPrice)); },
            error => { Logger.LogWarning("[NetServerManager] 售卖鱼失败: " + error); UIManager.Instance?.ShowTip("售卖失败，请重试"); }));
    }

    public void NotifyAddFish(int fishId, int quantity) { }
    public void NotifyRefreshUI() => PlayerDataManager.Instance?.RefreshUI();
    public void NotifyShowCatchResult(string itemName, float weight, Sprite icon) => UIManager.Instance?.ShowCatchResult(itemName, weight, icon);

    public void NotifySyncInventoryFromServer()
    {
        if (PlayerDataManager.Instance == null) return;
        PlayerDataManager.Instance.SyncInventoryFromServer();
        if (UIManager.Instance?.fishBagView != null && UIManager.Instance.fishBagView.gameObject.activeSelf)
            PlayerAniManager.Instance.StartCoroutine(DelayedRefreshFishBag());
        UIManager.Instance?.UpdateGoldDisplay(playerGold);
    }

    private IEnumerator DelayedRefreshFishBag()
    {
        yield return null;
        UIManager.Instance?.fishBagView?.RefreshItems();
    }

    // ========== 辅助数据类 ==========

    [Serializable] private class FishingCatchResponse { public bool success; public string message; public string fishName; public float weight; public int goldBalance; public bool isTrash; public int trashStreak; public float struggleTime; }
    [Serializable] private class AutoFishingResponse { public bool success; public string message; }
    [Serializable] private class FishingStatusResponse { public bool success; public bool isAutoFishing; public bool isPaused; public int trashStreak; public float nextFishingTime; public float continuousModeRemainingTime; public int fishingMode; public int currentWeatherId; public int timeSlotId; public int timeStatus; public LastCatchInfo lastCatch; }
    [Serializable] private class LastCatchInfo { public int fishId; public string fishName; public float weight; public float struggleTime; public int goldEarned; }
}