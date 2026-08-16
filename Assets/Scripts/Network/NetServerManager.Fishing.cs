using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
//using SharedModels;
using Logger = Utils.Logger;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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

    // 待处理的动画请求（收杆动画结束后执行）
    private enum PendingAnimationType { None, Idle, Lazy }
    private PendingAnimationType pendingAnimationRequest = PendingAnimationType.None;

    private long lastCatchTimestamp = 0;
    private LastCatchInfo pendingCatchInfo = null;
    private readonly Queue<LastCatchInfo> pendingCatchQueue = new Queue<LastCatchInfo>();

    // AA 句柄
    private AsyncOperationHandle<Sprite> _iconHandle;

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
                    isFishBagFull = GetTotalFishCount() >= fishBagCapacity;
                    if (isFishBagFull) NotifyPlayLazyAnimation();
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

        // ✅ 启动前检查鱼篓状态
        if (isFishBagFull)
        {
            Logger.Log("[NetServerManager] StartAutoFishing - 鱼篓已满，无法启动自动钓鱼");
            NotifyPlayLazyAnimation();
            GameUIManager.ShowMessage("鱼篓已满，无法继续钓鱼");
            return;
        }

        int actualBaitId = baitId == 0 && equippedBaitId != 0 ? equippedBaitId : baitId;
        int sceneId = GetCurrentSceneId();

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", actualBaitId }
        };

        Logger.Log($"[NetServerManager] StartAutoFishing - 发送启动请求, sceneId={sceneId}, baitId={actualBaitId}");

        StartCoroutine(SendRequest<AutoFishingResponse>(ServerUrls.Fishing.AutoStart, requestData,
            (resp) =>
            {
                if (resp != null && resp.success)
                {
                    isAutoFishing = true;
                    isPaused = false;
                    Logger.Log("[NetServerManager] 自动钓鱼已启动");
                }
                else
                {
                    Logger.LogWarning($"[NetServerManager] 启动自动钓鱼失败: {resp?.message ?? "未知错误"}");
                    GameUIManager.ShowMessage(resp?.message ?? "启动自动钓鱼失败");
                }
            },
            (error) =>
            {
                Logger.LogError($"[NetServerManager] 启动自动钓鱼请求失败: {error}");
                GameUIManager.ShowMessage("网络错误，启动自动钓鱼失败");
            }
        ));
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
        if (isAutoFishing)
        {
            Logger.Log("[NetServerManager] AutoStartFishing - 已经在自动钓鱼中");
            return;
        }

        // ✅ 再次检查鱼篓状态，防止在启动时鱼篓已满
        if (isFishBagFull)
        {
            Logger.Log("[NetServerManager] AutoStartFishing - 鱼篓已满，无法启动自动钓鱼");
            NotifyPlayLazyAnimation();
            return;
        }

        int baitId = equippedBaitId > 0 ? equippedBaitId : 0;
        Logger.Log($"[NetServerManager] AutoStartFishing - 开始自动钓鱼, baitId={baitId}");
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

                    if (!response.isAutoFishing && isAutoFishing)
                    {
                        Logger.Log($"[NetServerManager] 警告: 服务器返回 isAutoFishing=false, 但本地状态为 true, 可能是服务器缓存丢失");
                        Logger.Log($"[NetServerManager] 当前鱼篓状态: {GetTotalFishCount()}/{fishBagCapacity}, 是否已满: {isFishBagFull}");

                        if (!isFishBagFull)
                        {
                            Logger.Log($"[NetServerManager] 鱼篓未满，保持本地自动钓鱼状态");
                        }
                        else
                        {
                            Logger.Log($"[NetServerManager] 鱼篓已满，同步服务器状态为 false");
                            isAutoFishing = false;
                        }
                    }
                    else
                    {
                        isAutoFishing = response.isAutoFishing;
                    }

                    isPaused = response.isPaused;
                    trashStreak = response.trashStreak;

                    ProcessContinuousModeFromPoll(response);
                    ProcessNewCatch(response.lastCatch, ref lastCatchId);
                    ProcessReelAnimationRecovery(response);
                    UpdateAnimationState(wasPaused, wasFull);
                    ProcessWeatherAndTimeSync(response);
                    ProcessAutoSellFishBagUpdate();

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

    private bool _pendingIsFirstCatch = false;
    public bool PendingIsFirstCatch => _pendingIsFirstCatch;

    private void ProcessNewCatch(LastCatchInfo lastCatch, ref int lastCatchId)
    {
        if (lastCatch == null || lastCatch.fishId <= 0) return;

        // ✅ 使用 caughtTimestamp 去重（每次钓获有唯一时间戳，同种鱼连续上钩也不会丢失）
        if (lastCatch.caughtTimestamp == lastCatchTimestamp) return;
        lastCatchTimestamp = lastCatch.caughtTimestamp;

        // ✅ 在检测到新钓获时（而非动画开始时）检查是否为首次获取
        // 因为动画回调中的 FetchFishInventoryFromServer 可能已更新 fishInventory，
        // 导致后续排队捕获的首次判断失效
        int currentCount = 0;
        fishInventory.TryGetValue(lastCatch.fishId, out currentCount);
        lastCatch.isFirstCatch = (currentCount == 0);

        float struggleTime = lastCatch.struggleTime > 0 ? lastCatch.struggleTime : 1.5f;
        Logger.Log($"[NetServerManager] 检测到新钓获: {lastCatch.fishName} (ID:{lastCatch.fishId}), {lastCatch.weight}kg, 挣扎{struggleTime}秒, 时间戳:{lastCatch.caughtTimestamp}, 首次:{lastCatch.isFirstCatch}");

        // ✅ 如果正在播放收竿动画或鱼篓已满，将捕获加入队列，等动画结束后再显示
        if (isPlayingReelAnimation || isFishBagFull)
        {
            Logger.Log($"[NetServerManager] 收竿动画中/鱼篓已满，捕获加入待显示队列: {lastCatch.fishName}");
            pendingCatchQueue.Enqueue(lastCatch);
            return;
        }

        StartCatchAnimation(lastCatch, struggleTime);
    }

    /// <summary>
    /// 开始收竿动画并显示捕获结果
    /// </summary>
    private void StartCatchAnimation(LastCatchInfo catchInfo, float struggleTime)
    {
        // ✅ 使用在检测时已判定的 isFirstCatch，避免 fishInventory 被异步更新后导致误判
        _pendingIsFirstCatch = catchInfo.isFirstCatch;

        pendingCatchInfo = catchInfo;

        // ⭐ 获取鱼类稀有度颜色并设置到鱼饵提示动画
        SetFishTipColorByFishId(catchInfo.fishId, struggleTime);

        NotifyPlayReelAnimation(struggleTime, () =>
        {
            if (pendingCatchInfo != null)
            {
                ShowCatchResultFromServer(pendingCatchInfo);
                pendingCatchInfo = null;
            }
            StartCoroutine(FetchFishInventoryFromServer());

            // ✅ 动画结束后，检查队列中是否有待显示的捕获
            if (pendingCatchQueue.Count > 0)
            {
                var next = pendingCatchQueue.Dequeue();
                float nextStruggle = next.struggleTime > 0 ? next.struggleTime : 1.5f;
                Logger.Log($"[NetServerManager] 从队列中取出下一条捕获: {next.fishName}");
                StartCatchAnimation(next, nextStruggle);
            }
        });
    }

    /// <summary>
    /// 根据鱼类ID获取稀有度颜色，并设置到鱼饵提示动画
    /// </summary>
    private void SetFishTipColorByFishId(int fishId, float struggleTime)
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
            if (pendingCatchInfo != null)
            {
                ShowCatchResultFromServer(pendingCatchInfo);
                pendingCatchInfo = null;
            }
            StartCoroutine(FetchFishInventoryFromServer());
            NotifySyncInventoryFromServer();
            // ✅ 修复：数据同步完成后由 PlayerDataManager.CheckAndUpdateAnimationState() 决定最终动画
            // 避免使用旧的 isFishBagFull 值导致动画状态错误
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

    private void ProcessAutoSellFishBagUpdate()
    {
        var fishBagView = GameUIManager.Instance?.fishBagView;
        if (fishBagView != null && fishBagView.gameObject.activeSelf)
        {
            StartCoroutine(FetchAutoSellTimerAndUpdateFishBag());
        }
    }

    private IEnumerator FetchAutoSellTimerAndUpdateFishBag()
    {
        yield return StartCoroutine(FetchFishInventoryFromServer());
        yield return null;
        var fishBagView = GameUIManager.Instance?.fishBagView;
        if (fishBagView != null && fishBagView.gameObject.activeSelf)
        {
            fishBagView.RefreshItems();
        }
        GameUIManager.Instance?.UpdateGoldDisplay(playerGold);
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
        if (isPlayingReelAnimation)
        {
            pendingAnimationRequest = PendingAnimationType.Idle;
            Logger.Log("[NetServerManager] 收杆动画播放中，将Idle动画请求排入队列");
            return;
        }
        pendingAnimationRequest = PendingAnimationType.None;
        PlayerAniManager.Instance?.PlayIdleAnimation();
    }

    public void NotifyPlayLazyAnimation()
    {
        if (isPlayingReelAnimation)
        {
            pendingAnimationRequest = PendingAnimationType.Lazy;
            Logger.Log("[NetServerManager] 收杆动画播放中，将Lazy动画请求排入队列");
            return;
        }
        pendingAnimationRequest = PendingAnimationType.None;
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

            // ✅ 收杆动画结束后，执行待处理的动画请求
            ExecutePendingAnimationRequest();
        });
    }

    /// <summary>
    /// 执行待处理的动画请求
    /// </summary>
    private void ExecutePendingAnimationRequest()
    {
        if (pendingAnimationRequest == PendingAnimationType.None)
        {
            // 没有待处理的请求，让 PlayerDataManager.CheckAndUpdateAnimationState 来决定
            PlayerDataManager.Instance?.CheckAndUpdateAnimationState();
            return;
        }

        switch (pendingAnimationRequest)
        {
            case PendingAnimationType.Idle:
                Logger.Log("[NetServerManager] 执行待处理的Idle动画请求");
                PlayerAniManager.Instance?.PlayIdleAnimation();
                break;
            case PendingAnimationType.Lazy:
                Logger.Log("[NetServerManager] 执行待处理的Lazy动画请求");
                PlayerAniManager.Instance?.PlayLazyAnimation();
                break;
        }

        pendingAnimationRequest = PendingAnimationType.None;
    }

    // ========== 钓获显示 ==========

    /// <summary>
    /// 从服务器数据显示钓获结果
    /// </summary>
    private void ShowCatchResultFromServer(LastCatchInfo catchInfo)
    {
        if (catchInfo == null) return;

        // ✅ 异步加载图标
        LoadItemIcon(catchInfo.fishId, (icon) =>
        {
            bool isFish = IsFishItem(catchInfo.fishId);

            GameUIManager.Instance?.ShowCatchResult(
                catchInfo.fishName,
                catchInfo.weight,
                icon,
                catchInfo.starRatingId,
                catchInfo.fishId,
                isFish,
                catchInfo.isFirstCatch
            );
        });

        SyncCharacterDataFromServer();
    }

    /// <summary>
    /// 异步加载物品图标
    /// </summary>
    private void LoadItemIcon(int itemId, Action<Sprite> onLoaded)
    {
        if (LoadDataManager.Instance?.items == null)
        {
            onLoaded?.Invoke(null);
            return;
        }

        foreach (var item in LoadDataManager.Instance.items)
        {
            if (item.id == itemId && !string.IsNullOrEmpty(item.iconPath))
            {
                AssetManager.LoadFromAddressables<Sprite>(item.iconPath, (sprite, handle) =>
                {
                    _iconHandle = handle;
                    onLoaded?.Invoke(sprite);
                });
                return;
            }
        }

        onLoaded?.Invoke(null);
    }

    private bool IsFishItem(int itemId)
    {
        // ✅ 方案1：通过 fishDict 判断是否为鱼类（最可靠）
        if (LoadDataManager.Instance?.fishes != null)
        {
            foreach (var fish in LoadDataManager.Instance.fishes)
            {
                if (fish.id == itemId)
                    return true;
            }
        }

        // ✅ 方案2：通过 items 的 categoryId 判断（降级方案）
        if (LoadDataManager.Instance?.items != null)
        {
            foreach (var item in LoadDataManager.Instance.items)
            {
                if (item.id == itemId && item.categoryId == 11)  // 鱼类分类ID是11
                    return true;
            }
        }

        // ✅ 方案3：通过 fishId 范围判断（垃圾 ID 是 9001-9020）
        if (itemId >= 1001 && itemId <= 1999)
            return true;

        return false;
    }

    public void OnServerFishingResult(FishingResult result) { }

    // ========== 鱼操作 ==========
    public void OnSellFishItems(List<int> detailIds)
    {
        if (!CheckNetworkConnection()) return;

        if (detailIds == null || detailIds.Count == 0)
        {
            Logger.LogWarning("[NetServerManager] 售卖鱼失败：没有选择任何鱼");
            GameUIManager.Instance?.ShowTip("请选择要出售的鱼");
            return;
        }

        // ✅ 使用服务器期望的字段名：fishItemIds（不是 detailIds）
        var requestData = new Dictionary<string, object>
    {
        { "fishItemIds", detailIds },
        { "totalPrice", 0 }  // 服务器会自动计算价格，传0即可
    };

        Logger.Log($"[NetServerManager] 售卖鱼: {detailIds.Count}条, fishItemIds=[{string.Join(",", detailIds)}]");

        string jsonToSend = NetUtils.SerializeToJson(requestData);
        Logger.Log($"[NetServerManager] 发送JSON: {jsonToSend}");

        StartCoroutine(SendRequest<SellFishResponse>(ServerUrls.Player.SellFish(_currentPlayerId), requestData,
            response =>
            {
                Logger.Log("[NetServerManager] 售卖鱼成功");

                // ✅ 从服务器响应中提取金币和价格数据
                if (response != null)
                {
                    if (response.gold > 0)
                    {
                        playerGold = response.gold;
                        Logger.Log($"[NetServerManager] 售卖后金币: {playerGold}");
                        // ✅ 立即更新 UI，不等待 FetchPlayerGold
                        GameUIManager.Instance?.UpdateGoldDisplay(playerGold);
                    }
                    if (response.totalPrice > 0)
                    {
                        Logger.Log($"[NetServerManager] 售卖获得金币: {response.totalPrice}");

                        // ✅ 显示卖鱼成功Tip
                        string tipMessage = $"💰 出售成功！\n共出售 {response.soldCount} 条鱼\n获得 {response.totalPrice} 金币";
                        GameUIManager.Instance?.ShowTip(tipMessage);
                    }
                }

                StartCoroutine(FetchPlayerDataAfterSell(detailIds));
            },
            error =>
            {
                Logger.LogWarning("[NetServerManager] 售卖鱼失败: " + error);
                GameUIManager.Instance?.ShowTip("售卖失败，请重试");
            }));
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

    [Serializable] private class SellFishResponse { public bool success; public string message; public int gold; public int fishCount; public int remaining; public int capacity; public int soldCount; public int totalPrice; }
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
        public bool isFirstCatch;     // ✅ 客户端本地设置：是否为首次钓获该鱼
    }
}
