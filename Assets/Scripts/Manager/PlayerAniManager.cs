using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SharedModels;

/// <summary>
/// 玩家动画管理器
/// 包含所有业务逻辑：人物切换、动画控制、稀有度颜色、窝料动画等
/// </summary>
public class PlayerAniManager : SingletonMonoFromScene<PlayerAniManager>
{
    // ========== 日志标签 ==========
    private const string LOG_TAG = "SceneMat - PlayerAniManager";

    // ========== 枚举定义 ==========
    public enum PlayerAnimState
    {
        Idle,
        Reel,
        Lazy
    }

    // ========== 数据类 ==========
    [System.Serializable]
    public class CharacterAniData
    {
        public int characterId;
        public Texture2D idleTexture;
        public Texture2D lazyTexture;
        public Texture2D reelTexture;
        public int idleColumns = 15;
        public float idleSpeed = 15f;
        public int reelColumns = 12;
        public float reelSpeed = 20f;
        public int lazyColumns = 15;
        public float lazySpeed = 18f;
    }

    // ========== 稀有度颜色系统 ==========
    private static Dictionary<int, Color> rarityColorCache = new Dictionary<int, Color>();
    private static bool isRarityDataLoaded = false;

    // ========== Inspector 参数 ==========
    [Header("=== 控制器引用 ===")]
    [SerializeField] private SceneMatCtrl aniCtrl;
    [SerializeField] private SceneMatCtrl fishTipAniCtrl;
    [SerializeField] private SceneMatCtrl nestAniCtrl;

    [Header("=== 配置参数 ===")]
    [SerializeField] private float defaultBlinkInterval = 0.3f;
    [SerializeField] private float defaultBlinkDuration = 2f;
    [SerializeField] private int defaultCharacterId = 3401;

    [Header("=== 窝料动画配置 ===")]
    [SerializeField] private float nestFadeInDuration = 0.2f;
    [SerializeField] private float nestFadeOutDuration = 0.5f;
    [SerializeField] private int nestColumns = 4;
    [SerializeField] private int nestRows = 1;
    [SerializeField] private float nestFrameSpeed = 20f;

    // ========== 私有变量 ==========
    private Action pendingCallback;
    private bool isWaitingForAnimation = false;
    private bool isAnimationPlaying = false;

    private Dictionary<int, CharacterAniData> characterAniDict = new Dictionary<int, CharacterAniData>();
    private int currentCharacterId = 0;
    private PlayerAnimState currentPlayerState = PlayerAnimState.Idle;

    private const string ANI_PATH_PREFIX = "JsonData/PlayerAni/Ani/";
    private bool isInitialized = false;
    private bool isInitializing = false;

    private const string FISH_TIP_TAG = "FishTip";
    private int pendingCharacterId = 0;
    private Queue<Action> pendingActions = new Queue<Action>();

    private Coroutine nestAnimationCoroutine;
    private bool isNestPlaying = false;

    // ========== 人物动画状态 ==========
    private Coroutine playerAnimationCoroutine;

    // ========== 公共属性 ==========
    public int CurrentCharacterId => currentCharacterId;
    public PlayerAnimState CurrentPlayerState => currentPlayerState;
    public bool IsPlayingReel => isWaitingForAnimation;
    public bool IsNestPlaying => isNestPlaying;

    // ========== 初始化 ==========
    public void Init()
    {
        if (isInitialized || isInitializing) return;

        isInitializing = true;

        try
        {
            if (aniCtrl == null)
            {
                SceneMatCtrl[] ctrls = FindObjectsOfType<SceneMatCtrl>();
                foreach (var ctrl in ctrls)
                {
                    if (ctrl.ElementId == SceneMatManager.ElementType.Player)
                    {
                        aniCtrl = ctrl;
                        break;
                    }
                }

                if (aniCtrl == null)
                {
                    GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                    if (playerObj != null)
                    {
                        aniCtrl = playerObj.GetComponent<SceneMatCtrl>();
                        if (aniCtrl == null)
                        {
                            aniCtrl = playerObj.GetComponentInChildren<SceneMatCtrl>();
                        }
                    }
                }

                if (aniCtrl == null)
                {
                    aniCtrl = FindObjectOfType<SceneMatCtrl>();
                }
            }

            if (aniCtrl == null)
            {
                isInitializing = false;
                StartCoroutine(DelayedInit());
                return;
            }

            EnsureFishTipAniCtrl();
            PreloadAllCharacterAnimations();

            isInitialized = true;
            isInitializing = false;

            InitializeNestHidden();
            StartCoroutine(EnsurePlayerInitializedAndPlayIdle());

            CheckFishBagStateAndPlayAnimation();
            ExecutePendingActions();
        }
        catch (Exception e)
        {
            isInitializing = false;
            Debug.LogError($"[{LOG_TAG}] Init() - 初始化异常: {e.Message}");
        }
    }

    /// <summary>
    /// 等待 Player 控制器初始化完成后播放 Idle 动画
    /// </summary>
    private IEnumerator EnsurePlayerInitializedAndPlayIdle()
    {
        if (aniCtrl == null) yield break;

        if (aniCtrl.IsInitialized)
        {
            PlayPlayerAnimation(PlayerAnimState.Idle);
            yield break;
        }

        float waitTime = 0f;
        float maxWaitTime = 3f;

        while (!aniCtrl.IsInitialized && waitTime < maxWaitTime)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        yield return null;

        PlayPlayerAnimation(PlayerAnimState.Idle);
    }

    // ========== 鱼篓状态检查 ==========
    private void CheckFishBagStateAndPlayAnimation()
    {
        if (PlayerDataManager.Instance == null || NetServerManager.Instance == null) return;

        bool isFull = PlayerDataManager.Instance.IsFishBagFull();

        if (isFull)
        {
            PlayPlayerAnimation(PlayerAnimState.Lazy);
        }
    }

    // ========== 钓鱼提示控制器查找 ==========
    private void EnsureFishTipAniCtrl()
    {
        if (fishTipAniCtrl != null) return;

        GameObject fishTipObj = GameObject.FindGameObjectWithTag(FISH_TIP_TAG);
        if (fishTipObj != null)
        {
            fishTipAniCtrl = fishTipObj.GetComponent<SceneMatCtrl>();
            if (fishTipAniCtrl == null)
            {
                fishTipAniCtrl = fishTipObj.GetComponentInChildren<SceneMatCtrl>();
            }
            if (fishTipAniCtrl != null) return;
        }

        SceneMatCtrl[] ctrls = FindObjectsOfType<SceneMatCtrl>();
        foreach (var ctrl in ctrls)
        {
            if (ctrl.ElementId == SceneMatManager.ElementType.FishTip)
            {
                fishTipAniCtrl = ctrl;
                return;
            }
        }
    }

    // ========== 闪烁功能 ==========
    public void SetFishTip(Color color, float struggleTime = 2)
    {
        if (fishTipAniCtrl != null)
        {
            fishTipAniCtrl.SetBlinkColor(color);
            fishTipAniCtrl.SetBlinkInterval(defaultBlinkInterval);
            fishTipAniCtrl.SetBlinkEnabled(true);
            fishTipAniCtrl.StartCoroutine(AutoStopBlinkCoroutine(fishTipAniCtrl, struggleTime));
        }
    }

    public void SetFishTip(int rarityId)
    {
        Color color = GetRarityColor(rarityId);
        SetFishTip(color, defaultBlinkDuration);
    }

    public void StopFishTip()
    {
        if (fishTipAniCtrl != null)
        {
            fishTipAniCtrl.SetBlinkEnabled(false);
        }
    }

    private IEnumerator AutoStopBlinkCoroutine(SceneMatCtrl ctrl, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (ctrl != null)
        {
            ctrl.SetBlinkEnabled(false);
        }
    }

    // ========== 延迟初始化 ==========
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
                    aniCtrl = playerObj.GetComponent<SceneMatCtrl>();
                    if (aniCtrl == null)
                    {
                        aniCtrl = playerObj.GetComponentInChildren<SceneMatCtrl>();
                    }
                }

                if (aniCtrl == null)
                {
                    aniCtrl = FindObjectOfType<SceneMatCtrl>();
                }
            }

            if (aniCtrl != null)
            {
                isInitializing = false;

                EnsureFishTipAniCtrl();
                PreloadAllCharacterAnimations();
                isInitialized = true;

                if (pendingCharacterId > 0)
                {
                    DoSwitchCharacter(pendingCharacterId);
                    pendingCharacterId = 0;
                }

                CheckFishBagStateAndPlayAnimation();
                ExecutePendingActions();

                yield break;
            }
        }

        isInitializing = false;
        Debug.LogWarning("[PlayerAniManager] 延迟初始化超时");
    }

    // ========== 人物动画加载 ==========
    private void PreloadAllCharacterAnimations()
    {
        if (LoadDataManager.Instance == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] PreloadAllCharacterAnimations() - LoadDataManager 未初始化");
            return;
        }

        var characterConfigs = LoadDataManager.Instance.characters;
        if (characterConfigs == null || characterConfigs.Count == 0)
        {
            Debug.LogWarning($"[{LOG_TAG}] PreloadAllCharacterAnimations() - 没有人物配置数据");
            return;
        }

        foreach (var config in characterConfigs)
        {
            LoadCharacterAnimation(config);
        }
    }

    private void LoadCharacterAnimation(CharacterConfig config)
    {
        if (config == null) return;
        if (characterAniDict.ContainsKey(config.id)) return;

        CharacterAniData aniData = new CharacterAniData();
        aniData.characterId = config.id;

        aniData.idleColumns = config.idleColumns > 0 ? config.idleColumns : 15;
        aniData.idleSpeed = config.idleSpeed > 0 ? config.idleSpeed : 15f;
        aniData.reelColumns = config.reelColumns > 0 ? config.reelColumns : 12;
        aniData.reelSpeed = config.reelSpeed > 0 ? config.reelSpeed : 20f;
        aniData.lazyColumns = config.lazyColumns > 0 ? config.lazyColumns : 15;
        aniData.lazySpeed = config.lazySpeed > 0 ? config.lazySpeed : 18f;

        Debug.Log($"[{LOG_TAG}] LoadCharacterAnimation() - 人物 {config.id} ({config.name}) 动画参数: Idle={aniData.idleColumns}列/{aniData.idleSpeed}帧, Reel={aniData.reelColumns}列/{aniData.reelSpeed}帧, Lazy={aniData.lazyColumns}列/{aniData.lazySpeed}帧");

        string basePath = "JsonData/PlayerAni/Ani/" + config.id;

        string idlePath = !string.IsNullOrEmpty(config.idleTexturePath) ? config.idleTexturePath : basePath + "/Idle";
        string reelPath = !string.IsNullOrEmpty(config.reelTexturePath) ? config.reelTexturePath : basePath + "/Reel";
        string lazyPath = !string.IsNullOrEmpty(config.lazyTexturePath) ? config.lazyTexturePath : basePath + "/Lazy";

        aniData.idleTexture = Resources.Load<Texture2D>(idlePath);
        aniData.reelTexture = Resources.Load<Texture2D>(reelPath);
        aniData.lazyTexture = Resources.Load<Texture2D>(lazyPath);

        Debug.Log($"[{LOG_TAG}] ===== 纹理加载结果 for {config.id} ({config.name}) =====");
        Debug.Log($"[{LOG_TAG}] Idle: {(aniData.idleTexture != null ? "✅ " + aniData.idleTexture.name : "❌ NULL")} (路径: {idlePath})");
        Debug.Log($"[{LOG_TAG}] Reel: {(aniData.reelTexture != null ? "✅ " + aniData.reelTexture.name : "❌ NULL")} (路径: {reelPath})");
        Debug.Log($"[{LOG_TAG}] Lazy: {(aniData.lazyTexture != null ? "✅ " + aniData.lazyTexture.name : "❌ NULL")} (路径: {lazyPath})");
        Debug.Log($"[{LOG_TAG}] ============================================");

        if (aniData.reelTexture == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] Reel 纹理加载失败，尝试备用路径...");
            string[] alternativePaths = new string[]
            {
                "JsonData/PlayerAni/Ani/" + config.id + "/Reel",
                "PlayerAni/Ani/" + config.id + "/Reel",
                "Ani/" + config.id + "/Reel"
            };

            foreach (string altPath in alternativePaths)
            {
                aniData.reelTexture = Resources.Load<Texture2D>(altPath);
                if (aniData.reelTexture != null)
                {
                    Debug.Log($"[{LOG_TAG}] ✅ 使用备用路径加载 Reel 成功: {altPath}");
                    break;
                }
            }

            if (aniData.reelTexture == null)
            {
                Debug.LogError($"[{LOG_TAG}] ❌❌❌ Reel 纹理加载失败！请检查文件是否存在: {reelPath}");
            }
        }

        characterAniDict[config.id] = aniData;
        Debug.Log($"[{LOG_TAG}] LoadCharacterAnimation() - 加载人物 {config.id} 动画完成");
    }

    private CharacterConfig GetCharacterConfigFromLoadData(int characterId)
    {
        if (LoadDataManager.Instance == null) return null;
        return LoadDataManager.Instance.GetCharacterConfig(characterId);
    }

    private int GetEquippedCharacterId()
    {
        if (NetServerManager.Instance != null)
        {
            try
            {
                int characterId = CommunicateEvent.Request<int, int>("VIEW_EVENT_GET_EQUIPPED_CHARACTER", 0);
                if (characterId > 0)
                {
                    return characterId;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerAniManager] 获取装备人物ID失败: {ex.Message}");
            }
        }
        return 0;
    }

    // ========== 人物切换 ==========
    private void DoSwitchCharacter(int characterId)
    {
        if (currentCharacterId == characterId) return;

        if (!characterAniDict.ContainsKey(characterId))
        {
            var config = GetCharacterConfigFromLoadData(characterId);
            if (config != null)
            {
                LoadCharacterAnimation(config);
            }
            else
            {
                Debug.LogWarning($"[{LOG_TAG}] DoSwitchCharacter() - 未找到人物 {characterId} 的配置");
                return;
            }
        }

        if (!characterAniDict.TryGetValue(characterId, out var aniData)) return;

        currentCharacterId = characterId;

        if (aniCtrl == null) return;

        if (!aniCtrl.IsInitialized)
        {
            aniCtrl.Initialize();
        }

        if (aniData.idleTexture != null)
        {
            aniCtrl.SetMainTexture(aniData.idleTexture);
            aniCtrl.SetSpriteSheetParams(1, aniData.idleColumns, aniData.idleSpeed);
            aniCtrl.SetSpriteSheetEnabled(false);

            Debug.Log($"[{LOG_TAG}] DoSwitchCharacter() - 预设置 Idle 动画: {aniData.idleColumns}列, 速度={aniData.idleSpeed}");
        }
    }

    public void SwitchCharacter(int characterId)
    {
        if (!isInitialized)
        {
            pendingCharacterId = characterId;
            Init();
            return;
        }

        DoSwitchCharacter(characterId);
    }

    // ========== 人物动画播放核心 ==========

    /// <summary>
    /// 播放人物动画（播放时启用序列帧，播放完成后自动关闭）
    /// </summary>
    private void PlayPlayerAnimation(PlayerAnimState state)
    {
        Debug.Log($"[{LOG_TAG}] PlayPlayerAnimation() - 播放状态: {state}");

        if (aniCtrl == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] PlayPlayerAnimation() - aniCtrl 为空");
            return;
        }

        if (!aniCtrl.IsInitialized)
        {
            aniCtrl.Initialize();
        }

        if (currentCharacterId == 0)
        {
            EnsureDefaultCharacterLoaded();
            if (!characterAniDict.TryGetValue(currentCharacterId, out var aniData))
            {
                Debug.LogWarning($"[{LOG_TAG}] PlayPlayerAnimation() - 未找到人物数据");
                return;
            }
        }

        if (!characterAniDict.TryGetValue(currentCharacterId, out var data))
        {
            Debug.LogWarning($"[{LOG_TAG}] PlayPlayerAnimation() - 未找到人物 {currentCharacterId} 的数据");
            return;
        }

        // 停止当前动画协程
        if (playerAnimationCoroutine != null)
        {
            Debug.Log($"[{LOG_TAG}] PlayPlayerAnimation() - 停止当前动画协程");
            StopCoroutine(playerAnimationCoroutine);
            playerAnimationCoroutine = null;
            isAnimationPlaying = false;
        }

        // 根据状态设置纹理和参数
        Texture2D targetTexture = null;
        int columns = 4;
        float speed = 15f;

        switch (state)
        {
            case PlayerAnimState.Idle:
                targetTexture = data.idleTexture;
                columns = data.idleColumns;
                speed = data.idleSpeed;
                break;

            case PlayerAnimState.Reel:
                targetTexture = data.reelTexture;
                columns = data.reelColumns;
                speed = data.reelSpeed;
                break;

            case PlayerAnimState.Lazy:
                targetTexture = data.lazyTexture;
                columns = data.lazyColumns;
                speed = data.lazySpeed;
                break;
        }

        if (targetTexture != null)
        {
            aniCtrl.SetMainTexture(targetTexture);
            aniCtrl.SetSpriteSheetParams(1, columns, speed);
            Debug.Log($"[{LOG_TAG}] PlayPlayerAnimation() - 设置序列帧: {columns}列, 速度={speed}");
        }
        else
        {
            Debug.LogError($"[{LOG_TAG}] PlayPlayerAnimation() - 目标纹理为空！无法播放 {state} 动画！");
            return;
        }

        aniCtrl.SetSpriteSheetEnabled(true);
        currentPlayerState = state;

        Debug.Log($"[{LOG_TAG}] PlayPlayerAnimation() - 序列帧已启用，播放状态: {state}");
    }

    /// <summary>
    /// 停止人物动画（关闭序列帧，显示单帧纹理）
    /// </summary>
    private void StopPlayerAnimation()
    {
        Debug.Log($"[{LOG_TAG}] StopPlayerAnimation()");

        if (aniCtrl == null) return;

        if (playerAnimationCoroutine != null)
        {
            StopCoroutine(playerAnimationCoroutine);
            playerAnimationCoroutine = null;
            isAnimationPlaying = false;
        }

        aniCtrl.SetSpriteSheetEnabled(false);
        Debug.Log($"[{LOG_TAG}] StopPlayerAnimation() - 序列帧已关闭");
    }

    // ========== 公共动画方法 ==========

    /// <summary>
    /// 播放 Idle 动画
    /// </summary>
    public void PlayIdleAnimation()
    {
        if (!isInitialized)
        {
            pendingActions.Enqueue(() => PlayIdleAnimation());
            Init();
            return;
        }

        if (aniCtrl == null) return;

        if (currentCharacterId == 0)
        {
            EnsureDefaultCharacterLoaded();
        }

        PlayPlayerAnimation(PlayerAnimState.Idle);
    }

    /// <summary>
    /// 播放 Lazy 动画
    /// </summary>
    public void PlayLazyAnimation()
    {
        if (!isInitialized)
        {
            pendingActions.Enqueue(() => PlayLazyAnimation());
            Init();
            return;
        }

        if (aniCtrl == null) return;

        if (currentCharacterId == 0)
        {
            EnsureDefaultCharacterLoaded();
        }

        StopFishTip();
        PlayPlayerAnimation(PlayerAnimState.Lazy);
    }

    /// <summary>
    /// 播放 Reel 动画（播放指定时间后自动停止并关闭序列帧）
    /// </summary>
    public void PlayReelAnimation(float duration, Action callback)
    {
        Debug.Log($"[{LOG_TAG}] PlayReelAnimation() - ===== 收到请求，时长: {duration} =====");

        if (!isInitialized)
        {
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
            Debug.Log($"[{LOG_TAG}] PlayReelAnimation() - 鱼篓已满，跳过");
            callback?.Invoke();
            return;
        }

        // 如果已有动画在播放，先完成回调
        if (isWaitingForAnimation || isAnimationPlaying)
        {
            Debug.Log($"[{LOG_TAG}] PlayReelAnimation() - 已有动画正在播放，等待完成");
            pendingActions.Enqueue(() => callback?.Invoke());
            return;
        }

        if (currentCharacterId == 0)
        {
            EnsureDefaultCharacterLoaded();
        }

        // 直接播放 Reel 动画
        PlayPlayerAnimation(PlayerAnimState.Reel);
        isAnimationPlaying = true;
        isWaitingForAnimation = true;

        if (playerAnimationCoroutine != null)
        {
            StopCoroutine(playerAnimationCoroutine);
        }

        Debug.Log($"[{LOG_TAG}] PlayReelAnimation() - ===== 开始播放，时长: {duration}秒，当前时间: {Time.time} =====");

        playerAnimationCoroutine = StartCoroutine(AutoStopPlayerAnimation(duration, () =>
        {
            Debug.Log($"[{LOG_TAG}] PlayReelAnimation() - Reel 动画完成");
            isWaitingForAnimation = false;
            isAnimationPlaying = false;
            playerAnimationCoroutine = null;
            callback?.Invoke();

            if (pendingActions.Count > 0)
            {
                ExecutePendingActions();
            }
        }));
    }

    public void PlayReelAnimationWithTwoIds(int detectedFishId, int actualItemId, float struggleTime, bool isTrash, Action callback)
    {
        PlayReelAnimation(struggleTime, callback);
    }

    public void SwitchFromLazyToIdle()
    {
        PlayIdleAnimation();
    }

    private IEnumerator AutoStopPlayerAnimation(float duration, Action onComplete)
    {
        Debug.Log($"[{LOG_TAG}] AutoStopPlayerAnimation() - 开始等待 {duration} 秒");
        yield return new WaitForSeconds(duration);
        Debug.Log($"[{LOG_TAG}] AutoStopPlayerAnimation() - 等待完成，关闭序列帧");

        if (aniCtrl != null)
        {
            aniCtrl.SetSpriteSheetEnabled(false);
            Debug.Log($"[{LOG_TAG}] AutoStopPlayerAnimation() - 序列帧已自动关闭");
        }

        onComplete?.Invoke();
    }

    // ========== 窝料动画 ==========
    private void InitializeNestHidden()
    {
        if (nestAniCtrl == null) return;

        nestAniCtrl.SetAlphaImmediate(0f);
        nestAniCtrl.SetSpriteSheetEnabled(false);

        StartCoroutine(DelayedConfirmNestHidden());
    }

    private IEnumerator DelayedConfirmNestHidden()
    {
        yield return null;

        if (nestAniCtrl == null) yield break;

        nestAniCtrl.SetAlphaImmediate(0f);
        nestAniCtrl.SetSpriteSheetEnabled(false);

        Material mat = nestAniCtrl.Material;
        if (mat != null)
        {
            int colorPropId = Shader.PropertyToID("_Color");
            Color currentColor = mat.GetColor(colorPropId);
            if (currentColor.a > 0.01f)
            {
                currentColor.a = 0f;
                mat.SetColor(colorPropId, currentColor);
            }
        }
    }

    public void PlayNestAnimation(float displayDuration = 2f)
    {
        if (!isInitialized)
        {
            pendingActions.Enqueue(() => PlayNestAnimation(displayDuration));
            Init();
            return;
        }

        if (nestAniCtrl == null) return;

        if (isNestPlaying) return;

        Texture2D baitTexture = nestAniCtrl.MainTexture;
        if (baitTexture == null) return;

        nestAniCtrl.SetMainTexture(baitTexture);
        nestAniCtrl.SetSpriteSheetParams(nestRows, nestColumns, nestFrameSpeed);
        nestAniCtrl.SetSpriteSheetEnabled(true);
        nestAniCtrl.SetAlphaImmediate(0f);

        isNestPlaying = true;

        if (nestAnimationCoroutine != null)
        {
            StopCoroutine(nestAnimationCoroutine);
        }
        nestAnimationCoroutine = StartCoroutine(PlayNestAnimationSequence(displayDuration));
    }

    private IEnumerator PlayNestAnimationSequence(float displayDuration)
    {
        if (nestAniCtrl == null)
        {
            isNestPlaying = false;
            yield break;
        }

        nestAniCtrl.FadeTo(1f, nestFadeInDuration);
        yield return new WaitForSeconds(nestFadeInDuration);

        float elapsed = 0f;
        while (elapsed < displayDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        nestAniCtrl.FadeTo(0f, nestFadeOutDuration);
        yield return new WaitForSeconds(nestFadeOutDuration);

        nestAniCtrl.SetSpriteSheetEnabled(false);

        isNestPlaying = false;
        nestAnimationCoroutine = null;
    }

    public void StopNestAnimation()
    {
        if (!isInitialized)
        {
            pendingActions.Enqueue(() => StopNestAnimation());
            Init();
            return;
        }

        if (nestAniCtrl == null) return;

        if (nestAnimationCoroutine != null)
        {
            StopCoroutine(nestAnimationCoroutine);
            nestAnimationCoroutine = null;
        }

        isNestPlaying = false;
        nestAniCtrl.SetAlphaImmediate(0f);
        nestAniCtrl.SetSpriteSheetEnabled(false);
    }

    // ========== 辅助方法 ==========
    private void EnsureDefaultCharacterLoaded()
    {
        int defaultId = defaultCharacterId;
        int equippedId = GetEquippedCharacterId();
        if (equippedId > 0)
        {
            defaultId = equippedId;
        }

        if (currentCharacterId != defaultId)
        {
            DoSwitchCharacter(defaultId);
            return;
        }

        if (characterAniDict.TryGetValue(currentCharacterId, out var aniData))
        {
            if (aniCtrl != null)
            {
                if (aniData.idleTexture != null)
                {
                    aniCtrl.SetMainTexture(aniData.idleTexture);
                    aniCtrl.SetSpriteSheetParams(1, aniData.idleColumns, aniData.idleSpeed);
                }
                aniCtrl.SetSpriteSheetEnabled(false);
            }
        }
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

    // ========== 稀有度颜色系统 ==========
    private void LoadRarityData()
    {
        if (isRarityDataLoaded) return;

        try
        {
            if (LoadDataManager.Instance == null) return;

            List<RarityData> rarities = LoadDataManager.Instance.rarities;
            if (rarities == null || rarities.Count == 0) return;

            rarityColorCache.Clear();
            foreach (var rarity in rarities)
            {
                if (!string.IsNullOrEmpty(rarity.colorCode) && ColorUtility.TryParseHtmlString(rarity.colorCode, out Color color))
                {
                    rarityColorCache[rarity.id] = color;
                }
            }
            isRarityDataLoaded = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerAniManager] 加载稀有度数据异常: {e.Message}");
        }
    }

    private Color GetRarityColor(int rarityId)
    {
        LoadRarityData();

        if (rarityColorCache.TryGetValue(rarityId, out Color color))
        {
            return color;
        }

        if (LoadDataManager.Instance != null)
        {
            RarityData rarity = LoadDataManager.Instance.GetRarityById(rarityId);
            if (rarity != null && !string.IsNullOrEmpty(rarity.colorCode) && ColorUtility.TryParseHtmlString(rarity.colorCode, out Color newColor))
            {
                rarityColorCache[rarityId] = newColor;
                return newColor;
            }
        }

        return Color.white;
    }

    // ========== 数据获取 ==========
    public CharacterAniData GetCharacterAniData(int characterId)
    {
        return characterAniDict.TryGetValue(characterId, out var data) ? data : null;
    }

    public int GetCurrentCharacterId()
    {
        return currentCharacterId;
    }

    // ========== Unity 生命周期 ==========
    private void Update()
    {
        // ===== 移除 Update 中的计时逻辑，完全使用协程控制 =====
        // 所有动画计时现在由 AutoStopPlayerAnimation 协程管理
        // 不再需要 Update 中的 animationTimer 倒计时
    }

    private void OnEnable()
    {
        if (isInitialized && aniCtrl != null && currentCharacterId > 0)
        {
            if (characterAniDict.TryGetValue(currentCharacterId, out var aniData))
            {
                if (aniData.idleTexture != null)
                {
                    aniCtrl.SetMainTexture(aniData.idleTexture);
                    aniCtrl.SetSpriteSheetParams(1, aniData.idleColumns, aniData.idleSpeed);
                }
                aniCtrl.SetSpriteSheetEnabled(false);
            }
        }

        if (fishTipAniCtrl == null)
        {
            EnsureFishTipAniCtrl();
        }
    }

    private void OnDestroy()
    {
        if (nestAnimationCoroutine != null)
        {
            try { StopCoroutine(nestAnimationCoroutine); }
            catch { }
            nestAnimationCoroutine = null;
        }
        isNestPlaying = false;

        if (playerAnimationCoroutine != null)
        {
            try { StopCoroutine(playerAnimationCoroutine); }
            catch { }
            playerAnimationCoroutine = null;
        }
        isAnimationPlaying = false;
        isWaitingForAnimation = false;
    }
}