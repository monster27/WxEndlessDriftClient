using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Utils;
using Logger = Utils.Logger;
using ServerModels;


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

        Logger.LogColor("[NetServerManager] 网络服务器管理器初始化完成，服务器地址: " + serverUrl, "green");

        StartConnect();
    }

    #region 玩家连接状态管理

    /// <summary>
    /// 发送玩家退出请求（正常退出时调用）OnEquipBait 
    /// </summary>
    public void SendPlayerExit()
    {
        if (!isConnected)
        {
            Logger.Log("[NetServerManager] 未连接服务器，跳过退出请求");
            return;
        }

        Logger.LogColor("[NetServerManager] 发送玩家退出请求", "orange");

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>("/api/player/" + _currentPlayerId + "/exit", requestData,
            onSuccess: (response) =>
            {
                Logger.LogColor("[NetServerManager] 玩家退出请求成功", "green");
                StopHeartbeat();
            },
            onError: (error) =>
            {
                Logger.LogWarning("[NetServerManager] 玩家退出请求失败: " + error);
            },
            forcePost: true
        ));
    }

    /// <summary>
    /// 请求重连恢复状态
    /// </summary>
    public void RequestReconnect()
    {
        Logger.LogColor("[NetServerManager] 请求重连恢复状态", "orange");

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>("/api/player/" + _currentPlayerId + "/reconnect", requestData,
            onSuccess: (response) =>
            {
                Logger.LogColor("[NetServerManager] 重连请求成功，开始恢复钓鱼状态", "green");

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
                Logger.LogWarning("[NetServerManager] 重连请求失败: " + error + "，尝试重新连接");
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
        Logger.Log("[NetServerManager] 心跳协程已启动");
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
            Logger.Log("[NetServerManager] 心跳协程已停止");
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
                Logger.LogColor("[NetServerManager] 心跳发送成功", "cyan");
            },
            onError: (error) =>
            {
                Logger.LogWarning("[NetServerManager] 心跳发送失败: " + error);
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
        Logger.Log($"[NetServerManager] OnApplicationPause: {pause}");

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
                Logger.Log("[NetServerManager] 应用恢复但未连接到服务器，尝试重连");
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
        Logger.LogColor("[NetServerManager] OnApplicationQuit - 应用退出", "red");

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
        Logger.LogColor("[NetServerManager] OnDestroy - 对象销毁", "red");

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

        Logger.LogColor("[NetServerManager] 设置启用状态: " + enabled, "orange");
    }

    private void RegisterNetworkEvents()
    {
    }

    private void RegisterServerEvents()
    {
        Logger.Log("[NetServerManager] 注册网络模式下的事件处理器");

        // 注册连续模式相关的请求处理器
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_IN_CONTINUOUS_MODE, _ => isInContinuousMode);
        CommunicateEvent.RegisterRequest<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, _ => continuousModeRemainingTime);
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
        CommunicateEvent.RegisterRequest<int, PlayerNetworkData>(CommunicateEvent.EVENT_GET_PLAYER_DATA, _ => GetPlayerData());

        // 注册装备/卸下事件处理器
        CommunicateEvent.Register<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, OnEquipItem);
        CommunicateEvent.Register<int>(CommunicateEvent.EVENT_EQUIP_BAIT, OnEquipBait);

        // 注册人物相关请求处理器
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_CHARACTER_OBTAINED, characterId => IsCharacterObtained(characterId));
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId => IsSkillObtained(skillId));
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_ITEM_EQUIPPED, itemId => IsItemEquipped(itemId));

        // 注册 CharacterServerManager 相关的请求处理器
        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterServerManager_GetPlayerData", _ => GetPlayerCharacterData());
        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterManager_GetPlayerData", _ => GetPlayerCharacterData());
        CommunicateEvent.RegisterRequest<int, int>("CharacterServerManager_GetExpToNextLevel", _ => GetExpToNextLevel());
        CommunicateEvent.RegisterRequest<int, int>("CharacterManager_GetExpToNextLevel", _ => GetExpToNextLevel());

        // 注册自动钓鱼状态相关的请求处理器
        CommunicateEvent.RegisterRequest<int, bool>("IsAutoFishing", _ => isAutoFishing);
        CommunicateEvent.RegisterRequest<int, bool>("IsPaused", _ => isPaused);
        CommunicateEvent.RegisterRequest<int, float>("GetTimeUntilNextFishing", _ => timeUntilNextFishing);
        CommunicateEvent.RegisterRequest<int, int>("GetTrashStreak", _ => trashStreak);
        CommunicateEvent.RegisterRequest<int, bool>("IsFishBagFull", _ => isFishBagFull);
        CommunicateEvent.RegisterRequest<int, string>("GetCurrentFishingMode", _ => currentFishingMode);

        // 注册售卖鱼事件处理器
        CommunicateEvent.Register<(List<int>, int)>(CommunicateEvent.EVENT_SELL_FISH_ITEMS, OnSellFishItems);

        // 注册装备解锁事件处理器
        CommunicateEvent.Register<int>("Equip_Unlock", OnUnlockEquipment);

        // 注册商城相关请求处理器
        CommunicateEvent.RegisterRequest<int, Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_GET_MALL_ITEMS, _ => GetMallItems());
        CommunicateEvent.RegisterRequest<int, MallItemData>(CommunicateEvent.EVENT_GET_MALL_ITEM, itemId => GetMallItem(itemId));

        // 注册购买商城物品事件处理器
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_PURCHASE_MALL_ITEM, OnPurchaseMallItem);

        // 注册窝料消耗事件处理器（投喂窝料进入连续钓鱼模式）
        CommunicateEvent.Register(CommunicateEvent.EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE, OnConsumeBaitAndEnterContinuousMode);
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, _ => GetCurrentSceneBaitCount());
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
    
    // 玩家已解锁的人物列表（从专用表获取）
    private HashSet<int> unlockedCharacters = new HashSet<int>();
    
    // 玩家已解锁的装备列表（从服务器获取，用于判断装备状态）
    private HashSet<int> unlockedEquipment = new HashSet<int>();

    // 当前玩家ID
    private int _currentPlayerId = 1;

    public void SetCurrentPlayerId(int playerId)
    {
        _currentPlayerId = playerId;
        Logger.Log($"[NetServerManager] 当前玩家ID已更新为: {_currentPlayerId}");
    }

    public int GetCurrentPlayerId()
    {
        return _currentPlayerId;
    }

    // 玩家装备数据
    private int equippedRodId = 3001;
    private int equippedLineId = 3101;
    private int equippedHookId = 3201;
    private int equippedSkill1Id = 0;
    private int equippedSkill2Id = 0;
    private int equippedCharacterId = 3401;
    private int equippedBaitId = 0;
    private int characterLevel = 1;
    private int currentCharacterExp = 0;

    private int GetCurrentSceneBaitCount()
    {
        // 从背包中获取窝料(2501)的数量
        if (playerInventory.ContainsKey(2501))
        {
            return playerInventory[2501];
        }
        return 0;
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

        Logger.Log("[NetServerManager] 收到金币同步请求");

        int currentGold = playerGold;
        Logger.Log($"[NetServerManager] 当前金币: {currentGold}");

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

    /// <summary>
    /// 处理装备物品请求
    /// </summary>
    private void OnEquipItem((EquipmentSlotType slotType, int itemId) data)
    {
        if (!CheckNetworkConnection())
            return;

        var (slotType, itemId) = data;
        Logger.Log($"[NetServerManager] 处理装备请求: slotType={slotType}, itemId={itemId}");

        // 更新本地装备数据
        UpdateLocalEquippedItem(slotType, itemId);

        // 调用服务器API
        int slotTypeInt = (int)slotType;
        StartCoroutine(SendEquipRequest(slotTypeInt, itemId));
    }

    /// <summary>
    /// 处理装备鱼饵请求
    /// </summary>
    private void OnEquipBait(int itemId)
    {
        if (!CheckNetworkConnection())
            return;

        Logger.Log($"[NetServerManager] 处理装备鱼饵请求: itemId={itemId}");

        // 更新本地装备数据
        UpdateLocalEquippedItem(EquipmentSlotType.Bait, itemId);

        // 调用服务器API装备鱼饵（关键：必须调用服务器）
        StartCoroutine(SendEquipRequest((int)EquipmentSlotType.Bait, itemId));

        // 同步背包数据
        PlayerDataManager.Instance?.SyncInventoryFromServer();

        // 通知背包刷新
        CommunicateEvent.Modify("Bag_RefreshItems");
    }

    /// <summary>
    /// 更新本地装备数据
    /// </summary>
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

    /// <summary>
    /// 发送装备请求到服务器
    /// </summary>
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
                        
                        // 同步背包数据（服务器已处理物品增减）
                        if (PlayerDataManager.Instance != null)
                        {
                            PlayerDataManager.Instance.SyncInventoryFromServer();
                        }
                        
                        // 如果是人物装备，立即同步人物数据
                        if (slotType == (int)EquipmentSlotType.Character)
                        {
                            Logger.Log($"[NetServerManager] 检测到人物装备，立即同步人物数据...");
                            StartCoroutine(SyncCharacterDataAfterEquip(itemId));
                        }
                        
                        // 触发装备变更事件，通知UI刷新
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

    /// <summary>
    /// 装备响应数据结构
    /// </summary>
    private class EquipResponse
    {
        public bool success;
        public string message;
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

    /// <summary>
    /// 从服务器同步人物数据
    /// </summary>
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
                    
                    // 计算升级所需经验
                    int requiredExp = GetExpToNextLevel();
                    
                    // 触发人物数据更新事件，通知UI刷新
                    CommunicateEvent.Modify<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, (characterLevel, currentCharacterExp, requiredExp));
                }
            },
            (error) =>
            {
                Logger.LogError($"[NetServerManager] 人物数据同步失败: {error}");
            }));
    }

    private int GetExpToNextLevel()
    {
        // 根据当前等级计算升级所需经验
        // 1-10级: 10点/级, 11-20级: 20点/级, 21-30级: 30点/级, 以此类推
        int level = characterLevel;
        if (level >= 1 && level <= 10)
            return 10;
        if (level >= 11 && level <= 20)
            return 20;
        if (level >= 21 && level <= 30)
            return 30;
        if (level >= 31 && level <= 40)
            return 40;
        if (level >= 41 && level <= 50)
            return 50;
        if (level >= 51 && level <= 60)
            return 60;
        if (level >= 61 && level <= 70)
            return 70;
        if (level >= 71 && level <= 80)
            return 80;
        if (level >= 81 && level <= 90)
            return 90;
        if (level >= 91 && level <= 99)
            return 100;
        return 100; // 满级后返回默认值
    }
    
    /// <summary>
    /// 装备成功后同步人物数据
    /// </summary>
    private IEnumerator SyncCharacterDataAfterEquip(int expectedCharacterId)
    {
        // 等待一小段时间，确保服务器数据已更新
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
                        // 更新本地人物数据
                        equippedCharacterId = response.characterId;
                        characterLevel = response.level > 0 ? response.level : 1;
                        currentCharacterExp = response.exp;
                        
                        Logger.Log($"[NetServerManager] 装备后人物数据同步完成: CharacterId={equippedCharacterId}, Level={characterLevel}, Exp={currentCharacterExp}");
                        
                        // 计算升级所需经验
                        int requiredExp = GetExpToNextLevel();
                        
                        // 触发人物数据更新事件，通知UI刷新
                        CommunicateEvent.Modify<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, (characterLevel, currentCharacterExp, requiredExp));
                        
                        // 如果获取到的人物ID与预期不符，记录警告
                        if (response.characterId != expectedCharacterId)
                        {
                            Logger.LogWarning($"[NetServerManager] 警告：获取到的人物ID({response.characterId})与预期({expectedCharacterId})不符！");
                        }
                        
                        // 切换人物动画
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
    
    /// <summary>
    /// 同步玩家已解锁的人物列表（从服务器专用表获取）
    /// </summary>
    public void SyncUnlockedCharactersFromServer()
    {
        StartCoroutine(SyncUnlockedCharactersCoroutine());
    }
    
    private IEnumerator SyncUnlockedCharactersCoroutine()
    {
        string url = serverUrl + "/api/player/characters/" + _currentPlayerId;
        Logger.Log($"[NetServerManager] 同步人物列表: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log("[NetServerManager] 人物列表响应: " + json);
                    
                    // 清空旧数据
                    unlockedCharacters.Clear();
                    
                    // 解析JSON数组
                    var data = JsonUtility.FromJson<CharacterListResponse>(json);
                    if (data != null && data.characters != null)
                    {
                        foreach (var character in data.characters)
                        {
                            unlockedCharacters.Add(character.characterId);
                            Logger.Log($"[NetServerManager] 已解锁人物: {character.characterId}");
                        }
                    }
                    else
                    {
                        // 尝试直接解析数组
                        var characters = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CharacterData>>(json);
                        if (characters != null)
                        {
                            foreach (var character in characters)
                            {
                                unlockedCharacters.Add(character.characterId);
                                Logger.Log($"[NetServerManager] 已解锁人物: {character.characterId}");
                            }
                        }
                    }
                    
                    // 始终包含默认人物
                    unlockedCharacters.Add(3401);
                    
                    Logger.Log($"[NetServerManager] 同步人物列表完成，共 {unlockedCharacters.Count} 个已解锁人物");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析人物列表失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 获取人物列表失败: {request.error}");
            }
        }
    }
    
    // 辅助类：用于解析人物列表响应
    [System.Serializable]
    private class CharacterListResponse
    {
        public List<CharacterData> characters;
    }
    
    [System.Serializable]
    private class CharacterData
    {
        public int characterId;
        public int level;
        public int exp;
        public bool isActive;
    }
    
    /// <summary>
    /// 同步玩家已解锁的装备列表（从服务器获取）
    /// </summary>
    public void SyncUnlockedEquipmentFromServer()
    {
        StartCoroutine(SyncUnlockedEquipmentCoroutine());
    }
    
    private IEnumerator SyncUnlockedEquipmentCoroutine()
    {
        string url = serverUrl + "/api/player/" + _currentPlayerId + "/unlocked-equipment";
        Logger.Log($"[NetServerManager] 同步已解锁装备列表: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log("[NetServerManager] 已解锁装备列表响应: " + json);
                    
                    // 清空旧数据
                    unlockedEquipment.Clear();
                    
                    // 解析JSON
                    var response = JsonUtility.FromJson<UnlockedEquipmentResponse>(json);
                    if (response != null && response.success && response.unlockedEquipment != null)
                    {
                        foreach (var equipmentId in response.unlockedEquipment)
                        {
                            unlockedEquipment.Add(equipmentId);
                            Logger.Log($"[NetServerManager] 已解锁装备: {equipmentId}");
                        }
                    }
                    
                    Logger.Log($"[NetServerManager] 同步已解锁装备列表完成，共 {unlockedEquipment.Count} 个装备");
                    
                    // 使用事件机制通知 PlayerInventoryServerManager 更新已解锁装备列表
                    var equipmentList = new List<int>(unlockedEquipment);
                    CommunicateEvent.Modify<List<int>>("SyncUnlockedEquipment", equipmentList);
                    Logger.Log($"[NetServerManager] 已发送同步已解锁装备列表事件，共 {unlockedEquipment.Count} 个装备");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析已解锁装备列表失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 获取已解锁装备列表失败: {request.error}");
            }
        }
    }
    
    // 辅助类：用于解析已解锁装备列表响应
    [System.Serializable]
    private class UnlockedEquipmentResponse
    {
        public bool success;
        public List<int> unlockedEquipment;
    }
    
    /// <summary>
    /// 检查装备是否已解锁（已拥有）
    /// </summary>
    /// <param name="equipmentId">装备ID</param>
    /// <returns>是否已解锁</returns>
    public bool IsEquipmentUnlocked(int equipmentId)
    {
        // 首先检查已解锁装备列表
        if (unlockedEquipment.Contains(equipmentId))
        {
            return true;
        }
        // 然后检查背包
        if (playerInventory != null && playerInventory.ContainsKey(equipmentId) && playerInventory[equipmentId] > 0)
        {
            return true;
        }
        // 默认人物3401始终视为已解锁
        if (equipmentId == 3401)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 检查人物是否已获取（从专用表和背包数据判断）
    /// </summary>
    private bool IsCharacterObtained(int characterId)
    {
        // 首先检查专用表（主要来源）
        if (unlockedCharacters.Contains(characterId))
        {
            return true;
        }
        // 从背包数据中判断人物是否已获取
        if (playerInventory != null && playerInventory.ContainsKey(characterId))
        {
            return playerInventory[characterId] > 0;
        }
        // 默认人物3401始终视为已获取
        if (characterId == 3401)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 检查技能是否已获取（从背包数据判断）
    /// </summary>
    private bool IsSkillObtained(int skillId)
    {
        if (playerInventory != null && playerInventory.ContainsKey(skillId))
        {
            return playerInventory[skillId] > 0;
        }
        return false;
    }

    /// <summary>
    /// 检查物品是否已装备
    /// </summary>
    private bool IsItemEquipped(int itemId)
    {
        return equippedRodId == itemId ||
               equippedLineId == itemId ||
               equippedHookId == itemId ||
               equippedSkill1Id == itemId ||
               equippedSkill2Id == itemId ||
               equippedCharacterId == itemId;
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
                        Logger.Log("[NetServerManager] 更新连续模式状态: " + isInContinuousMode + ", 剩余时间: " + continuousModeRemainingTime);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析游戏状态失败: " + ex.Message);
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
                        Logger.Log("[NetServerManager] 更新鱼饵数量: " + currentSceneBaitCount);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析鱼饵数量失败: " + ex.Message);
                }
            }
        }
    }

    private IEnumerator FetchPlayerData()
    {
        if (!isConnected)
            yield break;

        Logger.Log($"[DEBUG] FetchPlayerData called with currentPlayerId={_currentPlayerId}");

        // 获取玩家金币
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/gold/" + _currentPlayerId))
        {
            Logger.Log($"[DEBUG] Requesting gold for playerId={_currentPlayerId}, URL={serverUrl}/api/player/gold/{_currentPlayerId}");
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log($"[DEBUG] Gold response: {json}");
                    var data = JsonUtility.FromJson<GoldResponse>(json);
                    if (data != null)
                    {
                        playerGold = data.gold;
                        Logger.Log("[NetServerManager] 更新玩家金币: " + playerGold);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析金币数据失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 获取金币数据失败: " + request.error);
            }
        }

        // 获取玩家背包
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/inventory/" + _currentPlayerId))
        {
            Logger.Log($"[DEBUG] Requesting inventory for playerId={_currentPlayerId}, URL={serverUrl}/api/player/inventory/{_currentPlayerId}");
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log($"[DEBUG] Inventory response: {json}");
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        playerInventory.Clear();
                        foreach (var item in data.items)
                        {
                            playerInventory[item.key] = item.value;
                            Logger.Log($"[DEBUG] 收到背包物品: ID={item.key}, 数量={item.value}");
                        }
                        Logger.Log("[NetServerManager] 更新玩家背包: " + playerInventory.Count + " 件物品");

                        // ========== 新增：强制通知背包数据更新 ==========
                        CommunicateEvent.Modify<Dictionary<int, int>>("BagDataUpdated", playerInventory);

                        // 检查鱼饵和窝料
                        if (playerInventory.ContainsKey(2001))
                        {
                            Logger.Log("[DEBUG] 鱼饵2001存在，数量=" + playerInventory[2001]);
                            CommunicateEvent.Modify("BaitDataUpdated");
                        }
                        else
                        {
                            Logger.Log("[DEBUG] 鱼饵2001不存在于背包数据中");
                        }

                        if (playerInventory.ContainsKey(2501))
                        {
                            Logger.Log("[DEBUG] 窝料2501存在，数量=" + playerInventory[2501]);
                            CommunicateEvent.Modify("BaitCountChanged");
                        }
                        else
                        {
                            Logger.Log("[DEBUG] 窝料2501不存在于背包数据中");
                        }
                        // ========== 新增结束 ==========
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析背包数据失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 获取背包数据失败: " + request.error);
            }
        }

        // 获取玩家已解锁的人物列表
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/characters/" + _currentPlayerId))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log("[NetServerManager] 人物列表响应: " + json);

                    unlockedCharacters.Clear();

                    var characters = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CharacterData>>(json);
                    if (characters != null)
                    {
                        foreach (var character in characters)
                        {
                            unlockedCharacters.Add(character.characterId);
                            Logger.Log($"[NetServerManager] 已解锁人物: {character.characterId}");
                        }
                    }

                    unlockedCharacters.Add(3401);

                    Logger.Log($"[NetServerManager] 同步人物列表完成，共 {unlockedCharacters.Count} 个已解锁人物");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析人物列表失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 获取人物列表失败: " + request.error);
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
                    Logger.Log("[NetServerManager] 鱼篓数据响应: " + json);
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        fishInventory.Clear();
                        foreach (var item in data.items)
                        {
                            fishInventory[item.key] = item.value;
                        }
                        int totalFish = GetTotalFishCount();
                        Logger.Log("[NetServerManager] 更新玩家鱼篓: " + fishInventory.Count + " 种鱼，总数量: " + totalFish);

                        isFishBagFull = totalFish >= fishBagCapacity;
                    }
                    else
                    {
                        Logger.LogWarning("[NetServerManager] 鱼篓数据为空或格式不正确");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析鱼篓数据失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 获取鱼篓数据失败: " + request.error);
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
                    Logger.Log("[NetServerManager] 鱼篓容量响应: " + json);
                    var data = JsonUtility.FromJson<CapacityResponse>(json);
                    if (data != null)
                    {
                        fishBagCapacity = data.capacity;
                        Logger.Log("[NetServerManager] 更新鱼篓容量: " + fishBagCapacity);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析鱼篓容量失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 获取鱼篓容量失败: " + request.error);
            }
        }

        // 获取玩家装备信息（包含人物数据）
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/equipment/" + _currentPlayerId))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log("[NetServerManager] 装备数据响应: " + json);
                    var data = JsonUtility.FromJson<EquipmentResponse>(json);
                    if (data != null)
                    {
                        equippedRodId = data.rodId > 0 ? data.rodId : 3001;
                        equippedLineId = data.lineId > 0 ? data.lineId : 3101;
                        equippedHookId = data.hookId > 0 ? data.hookId : 3201;
                        equippedSkill1Id = data.skill1Id;
                        equippedSkill2Id = data.skill2Id;
                        equippedCharacterId = data.characterId > 0 ? data.characterId : 3401;
                        characterLevel = data.characterLevel > 0 ? data.characterLevel : 1;
                        Logger.Log($"[NetServerManager] 更新玩家装备: Rod={equippedRodId}, Line={equippedLineId}, Hook={equippedHookId}, Character={equippedCharacterId}, Level={characterLevel}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析装备数据失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 获取装备数据失败: " + request.error);
            }
        }

        // 获取玩家人物数据（经验等）
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/character/" + _currentPlayerId))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log("[NetServerManager] 人物数据响应: " + json);
                    var data = JsonUtility.FromJson<CharacterSyncResponse>(json);
                    if (data != null)
                    {
                        equippedCharacterId = data.characterId > 0 ? data.characterId : 3401;
                        characterLevel = data.level > 0 ? data.level : 1;
                        currentCharacterExp = data.exp;
                        Logger.Log($"[NetServerManager] 更新人物数据: CharacterId={equippedCharacterId}, Level={characterLevel}, Exp={currentCharacterExp}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析人物数据失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 获取人物数据失败: " + request.error);
            }
        }

        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(EnsureBasicCharacter());

        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.SyncInventoryFromServer();
            PlayerDataManager.Instance.SyncGoldFromServer();
            Logger.Log("[NetServerManager] 已通知 PlayerDataManager 同步数据");
        }

        if (unlockedCharacters != null && unlockedCharacters.Count > 0)
        {
            var characterList = new List<int>(unlockedCharacters);
            CommunicateEvent.Modify<List<int>>("SyncUnlockedCharacters", characterList);
            Logger.Log($"[NetServerManager] 已发送同步人物列表事件，共 {unlockedCharacters.Count} 个人物");
        }

        SyncUnlockedEquipmentFromServer();
        SyncMallItemsFromServer();

        if (PlayerAniManager.Instance != null && equippedCharacterId > 0)
        {
            Logger.Log($"[NetServerManager] 切换人物动画: characterId={equippedCharacterId}");
            PlayerAniManager.Instance.SwitchCharacter(equippedCharacterId);
        }

        if (isFishBagFull)
        {
            Logger.Log("[NetServerManager] 鱼篓已满，不启动自动钓鱼，播放懒动画");
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
    
    /// <summary>
    /// 确保玩家拥有基础人物3401
    /// 如果没有，则添加基础人物并装备
    /// </summary>
    private IEnumerator EnsureBasicCharacter()
    {
        Logger.Log("[NetServerManager] 开始检查玩家是否拥有基础人物3401...");
        
        // 检查玩家背包中是否有人物3401
        // 人物ID范围是 3401-3500
        if (playerInventory != null && playerInventory.ContainsKey(3401))
        {
            Logger.Log($"[NetServerManager] 玩家已拥有基础人物3401，数量: {playerInventory[3401]}");
        }
        else
        {
            Logger.LogWarning("[NetServerManager] 玩家未拥有基础人物3401，正在添加...");
            
            // 添加基础人物3401到背包
            yield return StartCoroutine(AddCharacterToInventory(3401));
        }
        
        // ========== 关键修复：同时添加到PlayerCharacter表 ==========
        // 添加人物到PlayerCharacter表（人物等级系统），确保CharacterServerManager能找到记录
        yield return StartCoroutine(AddCharacterToPlayerCharacter(3401));
        // ========== 修复结束 ==========
        
        // 检查当前装备的人物是否是有效的
        if (equippedCharacterId < 3401 || equippedCharacterId > 3500)
        {
            Logger.LogWarning($"[NetServerManager] 当前装备的人物ID({equippedCharacterId})无效，正在装备基础人物3401...");
            
            // 如果没有装备有效的人物，装备3401
            yield return StartCoroutine(SendEquipRequest((int)EquipmentSlotType.Character, 3401));
        }
        else
        {
            Logger.Log($"[NetServerManager] 当前装备的人物ID有效: {equippedCharacterId}");
        }
        
        Logger.Log("[NetServerManager] 基础人物检查完成");
    }
    
    /// <summary>
    /// 添加人物到玩家背包
    /// </summary>
    private IEnumerator AddCharacterToInventory(int characterId)
    {
        string url = serverUrl + "/api/player/inventory/add";
        string jsonData = $"{{\"playerId\":{_currentPlayerId},\"itemId\":{characterId},\"quantity\":1}}";
        
        Logger.Log($"[NetServerManager] 添加人物到背包: {jsonData}");
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 添加人物响应: {responseText}");
                
                try
                {
                    var response = JsonUtility.FromJson<AddItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功添加人物 {characterId} 到背包");
                        
                        // 更新本地背包数据
                        if (playerInventory.ContainsKey(characterId))
                        {
                            playerInventory[characterId] += 1;
                        }
                        else
                        {
                            playerInventory[characterId] = 1;
                        }
                        
                        // 通知背包数据更新
                        if (PlayerDataManager.Instance != null)
                        {
                            PlayerDataManager.Instance.SyncInventoryFromServer();
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 添加人物失败: {response?.message ?? "未知错误"}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析添加人物响应失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 添加人物请求失败: {request.error}");
            }
        }
    }
    
    /// <summary>
    /// 添加物品响应
    /// </summary>
    [System.Serializable]
    private class AddItemResponse
    {
        public bool success;
        public string message;
    }
    
    /// <summary>
    /// 添加人物到PlayerCharacter表（人物等级系统）
    /// </summary>
    private IEnumerator AddCharacterToPlayerCharacter(int characterId)
    {
        string url = serverUrl + "/api/player/character/add";
        string jsonData = $"{{\"playerId\":{_currentPlayerId},\"characterId\":{characterId}}}";
        
        Logger.Log($"[NetServerManager] 添加人物到PlayerCharacter表: {jsonData}");
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 添加人物到PlayerCharacter响应: {responseText}");
                
                try
                {
                    var response = JsonUtility.FromJson<AddItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功添加人物 {characterId} 到PlayerCharacter表");
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 添加人物到PlayerCharacter失败: {response?.message ?? "未知错误"}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析添加人物到PlayerCharacter响应失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 添加人物到PlayerCharacter请求失败: {request.error}");
            }
        }
    }
    
    /// <summary>
    /// 解锁技能（通过广告等方式）
    /// </summary>
    public void UnlockSkill(int skillId, System.Action<bool> callback)
    {
        StartCoroutine(UnlockSkillCoroutine(skillId, callback));
    }
    
    /// <summary>
    /// 解锁人物（通过广告等方式）
    /// </summary>
    public void UnlockCharacter(int characterId, System.Action<bool> callback)
    {
        StartCoroutine(UnlockCharacterCoroutine(characterId, callback));
    }
    
    private IEnumerator UnlockCharacterCoroutine(int characterId, System.Action<bool> callback)
    {
        string url = serverUrl + "/api/player/character/add";
        string jsonData = $"{{\"playerId\":{_currentPlayerId},\"characterId\":{characterId}}}";
        
        Logger.Log($"[NetServerManager] 解锁人物请求: {jsonData}");
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 解锁人物响应: {responseText}");
                
                try
                {
                    var response = JsonUtility.FromJson<AddItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功解锁人物 {characterId}");
                        callback?.Invoke(true);
                        
                        // 刷新背包数据
                        PlayerDataManager.Instance.SyncInventoryFromServer();
                        
                        // 同步人物列表
                        SyncUnlockedCharactersFromServer();
                        
                        // 同步已解锁装备列表
                        SyncUnlockedEquipmentFromServer();
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 解锁人物失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析解锁人物响应失败: {ex.Message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 解锁人物请求失败: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    private IEnumerator UnlockSkillCoroutine(int skillId, System.Action<bool> callback)
    {
        string url = serverUrl + "/api/player/skills/unlock";
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"ComponentId\":{skillId}}}";
        
        Logger.Log($"[NetServerManager] 解锁技能请求: {jsonData}");
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 解锁技能响应: {responseText}");
                
                try
                {
                    var response = JsonUtility.FromJson<AddItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功解锁技能 {skillId}");
                        callback?.Invoke(true);
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 解锁技能失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析解锁技能响应失败: {ex.Message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 解锁技能请求失败: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// 升级技能
    /// </summary>
    public void UpgradeSkill(int skillId, int newLevel, System.Action<bool> callback)
    {
        StartCoroutine(UpgradeSkillCoroutine(skillId, newLevel, callback));
    }

    private IEnumerator UpgradeSkillCoroutine(int skillId, int newLevel, System.Action<bool> callback)
    {
        string url = serverUrl + "/api/player/skills/upgrade";
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"ComponentId\":{skillId},\"NewLevel\":{newLevel}}}";
        
        Logger.Log($"[NetServerManager] 升级技能请求: {jsonData}");
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 升级技能响应: {responseText}");
                
                try
                {
                    var response = JsonUtility.FromJson<AddItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功升级技能 {skillId} 到等级 {newLevel}");
                        callback?.Invoke(true);
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 升级技能失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析升级技能响应失败: {ex.Message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 升级技能请求失败: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    private void AutoStartFishing()
    {
        try
        {
            if (isAutoFishing)
            {
                Logger.Log("[NetServerManager] 已在自动钓鱼状态，无需重复启动");
                return;
            }

            int defaultBaitId = 2501;
            Logger.Log($"[NetServerManager] 登录成功，自动开始钓鱼，使用鱼饵ID: {defaultBaitId}");
            StartAutoFishing(defaultBaitId);
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning("[NetServerManager] 自动开始钓鱼失败: " + ex.Message);
        }
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
        Logger.LogColor("[NetServerManager] 正在连接到服务器: " + serverUrl, "yellow");

        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/ping"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Logger.LogColor("[NetServerManager] 连接服务器成功", "green");
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
                Logger.LogError("[NetServerManager] 连接服务器失败: " + request.error);
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
            Logger.LogColor("[NetServerManager] 请求成功: " + endpoint, "cyan");
            try
            {
                string jsonResponse = request.downloadHandler.text;
                T? response = NetUtils.ParseJson<T>(jsonResponse);
                onSuccess?.Invoke(response);
            }
            catch (System.Exception ex)
            {
                Logger.LogError("[NetServerManager] 解析响应失败: " + ex.Message);
                onError?.Invoke("解析响应失败");
            }
        }
        else
        {
            Logger.LogError("[NetServerManager] 请求失败: " + endpoint + ", 错误: " + request.error);
            UIManager.Instance?.ShowTip("网络请求失败，请检查网络连接");
            onError?.Invoke(request.error);
        }
    }

    private bool CheckNetworkConnection()
    {
        if (!_isEnabled)
        {
            Logger.LogWarning("[NetServerManager] 网络管理器未启用");
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
        Logger.LogError("[NetServerManager] 网络连接失败，请检查网络连接后重试");
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
                    Logger.Log("[NetServerManager] 等待心跳响应，未收到响应次数: " + missedHeartbeats);
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

        // 每帧更新连续模式剩余时间（基于服务器时间）
        UpdateContinuousModeRemainingTime();
    }

    private void CheckHeartbeatTimeout()
    {
        if (!_isEnabled)
            return;

        if (missedHeartbeats >= NetUtils.MAX_MISSED_HEARTBEATS)
        {
            Logger.LogError("[NetServerManager] 心跳超时，断开连接");
            networkState = NetUtils.NetworkState.Reconnecting;
            isConnected = false;
            missedHeartbeats = 0;
            UIManager.Instance?.ShowTip("网络连接断开，正在尝试重新连接...");
        }
    }

    private IEnumerator SendHeartbeatRequest()
    {
        Logger.Log("[NetServerManager] SendHeartbeat 被调用");

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
                    Logger.Log("[NetServerManager] OnHeartbeatResponse 收到心跳响应");
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
                Logger.LogWarning("[NetServerManager] 心跳请求失败: " + error);
                isConnected = false;
            });
    }

    public void OnAddItem((int itemId, int quantity) data)
    {
        if (!CheckNetworkConnection())
            return;
        Logger.Log("[NetServerManager] 处理添加物品请求: itemId=" + data.itemId + ", quantity=" + data.quantity);

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
        Logger.Log("[NetServerManager] 处理移除物品请求: itemId=" + data.itemId + ", quantity=" + data.quantity);

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
        Logger.Log("[NetServerManager] 处理添加鱼请求: fishId=" + data.fishId + ", quantity=" + data.quantity);

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
        Logger.Log($"[NetServerManager] 处理售卖鱼请求: itemIds={string.Join(",", itemIds)}, totalPrice={totalPrice}");

        var requestData = new Dictionary<string, object>
        {
            { "itemIds", itemIds },
            { "totalPrice", totalPrice }
        };

        StartCoroutine(SendRequest<object>($"/api/player/fish-bag/{_currentPlayerId}/sell", requestData,
            (response) =>
            {
                Logger.Log("[NetServerManager] 售卖鱼成功");

                // ========== 修复：售卖成功后，重新从服务器获取最新数据 ==========
                // 避免手动递减导致客户端与服务端数据不一致
                StartCoroutine(FetchPlayerDataAfterSell(itemIds, totalPrice));
            },
            (error) =>
            {
                Logger.LogWarning("[NetServerManager] 售卖鱼失败: " + error);
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
                    Logger.Log("[NetServerManager] 售卖后鱼篓数据响应: " + json);
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        fishInventory.Clear();
                        foreach (var item in data.items)
                        {
                            fishInventory[item.key] = item.value;
                        }
                        int totalFish = fishInventory.Values.Sum();
                        Logger.Log("[NetServerManager] 售卖后更新鱼篓: " + fishInventory.Count + " 种鱼，总数量: " + totalFish);

                        // 更新鱼篓满状态
                        isFishBagFull = totalFish >= fishBagCapacity;
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 售卖后解析鱼篓数据失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 售卖后获取鱼篓数据失败: " + request.error);
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
                        Logger.Log("[NetServerManager] 售卖后更新金币: " + playerGold);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 售卖后解析金币数据失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 售卖后获取金币数据失败: " + request.error);
            }
        }

        // 更新鱼篓满状态
        isFishBagFull = fishInventory.Values.Sum() >= fishBagCapacity;
        if (!isFishBagFull && !isPlayingReelAnimation)
        {
            NotifyPlayIdleAnimation();
        }

        // ========== 修复：鱼篓空了之后自动重新启动自动钓鱼 ==========
        if (!isFishBagFull && !isAutoFishing)
        {
            Logger.Log("[NetServerManager] 鱼篓已空，自动重新启动自动钓鱼");
            AutoStartFishing();
        }

        // ========== 修复：通知 PlayerDataManager 同步数据，触发UI刷新 ==========
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.SyncInventoryFromServer();
            PlayerDataManager.Instance.SyncGoldFromServer();
            Logger.Log("[NetServerManager] 售卖后已通知 PlayerDataManager 同步数据");
        }
    }

    /// <summary>
    /// 处理装备解锁请求
    /// </summary>
    public void OnUnlockEquipment(int equipId)
    {
        if (!CheckNetworkConnection())
            return;

        Logger.Log($"[NetServerManager] 处理装备解锁请求: equipId={equipId}");

        // 确定装备类型
        string equipmentType = GetEquipmentType(equipId);
        if (string.IsNullOrEmpty(equipmentType))
        {
            Logger.LogWarning($"[NetServerManager] 无法确定装备类型: equipId={equipId}");
            UIManager.Instance?.ShowTip("装备类型错误");
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
                
                // 重新获取玩家数据
                StartCoroutine(FetchPlayerDataAfterUnlock());
            },
            (error) =>
            {
                Logger.LogWarning($"[NetServerManager] 装备解锁失败: {error}");
                UIManager.Instance?.ShowTip("解锁失败，请重试");
            }));
    }

    /// <summary>
    /// 根据装备ID确定装备类型
    /// </summary>
    private string GetEquipmentType(int equipId)
    {
        if (equipId >= 3001 && equipId <= 3099)
            return "Rod";
        else if (equipId >= 3101 && equipId <= 3199)
            return "Line";
        else if (equipId >= 3201 && equipId <= 3299)
            return "Hook";
        else if (equipId >= 3401 && equipId <= 3499)
            return "Character";
        else if (equipId >= 3301 && equipId <= 3399)
            return "Skill";
        else
            return null;
    }

    /// <summary>
    /// 解锁装备后重新获取玩家数据
    /// </summary>
    private System.Collections.IEnumerator FetchPlayerDataAfterUnlock()
    {
        // 等待一帧，确保服务器数据已更新
        yield return null;

        // 重新获取玩家背包数据
        yield return StartCoroutine(FetchPlayerData());

        // 触发装备列表刷新事件
        CommunicateEvent.Modify("Equipment_Refresh");

        // 触发人物数据刷新事件（如果是人物解锁）
        CommunicateEvent.Modify("Character_Refresh");

        // 触发背包数据变更事件，通知所有监听者
        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, (0, 0));

        // 显示解锁成功提示
        UIManager.Instance?.ShowTip("解锁成功！");

        Logger.Log("[NetServerManager] 装备解锁后已刷新玩家数据");
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
                    Logger.Log("[NetServerManager] 从服务器获取鱼篓数据: " + json);
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        fishInventory.Clear();
                        foreach (var item in data.items)
                        {
                            fishInventory[item.key] = item.value;
                        }
                        int totalFish = fishInventory.Values.Sum();
                        Logger.Log("[NetServerManager] 服务器鱼篓数据: " + fishInventory.Count + " 种鱼，总数量: " + totalFish);

                        // 更新鱼篓满状态
                        isFishBagFull = totalFish >= fishBagCapacity;
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析服务器鱼篓数据失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 获取服务器鱼篓数据失败: " + request.error);
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

        // 如果没有传入 baitId，使用装备的鱼饵
        int actualBaitId = baitId;
        if (actualBaitId == 0 && equippedBaitId != 0)
        {
            actualBaitId = equippedBaitId;
        }

        int sceneId = GetCurrentSceneId();

        NetUtils.LogRequest("DoFishing", new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", actualBaitId }
        });

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", actualBaitId }
        };
        StartCoroutine(DoFishingCoroutine("/api/fishing/catch", requestData));
    }

    private IEnumerator DoFishingCoroutine(string url, Dictionary<string, object> requestData)
    {
        if (!isConnected)
        {
            Logger.LogWarning("[NetServerManager] 未连接到服务器，无法钓鱼");
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
                        Logger.Log("[NetServerManager] 钓鱼成功: " + response.fishName + " (" + response.weight + "kg)");

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
                        Logger.LogWarning("[NetServerManager] 钓鱼失败: " + (response?.message ?? "未知错误"));
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("[NetServerManager] 解析钓鱼响应失败: " + ex.Message);
                }
            }
            else
            {
                Logger.LogError("[NetServerManager] 钓鱼请求失败: " + request.error);
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
            Logger.Log("[NetServerManager] 正在播放 Reel 动画，忽略 Idle 请求");
            return;
        }
        Logger.Log("[NetServerManager] 通知播放Idle动画");
        PlayerAniManager.Instance?.PlayIdleAnimation();
    }

    public void NotifyPlayLazyAnimation()
    {
        if (isPlayingReelAnimation)
        {
            Logger.Log("[NetServerManager] 正在播放 Reel 动画，忽略 Lazy 请求");
            return;
        }
        Logger.Log("[NetServerManager] 通知播放Lazy动画");
        PlayerAniManager.Instance?.PlayLazyAnimation();
    }

    public void NotifyPlayReelAnimation(float struggleTime, System.Action onComplete)
    {
        if (isPlayingReelAnimation)
        {
            Logger.Log("[NetServerManager] 已在播放 Reel 动画，忽略新请求");
            onComplete?.Invoke();
            return;
        }

        isPlayingReelAnimation = true;
        // 记录挣扎开始时间和时长，用于显示倒计时
        struggleStartTime = Time.time;
        currentStruggleTime = struggleTime;

        Logger.Log($"[NetServerManager] 通知播放Reel动画，挣扎时间: {struggleTime}秒");

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
                Logger.Log("[NetServerManager] 鱼篓界面已打开，强制刷新");
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
        Logger.Log("[NetServerManager] 通知同步背包数据");
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
            Logger.Log("[NetServerManager] 延迟刷新鱼篓UI完成");
        }
    }

    public void NotifyAddFish(int fishId, int quantity)
    {
        Logger.Log("[NetServerManager] 通知添加鱼: fishId=" + fishId + ", quantity=" + quantity);
    }

    public void NotifyRefreshUI()
    {
        Logger.Log("[NetServerManager] 通知刷新UI");
        PlayerDataManager.Instance?.RefreshUI();
    }

    public void NotifyShowCatchResult(string itemName, float weight, Sprite icon)
    {
        Logger.Log("[NetServerManager] 通知显示捕获结果: " + itemName);
        UIManager.Instance?.ShowCatchResult(itemName, weight, icon);
    }

    /// <summary>
    /// 从服务器钓获信息显示MainTile
    /// </summary>
    private void ShowCatchResultFromServer(LastCatchInfo catchInfo)
    {
        if (catchInfo == null)
            return;

        Logger.Log($"[NetServerManager] 显示钓获结果: {catchInfo.fishName}, 重量: {catchInfo.weight}kg");

        // 获取物品图标
        Sprite icon = GetItemIcon(catchInfo.fishId);

        UIManager.Instance?.ShowCatchResult(catchInfo.fishName, catchInfo.weight, icon);
        
        // 钓获后同步人物经验数据
        SyncCharacterDataFromServer();
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

        // 如果没有传入 baitId，使用装备的鱼饵
        int actualBaitId = baitId;
        if (actualBaitId == 0 && equippedBaitId != 0)
        {
            actualBaitId = equippedBaitId;
        }

        // 先检查鱼篓是否已满
        if (isFishBagFull)
        {
            Logger.Log("[NetServerManager] 鱼篓已满，无法启动自动钓鱼");
            NotifyPlayLazyAnimation();
            return;
        }

        int sceneId = GetCurrentSceneId();

        NetUtils.LogRequest("StartAutoFishing", new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", actualBaitId }
        });

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "sceneId", sceneId },
            { "baitId", actualBaitId }
        };
        StartCoroutine(SendRequest<AutoFishingResponse>("/api/fishing/auto/start", requestData, (response) =>
        {
            if (response != null && response.success)
            {
                isAutoFishing = true;
                Logger.Log("[NetServerManager] 自动钓鱼已启动");
            }
            else
            {
                Logger.LogWarning("[NetServerManager] 启动自动钓鱼失败: " + (response?.message ?? "未知错误"));
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
                Logger.Log("[NetServerManager] 自动钓鱼已停止");
            }
            else
            {
                Logger.LogWarning("[NetServerManager] 停止自动钓鱼失败: " + (response?.message ?? "未知错误"));
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
                            
                            // ========== 修复：直接使用服务器返回的 continuousModeRemainingTime ==========
                            // 如果服务器返回的值大于0，使用服务器的值
                            if (response.continuousModeRemainingTime > 0)
                            {
                                continuousModeRemainingTime = response.continuousModeRemainingTime;
                                isInContinuousMode = true;
                                baitEndTimeIsSeconds = true;  // 标记为使用 remainingTime 计时
                            }
                            else
                            {
                                // 如果服务器返回0，检查本地倒计时
                                if (continuousModeRemainingTime > 0)
                                {
                                    // 本地递减
                                    continuousModeRemainingTime -= 2f; // 轮询间隔为2秒
                                    if (continuousModeRemainingTime <= 0)
                                    {
                                        continuousModeRemainingTime = 0;
                                        isInContinuousMode = false;
                                        baitEndTimeIsSeconds = false;
                                    }
                                }
                                else
                                {
                                    isInContinuousMode = false;
                                    baitEndTimeIsSeconds = false;
                                }
                            }
                            
                            currentFishingMode = continuousModeRemainingTime > 0 ? "Continuous" : "Normal";
                            
                            // 调试日志
                            Logger.Log($"[NetServerManager] 轮询钓鱼状态: continuousModeRemainingTime={continuousModeRemainingTime:F1}, isInContinuousMode={isInContinuousMode}, currentFishingMode={currentFishingMode}");

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

                                Logger.Log($"[NetServerManager] 检测到新钓获: {response.lastCatch.fishName} (ID:{response.lastCatch.fishId}), 重量:{response.lastCatch.weight}kg, 挣扎时间:{struggleTime}秒");

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
                                        Logger.Log("[NetServerManager] Reel动画结束");

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

                                Logger.Log($"[NetServerManager] 恢复收竿动画状态，挣扎时间: {currentStruggleTime}秒");

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
                                        Logger.Log("[NetServerManager] 鱼篓已满，切换到懒动画");
                                        NotifyPlayLazyAnimation();
                                    }
                                }
                                else
                                {
                                    if (wasFull || wasPaused)
                                    {
                                        Logger.Log("[NetServerManager] 鱼篓未满，切换到空闲动画");
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

                            // ========== 天气和时间同步 ==========
                            // 服务器只传输ID，客户端根据配置表显示名称
                            if (response.currentWeatherId > 0)
                            {
                                currentWeatherId = response.currentWeatherId;
                                currentWeatherName = GetWeatherNameById(response.currentWeatherId);
                                // 通知EnvManager和UI更新天气显示（使用客户端事件）
                                CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_WEATHER_CHANGED, new Dictionary<string, object>
                                {
                                    { "weatherId", currentWeatherId },
                                    { "weatherName", currentWeatherName }
                                });
                                Logger.Log($"[NetServerManager] 更新天气: ID={currentWeatherId}, 名称={currentWeatherName}");
                            }
                            
                            if (response.timeSlotId > 0)
                            {
                                currentTimeSlotId = response.timeSlotId;
                                currentTimeSlotName = GetTimeSlotNameById(response.timeSlotId);
                                currentTimeStatus = (TimeStatus)response.timeStatus;
                                // 通知EnvManager和UI更新时间显示（使用客户端事件）
                                CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_TIME_SLOT_CHANGED, new Dictionary<string, object>
                                {
                                    { "timeSlotId", currentTimeSlotId },
                                    { "timeSlotName", currentTimeSlotName },
                                    { "timeStatus", (int)currentTimeStatus },
                                    { "weatherId", currentWeatherId } // 同时传递当前天气ID
                                });
                                Logger.Log($"[NetServerManager] 更新时间: ID={currentTimeSlotId}, 名称={currentTimeSlotName}, 状态={currentTimeStatus}");
                            }

                            Logger.Log("[NetServerManager] 更新钓鱼状态: 自动钓鱼=" + isAutoFishing +
                                     ", 停滞=" + isPaused + ", 鱼篓满=" + isFishBagFull +
                                     ", 垃圾连续=" + trashStreak + ", 鱼篓总数=" + GetTotalFishCount() +
                                     ", 下次钓鱼=" + nextFishingDisplay);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("[NetServerManager] 解析钓鱼状态失败: " + ex.Message);
                    }
                }
                else
                {
                    Logger.LogWarning("[NetServerManager] 获取钓鱼状态失败: " + request.error);
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

        Logger.LogColor("[NetServerManager] 尝试重新连接...", "orange");
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
        Logger.LogColor("[NetServerManager] 已断开连接", "red");
    }

    /// <summary>
    /// 解锁装备API
    /// </summary>
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

    /// <summary>
    /// 解锁装备响应
    /// </summary>
    private class UnlockEquipmentResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
    }

    // ==================== 商城相关方法 ====================

    /// <summary>
    /// 处理购买商城物品事件
    /// </summary>
    private void OnPurchaseMallItem((int itemId, int quantity) request)
    {
        var (itemId, quantity) = request;
        Logger.Log($"[NetServerManager] OnPurchaseMallItem - itemId={itemId}, quantity={quantity}");
        PurchaseMallItem(itemId, quantity, (success, message) =>
        {
            if (success)
            {
                Logger.Log($"[NetServerManager] 购买成功: {message}");
            }
            else
            {
                Logger.LogWarning($"[NetServerManager] 购买失败: {message}");
            }
        });
    }

    /// <summary>
    /// 商城物品数据缓存
    /// </summary>
    private Dictionary<int, MallItemData> mallItems = new Dictionary<int, MallItemData>();

    /// <summary>
    /// 同步商城物品列表
    /// </summary>
    public void SyncMallItemsFromServer()
    {
        StartCoroutine(SyncMallItemsCoroutine());
    }

    private IEnumerator SyncMallItemsCoroutine()
    {
        string url = serverUrl + "/api/player/mall/items";
        Logger.Log($"[NetServerManager] 同步商城物品列表: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log("[NetServerManager] 商城物品列表响应: " + json);

                    // 解析JSON
                    var response = JsonUtility.FromJson<MallItemsResponse>(json);
                    if (response != null && response.success && response.items != null)
                    {
                        mallItems.Clear();
                        foreach (var item in response.items)
                        {
                            mallItems[item.itemId] = new MallItemData
                            {
                                id = item.itemId,
                                price = item.price,
                                stock = item.stock
                            };
                            Logger.Log($"[NetServerManager] 商城物品: ID={item.itemId}, 价格={item.price}, 库存={item.stock}");
                        }

                        Logger.Log($"[NetServerManager] 同步商城物品列表完成，共 {mallItems.Count} 个商品");

                        // 通知商城数据更新
                        CommunicateEvent.Modify<Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_MALL_DATA_CHANGED, mallItems);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析商城物品列表失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 获取商城物品列表失败: {request.error}");
            }
        }
    }

    /// <summary>
    /// 获取商城物品列表
    /// </summary>
    public Dictionary<int, MallItemData> GetMallItems()
    {
        return new Dictionary<int, MallItemData>(mallItems);
    }

    /// <summary>
    /// 获取单个商城物品
    /// </summary>
    public MallItemData GetMallItem(int itemId)
    {
        return mallItems.ContainsKey(itemId) ? mallItems[itemId] : null;
    }

    /// <summary>
    /// 购买商城物品
    /// </summary>
    public void PurchaseMallItem(int itemId, int quantity, System.Action<bool, string> callback)
    {
        StartCoroutine(PurchaseMallItemCoroutine(itemId, quantity, callback));
    }

    private IEnumerator PurchaseMallItemCoroutine(int itemId, int quantity, System.Action<bool, string> callback)
    {
        string url = serverUrl + "/api/player/mall/purchase";
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"ItemId\":{itemId},\"Quantity\":{quantity}}}";

        Logger.Log($"[NetServerManager] 购买商城物品请求: {jsonData}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 购买商城物品响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<PurchaseMallItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功购买物品 {itemId}, 数量 {quantity}, 总价 {response.totalPrice}");
                        callback?.Invoke(true, response.message);

                        // ========== 新增：立即更新本地背包数据 ==========
                        if (playerInventory.ContainsKey(itemId))
                        {
                            playerInventory[itemId] += quantity;
                            Logger.Log($"[NetServerManager] 本地背包数据更新: ItemId={itemId}, 新数量={playerInventory[itemId]}");
                        }
                        else
                        {
                            playerInventory[itemId] = quantity;
                            Logger.Log($"[NetServerManager] 本地背包数据新增: ItemId={itemId}, 数量={quantity}");
                        }
                        // ========== 本地数据更新结束 ==========

                        // 刷新背包数据
                        PlayerDataManager.Instance?.SyncInventoryFromServer();

                        // 通知商城数据更新
                        CommunicateEvent.Modify("Mall_PurchaseSuccess", itemId);

                        // 通知背包刷新
                        CommunicateEvent.Modify("Bag_RefreshItems");

                        // ========== 新增：专门通知物品数量变化 ==========
                        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, (itemId, playerInventory[itemId]));

                        // 如果是窝料(2501)，专门通知窝料数量更新
                        if (itemId == 2501)
                        {
                            CommunicateEvent.Modify("BaitCountChanged");
                            Logger.Log("[NetServerManager] 发送窝料数量更新事件");

                            // 立即同步连续模式状态
                            StartCoroutine(SyncContinuousModeStatusCoroutine());
                        }

                        // 如果是鱼饵(2001-2007)，通知鱼饵数量更新
                        if (itemId >= 2001 && itemId <= 2007)
                        {
                            CommunicateEvent.Modify("BaitCountChanged");
                            CommunicateEvent.Modify("BaitDataUpdated");
                            Logger.Log($"[NetServerManager] 发送鱼饵数量更新事件: itemId={itemId}");
                        }
                        // ========== 新增结束 ==========

                        // 刷新商城数据
                        SyncMallItemsFromServer();
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 购买商城物品失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false, response?.message ?? "购买失败");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析购买商城物品响应失败: {ex.Message}");
                    callback?.Invoke(false, "解析响应失败");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 购买商城物品请求失败: {request.error}");
                callback?.Invoke(false, request.error);
            }
        }
    }

    /// <summary>
    /// 商城物品列表响应
    /// </summary>
    [System.Serializable]
    private class MallItemsResponse
    {
        public bool success;
        public MallItemDataJson[] items;
    }

    [System.Serializable]
    private class MallItemDataJson
    {
        public int itemId;
        public int price;
        public int stock;
    }

    /// <summary>
    /// 购买商城物品响应
    /// </summary>
    [System.Serializable]
    private class PurchaseMallItemResponse
    {
        public bool success;
        public string message;
        public int totalPrice;
        public int remainingGold;
    }

    // ==================== 窝料/连续钓鱼模式相关方法 ====================

    /// <summary>
    /// 窝料结束时间戳（服务器时间，秒）
    /// </summary>
    private long baitEndTime = 0;
    
    /// <summary>
    /// baitEndTime 是否为剩余秒数标记
    /// </summary>
    private bool baitEndTimeIsSeconds = false;

    // ==================== 天气和时间相关字段 ====================
    private int currentWeatherId = 0;          // 当前天气ID
    private string currentWeatherName = "";    // 当前天气名称
    private int currentTimeSlotId = 0;         // 当前时间段ID
    private string currentTimeSlotName = "";   // 当前时间段名称
    private TimeStatus currentTimeStatus = TimeStatus.Daytime; // 当前时间状态

    /// <summary>
    /// 处理消耗窝料并进入连续钓鱼模式事件
    /// </summary>
    private void OnConsumeBaitAndEnterContinuousMode()
    {
        Logger.Log("[NetServerManager] OnConsumeBaitAndEnterContinuousMode - 准备增加窝料时间");
        StartCoroutine(AddBaitTimeCoroutine());
    }

    /// <summary>
    /// 发送增加窝料时间请求到服务器
    /// 服务器逻辑：如果没有使用窝料，设置当前时间为起点；如果已使用，加30秒
    /// 返回窝料结束时间戳
    /// </summary>
    private IEnumerator AddBaitTimeCoroutine()
    {
        // 先尝试新的API
        string newUrl = serverUrl + "/api/game/add-bait-time";
        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "addSeconds", 30 }
        };

        Logger.Log($"[NetServerManager] 调用增加窝料时间API: {newUrl}");

        string jsonData = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(newUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 增加窝料时间响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<AddBaitTimeResponse>(responseText);
                    if (response != null && response.success)
                    {
                        // 优先使用服务器返回的 remainingTime（剩余秒数）
                        if (response.remainingTime > 0)
                        {
                            continuousModeRemainingTime = response.remainingTime;
                            isInContinuousMode = true;
                            currentFishingMode = "Continuous";
                            
                            // 如果 baitEndTime 是有效的时间戳，保存它
                            if (response.baitEndTime > 1000000000)
                            {
                                // baitEndTime 是有效的Unix时间戳
                                baitEndTime = response.baitEndTime;
                                baitEndTimeIsSeconds = false;
                                Logger.Log($"[NetServerManager] 成功增加窝料时间，剩余时间: {continuousModeRemainingTime}秒, 结束时间戳: {baitEndTime}");
                            }
                            else
                            {
                                // baitEndTime 是剩余秒数或无效，不保存它
                                baitEndTime = 0;
                                baitEndTimeIsSeconds = true;
                                Logger.Log($"[NetServerManager] 成功增加窝料时间，剩余时间: {continuousModeRemainingTime}秒（使用remainingTime计时）");
                            }
                        }
                        else if (response.baitEndTime > 0)
                        {
                            // 旧服务器可能只返回 baitEndTime，需要判断是时间戳还是剩余秒数
                            baitEndTime = response.baitEndTime;
                            isInContinuousMode = true;
                            currentFishingMode = "Continuous";
                            
                            // 根据服务器时间计算剩余时间
                            UpdateContinuousModeRemainingTime();
                            
                            Logger.Log($"[NetServerManager] 成功增加窝料时间，结束时间戳: {baitEndTime}, 当前剩余时间: {continuousModeRemainingTime}秒");
                        }
                        else
                        {
                            Logger.LogWarning("[NetServerManager] 增加窝料时间成功但未返回有效时间数据");
                            yield break;
                        }

                        // 同步更新背包数据（窝料已消耗）
                        PlayerDataManager.Instance?.SyncInventoryFromServer();

                        // 通知背包刷新
                        CommunicateEvent.Modify("Bag_RefreshItems");

                        // 通知UI更新
                        UpdateContinuousModeUI();
                        yield break;
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 增加窝料时间失败: {response?.message ?? "未知错误"}");
                        UIManager.ShowMessage(response?.message ?? "操作失败");
                        yield break;
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析增加窝料时间响应失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogWarning($"[NetServerManager] 新API不可用，尝试使用旧API: {request.error}");
            }
        }

        // 如果新API不可用，使用旧的进入连续模式API
        Logger.Log("[NetServerManager] 使用旧API进入连续钓鱼模式");
        yield return StartCoroutine(EnterContinuousModeWithBaitEndTimeCoroutine());
    }

    /// <summary>
    /// 使用旧API进入连续钓鱼模式，但同时获取baitEndTime
    /// </summary>
    private IEnumerator EnterContinuousModeWithBaitEndTimeCoroutine()
    {
        string url = serverUrl + "/api/game/enter-continuous-mode";

        Logger.Log($"[NetServerManager] 调用进入连续钓鱼模式API（旧）: {url}");

        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 进入连续钓鱼模式响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<EnterContinuousModeWithBaitEndTimeResponse>(responseText);
                    if (response != null && response.success)
                    {
                        // 优先使用 remainingTime
                        if (response.remainingTime > 0)
                        {
                            continuousModeRemainingTime = response.remainingTime;
                            isInContinuousMode = true;
                            currentFishingMode = "Continuous";
                            
                            // 如果 baitEndTime 是有效的时间戳，保存它
                            if (response.baitEndTime > 1000000000)
                            {
                                baitEndTime = response.baitEndTime;
                                baitEndTimeIsSeconds = false;
                                Logger.Log($"[NetServerManager] 成功进入连续钓鱼模式，剩余时间: {continuousModeRemainingTime}秒, 结束时间戳: {baitEndTime}");
                            }
                            else
                            {
                                // baitEndTime 是剩余秒数或无效，不保存它
                                baitEndTime = 0;
                                baitEndTimeIsSeconds = true;
                                Logger.Log($"[NetServerManager] 成功进入连续钓鱼模式，剩余时间: {continuousModeRemainingTime}秒（使用remainingTime计时）");
                            }
                        }
                        else if (response.baitEndTime > 0)
                        {
                            // 旧服务器可能只返回 baitEndTime，需要判断是时间戳还是剩余秒数
                            baitEndTime = response.baitEndTime;
                            isInContinuousMode = true;
                            currentFishingMode = "Continuous";
                            
                            // 根据服务器时间计算剩余时间
                            UpdateContinuousModeRemainingTime();
                            Logger.Log($"[NetServerManager] 成功进入连续钓鱼模式，结束时间戳: {baitEndTime}, 当前剩余时间: {continuousModeRemainingTime}秒");
                        }
                        else
                        {
                            Logger.LogWarning("[NetServerManager] 进入连续钓鱼模式成功但未返回有效时间数据");
                            yield break;
                        }

                        // 同步更新背包数据（窝料已消耗）
                        PlayerDataManager.Instance?.SyncInventoryFromServer();

                        // 通知背包刷新
                        CommunicateEvent.Modify("Bag_RefreshItems");

                        // 通知UI更新
                        UpdateContinuousModeUI();
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 进入连续钓鱼模式失败: {response?.message ?? "未知错误"}");
                        UIManager.ShowMessage("窝料不足，无法进入连续钓鱼模式");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析进入连续钓鱼模式响应失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 进入连续钓鱼模式请求失败: {request.error}");
            }
        }
    }

    /// <summary>
    /// 根据服务器时间更新连续模式剩余时间
    /// </summary>
    private void UpdateContinuousModeRemainingTime()
    {
        bool wasInContinuousMode = isInContinuousMode;
        float previousRemainingTime = continuousModeRemainingTime;
        
        // 如果 baitEndTimeIsSeconds = true，说明使用的是 remainingTime 计时
        if (baitEndTimeIsSeconds)
        {
            if (continuousModeRemainingTime > 0)
            {
                // 按帧递减（每帧调用，deltaTime约0.016秒）
                continuousModeRemainingTime -= Time.deltaTime;
                if (continuousModeRemainingTime < 0)
                {
                    continuousModeRemainingTime = 0;
                }
                
                isInContinuousMode = true;
                currentFishingMode = "Continuous";
                
                if (!wasInContinuousMode)
                {
                    Logger.Log($"[NetServerManager] 进入连续模式，剩余时间: {continuousModeRemainingTime:F2}秒");
                }
                
                // 如果倒计时结束
                if (continuousModeRemainingTime <= 0)
                {
                    isInContinuousMode = false;
                    currentFishingMode = "Normal";
                    baitEndTimeIsSeconds = false;
                    
                    if (wasInContinuousMode)
                    {
                        Logger.Log("[NetServerManager] 连续模式结束");
                    }
                }
            }
            else
            {
                // 剩余时间已到
                continuousModeRemainingTime = 0;
                isInContinuousMode = false;
                currentFishingMode = "Normal";
                baitEndTimeIsSeconds = false;
                
                if (wasInContinuousMode)
                {
                    Logger.Log("[NetServerManager] 连续模式结束");
                }
            }
            
            // 如果状态发生变化，通知其他模块
            if (wasInContinuousMode != isInContinuousMode || 
                Mathf.Abs(previousRemainingTime - continuousModeRemainingTime) > 0.1f)
            {
                // 通过事件通知状态变化
                CommunicateEvent.Modify<float>("ContinuousModeTimeUpdated", continuousModeRemainingTime);
            }
            return;
        }
        
        // 以下是使用 baitEndTime 时间戳的逻辑（保留兼容）
        if (baitEndTime <= 0)
        {
            continuousModeRemainingTime = 0;
            isInContinuousMode = false;
            currentFishingMode = "Normal";
            
            if (wasInContinuousMode)
            {
                Logger.Log("[NetServerManager] 连续模式结束");
            }
            return;
        }

        // 获取当前服务器时间（毫秒转秒）
        long currentServerTime = lastServerTime / 1000;
        
        // 如果服务器时间无效，使用客户端时间作为备选
        if (currentServerTime <= 0)
        {
            currentServerTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Logger.LogWarning("[NetServerManager] 服务器时间无效，使用客户端时间");
        }
        
        // baitEndTime 是绝对时间戳，计算剩余时间
        float remaining = baitEndTime - currentServerTime;
        
        // 调试日志
        Logger.Log($"[NetServerManager] 计算剩余时间: baitEndTime={baitEndTime}, currentServerTime={currentServerTime}, remaining={remaining}");
        
        // 直接使用服务器计算的剩余时间（这是最可靠的方式）
        if (remaining > 0)
        {
            continuousModeRemainingTime = remaining;
            isInContinuousMode = true;
            currentFishingMode = "Continuous";
            
            if (!wasInContinuousMode)
            {
                Logger.Log($"[NetServerManager] 进入连续模式，剩余时间: {continuousModeRemainingTime:F2}秒");
            }
        }
        else
        {
            // 剩余时间已到
            continuousModeRemainingTime = 0;
            isInContinuousMode = false;
            currentFishingMode = "Normal";
            baitEndTime = 0;
            
            if (wasInContinuousMode)
            {
                Logger.Log("[NetServerManager] 连续模式结束");
            }
        }
        
        // 如果状态发生变化，通知其他模块
        if (wasInContinuousMode != isInContinuousMode || 
            Mathf.Abs(previousRemainingTime - continuousModeRemainingTime) > 0.1f)
        {
            // 通过事件通知状态变化
            CommunicateEvent.Modify<float>("ContinuousModeTimeUpdated", continuousModeRemainingTime);
        }
    }

    /// <summary>
    /// 同步连续钓鱼模式状态
    /// </summary>
    public void SyncContinuousModeStatus()
    {
        StartCoroutine(SyncContinuousModeStatusCoroutine());
    }

    private IEnumerator SyncContinuousModeStatusCoroutine()
    {
        string url = serverUrl + "/api/game/continuous-mode/status";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<ContinuousModeStatus>(json);

                    if (response != null)
                    {
                        // 保存服务器返回的窝料结束时间戳
                        baitEndTime = response.baitEndTime;
                        
                        // 根据服务器时间计算剩余时间
                        UpdateContinuousModeRemainingTime();

                        Logger.Log($"[NetServerManager] 同步连续模式状态: isInContinuousMode={isInContinuousMode}, remainingTime={continuousModeRemainingTime}, baitEndTime={baitEndTime}");

                        // ========== 添加UI更新 ==========
                        // 通过事件通知UI更新倒计时
                        CommunicateEvent.Modify<float>("ContinuousModeTimeUpdated", continuousModeRemainingTime);

                        // 尝试查找并更新窝料倒计时UI
                        try
                        {
                            GameObject countdownObj = GameObject.Find("Canvas/BaitCountdownText");
                            if (countdownObj != null)
                            {
                                var countdownText = countdownObj.GetComponent<UnityEngine.UI.Text>();
                                if (countdownText != null)
                                {
                                    if (isInContinuousMode && continuousModeRemainingTime > 0)
                                    {
                                        int minutes = (int)(continuousModeRemainingTime / 60);
                                        int seconds = (int)(continuousModeRemainingTime % 60);
                                        countdownText.text = $"{minutes:00}:{seconds:00}";
                                        Logger.Log($"[NetServerManager] 更新倒计时UI: {minutes:00}:{seconds:00}");
                                    }
                                    else
                                    {
                                        countdownText.text = "00:00";
                                    }
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogWarning($"[NetServerManager] 更新倒计时UI失败: {ex.Message}");
                        }
                        // ========== UI更新结束 ==========
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析连续模式状态失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 获取连续模式状态失败: {request.error}");
            }
        }
    }

    /// <summary>
    /// 更新连续模式UI显示
    /// </summary>
    private void UpdateContinuousModeUI()
    {
        // 通过 CommunicateEvent 通知UI更新连续模式时间
        CommunicateEvent.Modify<float>("ContinuousModeTimeUpdated", continuousModeRemainingTime);

        // 如果有直接引用，也可以直接更新
        if (UIManager.Instance != null)
        {
            // 查找倒计时文本组件（根据实际UI结构调整）
            var countdownText = UIManager.Instance.transform.Find("Canvas/BaitCountdownText")?.GetComponent<UnityEngine.UI.Text>();
            if (countdownText != null)
            {
                if (isInContinuousMode && continuousModeRemainingTime > 0)
                {
                    int minutes = (int)(continuousModeRemainingTime / 60);
                    int seconds = (int)(continuousModeRemainingTime % 60);
                    countdownText.text = $"{minutes:00}:{seconds:00}";
                    Logger.Log($"[NetServerManager] 更新倒计时UI: {minutes:00}:{seconds:00}");
                }
                else
                {
                    countdownText.text = "00:00";
                }
            }
        }
    }

    /// <summary>
    /// 进入连续钓鱼模式响应
    /// </summary>
    [System.Serializable]
    private class EnterContinuousModeResponse
    {
        public bool success;
        public string message;
        public float remainingTime;
    }

    /// <summary>
    /// 进入连续钓鱼模式响应（带baitEndTime）
    /// </summary>
    [System.Serializable]
    private class EnterContinuousModeWithBaitEndTimeResponse
    {
        public bool success;
        public string message;
        public float remainingTime;
        public long baitEndTime; // 窝料结束时间戳（秒）
    }

    /// <summary>
    /// 增加窝料时间响应
    /// </summary>
    [System.Serializable]
    private class AddBaitTimeResponse
    {
        public bool success;
        public string message;
        public float remainingTime; // 剩余秒数（优先使用）
        public long baitEndTime;    // 窝料结束时间戳（秒）- 如果服务器返回的是剩余秒数则可能小于1000000000
    }

    /// <summary>
    /// 连续模式状态
    /// </summary>
    [System.Serializable]
    private class ContinuousModeStatus
    {
        public bool isInContinuousMode;
        public float remainingTime;
        public long baitEndTime; // 新增：窝料结束时间戳（秒）
    }

    // ==================== 天气和时间相关方法 ====================

    /// <summary>
    /// 根据天气ID获取天气名称
    /// </summary>
    private string GetWeatherNameById(int weatherId)
    {
        // 尝试从配置数据中查找天气名称（配置文件中天气ID范围是301-316）
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.weathers != null)
        {
            var weather = LoadDataManager.Instance.weathers.Find(w => w.id == weatherId);
            if (weather != null)
            {
                return weather.name;
            }
        }
        
        // 默认返回ID对应的名称
        switch (weatherId)
        {
            case 301: return "晴天";
            case 302: return "多云天";
            case 303: return "阴天";
            case 304: return "微风天";
            case 305: return "小到中雨";
            case 306: return "薄雾";
            case 307: return "明月天";
            case 308: return "雷阵雨天";
            case 309: return "阵风天";
            case 310: return "暴雨";
            case 311: return "大雾天";
            case 312: return "彩虹天";
            case 313: return "热浪";
            case 314: return "火烧云";
            case 315: return "荧光海";
            case 316: return "海市蜃楼";
            default: return $"未知天气({weatherId})";
        }
    }

    /// <summary>
    /// 根据时间槽ID获取时间名称
    /// </summary>
    private string GetTimeSlotNameById(int timeSlotId)
    {
        // 尝试从配置数据中查找时间名称（配置文件中时间槽ID范围是401-404）
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.timeSlots != null)
        {
            var timeSlot = LoadDataManager.Instance.timeSlots.Find(t => t.id == timeSlotId);
            if (timeSlot != null)
            {
                return timeSlot.name;
            }
        }
        
        // 默认返回ID对应的名称
        switch (timeSlotId)
        {
            case 401: return "清晨";
            case 402: return "日间";
            case 403: return "傍晚";
            case 404: return "深夜";
            default: return $"未知时段({timeSlotId})";
        }
    }
}