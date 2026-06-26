// ============================================
// 文件: NetServerManager.Init.cs
// 功能: 网络数据初始化管理（含进度）
// ============================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Logger = Utils.Logger;
using SharedModels;

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
            Logger.Log("[NetServerManager] 已经初始化完成，跳过");
            OnInitializationComplete?.Invoke();
            return;
        }

        if (_isInitializing)
        {
            Logger.Log("[NetServerManager] 正在初始化中，跳过重复调用");
            return;
        }

        if (!_isInitCalled)
        {
            Logger.LogWarning("[NetServerManager] Init() 尚未调用，自动调用 Init()");
            Init();
        }

        if (_isEnabled == false)
        {
            _isEnabled = true;
        }

        if (!isConnected)
        {
            Logger.Log("[NetServerManager] 等待服务器连接...");
            StartConnect();
        }

        Logger.LogColor("[NetServerManager] 开始网络数据初始化...", "cyan");
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
        fishDetailData.Clear();
        unlockedCharacters.Clear();
        unlockedEquipment.Clear();
        mallItems.Clear();

        Logger.Log("[NetServerManager] 初始化状态已重置");
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
            Logger.Log("[NetServerManager] 等待服务器连接...");
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

            Logger.Log($"[NetServerManager] 执行初始化步骤 [{i + 1}/{_initSteps.Count}]: {step.Name}");

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

        Logger.LogColor("[NetServerManager] 网络数据初始化完成！", "green");
        OnProgressUpdated?.Invoke(1f, "完成");

        OnInitializationComplete?.Invoke();

        yield return null;

        NotifyPlayerDataSyncedInternal();
        SyncMallItemsFromServer();

        // 安全切换角色动画
        //if (equippedCharacterId > 0)
        //{
        //    if (PlayerAniManager.Instance != null)
        //    {
        //        PlayerAniManager.Instance.SwitchCharacter(equippedCharacterId);
        //    }
        //    else
        //    {
        //        Logger.LogWarning("[NetServerManager] PlayerAniManager 尚未初始化，跳过角色切换");
        //    }
        //}

        // ⭐ 启动钓鱼状态轮询
        Logger.Log("[NetServerManager] 启动钓鱼状态轮询...");
        StartCoroutine(PollFishingStatus());

        // ⭐ 启动自动钓鱼
        if (isFishBagFull)
        {
            NotifyPlayLazyAnimation();
            Logger.Log("[NetServerManager] 鱼篓已满，播放Lazy动画");
        }
        else
        {
            AutoStartFishing();
            Logger.Log("[NetServerManager] 自动钓鱼已启动");
        }
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
                Logger.Log($"[NetServerManager] 正在连接服务器... (等待中)");
                yield return new WaitForSeconds(waitTime);
                continue;
            }

            retryCount++;
            Logger.Log($"[NetServerManager] 连接失败，第 {retryCount}/{maxRetries} 次重试...");
            yield return StartCoroutine(ConnectToServer());
            yield return new WaitForSeconds(waitTime);
        }

        if (!isConnected)
        {
            Logger.LogError($"[NetServerManager] 连接服务器失败，已重试 {retryCount} 次");
        }
        else
        {
            Logger.Log("[NetServerManager] 服务器连接成功");
        }
    }

    // ========== 构建初始化步骤 ==========

    private void BuildInitSteps()
    {
        _initSteps.Clear();

        _initSteps.Add(new InitStep("加载背包数据", FetchPlayerInventoryCoroutine, 1.5f));
        _initSteps.Add(new InitStep("加载装备数据", FetchPlayerEquipmentCoroutine, 1.5f));
        _initSteps.Add(new InitStep("加载鱼篓数据", FetchPlayerFishInventoryCoroutine, 1.5f));
        _initSteps.Add(new InitStep("加载人物数据", FetchPlayerCharacterDataCoroutine, 1.5f));
        _initSteps.Add(new InitStep("加载金币数据", FetchPlayerGoldCoroutine, 1.0f));
        _initSteps.Add(new InitStep("加载人物列表", FetchUnlockedCharactersCoroutine, 1.0f));
        _initSteps.Add(new InitStep("加载鱼篓容量", FetchFishBagCapacityCoroutine, 1.0f));
        _initSteps.Add(new InitStep("加载连续模式状态", FetchContinuousModeStatusCoroutine, 0.5f));
        _initSteps.Add(new InitStep("加载窝料数量", FetchBaitCountCoroutine, 0.5f));
        _initSteps.Add(new InitStep("确保基础人物", EnsureBasicCharacterCoroutine, 1.0f));
    }

    // ========== 各个步骤的 Coroutine ==========

    private IEnumerator FetchPlayerGoldCoroutine()
    {
        yield return FetchGetJson<GoldResponse>("/api/player/gold/" + _currentPlayerId, data =>
        {
            if (data != null)
            {
                playerGold = data.gold;
                Logger.Log("[NetServerManager] 初始化 - 金币: " + playerGold);
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
            Logger.Log($"[NetServerManager] 初始化 - 已解锁人物: {unlockedCharacters.Count} 个");
        }, "人物列表");
    }

    private IEnumerator FetchFishBagCapacityCoroutine()
    {
        yield return FetchGetJson<CapacityResponse>("/api/inventory/fish/" + _currentPlayerId + "/capacity", data =>
        {
            if (data != null)
            {
                fishBagCapacity = data.capacity;
                Logger.Log("[NetServerManager] 初始化 - 鱼篓容量: " + fishBagCapacity);
            }
            else
            {
                fishBagCapacity = 20;
                Logger.LogWarning("[NetServerManager] 初始化 - 使用默认鱼篓容量: 20");
            }
        }, "鱼篓容量");
    }

    private IEnumerator FetchContinuousModeStatusCoroutine()
    {
        yield return FetchGetJson<ContinuousModeStatus>("/api/game/continuous-mode/status", data =>
        {
            if (data != null)
            {
                baitEndTime = data.baitEndTime;
                UpdateContinuousModeRemainingTime();
                Logger.Log($"[NetServerManager] 初始化 - 连续模式状态: isIn={isInContinuousMode}, time={continuousModeRemainingTime}");
            }
        }, "连续模式状态");
    }

    private IEnumerator FetchBaitCountCoroutine()
    {
        yield return FetchGetJson<BaitCountResponse>("/api/game/bait/count", data =>
        {
            if (data != null)
            {
                currentSceneBaitCount = data.baitCount;
                Logger.Log("[NetServerManager] 初始化 - 窝料数量: " + currentSceneBaitCount);
            }
        }, "窝料数量");
    }

    private IEnumerator EnsureBasicCharacterCoroutine()
    {
        if (!playerInventory.ContainsKey(3401) || playerInventory[3401] <= 0)
        {
            Logger.LogWarning("[NetServerManager] 初始化 - 玩家未拥有基础人物3401，正在添加...");
            yield return StartCoroutine(AddCharacterToInventory(3401));
        }

        yield return StartCoroutine(AddCharacterToPlayerCharacter(3401));

        if (equippedCharacterId < 3401 || equippedCharacterId > 3500)
        {
            Logger.LogWarning($"[NetServerManager] 初始化 - 当前装备的人物ID({equippedCharacterId})无效，装备基础人物3401");
            yield return StartCoroutine(SendEquipRequest((int)EquipmentSlotType.Character, 3401));
            equippedCharacterId = 3401;
        }

        Logger.Log("[NetServerManager] 初始化 - 基础人物检查完成");
    }
}