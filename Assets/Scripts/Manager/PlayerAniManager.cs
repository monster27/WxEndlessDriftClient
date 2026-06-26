using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SharedModels;

public class PlayerAniManager : SingletonMonoFromScene<PlayerAniManager>
{
    [Header("拖拽赋值（推荐）")]
    [SerializeField] private PlayerAniCtrl aniCtrl;
    [SerializeField] private FishingTipAniCtrl fishTipAniCtrl;
    [SerializeField] private NestAniCtrl nestAniCtrl;

    private Action pendingCallback;
    private float animationTimer = 0f;
    private bool isWaitingForAnimation = false;

    private Dictionary<int, CharacterAniData> characterAniDict = new Dictionary<int, CharacterAniData>();
    private int currentCharacterId = 0;

    private const string ANI_PATH_PREFIX = "JsonData/PlayerAni/Ani/";
    private bool isInitialized = false;
    private bool isInitializing = false;

    private const string FISH_TIP_TAG = "FishTip";
    private int pendingCharacterId = 0;

    // 缓存队列
    private Queue<Action> pendingActions = new Queue<Action>();

    public void Init()
    {
        if (isInitialized) return;
        if (isInitializing) return;

        isInitializing = true;

        try
        {
            if (aniCtrl == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    aniCtrl = playerObj.GetComponent<PlayerAniCtrl>();
                    if (aniCtrl == null)
                    {
                        aniCtrl = playerObj.GetComponentInChildren<PlayerAniCtrl>();
                    }
                }

                if (aniCtrl == null)
                {
                    aniCtrl = FindObjectOfType<PlayerAniCtrl>();
                }

                if (aniCtrl != null)
                {
                    Debug.Log($"[PlayerAniManager] Init - 找到 PlayerAniCtrl: {aniCtrl.gameObject.name}");
                }
            }

            if (aniCtrl == null)
            {
                Debug.LogWarning("[PlayerAniManager] Init - 未找到 PlayerAniCtrl，延迟初始化");
                isInitializing = false;
                StartCoroutine(DelayedInit());
                return;
            }

            EnsureFishTipAniCtrl();
            PreloadAllCharacterAnimations();

            isInitialized = true;
            isInitializing = false;
            Debug.Log($"[PlayerAniManager] 初始化完成，已预加载 {characterAniDict.Count} 个人物的动画资源");

            ExecutePendingActions();
        }
        catch (Exception e)
        {
            isInitializing = false;
            Debug.LogError($"[PlayerAniManager] 初始化异常: {e.Message}");
        }
    }

    private void EnsureFishTipAniCtrl()
    {
        if (fishTipAniCtrl != null) return;

        GameObject fishTipObj = GameObject.FindGameObjectWithTag(FISH_TIP_TAG);
        if (fishTipObj != null)
        {
            fishTipAniCtrl = fishTipObj.GetComponent<FishingTipAniCtrl>();
            if (fishTipAniCtrl == null)
            {
                fishTipAniCtrl = fishTipObj.GetComponentInChildren<FishingTipAniCtrl>();
            }
            if (fishTipAniCtrl != null)
            {
                Debug.Log($"[PlayerAniManager] 通过 Tag 找到 FishingTipAniCtrl");
                return;
            }
        }

        fishTipAniCtrl = FindObjectOfType<FishingTipAniCtrl>();
        if (fishTipAniCtrl != null)
        {
            Debug.Log($"[PlayerAniManager] 通过 FindObjectOfType 找到 FishingTipAniCtrl");
            return;
        }

        Debug.LogWarning("[PlayerAniManager] 未找到 FishingTipAniCtrl");
    }

    private FishingTipAniCtrl GetFishTipAniCtrl()
    {
        if (fishTipAniCtrl == null)
        {
            EnsureFishTipAniCtrl();
        }
        return fishTipAniCtrl;
    }

    public void SetFishTip(Color color, float struggleTime = 2)
    {
        FishingTipAniCtrl ctrl = GetFishTipAniCtrl();
        if (ctrl != null)
        {
            ctrl.SetBlinkState(color, struggleTime, 0.3f);
        }
    }

    public void SetFishTip(int rarityId)
    {
        FishingTipAniCtrl ctrl = GetFishTipAniCtrl();
        if (ctrl != null)
        {
            ctrl.SetBlinkState(rarityId, 0.3f);
        }
    }

    public void StopFishTip()
    {
        FishingTipAniCtrl ctrl = GetFishTipAniCtrl();
        if (ctrl != null)
        {
            ctrl.StopBlink();
        }
    }

    private IEnumerator DelayedInit()
    {
        int retryCount = 0;
        int maxRetries = 10;

        while (retryCount < maxRetries)
        {
            yield return new WaitForSeconds(0.5f);
            retryCount++;

            if (aniCtrl == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    aniCtrl = playerObj.GetComponent<PlayerAniCtrl>();
                    if (aniCtrl == null)
                    {
                        aniCtrl = playerObj.GetComponentInChildren<PlayerAniCtrl>();
                    }
                }

                if (aniCtrl == null)
                {
                    aniCtrl = FindObjectOfType<PlayerAniCtrl>();
                }
            }

            if (aniCtrl != null)
            {
                Debug.Log($"[PlayerAniManager] 延迟初始化成功 (重试 {retryCount} 次)");
                isInitializing = false;

                EnsureFishTipAniCtrl();
                PreloadAllCharacterAnimations();
                isInitialized = true;

                if (pendingCharacterId > 0)
                {
                    DoSwitchCharacter(pendingCharacterId);
                    pendingCharacterId = 0;
                }

                Debug.Log($"[PlayerAniManager] 延迟初始化完成");
                ExecutePendingActions();

                yield break;
            }

            if (retryCount % 3 == 0)
            {
                Debug.Log($"[PlayerAniManager] 延迟初始化等待中... (重试 {retryCount}/{maxRetries})");
            }
        }

        Debug.LogWarning("[PlayerAniManager] 延迟初始化超时");
        isInitializing = false;
    }

    private void PreloadAllCharacterAnimations()
    {
        List<CharacterConfig> configList = null;

        if (NetServerManager.Instance != null && NetServerManager.Instance.IsConnected)
        {
            Debug.Log("[PlayerAniManager] 网络模式，使用默认人物配置");
            configList = GetDefaultCharacterConfigs();
        }
        else if (CharacterServerManager.Instance != null)
        {
            configList = CharacterServerManager.Instance.GetAllCharacterConfigs();
        }

        if (configList == null || configList.Count == 0)
        {
            Debug.LogWarning("[PlayerAniManager] 使用默认人物配置");
            configList = GetDefaultCharacterConfigs();
        }

        foreach (var config in configList)
        {
            LoadCharacterAnimation(config.id);
        }
    }

    private List<CharacterConfig> GetDefaultCharacterConfigs()
    {
        return new List<CharacterConfig>
        {
            new CharacterConfig { id = 3401, name = "默认人物" },
            new CharacterConfig { id = 3402, name = "钓鱼大师" }
        };
    }

    private void LoadCharacterAnimation(int characterId)
    {
        if (characterAniDict.ContainsKey(characterId)) return;

        CharacterAniData aniData = new CharacterAniData();
        aniData.characterId = characterId;

        CharacterConfig characterConfig = GetCharacterConfig(characterId);
        if (characterConfig != null)
        {
            aniData.idleColumns = characterConfig.idleColumns;
            aniData.idleSpeed = characterConfig.idleSpeed;
            aniData.reelColumns = characterConfig.reelColumns;
            aniData.reelSpeed = characterConfig.reelSpeed;
            aniData.lazyColumns = characterConfig.lazyColumns;
            aniData.lazySpeed = characterConfig.lazySpeed;
        }

        string basePath = ANI_PATH_PREFIX + characterId;

        aniData.idleTexture = Resources.Load<Texture2D>(basePath + "/Idle");
        aniData.lazyTexture = Resources.Load<Texture2D>(basePath + "/Lazy");
        aniData.reelTexture = Resources.Load<Texture2D>(basePath + "/Reel");

        characterAniDict[characterId] = aniData;

        Debug.Log($"[PlayerAniManager] 加载人物 {characterId} 动画完成");
    }

    private CharacterConfig GetCharacterConfig(int characterId)
    {
        if (LoadDataManager.Instance != null)
        {
            return LoadDataManager.Instance.GetCharacterConfig(characterId);
        }

        CharacterConfigList configList = CharacterConfigListExtensions.LoadFromResources();
        if (configList != null && configList.characters != null)
        {
            return configList.characters.Find(c => c.id == characterId);
        }

        return null;
    }

    /// <summary>
    /// 获取当前装备的人物ID（从 NetServerManager 获取）
    /// </summary>
    private int GetEquippedCharacterId()
    {
        if (NetServerManager.Instance != null)
        {
            // 通过 CommunicateEvent 请求当前装备的人物ID
            try
            {
                int characterId = CommunicateEvent.Request<int, int>("VIEW_EVENT_GET_EQUIPPED_CHARACTER", 0);
                if (characterId > 0)
                {
                    return characterId;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerAniManager] 获取装备人物ID失败: {ex.Message}");
            }
        }
        return 0;
    }

    private void DoSwitchCharacter(int characterId)
    {
        if (currentCharacterId == characterId) return;
        if (aniCtrl == null) return;

        if (!characterAniDict.ContainsKey(characterId))
        {
            LoadCharacterAnimation(characterId);
        }

        if (!characterAniDict.ContainsKey(characterId)) return;

        currentCharacterId = characterId;
        var aniData = characterAniDict[characterId];

        aniCtrl.SetAnimationParams(aniData.idleColumns, aniData.idleSpeed,
            aniData.reelColumns, aniData.reelSpeed,
            aniData.lazyColumns, aniData.lazySpeed);
        aniCtrl.SetCharacterTextures(aniData.idleTexture, aniData.lazyTexture, aniData.reelTexture);

        Debug.Log($"[PlayerAniManager] 切换人物: {characterId}");
    }

    public void SwitchCharacter(int characterId)
    {
        if (!isInitialized)
        {
            Debug.Log($"[PlayerAniManager] SwitchCharacter - 缓存请求: {characterId}");
            pendingCharacterId = characterId;
            Init();
            return;
        }

        DoSwitchCharacter(characterId);
    }

    public CharacterAniData GetCharacterAniData(int characterId)
    {
        return characterAniDict.TryGetValue(characterId, out var data) ? data : null;
    }

    public int GetCurrentCharacterId()
    {
        return currentCharacterId;
    }

    private void ExecutePendingActions()
    {
        if (!isInitialized || aniCtrl == null) return;

        while (pendingActions.Count > 0)
        {
            Action action = pendingActions.Dequeue();
            action?.Invoke();
        }
    }

    public void PlayIdleAnimation()
    {
        if (!isInitialized)
        {
            Debug.Log("[PlayerAniManager] PlayIdleAnimation - 缓存请求");
            pendingActions.Enqueue(() => PlayIdleAnimation());
            Init();
            return;
        }

        if (aniCtrl == null) return;

        // 确保当前角色已加载
        if (currentCharacterId == 0)
        {
            EnsureDefaultCharacterLoaded();
        }

        aniCtrl.PlayIdleAnimation();
    }

    public void PlayLazyAnimation()
    {
        if (!isInitialized)
        {
            Debug.Log("[PlayerAniManager] PlayLazyAnimation - 缓存请求");
            pendingActions.Enqueue(() => PlayLazyAnimation());
            Init();
            return;
        }

        if (aniCtrl == null) return;

        // 确保当前角色已加载
        if (currentCharacterId == 0)
        {
            EnsureDefaultCharacterLoaded();
        }

        StopFishTip();
        aniCtrl.PlayLazyAnimation();
        Debug.Log("[PlayerAniManager] 播放 Lazy 动画");
    }

    public void PlayReelAnimation(float duration, Action callback)
    {
        if (!isInitialized)
        {
            Debug.Log("[PlayerAniManager] PlayReelAnimation - 缓存请求");
            pendingActions.Enqueue(() => PlayReelAnimation(duration, callback));
            Init();
            return;
        }

        if (aniCtrl == null)
        {
            callback?.Invoke();
            return;
        }

        if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsFishBagFull())
        {
            Debug.Log("[PlayerAniManager] 鱼篓已满，不播放拉钩动画");
            callback?.Invoke();
            return;
        }

        if (isWaitingForAnimation)
        {
            Debug.Log("[PlayerAniManager] 已有拉钩动画正在播放");
            callback?.Invoke();
            return;
        }

        // 确保当前角色已加载
        if (currentCharacterId == 0)
        {
            EnsureDefaultCharacterLoaded();
        }

        aniCtrl.PlayReelAnimation();
        pendingCallback = callback;
        animationTimer = duration;
        isWaitingForAnimation = true;
    }

    public void PlayReelAnimationWithTwoIds(int detectedFishId, int actualItemId, float struggleTime, bool isTrash, Action callback)
    {
        PlayReelAnimation(struggleTime, callback);
    }

    public void SwitchFromLazyToIdle()
    {
        PlayIdleAnimation();
    }

    /// <summary>
    /// 确保默认角色已加载
    /// </summary>
    private void EnsureDefaultCharacterLoaded()
    {
        int defaultId = 3401;
        int equippedId = GetEquippedCharacterId();
        if (equippedId > 0)
        {
            defaultId = equippedId;
        }

        if (currentCharacterId != defaultId)
        {
            DoSwitchCharacter(defaultId);
        }
        else
        {
            // 如果角色ID相同但数据可能还没应用，重新应用一次
            if (characterAniDict.TryGetValue(currentCharacterId, out var aniData))
            {
                aniCtrl.SetAnimationParams(aniData.idleColumns, aniData.idleSpeed,
                    aniData.reelColumns, aniData.reelSpeed,
                    aniData.lazyColumns, aniData.lazySpeed);
                aniCtrl.SetCharacterTextures(aniData.idleTexture, aniData.lazyTexture, aniData.reelTexture);
            }
        }
    }

    private void Update()
    {
        if (isWaitingForAnimation)
        {
            animationTimer -= Time.deltaTime;
            if (animationTimer <= 0f)
            {
                isWaitingForAnimation = false;
                pendingCallback?.Invoke();
                pendingCallback = null;

                if (pendingActions.Count > 0)
                {
                    Debug.Log($"[PlayerAniManager] Reel 完成，执行 {pendingActions.Count} 个缓存请求");
                    ExecutePendingActions();
                }
            }
        }
    }

    private void OnEnable()
    {
        if (isInitialized && aniCtrl != null && currentCharacterId > 0)
        {
            if (characterAniDict.TryGetValue(currentCharacterId, out var aniData))
            {
                aniCtrl.SetAnimationParams(aniData.idleColumns, aniData.idleSpeed,
                    aniData.reelColumns, aniData.reelSpeed,
                    aniData.lazyColumns, aniData.lazySpeed);
                aniCtrl.SetCharacterTextures(aniData.idleTexture, aniData.lazyTexture, aniData.reelTexture);
            }
        }

        if (fishTipAniCtrl == null)
        {
            EnsureFishTipAniCtrl();
        }
    }
}

public class CharacterAniData
{
    public int characterId;
    public Texture2D idleTexture;
    public Texture2D lazyTexture;
    public Texture2D reelTexture;

    public int idleColumns = 4;
    public float idleSpeed = 15f;
    public int reelColumns = 4;
    public float reelSpeed = 20f;
    public int lazyColumns = 4;
    public float lazySpeed = 18f;
}