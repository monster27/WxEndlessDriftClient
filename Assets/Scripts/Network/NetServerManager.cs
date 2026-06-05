using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class NetServerManager : SingletonMono<NetServerManager>
{
    private string serverUrl = "http://localhost:5000";

    private float heartbeatTimer = 0f;
    private int missedHeartbeats = 0;
    private bool isConnected = false;
    private long lastServerTime = 0;
    private NetUtils.NetworkState networkState = NetUtils.NetworkState.Disconnected;

    private bool _isEnabled = true;
    public bool IsEnabled => _isEnabled;

    public NetUtils.NetworkState NetworkState => networkState;
    public bool IsConnected => isConnected;
    public int MissedHeartbeats => missedHeartbeats;
    public long LastServerTime => lastServerTime;

    private Coroutine connectCoroutine;

    // 动画播放标志位
    private bool isPlayingReelAnimation = false;

    // 记录上次钓获信息，用于检测新钓获
    private int lastCatchFishId = -1;
    private float lastCatchStruggleTime = 1.5f;

    // 保存当前钓获信息，用于在拉杆动画结束后显示MainTile
    private LastCatchInfo pendingCatchInfo = null;

    // 玩家连接状态管理
    private bool isApplicationPaused = false;
    private bool hasSentExitOnPause = false;
    private Coroutine heartbeatCoroutine;
    private const float HEARTBEAT_INTERVAL = 10f;
    private const float HEARTBEAT_TIMEOUT = 30f;

    // 添加公开属性
    public bool IsPaused => isPaused;
    public bool IsPlayingReelAnimation => isPlayingReelAnimation;

    private float struggleStartTime = 0f;      // 挣扎开始时间
    private float currentStruggleTime = 0f;    // 当前挣扎总时长
    public void Init()
    {
        RegisterNetworkEvents();
        RegisterServerEvents();

        Debug.Log("<color=green>[NetServerManager] 网络服务器管理器初始化完成，服务器地址: " + serverUrl + "</color>");

        StartConnect();
    }

    #region 玩家连接状态管理

    /// <summary>
    /// 发送玩家退出请求（正常退出时调用）
    /// </summary>
    public void SendPlayerExit()
    {
        if (!isConnected)
        {
            Debug.Log("[NetServerManager] 未连接服务器，跳过退出请求");
            return;
        }

        Debug.Log("<color=orange>[NetServerManager] 发送玩家退出请求</color>");

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>("/api/player/" + _currentPlayerId + "/exit", requestData,
            onSuccess: (response) =>
            {
                Debug.Log("<color=green>[NetServerManager] 玩家退出请求成功</color>");
                StopHeartbeat();
            },
            onError: (error) =>
            {
                Debug.LogWarning("[NetServerManager] 玩家退出请求失败: " + error);
            },
            forcePost: true
        ));
    }

    /// <summary>
    /// 请求重连恢复状态
    /// </summary>
    public void RequestReconnect()
    {
        Debug.Log("<color=orange>[NetServerManager] 请求重连恢复状态</color>");

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>("/api/player/" + _currentPlayerId + "/reconnect", requestData,
            onSuccess: (response) =>
            {
                Debug.Log("<color=green>[NetServerManager] 重连请求成功，开始恢复钓鱼状态</color>");

                // 重新启动钓鱼状态轮询
                StartCoroutine(FetchGameState());
                StartCoroutine(FetchBaitCount());
                StartCoroutine(FetchPlayerData());
                StartCoroutine(PollFishingStatus());

                // 重新启动心跳
                StartHeartbeat();
            },
            onError: (error) =>
            {
                Debug.LogWarning("[NetServerManager] 重连请求失败: " + error + "，尝试重新连接");
                // 如果重连失败，尝试重新连接服务器
                Reconnect();
            },
            forcePost: true
        ));
    }

    /// <summary>
    /// 开始发送心跳
    /// </summary>
    private void StartHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
        }
        heartbeatCoroutine = StartCoroutine(SendHeartbeatCoroutine());
        Debug.Log("[NetServerManager] 心跳协程已启动");
    }

    /// <summary>
    /// 停止心跳
    /// </summary>
    private void StopHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
            Debug.Log("[NetServerManager] 心跳协程已停止");
        }
    }

    /// <summary>
    /// 发送心跳协程
    /// </summary>
    private IEnumerator SendHeartbeatCoroutine()
    {
        while (isConnected && this != null)
        {
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);

            if (!isConnected || this == null)
                yield break;

            SendHeartbeat();
        }
    }

    /// <summary>
    /// 发送单个心跳请求
    /// </summary>
    private void SendHeartbeat()
    {
        if (!isConnected)
            return;

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "clientTime", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>("/api/player/" + _currentPlayerId + "/heartbeat", requestData,
            onSuccess: (response) =>
            {
                Debug.Log("<color=cyan>[NetServerManager] 心跳发送成功</color>");
            },
            onError: (error) =>
            {
                Debug.LogWarning("[NetServerManager] 心跳发送失败: " + error);
            },
            forcePost: true
        ));
    }

    #endregion

    #region Unity生命周期回调

    /// <summary>
    /// Unity应用暂停/恢复回调（切后台）
    /// </summary>
    void OnApplicationPause(bool pause)
    {
        Debug.Log($"[NetServerManager] OnApplicationPause: {pause}");

        if (pause)
        {
            // 应用进入后台，发送退出请求
            isApplicationPaused = true;
            if (isConnected && !hasSentExitOnPause)
            {
                SendPlayerExit();
                hasSentExitOnPause = true;
            }
            StopHeartbeat();
        }
        else
        {
            // 应用从后台恢复
            isApplicationPaused = false;
            if (!isConnected)
            {
                Debug.Log("[NetServerManager] 应用恢复但未连接到服务器，尝试重连");
                RequestReconnect();
            }
            else
            {
                // 已连接，重新启动心跳
                StartHeartbeat();
            }
            hasSentExitOnPause = false;
        }
    }

    /// <summary>
    /// Unity应用退出回调
    /// </summary>
    void OnApplicationQuit()
    {
        Debug.Log("<color=red>[NetServerManager] OnApplicationQuit - 应用退出</color>");

        // 发送退出请求
        SendPlayerExit();

        // 确保断开连接
        StopHeartbeat();
        isConnected = false;
    }

    /// <summary>
    /// 当对象被销毁时
    /// </summary>
    /// <summary>
    /// 当对象被销毁时
    /// </summary>
    private void OnDestroy()
    {
        Debug.Log("<color=red>[NetServerManager] OnDestroy - 对象销毁</color>");

        // 发送退出请求
        if (isConnected)
        {
            SendPlayerExit();
        }

        // 停止心跳协程
        StopHeartbeat();

        // 停止连接协程
        if (connectCoroutine != null)
        {
            StopCoroutine(connectCoroutine);
            connectCoroutine = null;
        }

        // 停止所有协程
        StopAllCoroutines();

        // 断开连接
        Disconnect();
    }

    #endregion

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;

        if (enabled && networkState == NetUtils.NetworkState.Disconnected)
        {
            StartConnect();
        }
        else if (!enabled)
        {
            Disconnect();
        }

        Debug.Log("<color=orange>[NetServerManager] 设置启用状态: " + enabled + "</color>");
    }

    private void RegisterNetworkEvents()
    {
    }

    private void RegisterServerEvents()
    {
        Debug.Log("[NetServerManager] 注册网络模式下的事件处理器");

        // 注册连续模式相关的请求处理器
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_IN_CONTINUOUS_MODE, _ => IsInContinuousMode());
        CommunicateEvent.RegisterRequest<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, _ => GetContinuousModeRemainingTime());
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, _ => GetCurrentSceneBaitCount());

        // 注册玩家数据相关的请求处理器
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, _ => GetPlayerInventory());
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_FISH_INVENTORY, _ => GetPlayerFishInventory());
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_FISH_BAG_CAPACITY, _ => GetFishBagCapacity());
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_GOLD, _ => GetPlayerGold());

        // 注册金币同步请求处理器（在线模式）
        CommunicateEvent.Register(CommunicateEvent.EVENT_SYNC_GOLD, OnSyncGold);

        // 注册装备相关的请求处理器
        CommunicateEvent.RegisterRequest<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType => GetEquippedItem(slotType));
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, _ => GetCharacterLevel());
        CommunicateEvent.RegisterRequest<int, PlayerData>(CommunicateEvent.EVENT_GET_PLAYER_DATA, _ => GetPlayerData());

        // 注册 CharacterServerManager 相关的请求处理器
        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterServerManager_GetPlayerData", _ => GetPlayerCharacterData());
        CommunicateEvent.RegisterRequest<int, int>("CharacterServerManager_GetExpToNextLevel", _ => GetExpToNextLevel());

        // 注册自动钓鱼状态相关的请求处理器
        CommunicateEvent.RegisterRequest<int, bool>("IsAutoFishing", _ => isAutoFishing);
        CommunicateEvent.RegisterRequest<int, bool>("IsPaused", _ => isPaused);
        CommunicateEvent.RegisterRequest<int, float>("GetTimeUntilNextFishing", _ => timeUntilNextFishing);
        CommunicateEvent.RegisterRequest<int, int>("GetTrashStreak", _ => trashStreak);
        CommunicateEvent.RegisterRequest<int, bool>("IsFishBagFull", _ => isFishBagFull);
        CommunicateEvent.RegisterRequest<int, string>("GetCurrentFishingMode", _ => currentFishingMode);

        // 注册售卖鱼事件处理器
        CommunicateEvent.Register<(List<int>, int)>(CommunicateEvent.EVENT_SELL_FISH_ITEMS, OnSellFishItems);
    }

    private bool isInContinuousMode = false;
    private float continuousModeRemainingTime = 0f;
    private int currentSceneBaitCount = 0;

    // 自动钓鱼状态
    private bool isAutoFishing = false;
    private bool isPaused = false;
    private float timeUntilNextFishing = 0f;
    private int trashStreak = 0;
    private bool isFishBagFull = false;
    private string currentFishingMode = "Normal";

    // 玩家数据
    private Dictionary<int, int> playerInventory = new Dictionary<int, int>();
    private Dictionary<int, int> fishInventory = new Dictionary<int, int>();
    private int fishBagCapacity = 20;
    private int playerGold = 0;

    // 当前玩家ID
    private int _currentPlayerId = 1;

    public void SetCurrentPlayerId(int playerId)
    {
        _currentPlayerId = playerId;
        Debug.Log($"[NetServerManager] 当前玩家ID已更新为: {_currentPlayerId}");
    }

    public int GetCurrentPlayerId()
    {
        return _currentPlayerId;
    }

    // 玩家装备数据
    private int equippedRodId = 1;
    private int equippedLineId = 1;
    private int equippedHookId = 1;
    private int equippedSkill1Id = 0;
    private int equippedSkill2Id = 0;
    private int equippedCharacterId = 3401;
    private int characterLevel = 1;

    private bool IsInContinuousMode()
    {
        return isInContinuousMode;
    }

    private float GetContinuousModeRemainingTime()
    {
        return continuousModeRemainingTime;
    }

    private int GetCurrentSceneBaitCount()
    {
        return currentSceneBaitCount;
    }

    private Dictionary<int, int> GetPlayerInventory()
    {
        return playerInventory;
    }

    private Dictionary<int, int> GetPlayerFishInventory()
    {
        return fishInventory;
    }

    private int GetFishBagCapacity()
    {
        return fishBagCapacity;
    }

    public int GetPlayerGold()
    {
        return playerGold;
    }

    private void OnSyncGold()
    {
        if (!_isEnabled)
            return;

        Debug.Log("[NetServerManager] 收到金币同步请求");

        int currentGold = playerGold;
        Debug.Log($"[NetServerManager] 当前金币: {currentGold}");

        var goldData = new Dictionary<string, object>
        {
            { "gold", currentGold },
            { "add", 0 },
            { "reduce", 0 }
        };

        CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
    }

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
            default:
                return 0;
        }
    }

    private int GetCharacterLevel()
    {
        return characterLevel;
    }

    private PlayerData GetPlayerData()
    {
        return new PlayerData
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
            currentExp = 0
        };
    }

    private int GetExpToNextLevel()
    {
        return 100;
    }

    private IEnumerator FetchGameState()
    {
        if (!isConnected)
            yield break;

        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/game/continuous-mode/status"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<ContinuousModeStatus>(json);
                    if (data != null)
                    {
                        isInContinuousMode = data.isInContinuousMode;
                        continuousModeRemainingTime = data.remainingTime;
                        Debug.Log("[NetServerManager] 更新连续模式状态: " + isInContinuousMode + ", 剩余时间: " + continuousModeRemainingTime);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析游戏状态失败: " + ex.Message);
                }
            }
        }
    }

    private IEnumerator FetchBaitCount()
    {
        if (!isConnected)
            yield break;

        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/game/bait/count"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<BaitCountResponse>(json);
                    if (data != null)
                    {
                        currentSceneBaitCount = data.baitCount;
                        Debug.Log("[NetServerManager] 更新鱼饵数量: " + currentSceneBaitCount);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析鱼饵数量失败: " + ex.Message);
                }
            }
        }
    }

    private IEnumerator FetchPlayerData()
    {
        if (!isConnected)
            yield break;

        Debug.Log($"[DEBUG] FetchPlayerData called with currentPlayerId={_currentPlayerId}");

        // 获取玩家金币
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/gold/" + _currentPlayerId))
        {
            Debug.Log($"[DEBUG] Requesting gold for playerId={_currentPlayerId}, URL={serverUrl}/api/player/gold/{_currentPlayerId}");
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Debug.Log($"[DEBUG] Gold response: {json}");
                    var data = JsonUtility.FromJson<GoldResponse>(json);
                    if (data != null)
                    {
                        playerGold = data.gold;
                        Debug.Log("[NetServerManager] 更新玩家金币: " + playerGold);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析金币数据失败: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NetServerManager] 获取金币数据失败: " + request.error);
            }
        }

        // 获取玩家背包
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/inventory/" + _currentPlayerId))
        {
            Debug.Log($"[DEBUG] Requesting inventory for playerId={_currentPlayerId}, URL={serverUrl}/api/player/inventory/{_currentPlayerId}");
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Debug.Log($"[DEBUG] Inventory response: {json}");
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        playerInventory.Clear();
                        foreach (var item in data.items)
                        {
                            playerInventory[item.key] = item.value;
                        }
                        Debug.Log("[NetServerManager] 更新玩家背包: " + playerInventory.Count + " 件物品");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析背包数据失败: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NetServerManager] 获取背包数据失败: " + request.error);
            }
        }

        // 获取玩家鱼篓
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/fish-bag/" + _currentPlayerId))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Debug.Log("[NetServerManager] 鱼篓数据响应: " + json);
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        fishInventory.Clear();
                        foreach (var item in data.items)
                        {
                            fishInventory[item.key] = item.value;
                        }
                        int totalFish = GetTotalFishCount();
                        Debug.Log("[NetServerManager] 更新玩家鱼篓: " + fishInventory.Count + " 种鱼，总数量: " + totalFish);

                        // 更新鱼篓满状态
                        isFishBagFull = totalFish >= fishBagCapacity;
                    }
                    else
                    {
                        Debug.LogWarning("[NetServerManager] 鱼篓数据为空或格式不正确");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析鱼篓数据失败: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NetServerManager] 获取鱼篓数据失败: " + request.error);
            }
        }

        // 获取鱼篓容量
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/inventory/fish/" + _currentPlayerId + "/capacity"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Debug.Log("[NetServerManager] 鱼篓容量响应: " + json);
                    var data = JsonUtility.FromJson<CapacityResponse>(json);
                    if (data != null)
                    {
                        fishBagCapacity = data.capacity;
                        Debug.Log("[NetServerManager] 更新鱼篓容量: " + fishBagCapacity);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析鱼篓容量失败: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NetServerManager] 获取鱼篓容量失败: " + request.error);
            }
        }

        // ========== 关键修复：同步数据到 PlayerDataManager ==========
        // 所有数据获取完成后，通知 PlayerDataManager 同步
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.SyncInventoryFromServer();
            PlayerDataManager.Instance.SyncGoldFromServer();
            Debug.Log("[NetServerManager] 已通知 PlayerDataManager 同步数据");
        }
        // ========== 修复结束 ==========

        // 根据鱼篓满状态决定是否启动自动钓鱼
        if (isFishBagFull)
        {
            Debug.Log("[NetServerManager] 鱼篓已满，不启动自动钓鱼，播放懒动画");
            NotifyPlayLazyAnimation();
        }
        else
        {
            AutoStartFishing();
        }
    }

    // 辅助方法：计算鱼篓总数量
    private int GetTotalFishCount()
    {
        int total = 0;
        foreach (var kvp in fishInventory)
        {
            total += kvp.Value;
        }
        return total;
    }

    private void AutoStartFishing()
    {
        try
        {
            if (isAutoFishing)
            {
                Debug.Log("[NetServerManager] 已在自动钓鱼状态，无需重复启动");
                return;
            }

            int defaultBaitId = 2501;
            Debug.Log($"[NetServerManager] 登录成功，自动开始钓鱼，使用鱼饵ID: {defaultBaitId}");
            StartAutoFishing(defaultBaitId);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[NetServerManager] 自动开始钓鱼失败: " + ex.Message);
        }
    }

    [System.Serializable]
    private class ContinuousModeStatus
    {
        public bool isInContinuousMode;
        public float remainingTime;
    }

    [System.Serializable]
    private class BaitCountResponse
    {
        public int baitCount;
    }

    [System.Serializable]
    private class GoldResponse
    {
        public int gold;
    }

    [System.Serializable]
    private class InventoryResponse
    {
        public List<KeyValuePair> items;
    }

    [System.Serializable]
    private class KeyValuePair
    {
        public int key;
        public int value;
    }

    [System.Serializable]
    private class CapacityResponse
    {
        public int capacity;
    }

    private void StartConnect()
    {
        if (connectCoroutine != null)
        {
            StopCoroutine(connectCoroutine);
        }
        connectCoroutine = StartCoroutine(ConnectToServer());
    }

    private IEnumerator ConnectToServer()
    {
        if (networkState == NetUtils.NetworkState.Connecting)
            yield break;

        networkState = NetUtils.NetworkState.Connecting;
        Debug.Log("<color=yellow>[NetServerManager] 正在连接到服务器: " + serverUrl + "</color>");

        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/ping"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("<color=green>[NetServerManager] 连接服务器成功</color>");
                networkState = NetUtils.NetworkState.Connected;
                isConnected = true;
                missedHeartbeats = 0;

                StartCoroutine(FetchGameState());
                StartCoroutine(FetchBaitCount());
                StartCoroutine(FetchPlayerData());

                StartCoroutine(PollFishingStatus());

                // 启动心跳协程
                StartHeartbeat();
            }
            else
            {
                Debug.LogError("<color=red>[NetServerManager] 连接服务器失败: " + request.error + "</color>");
                networkState = NetUtils.NetworkState.Disconnected;
                isConnected = false;
                UIManager.Instance?.ShowTip("无法连接到服务器，请检查网络连接");
            }
        }
    }

    private IEnumerator SendRequest<T>(string endpoint, object? data = null, System.Action<T>? onSuccess = null, System.Action<string>? onError = null, bool forcePost = false)
    {
        if (!_isEnabled)
        {
            yield break;
        }

        if (!isConnected || networkState != NetUtils.NetworkState.Connected)
        {
            ShowNetworkError();
            onError?.Invoke("未连接到服务器");
            yield break;
        }

        string url = serverUrl + endpoint;
        UnityWebRequest request;

        if (data != null || forcePost)
        {
            string jsonData = data != null ? NetUtils.SerializeToJson(data as Dictionary<string, object>) : "{}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
        }
        else
        {
            request = UnityWebRequest.Get(url);
        }

        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("<color=cyan>[NetServerManager] 请求成功: " + endpoint + "</color>");
            try
            {
                string jsonResponse = request.downloadHandler.text;
                T? response = NetUtils.ParseJson<T>(jsonResponse);
                onSuccess?.Invoke(response);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[NetServerManager] 解析响应失败: " + ex.Message);
                onError?.Invoke("解析响应失败");
            }
        }
        else
        {
            Debug.LogError("<color=red>[NetServerManager] 请求失败: " + endpoint + ", 错误: " + request.error + "</color>");
            UIManager.Instance?.ShowTip("网络请求失败，请检查网络连接");
            onError?.Invoke(request.error);
        }
    }

    private bool CheckNetworkConnection()
    {
        if (!_isEnabled)
        {
            Debug.LogWarning("[NetServerManager] 网络管理器未启用");
            return false;
        }

        if (!isConnected || networkState != NetUtils.NetworkState.Connected)
        {
            ShowNetworkError();
            return false;
        }

        return true;
    }

    private void ShowNetworkError()
    {
        Debug.LogError("[NetServerManager] 网络连接失败，请检查网络连接后重试");
        UIManager.Instance?.ShowTip("网络连接失败，请检查网络连接后重试");
    }

    private void Update()
    {
        if (!_isEnabled)
            return;

        if (SimulationServer.Instance != null && SimulationServer.Instance.IsRunning())
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= NetUtils.HEARTBEAT_INTERVAL)
            {
                heartbeatTimer = 0f;

                if (networkState == NetUtils.NetworkState.Connected)
                {
                    missedHeartbeats++;
                    Debug.Log("[NetServerManager] 等待心跳响应，未收到响应次数: " + missedHeartbeats);
                    StartCoroutine(SendHeartbeatRequest());
                }
                else if (networkState == NetUtils.NetworkState.Reconnecting)
                {
                    StartConnect();
                }
                else if (networkState == NetUtils.NetworkState.Disconnected)
                {
                    StartConnect();
                }
            }
        }

        CheckHeartbeatTimeout();
    }

    private void CheckHeartbeatTimeout()
    {
        if (!_isEnabled)
            return;

        if (missedHeartbeats >= NetUtils.MAX_MISSED_HEARTBEATS)
        {
            Debug.LogError("[NetServerManager] 心跳超时，断开连接");
            networkState = NetUtils.NetworkState.Reconnecting;
            isConnected = false;
            missedHeartbeats = 0;
            UIManager.Instance?.ShowTip("网络连接断开，正在尝试重新连接...");
        }
    }

    private IEnumerator SendHeartbeatRequest()
    {
        Debug.Log("[NetServerManager] SendHeartbeat 被调用");

        long clientTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var requestData = new Dictionary<string, object>
        {
            { "clientTime", clientTime }
        };

        yield return SendRequest<HeartbeatResponse>("/api/heartbeat", requestData,
            (response) =>
            {
                if (response != null)
                {
                    Debug.Log("[NetServerManager] OnHeartbeatResponse 收到心跳响应");
                    lastServerTime = response.serverTime;
                    isConnected = true;
                    missedHeartbeats = 0;
                    networkState = NetUtils.NetworkState.Connected;
                    NetUtils.LogResponse("HeartbeatResponse", new Dictionary<string, object>
                    {
                        { "serverTime", lastServerTime }
                    });
                }
            },
            (error) =>
            {
                Debug.LogWarning("[NetServerManager] 心跳请求失败: " + error);
                isConnected = false;
            });
    }

    public void OnAddItem((int itemId, int quantity) data)
    {
        if (!CheckNetworkConnection())
            return;
        Debug.Log("[NetServerManager] 处理添加物品请求: itemId=" + data.itemId + ", quantity=" + data.quantity);

        var requestData = new Dictionary<string, object>
        {
            { "itemId", data.itemId },
            { "quantity", data.quantity }
        };
        StartCoroutine(SendRequest<object>("/api/inventory/add", requestData));
    }

    public void OnRemoveItem((int itemId, int quantity) data)
    {
        if (!CheckNetworkConnection())
            return;
        Debug.Log("[NetServerManager] 处理移除物品请求: itemId=" + data.itemId + ", quantity=" + data.quantity);

        var requestData = new Dictionary<string, object>
        {
            { "itemId", data.itemId },
            { "quantity", data.quantity }
        };
        StartCoroutine(SendRequest<object>("/api/inventory/remove", requestData));
    }

    public void OnAddFish((int fishId, int quantity) data)
    {
        if (!CheckNetworkConnection())
            return;
        Debug.Log("[NetServerManager] 处理添加鱼请求: fishId=" + data.fishId + ", quantity=" + data.quantity);

        var requestData = new Dictionary<string, object>
        {
            { "itemId", data.fishId },
            { "quantity", data.quantity }
        };
        StartCoroutine(SendRequest<object>("/api/inventory/fish/add", requestData));
    }

    public void OnSellFishItems((List<int>, int) data)
    {
        if (!CheckNetworkConnection())
            return;

        var (itemIds, totalPrice) = data;
        Debug.Log($"[NetServerManager] 处理售卖鱼请求: itemIds={string.Join(",", itemIds)}, totalPrice={totalPrice}");

        var requestData = new Dictionary<string, object>
        {
            { "itemIds", itemIds },
            { "totalPrice", totalPrice }
        };

        StartCoroutine(SendRequest<object>($"/api/player/fish-bag/{_currentPlayerId}/sell", requestData,
            (response) =>
            {
                Debug.Log("[NetServerManager] 售卖鱼成功");

                // ========== 修复：售卖成功后，重新从服务器获取最新数据 ==========
                // 避免手动递减导致客户端与服务端数据不一致
                StartCoroutine(FetchPlayerDataAfterSell(itemIds, totalPrice));
            },
            (error) =>
            {
                Debug.LogWarning("[NetServerManager] 售卖鱼失败: " + error);
                UIManager.Instance?.ShowTip("售卖失败，请重试");
            }));
    }

    /// <summary>
    /// 售卖后重新获取玩家数据，确保客户端与服务端一致
    /// </summary>
    private System.Collections.IEnumerator FetchPlayerDataAfterSell(List<int> itemIds, int totalPrice)
    {
        // 等待一帧，确保服务器数据已更新
        yield return null;

        // 重新获取鱼篓数据
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/fish-bag/" + _currentPlayerId))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Debug.Log("[NetServerManager] 售卖后鱼篓数据响应: " + json);
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        fishInventory.Clear();
                        foreach (var item in data.items)
                        {
                            fishInventory[item.key] = item.value;
                        }
                        int totalFish = fishInventory.Values.Sum();
                        Debug.Log("[NetServerManager] 售卖后更新鱼篓: " + fishInventory.Count + " 种鱼，总数量: " + totalFish);

                        // 更新鱼篓满状态
                        isFishBagFull = totalFish >= fishBagCapacity;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 售卖后解析鱼篓数据失败: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NetServerManager] 售卖后获取鱼篓数据失败: " + request.error);
            }
        }

        // 重新获取金币数据
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/gold/" + _currentPlayerId))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<GoldResponse>(json);
                    if (data != null)
                    {
                        playerGold = data.gold;
                        Debug.Log("[NetServerManager] 售卖后更新金币: " + playerGold);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 售卖后解析金币数据失败: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NetServerManager] 售卖后获取金币数据失败: " + request.error);
            }
        }

        // 更新鱼篓满状态
        isFishBagFull = fishInventory.Values.Sum() >= fishBagCapacity;
        if (!isFishBagFull && !isPlayingReelAnimation)
        {
            NotifyPlayIdleAnimation();
        }

        // ========== 修复：通知 PlayerDataManager 同步数据，触发UI刷新 ==========
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.SyncInventoryFromServer();
            PlayerDataManager.Instance.SyncGoldFromServer();
            Debug.Log("[NetServerManager] 售卖后已通知 PlayerDataManager 同步数据");
        }
    }

    /// <summary>
    /// 从服务器重新获取鱼篓数据，确保客户端与服务端一致
    /// </summary>
    private System.Collections.IEnumerator FetchFishInventoryFromServer()
    {
        yield return null;

        using (var request = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/api/player/fish-bag/" + _currentPlayerId))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Debug.Log("[NetServerManager] 从服务器获取鱼篓数据: " + json);
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        fishInventory.Clear();
                        foreach (var item in data.items)
                        {
                            fishInventory[item.key] = item.value;
                        }
                        int totalFish = fishInventory.Values.Sum();
                        Debug.Log("[NetServerManager] 服务器鱼篓数据: " + fishInventory.Count + " 种鱼，总数量: " + totalFish);

                        // 更新鱼篓满状态
                        isFishBagFull = totalFish >= fishBagCapacity;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析服务器鱼篓数据失败: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NetServerManager] 获取服务器鱼篓数据失败: " + request.error);
            }
        }

        // 通知 PlayerDataManager 同步数据
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.SyncInventoryFromServer();
        }
    }

    public void DoFishing(int baitId = 0)
    {
        if (!CheckNetworkConnection())
            return;

        int sceneId = GetCurrentSceneId();

        NetUtils.LogRequest("DoFishing", new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", baitId }
        });

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", baitId }
        };
        StartCoroutine(DoFishingCoroutine("/api/fishing/catch", requestData));
    }

    private IEnumerator DoFishingCoroutine(string url, Dictionary<string, object> requestData)
    {
        if (!isConnected)
        {
            Debug.LogWarning("[NetServerManager] 未连接到服务器，无法钓鱼");
            yield break;
        }

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(serverUrl + url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string responseJson = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<FishingCatchResponse>(responseJson);

                    if (response != null && response.success)
                    {
                        Debug.Log("[NetServerManager] 钓鱼成功: " + response.fishName + " (" + response.weight + "kg)");

                        int goldChange = response.goldBalance - playerGold;
                        playerGold = response.goldBalance;

                        if (goldChange != 0)
                        {
                            var goldData = new Dictionary<string, object>
                            {
                                { "gold", playerGold },
                                { "addedAmount", goldChange > 0 ? goldChange : 0 },
                                { "deductedAmount", goldChange < 0 ? -goldChange : 0 }
                            };
                            CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
                        }

                        // 播放动画
                        if (response.isTrash)
                        {
                            trashStreak = response.trashStreak;

                            // 从服务器同步鱼篓数据，避免手动递增导致数据不一致
                            StartCoroutine(FetchFishInventoryFromServer());
                        }
                        else
                        {
                            float struggleTime = response.struggleTime > 0 ? response.struggleTime : 2f;
                            NotifyPlayReelAnimation(struggleTime, () =>
                            {
                                trashStreak = 0;

                                // 从服务器同步鱼篓数据，避免手动递增导致数据不一致
                                StartCoroutine(FetchFishInventoryFromServer());
                            });
                        }

                        isFishBagFull = fishInventory.Values.Sum() >= fishBagCapacity;
                        if (isFishBagFull && !isPlayingReelAnimation)
                        {
                            NotifyPlayLazyAnimation();
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[NetServerManager] 钓鱼失败: " + (response?.message ?? "未知错误"));
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析钓鱼响应失败: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NetServerManager] 钓鱼请求失败: " + request.error);
            }
        }
    }

    public void OnServerFishingResult(FishingResult result)
    {
        if (!_isEnabled)
            return;
    }

    public void NotifyPlayIdleAnimation()
    {
        if (isPlayingReelAnimation)
        {
            Debug.Log("[NetServerManager] 正在播放 Reel 动画，忽略 Idle 请求");
            return;
        }
        Debug.Log("[NetServerManager] 通知播放Idle动画");
        PlayerAniManager.Instance?.PlayIdleAnimation();
    }

    public void NotifyPlayLazyAnimation()
    {
        if (isPlayingReelAnimation)
        {
            Debug.Log("[NetServerManager] 正在播放 Reel 动画，忽略 Lazy 请求");
            return;
        }
        Debug.Log("[NetServerManager] 通知播放Lazy动画");
        PlayerAniManager.Instance?.PlayLazyAnimation();
    }

    public void NotifyPlayReelAnimation(float struggleTime, System.Action onComplete)
    {
        if (isPlayingReelAnimation)
        {
            Debug.Log("[NetServerManager] 已在播放 Reel 动画，忽略新请求");
            onComplete?.Invoke();
            return;
        }

        isPlayingReelAnimation = true;
        // 记录挣扎开始时间和时长，用于显示倒计时
        struggleStartTime = Time.time;
        currentStruggleTime = struggleTime;

        Debug.Log($"[NetServerManager] 通知播放Reel动画，挣扎时间: {struggleTime}秒");

        PlayerAniManager.Instance?.PlayReelAnimation(struggleTime, () =>
        {
            isPlayingReelAnimation = false;
            onComplete?.Invoke();

            // 重置挣扎时间记录
            struggleStartTime = 0f;
            currentStruggleTime = 0f;

            // 注意：不再调用 NotifyFishingComplete()，因为服务器不需要这个通知
            // 钓鱼完成状态已通过 PollFishingStatus 轮询同步

            // 强制同步鱼篓数据并刷新UI
            NotifySyncInventoryFromServer();

            // 额外：如果鱼篓界面是打开的，强制刷新
            if (UIManager.Instance?.fishBagView != null && UIManager.Instance.fishBagView.gameObject.activeSelf)
            {
                UIManager.Instance.fishBagView.RefreshItems();
                Debug.Log("[NetServerManager] 鱼篓界面已打开，强制刷新");
            }

            // 根据当前状态切换到正确的动画
            if (isFishBagFull || isPaused)
            {
                NotifyPlayLazyAnimation();
            }
            else
            {
                NotifyPlayIdleAnimation();
            }
        });
    }
    public void NotifySyncInventoryFromServer()
    {
        Debug.Log("[NetServerManager] 通知同步背包数据");
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.SyncInventoryFromServer();

            // 【修复】同步完成后，如果鱼篓界面是打开的，刷新UI
            if (UIManager.Instance?.fishBagView != null && UIManager.Instance.fishBagView.gameObject.activeSelf)
            {
                // 延迟一帧刷新，确保数据已经更新
                PlayerAniManager.Instance.StartCoroutine(DelayedRefreshFishBag());
            }

            // 刷新金币显示
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateGoldDisplay(playerGold);
            }
        }
    }

    private IEnumerator DelayedRefreshFishBag()
    {
        yield return null; // 等待一帧
        if (UIManager.Instance?.fishBagView != null)
        {
            UIManager.Instance.fishBagView.RefreshItems();
            Debug.Log("[NetServerManager] 延迟刷新鱼篓UI完成");
        }
    }

    public void NotifyAddFish(int fishId, int quantity)
    {
        Debug.Log("[NetServerManager] 通知添加鱼: fishId=" + fishId + ", quantity=" + quantity);
    }

    public void NotifyRefreshUI()
    {
        Debug.Log("[NetServerManager] 通知刷新UI");
        PlayerDataManager.Instance?.RefreshUI();
    }

    public void NotifyShowCatchResult(string itemName, float weight, Sprite icon)
    {
        Debug.Log("[NetServerManager] 通知显示捕获结果: " + itemName);
        UIManager.Instance?.ShowCatchResult(itemName, weight, icon);
    }

    /// <summary>
    /// 从服务器钓获信息显示MainTile
    /// </summary>
    private void ShowCatchResultFromServer(LastCatchInfo catchInfo)
    {
        if (catchInfo == null)
            return;

        Debug.Log($"[NetServerManager] 显示钓获结果: {catchInfo.fishName}, 重量: {catchInfo.weight}kg");

        // 获取物品图标
        Sprite icon = GetItemIcon(catchInfo.fishId);

        UIManager.Instance?.ShowCatchResult(catchInfo.fishName, catchInfo.weight, icon);
    }

    /// <summary>
    /// 获取物品图标
    /// </summary>
    private Sprite GetItemIcon(int itemId)
    {
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.items != null)
        {
            foreach (ItemData item in LoadDataManager.Instance.items)
            {
                if (item.id == itemId)
                {
                    if (!string.IsNullOrEmpty(item.iconPath))
                    {
                        return Resources.Load<Sprite>(item.iconPath);
                    }
                    break;
                }
            }
        }
        return null;
    }

    private int GetCurrentSceneId()
    {
        if (EnvManager.Instance != null)
        {
            return EnvManager.Instance.currentSceneId;
        }
        return 1;
    }

    public void StartAutoFishing(int baitId = 0)
    {
        if (!CheckNetworkConnection())
            return;

        // 先检查鱼篓是否已满
        if (isFishBagFull)
        {
            Debug.Log("[NetServerManager] 鱼篓已满，无法启动自动钓鱼");
            NotifyPlayLazyAnimation();
            return;
        }

        int sceneId = GetCurrentSceneId();

        NetUtils.LogRequest("StartAutoFishing", new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", baitId }
        });

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", baitId }
        };
        StartCoroutine(SendRequest<AutoFishingResponse>("/api/fishing/auto/start", requestData, (response) =>
        {
            if (response != null && response.success)
            {
                isAutoFishing = true;
                Debug.Log("[NetServerManager] 自动钓鱼已启动");
            }
            else
            {
                Debug.LogWarning("[NetServerManager] 启动自动钓鱼失败: " + (response?.message ?? "未知错误"));
            }
        }));
    }

    public void StopAutoFishing()
    {
        if (!CheckNetworkConnection())
            return;

        NetUtils.LogRequest("StopAutoFishing", new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId }
        });

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId }
        };
        StartCoroutine(SendRequest<AutoFishingResponse>("/api/fishing/auto/stop", requestData, (response) =>
        {
            if (response != null && response.success)
            {
                isAutoFishing = false;
                Debug.Log("[NetServerManager] 自动钓鱼已停止");
            }
            else
            {
                Debug.LogWarning("[NetServerManager] 停止自动钓鱼失败: " + (response?.message ?? "未知错误"));
            }
        }));
    }

    #region PollFishingStatus 轮询钓鱼状态

    /// <summary>
    /// 轮询钓鱼状态
    /// </summary>
    private IEnumerator PollFishingStatus()
    {
        int lastCatchId = -1;

        while (isConnected && this != null && gameObject != null)
        {
            yield return new WaitForSeconds(2f);

            if (!isConnected || this == null || gameObject == null)
                yield break;

            using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/fishing/status?playerId=" + _currentPlayerId))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        var response = JsonUtility.FromJson<FishingStatusResponse>(json);

                        if (response != null && response.success)
                        {
                            bool wasPaused = isPaused;
                            bool wasFull = isFishBagFull;

                            isAutoFishing = response.isAutoFishing;
                            isPaused = response.isPaused;
                            trashStreak = response.trashStreak;
                            continuousModeRemainingTime = response.continuousModeRemainingTime;
                            currentFishingMode = response.continuousModeRemainingTime > 0 ? "Continuous" : "Normal";

                            // 服务器直接返回剩余秒数
                            if (response.nextFishingTime > 0)
                            {
                                timeUntilNextFishing = response.nextFishingTime;
                            }
                            else
                            {
                                timeUntilNextFishing = 0;
                            }

                            // 重新计算鱼篓是否满（从服务器同步的数据）
                            int currentFishCount = GetTotalFishCount();
                            isFishBagFull = currentFishCount >= fishBagCapacity;

                            // 检测新钓获
                            bool detectedNewCatch = false;
                            if (response.lastCatch != null && response.lastCatch.fishId > 0 && response.lastCatch.fishId != lastCatchId)
                            {
                                lastCatchId = response.lastCatch.fishId;
                                float struggleTime = response.lastCatch.struggleTime > 0 ? response.lastCatch.struggleTime : 1.5f;

                                Debug.Log($"[NetServerManager] 检测到新钓获: {response.lastCatch.fishName} (ID:{response.lastCatch.fishId}), 重量:{response.lastCatch.weight}kg, 挣扎时间:{struggleTime}秒");

                                if (!isPlayingReelAnimation && !isFishBagFull)
                                {
                                    detectedNewCatch = true;

                                    // 保存钓获信息，用于在拉杆动画结束后显示MainTile
                                    pendingCatchInfo = response.lastCatch;

                                    // 触发金币变化事件
                                    if (response.lastCatch.goldEarned > 0)
                                    {
                                        var goldData = new Dictionary<string, object>
                                    {
                                        { "gold", playerGold + response.lastCatch.goldEarned },
                                        { "addedAmount", response.lastCatch.goldEarned },
                                        { "deductedAmount", 0 }
                                    };
                                        playerGold += response.lastCatch.goldEarned;
                                        CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
                                    }

                                    // 播放拉钩动画
                                    NotifyPlayReelAnimation(struggleTime, () =>
                                    {
                                        Debug.Log("[NetServerManager] Reel动画结束");

                                        // 显示MainTile
                                        if (pendingCatchInfo != null)
                                        {
                                            ShowCatchResultFromServer(pendingCatchInfo);
                                            pendingCatchInfo = null;
                                        }

                                        StartCoroutine(FetchFishInventoryFromServer());
                                    });
                                }
                            }

                            // ========== 修复：如果处于停滞状态但没有播放动画，从服务器数据恢复挣扎信息 ==========
                            if (isPaused && !isPlayingReelAnimation && response.lastCatch != null && response.lastCatch.struggleTime > 0)
                            {
                                // 重连或状态同步时，恢复挣扎动画状态
                                struggleStartTime = Time.time;
                                currentStruggleTime = response.lastCatch.struggleTime;
                                isPlayingReelAnimation = true;

                                Debug.Log($"[NetServerManager] 恢复收竿动画状态，挣扎时间: {currentStruggleTime}秒");

                                // 重新播放动画（如果需要）
                                PlayerAniManager.Instance?.PlayReelAnimation(currentStruggleTime, () =>
                                {
                                    isPlayingReelAnimation = false;
                                    struggleStartTime = 0f;
                                    currentStruggleTime = 0f;

                                    // 显示MainTile
                                    if (pendingCatchInfo != null)
                                    {
                                        ShowCatchResultFromServer(pendingCatchInfo);
                                        pendingCatchInfo = null;
                                    }

                                    StartCoroutine(FetchFishInventoryFromServer());
                                    NotifySyncInventoryFromServer();

                                    if (isFishBagFull || isPaused)
                                    {
                                        NotifyPlayLazyAnimation();
                                    }
                                    else
                                    {
                                        NotifyPlayIdleAnimation();
                                    }
                                });
                            }

                            // 动画切换逻辑
                            if (!isPlayingReelAnimation && !detectedNewCatch)
                            {
                                if (isFishBagFull)
                                {
                                    if (!wasFull)
                                    {
                                        Debug.Log("[NetServerManager] 鱼篓已满，切换到懒动画");
                                        NotifyPlayLazyAnimation();
                                    }
                                }
                                else
                                {
                                    if (wasFull || wasPaused)
                                    {
                                        Debug.Log("[NetServerManager] 鱼篓未满，切换到空闲动画");
                                        NotifyPlayIdleAnimation();
                                    }
                                }
                            }

                            // ========== 生成显示文本（三种状态） ==========
                            string nextFishingDisplay;

                            if (isFishBagFull)
                            {
                                // 状态1: 鱼篓已满
                                nextFishingDisplay = "鱼篓已满";
                            }
                            else if (isPaused)
                            {
                                // 状态2: 收竿中
                                if (isPlayingReelAnimation && currentStruggleTime > 0)
                                {
                                    // 正在播放动画，计算剩余时间
                                    float elapsed = Time.time - struggleStartTime;
                                    float remaining = Mathf.Max(0, currentStruggleTime - elapsed);
                                    nextFishingDisplay = $"收竿中 {remaining:F1}秒";
                                }
                                else if (response.lastCatch != null && response.lastCatch.struggleTime > 0)
                                {
                                    // 停滞状态但没有播放动画，显示总挣扎时间
                                    nextFishingDisplay = $"收竿中 {response.lastCatch.struggleTime:F1}秒";
                                }
                                else
                                {
                                    nextFishingDisplay = "收竿中";
                                }
                            }
                            else if (response.nextFishingTime > 0)
                            {
                                // 状态3: 等待钓鱼（显示倒计时）
                                nextFishingDisplay = $"{response.nextFishingTime:F1}秒";
                            }
                            else
                            {
                                nextFishingDisplay = "等待中";
                            }

                            Debug.Log("[NetServerManager] 更新钓鱼状态: 自动钓鱼=" + isAutoFishing +
                                     ", 停滞=" + isPaused + ", 鱼篓满=" + isFishBagFull +
                                     ", 垃圾连续=" + trashStreak + ", 鱼篓总数=" + GetTotalFishCount() +
                                     ", 下次钓鱼=" + nextFishingDisplay);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("[NetServerManager] 解析钓鱼状态失败: " + ex.Message);
                    }
                }
                else
                {
                    Debug.LogWarning("[NetServerManager] 获取钓鱼状态失败: " + request.error);
                }
            }
        }
    }
#endregion

    public void Reconnect()
    {
        if (!_isEnabled)
            return;

        networkState = NetUtils.NetworkState.Reconnecting;
        isConnected = false;
        missedHeartbeats = 0;
        StopHeartbeat();

        Debug.Log("<color=orange>[NetServerManager] 尝试重新连接...</color>");
        StartConnect();
    }

    public void Disconnect()
    {
        if (!_isEnabled)
            return;

        // 发送退出请求
        SendPlayerExit();
        StopHeartbeat();

        networkState = NetUtils.NetworkState.Disconnected;
        isConnected = false;
        isPlayingReelAnimation = false;
        Debug.Log("<color=red>[NetServerManager] 已断开连接</color>");
    }

    [System.Serializable]
    private class HeartbeatResponse
    {
        public long serverTime;
        public long clientTime;
        public bool isConnected;
    }

    [System.Serializable]
    private class HeartbeatRequest
    {
        public long clientTime;
    }

    [System.Serializable]
    public class PlayerData
    {
        public int playerId;
        public string nickname;
        public int gold;
        public int level;
        public int experience;
        public int currentSceneId;
        public int maxFishBagCapacity;
        public int FishBagCapacity => maxFishBagCapacity;
    }

    [System.Serializable]
    public class FishingCatchResponse
    {
        public bool success;
        public int fishId;
        public string fishName;
        public float weight;
        public int goldEarned;
        public int expEarned;
        public int goldBalance;
        public int expBalance;
        public int durability;
        public string message;
        public bool isTrash;
        public int trashStreak;
        public float struggleTime;
    }

    [System.Serializable]
    public class AutoFishingResponse
    {
        public bool success;
        public string message;
        public int catchCount;
        public int totalGold;
        public int totalExp;
    }

    [System.Serializable]
    public class FishingStatusResponse
    {
        public bool success;
        public int level;
        public int gold;
        public int diamonds;
        public int exp;
        public int durability;
        public int todayFishCount;
        public int comboCount;
        public bool isAutoFishing;
        public bool isPaused;
        public int trashStreak;
        public float continuousModeRemainingTime;
        public float nextFishingTime;
        public LastCatchInfo lastCatch;  // 新增：最近钓获信息
    }

    [System.Serializable]
    public class LastCatchInfo
    {
        public int fishId;
        public string fishName;
        public float weight;
        public int goldEarned;
        public int expEarned;
        public bool isTrash;
        public float struggleTime;
    }
}