// ============================================
// 文件: NetServerManager.Init.cs
// 功能: 网络数据初始化管理（含进度）
// ============================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
//using Z_Logger = Utils.Z_Logger;
//using SharedModels;

public partial class NetServerManager
{
    // ========== 初始化状态 ==========

    private float _initProgress = 0f;
    public float InitProgress => _initProgress;

    private bool _isInitialized = false;
    public bool IsInitialized => _isInitialized;

    private bool _initFailed = false;
    public bool InitFailed => _initFailed;

    private string _initErrorMessage = "";
    public string InitErrorMessage => _initErrorMessage;

    private List<InitStep> _initSteps = new List<InitStep>();
    private int _currentStepIndex = 0;
    public string CurrentStepName => _currentStepIndex < _initSteps.Count ? _initSteps[_currentStepIndex].Name : "完成";

    public event Action OnInitializationComplete;
    public event Action<string> OnInitializationFailed;
    public event Action<float, string> OnProgressUpdated;

    // ========== 初始化步骤定义 ==========

    [Serializable]
    private class InitStep
    {
        public string Name;
        public Func<IEnumerator> Coroutine;
        public float Weight;

        public InitStep(string name, Func<IEnumerator> coroutine, float weight = 1f)
        {
            Name = name;
            Coroutine = coroutine;
            Weight = weight;
        }
    }

    // ========== 公开初始化方法 ==========

    public void StartInitialization()
    {
        if (_isInitialized)
        {
            Z_Logger.Log("[NetServerManager] 已经初始化完成，跳过");
            OnInitializationComplete?.Invoke();
            return;
        }

        if (_isInitializing)
        {
            Z_Logger.Log("[NetServerManager] 正在初始化中，跳过重复调用");
            return;
        }

        if (!_isInitCalled)
        {
            Z_Logger.LogWarning("[NetServerManager] Init() 尚未调用，自动调用 Init()");
            Init();
        }

        if (_isEnabled == false)
        {
            _isEnabled = true;
        }

        if (!isConnected)
        {
            Z_Logger.Log("[NetServerManager] 等待服务器连接...");
            StartConnect();
        }

        Z_Logger.LogColor("[NetServerManager] 开始网络数据初始化...", "cyan");
        StartCoroutine(InitializeCoroutine());
    }

    private bool _isInitializing = false;

    public void ResetInitialization()
    {
        _initProgress = 0f;
        _isInitialized = false;
        _initFailed = false;
        _initErrorMessage = "";
        _currentStepIndex = 0;
        _initSteps.Clear();
        _isInitializing = false;

        playerInventory.Clear();
        fishInventory.Clear();
        fishBagDetailData.Clear();
        unlockedCharacters.Clear();
        unlockedEquipment.Clear();
        mallItems.Clear();

        Z_Logger.Log("[NetServerManager] 初始化状态已重置");
    }

    // ========== 初始化协程 ==========

    private IEnumerator InitializeCoroutine()
    {
        if (_isInitializing)
        {
            yield break;
        }
        _isInitializing = true;

        if (!isConnected)
        {
            Z_Logger.Log("[NetServerManager] 等待服务器连接...");
            yield return StartCoroutine(WaitForConnection());

            if (!isConnected)
            {
                _initFailed = true;
                _initErrorMessage = "无法连接到服务器";
                _isInitializing = false;
                OnInitializationFailed?.Invoke(_initErrorMessage);
                yield break;
            }
        }

        BuildInitSteps();

        float totalWeight = 0f;
        foreach (var step in _initSteps)
        {
            totalWeight += step.Weight;
        }

        float completedWeight = 0f;
        _currentStepIndex = 0;

        for (int i = 0; i < _initSteps.Count; i++)
        {
            _currentStepIndex = i;
            var step = _initSteps[i];

            Z_Logger.Log($"[NetServerManager] 执行初始化步骤 [{i + 1}/{_initSteps.Count}]: {step.Name}");

            float stepProgress = completedWeight / totalWeight;
            OnProgressUpdated?.Invoke(stepProgress, step.Name);

            yield return StartCoroutine(step.Coroutine());

            if (_initFailed)
            {
                _isInitializing = false;
                OnInitializationFailed?.Invoke(_initErrorMessage);
                yield break;
            }

            completedWeight += step.Weight;
            stepProgress = completedWeight / totalWeight;
            OnProgressUpdated?.Invoke(Mathf.Min(stepProgress, 0.99f), step.Name);
        }

        _initProgress = 1f;
        _isInitialized = true;
        _initFailed = false;
        _isInitializing = false;

        Z_Logger.LogColor("[NetServerManager] 网络数据初始化完成！", "green");
        OnProgressUpdated?.Invoke(1f, "完成");

        OnInitializationComplete?.Invoke();

        yield return null;

        NotifyPlayerDataSyncedInternal();
        SyncMallItemsFromServer();

        // ✅ 修复：在所有数据加载完成后，重新计算鱼篓状态
        int totalFishCount = GetTotalFishCount();
        isFishBagFull = totalFishCount >= fishBagCapacity;
        Z_Logger.Log($"[NetServerManager] 初始化完成，鱼篓状态: {totalFishCount}/{fishBagCapacity}, isFull={isFishBagFull}");

        // ⭐ 启动钓鱼状态轮询
        Z_Logger.Log("[NetServerManager] 启动钓鱼状态轮询...");
        StartCoroutine(PollFishingStatus());

        // ⭐ 根据鱼篓状态启动自动钓鱼或播放Lazy动画
        if (isFishBagFull)
        {
            NotifyPlayLazyAnimation();
            Z_Logger.Log("[NetServerManager] 鱼篓已满，播放Lazy动画");
        }
        else
        {
            AutoStartFishing();
            Z_Logger.Log("[NetServerManager] 自动钓鱼已启动");
        }

        // ✅ 背包刷新事件(EVENT_REFRESH_BAG)已移至 ManagerManager.OnAllLoadingComplete 中发送
        // 确保在 EVENT_ALL_LOADING_COMPLETE（SkinManager 同步皮肤数据）之后才刷新背包 UI
        Z_Logger.Log("[NetServerManager] 初始化完成，等待 ManagerManager 触发背包刷新");
    }

    // ========== 等待连接完成的协程 ==========
    private IEnumerator WaitForConnection()
    {
        int maxRetries = 5;
        int retryCount = 0;
        float waitTime = 0.5f;

        while (!isConnected && retryCount < maxRetries)
        {
            if (networkState == NetUtils.NetworkState.Connected)
            {
                isConnected = true;
                break;
            }

            if (networkState == NetUtils.NetworkState.Connecting)
            {
                Z_Logger.Log($"[NetServerManager] 正在连接服务器... (等待中)");
                yield return new WaitForSeconds(waitTime);
                continue;
            }

            retryCount++;
            Z_Logger.Log($"[NetServerManager] 连接失败，第 {retryCount}/{maxRetries} 次重试...");
            yield return StartCoroutine(ConnectToServer());
            yield return new WaitForSeconds(waitTime);
        }

        if (!isConnected)
        {
            Z_Logger.LogError($"[NetServerManager] 连接服务器失败，已重试 {retryCount} 次");
        }
        else
        {
            Z_Logger.Log("[NetServerManager] 服务器连接成功");
        }
    }

    // ========== 构建初始化步骤 ==========

    private void BuildInitSteps()
    {
        _initSteps.Clear();

        _initSteps.Add(new InitStep("加载背包数据", FetchPlayerInventoryCoroutine, 1.5f));
        _initSteps.Add(new InitStep("加载装备数据", FetchPlayerEquipmentCoroutine, 1.5f));
        _initSteps.Add(new InitStep("加载皮肤数据", FetchPlayerSkinsCoroutine, 1.0f));
        _initSteps.Add(new InitStep("加载鱼篓数据", FetchPlayerFishInventoryCoroutine, 1.5f));
        _initSteps.Add(new InitStep("加载人物数据", FetchPlayerCharacterDataCoroutine, 1.5f));
        _initSteps.Add(new InitStep("加载金币数据", FetchPlayerGoldCoroutine, 1.0f));
        _initSteps.Add(new InitStep("加载人物列表", FetchUnlockedCharactersCoroutine, 1.0f));
        _initSteps.Add(new InitStep("加载鱼篓容量", FetchFishBagCapacityCoroutine, 1.0f));
        _initSteps.Add(new InitStep("加载场景数据", FetchPlayerSceneDataCoroutine, 1.0f));
        _initSteps.Add(new InitStep("加载连续模式状态", FetchContinuousModeStatusCoroutine, 0.5f));
        _initSteps.Add(new InitStep("加载窝料数量", FetchBaitCountCoroutine, 0.5f));
        _initSteps.Add(new InitStep("加载鱼缸数据", FetchFishTankDataCoroutine, 1.0f));
    }

    // ========== 各个步骤的 Coroutine ==========

    private IEnumerator FetchPlayerSkinsCoroutine()
    {
        yield return RequestPlayerSkinsCoroutine();
        Z_Logger.Log("[NetServerManager] 初始化 - 皮肤数据加载完成");
    }

    private IEnumerator FetchPlayerGoldCoroutine()
    {
        yield return FetchGetJson<GoldResponse>(ServerUrls.Player.GoldById(_currentPlayerId), data =>
        {
            if (data != null)
            {
                playerGold = data.gold;
                Z_Logger.Log("[NetServerManager] 初始化 - 金币: " + playerGold);
            }
            else
            {
                _initFailed = true;
                _initErrorMessage = "加载金币数据失败";
            }
        }, "金币数据");
    }

    private IEnumerator FetchUnlockedCharactersCoroutine()
    {
        yield return FetchGetJson(ServerUrls.Player.Characters(_currentPlayerId), json =>
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
                Z_Logger.LogError($"[NetServerManager] 解析人物列表失败: {ex.Message}");
            }
            unlockedCharacters.Add(3401);
            Z_Logger.Log($"[NetServerManager] 初始化 - 已解锁人物: {unlockedCharacters.Count} 个");
        }, "人物列表");
    }

    private IEnumerator FetchFishBagCapacityCoroutine()
    {
        yield return FetchGetJson<CapacityResponse>(ServerUrls.Inventory.FishCapacityById(_currentPlayerId), data =>
        {
            if (data != null)
            {
                fishBagCapacity = data.capacity;
                Z_Logger.Log("[NetServerManager] 初始化 - 鱼篓容量: " + fishBagCapacity);
            }
            else
            {
                fishBagCapacity = 20;
                Z_Logger.LogWarning("[NetServerManager] 初始化 - 使用默认鱼篓容量: 20");
            }
        }, "鱼篓容量");
    }

    private IEnumerator FetchContinuousModeStatusCoroutine()
    {
        yield return FetchGetJson<ContinuousModeStatus>(ServerUrls.Game.ContinuousModeStatus, data =>
        {
            if (data != null)
            {
                baitEndTime = data.baitEndTime;
                UpdateContinuousModeRemainingTime();
                Z_Logger.Log($"[NetServerManager] 初始化 - 连续模式状态: isIn={isInContinuousMode}, time={continuousModeRemainingTime}");
            }
        }, "连续模式状态");
    }

    private IEnumerator FetchBaitCountCoroutine()
    {
        yield return FetchGetJson<BaitCountResponse>(ServerUrls.Game.BaitCount, data =>
        {
            if (data != null)
            {
                currentSceneBaitCount = data.baitCount;
                Z_Logger.Log("[NetServerManager] 初始化 - 窝料数量: " + currentSceneBaitCount);
            }
        }, "窝料数量");
    }

    /// <summary>
    /// 获取所有鱼缸数据（登录时调用）
    /// </summary>
    /// <summary>
    /// 获取所有鱼缸数据（登录时调用）
    /// </summary>
    private IEnumerator FetchFishTankDataCoroutine()
    {
        bool success = false;
        yield return StartCoroutine(FetchAllFishTanksCoroutine(result => success = result));

        if (success)
        {
            Z_Logger.Log("[NetServerManager] 初始化 - 鱼缸数据加载完成");
        }
        else
        {
            Z_Logger.LogWarning("[NetServerManager] 初始化 - 鱼缸数据加载失败，使用默认值");

            // ✅ 使用 PlayerDataManager 作为唯一数据源
            if (PlayerDataManager.Instance != null)
            {
                var defaultTanks = new List<FishTankStatusResponse>
            {
                new FishTankStatusResponse
                {
                    success = true,
                    tankId = 1,
                    Name = "特殊鱼缸",
                    Type = "special",
                    PurchaseCost = 0,
                    isUnlocked = false,
                    level = 1,
                    capacity = 10,
                    currentCount = 0,
                    remainingSpace = 10,
                    items = new List<FishDetailData>()
                }
            };

                PlayerDataManager.Instance.UpdateFishTankFromResponse(defaultTanks);
                Z_Logger.Log("[NetServerManager] 初始化 - 使用默认鱼缸数据");
            }
            else
            {
                Z_Logger.LogWarning("[NetServerManager] 初始化 - PlayerDataManager 不可用，无法设置默认鱼缸数据");
            }
        }
    }
}
