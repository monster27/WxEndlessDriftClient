using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SharedModels;

/// <summary>
/// 玩家动画管理器
/// </summary>
public class PlayerAniManager : SingletonMonoFromScene<PlayerAniManager>
{
    private const string LOG_TAG = "SceneMat - PlayerAniManager";

    public enum PlayerAnimState
    {
        Idle,
        Reel,
        Lazy
    }

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

    private static Dictionary<int, Color> rarityColorCache = new Dictionary<int, Color>();
    private static bool isRarityDataLoaded = false;

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

    private Dictionary<int, CharacterAniData> characterAniDict = new Dictionary<int, CharacterAniData>();
    private int currentCharacterId = 0;
    private PlayerAnimState currentPlayerState = PlayerAnimState.Idle;

    private bool isInitialized = false;
    private bool isInitializing = false;
    private int pendingCharacterId = 0;
    private Queue<Action> pendingActions = new Queue<Action>();

    private Coroutine nestAnimationCoroutine;
    private bool isNestPlaying = false;
    private Coroutine playerAnimationCoroutine;
    private bool isWaitingForAnimation = false;
    private bool isAnimationPlaying = false;

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
            // 1. 查找 aniCtrl
            FindAniCtrl();

            // 2. 如果 aniCtrl 为空，延迟重试
            if (aniCtrl == null)
            {
                isInitializing = false;
                StartCoroutine(DelayedInit());
                return;
            }

            // 3. 确保 aniCtrl 已初始化
            if (!aniCtrl.IsInitialized)
            {
                try
                {
                    aniCtrl.Initialize();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{LOG_TAG}] aniCtrl.Initialize() 失败: {ex.Message}");
                    isInitializing = false;
                    StartCoroutine(DelayedInit());
                    return;
                }
            }

            // 4. 查找 fishTipAniCtrl
            FindFishTipAniCtrl();

            // 5. 加载人物动画
            PreloadAllCharacterAnimations();

            // 6. 初始化窝料
            InitializeNestHidden();

            isInitialized = true;
            isInitializing = false;

            // 7. 播放 Idle
            StartCoroutine(DelayedPlayIdle());

            // 8. 检查鱼篓状态
            CheckFishBagStateAndPlayAnimation();
            ExecutePendingActions();

            Debug.Log($"[{LOG_TAG}] ✅ 初始化完成");
        }
        catch (Exception e)
        {
            isInitializing = false;
            Debug.LogError($"[{LOG_TAG}] ❌ Init() 异常: {e.Message}\n{e.StackTrace}");
            StartCoroutine(DelayedInit());
        }
    }

    private void FindAniCtrl()
    {
        if (aniCtrl != null) return;

        // 通过 ElementId 查找
        SceneMatCtrl[] ctrls = FindObjectsOfType<SceneMatCtrl>();
        foreach (var ctrl in ctrls)
        {
            if (ctrl.ElementId == SceneMatManager.ElementType.Player)
            {
                aniCtrl = ctrl;
                return;
            }
        }

        // 通过 Tag 查找
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            aniCtrl = playerObj.GetComponent<SceneMatCtrl>();
            if (aniCtrl == null) aniCtrl = playerObj.GetComponentInChildren<SceneMatCtrl>();
        }

        // 保底：查找任意 SceneMatCtrl
        if (aniCtrl == null)
        {
            aniCtrl = FindObjectOfType<SceneMatCtrl>();
        }
    }

    private void FindFishTipAniCtrl()
    {
        if (fishTipAniCtrl != null) return;

        // 通过 Tag 查找
        GameObject fishTipObj = GameObject.FindGameObjectWithTag("FishTip");
        if (fishTipObj != null)
        {
            fishTipAniCtrl = fishTipObj.GetComponent<SceneMatCtrl>();
            if (fishTipAniCtrl == null) fishTipAniCtrl = fishTipObj.GetComponentInChildren<SceneMatCtrl>();
        }

        // 通过 ElementId 查找
        if (fishTipAniCtrl == null)
        {
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
    }

    private IEnumerator DelayedInit()
    {
        int retryCount = 0;
        int maxRetries = 20;

        while (retryCount < maxRetries)
        {
            yield return new WaitForSeconds(0.5f);
            retryCount++;

            try
            {
                FindAniCtrl();

                if (aniCtrl != null)
                {
                    if (!aniCtrl.IsInitialized)
                    {
                        try { aniCtrl.Initialize(); }
                        catch { continue; }
                    }

                    FindFishTipAniCtrl();
                    PreloadAllCharacterAnimations();
                    InitializeNestHidden();

                    isInitialized = true;
                    isInitializing = false;

                    if (pendingCharacterId > 0)
                    {
                        DoSwitchCharacter(pendingCharacterId);
                        pendingCharacterId = 0;
                    }

                    CheckFishBagStateAndPlayAnimation();
                    ExecutePendingActions();

                    Debug.Log($"[{LOG_TAG}] ✅ 延迟初始化完成 (第 {retryCount} 次尝试)");
                    yield break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{LOG_TAG}] 延迟初始化第 {retryCount} 次失败: {e.Message}");
            }
        }

        isInitializing = false;
        Debug.LogError($"[{LOG_TAG}] ❌ 延迟初始化超时，请检查 SceneMatCtrl 是否存在于场景中");
    }

    private IEnumerator DelayedPlayIdle()
    {
        yield return null; // 等待一帧

        if (aniCtrl != null && aniCtrl.IsInitialized)
        {
            EnsureDefaultCharacterLoaded();
            PlayPlayerAnimation(PlayerAnimState.Idle);
        }
    }

    // ========== 人物动画加载 ==========
    private void PreloadAllCharacterAnimations()
    {
        if (LoadDataManager.Instance == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] LoadDataManager 未初始化");
            return;
        }

        var characterConfigs = LoadDataManager.Instance.characters;
        if (characterConfigs == null || characterConfigs.Count == 0)
        {
            Debug.LogWarning($"[{LOG_TAG}] 没有人物配置数据");
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

        string basePath = "GameScene/Player/Ani/" + config.id;

        string idlePath = !string.IsNullOrEmpty(config.idleTexturePath) ? config.idleTexturePath : basePath + "/Idle";
        string reelPath = !string.IsNullOrEmpty(config.reelTexturePath) ? config.reelTexturePath : basePath + "/Reel";
        string lazyPath = !string.IsNullOrEmpty(config.lazyTexturePath) ? config.lazyTexturePath : basePath + "/Lazy";

        // 使用 Resources.Load 加载纹理
        aniData.idleTexture = Resources.Load<Texture2D>(idlePath);
        aniData.reelTexture = Resources.Load<Texture2D>(reelPath);
        aniData.lazyTexture = Resources.Load<Texture2D>(lazyPath);

        // 如果加载失败，尝试备用路径
        if (aniData.reelTexture == null)
        {
            string[] altPaths = new string[]
            {
                "JsonData/PlayerAni/Ani/" + config.id + "/Reel",
                "PlayerAni/Ani/" + config.id + "/Reel",
                "Ani/" + config.id + "/Reel"
            };

            foreach (string altPath in altPaths)
            {
                aniData.reelTexture = Resources.Load<Texture2D>(altPath);
                if (aniData.reelTexture != null) break;
            }
        }

        characterAniDict[config.id] = aniData;
        Debug.Log($"[{LOG_TAG}] 加载人物 {config.id} ({config.name}) 动画完成");
    }

    private CharacterConfig GetCharacterConfigFromLoadData(int characterId)
    {
        if (LoadDataManager.Instance == null) return null;
        return LoadDataManager.Instance.GetCharacterConfig(characterId);
    }

    private int GetEquippedCharacterId()
    {
        try
        {
            return CommunicateEvent.Request<EquipmentSlotType, int>(
                CommunicateEvent.EVENT_GET_EQUIPPED_ITEM,
                EquipmentSlotType.Character);
        }
        catch
        {
            return 0;
        }
    }

    // ========== 人物切换 ==========
    private void DoSwitchCharacter(int characterId)
    {
        if (currentCharacterId == characterId) return;

        if (!characterAniDict.ContainsKey(characterId))
        {
            var config = GetCharacterConfigFromLoadData(characterId);
            if (config != null) LoadCharacterAnimation(config);
            else return;
        }

        if (!characterAniDict.TryGetValue(characterId, out var aniData)) return;

        currentCharacterId = characterId;

        if (aniCtrl == null) return;

        if (!aniCtrl.IsInitialized)
        {
            try { aniCtrl.Initialize(); }
            catch { return; }
        }

        if (aniData.idleTexture != null)
        {
            aniCtrl.SetMainTexture(aniData.idleTexture);
            aniCtrl.SetSpriteSheetParams(1, aniData.idleColumns, aniData.idleSpeed);
            aniCtrl.SetSpriteSheetEnabled(false);
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

    // ========== 人物动画播放 ==========
    private void PlayPlayerAnimation(PlayerAnimState state)
    {
        if (aniCtrl == null || !aniCtrl.IsInitialized) return;

        if (currentCharacterId == 0)
        {
            EnsureDefaultCharacterLoaded();
        }

        if (!characterAniDict.TryGetValue(currentCharacterId, out var data)) return;

        if (playerAnimationCoroutine != null)
        {
            StopCoroutine(playerAnimationCoroutine);
            playerAnimationCoroutine = null;
            isAnimationPlaying = false;
        }

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

        if (targetTexture == null)
        {
            Debug.LogWarning($"[{LOG_TAG}] 纹理为空，无法播放 {state}");
            return;
        }

        aniCtrl.SetMainTexture(targetTexture);
        aniCtrl.SetSpriteSheetParams(1, columns, speed);
        aniCtrl.SetSpriteSheetEnabled(true);
        currentPlayerState = state;
    }

    private void StopPlayerAnimation()
    {
        if (aniCtrl == null) return;

        if (playerAnimationCoroutine != null)
        {
            StopCoroutine(playerAnimationCoroutine);
            playerAnimationCoroutine = null;
            isAnimationPlaying = false;
        }

        aniCtrl.SetSpriteSheetEnabled(false);
        isWaitingForAnimation = false;
    }

    // ========== 公共动画方法 ==========
    public void PlayIdleAnimation()
    {
        if (!isInitialized)
        {
            pendingActions.Enqueue(() => PlayIdleAnimation());
            Init();
            return;
        }

        if (aniCtrl == null || !aniCtrl.IsInitialized) return;

        if (currentCharacterId == 0) EnsureDefaultCharacterLoaded();

        PlayPlayerAnimation(PlayerAnimState.Idle);
    }

    public void PlayLazyAnimation()
    {
        if (!isInitialized)
        {
            pendingActions.Enqueue(() => PlayLazyAnimation());
            Init();
            return;
        }

        if (aniCtrl == null || !aniCtrl.IsInitialized) return;

        if (currentCharacterId == 0) EnsureDefaultCharacterLoaded();

        StopFishTip();
        PlayPlayerAnimation(PlayerAnimState.Lazy);
    }

    public void PlayReelAnimation(float duration, Action callback)
    {
        if (!isInitialized)
        {
            pendingActions.Enqueue(() => PlayReelAnimation(duration, callback));
            Init();
            return;
        }

        if (aniCtrl == null || !aniCtrl.IsInitialized)
        {
            callback?.Invoke();
            return;
        }

        if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsFishBagFull())
        {
            callback?.Invoke();
            return;
        }

        if (isWaitingForAnimation || isAnimationPlaying)
        {
            pendingActions.Enqueue(() => callback?.Invoke());
            return;
        }

        if (currentCharacterId == 0) EnsureDefaultCharacterLoaded();

        PlayPlayerAnimation(PlayerAnimState.Reel);
        isAnimationPlaying = true;
        isWaitingForAnimation = true;

        if (playerAnimationCoroutine != null) StopCoroutine(playerAnimationCoroutine);

        playerAnimationCoroutine = StartCoroutine(AutoStopPlayerAnimation(duration, () =>
        {
            isWaitingForAnimation = false;
            isAnimationPlaying = false;
            playerAnimationCoroutine = null;
            callback?.Invoke();

            if (pendingActions.Count > 0) ExecutePendingActions();
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
        yield return new WaitForSeconds(duration);

        if (aniCtrl != null)
        {
            aniCtrl.SetSpriteSheetEnabled(false);
        }

        onComplete?.Invoke();
    }

    // ========== 鱼篓状态检查 ==========
    private void CheckFishBagStateAndPlayAnimation()
    {
        try
        {
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsFishBagFull())
            {
                PlayLazyAnimation();
            }
        }
        catch { }
    }

    // ========== 窝料动画 ==========
    private void InitializeNestHidden()
    {
        if (nestAniCtrl == null) return;

        try
        {
            nestAniCtrl.SetAlphaImmediate(0f);
            nestAniCtrl.SetSpriteSheetEnabled(false);
        }
        catch { }
    }

    public void PlayNestAnimation(float displayDuration = 2f)
    {
        if (!isInitialized)
        {
            pendingActions.Enqueue(() => PlayNestAnimation(displayDuration));
            Init();
            return;
        }

        if (nestAniCtrl == null || isNestPlaying) return;

        Texture2D baitTexture = nestAniCtrl.MainTexture;
        if (baitTexture == null) return;

        nestAniCtrl.SetMainTexture(baitTexture);
        nestAniCtrl.SetSpriteSheetParams(nestRows, nestColumns, nestFrameSpeed);
        nestAniCtrl.SetSpriteSheetEnabled(true);
        nestAniCtrl.SetAlphaImmediate(0f);

        isNestPlaying = true;

        if (nestAnimationCoroutine != null) StopCoroutine(nestAnimationCoroutine);
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

    // ========== 闪烁功能 ==========
    public void SetFishTip(Color color, float struggleTime = 2)
    {
        if (fishTipAniCtrl == null) return;

        fishTipAniCtrl.SetBlinkColor(color);
        fishTipAniCtrl.SetBlinkInterval(defaultBlinkInterval);
        fishTipAniCtrl.SetBlinkEnabled(true);
        StartCoroutine(AutoStopBlinkCoroutine(fishTipAniCtrl, struggleTime));
    }

    public void SetFishTip(int rarityId)
    {
        Color color = GetRarityColor(rarityId);
        SetFishTip(color, defaultBlinkDuration);
    }

    public void StopFishTip()
    {
        if (fishTipAniCtrl != null) fishTipAniCtrl.SetBlinkEnabled(false);
    }

    private IEnumerator AutoStopBlinkCoroutine(SceneMatCtrl ctrl, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (ctrl != null) ctrl.SetBlinkEnabled(false);
    }

    // ========== 辅助方法 ==========
    private void EnsureDefaultCharacterLoaded()
    {
        int targetId = defaultCharacterId;
        int equippedId = GetEquippedCharacterId();

        if (equippedId > 0) targetId = equippedId;

        if (currentCharacterId != targetId)
        {
            DoSwitchCharacter(targetId);
        }
        else if (currentCharacterId == 0)
        {
            DoSwitchCharacter(targetId);
        }
    }

    private void ExecutePendingActions()
    {
        while (pendingActions.Count > 0)
        {
            pendingActions.Dequeue()?.Invoke();
        }
    }

    // ========== 稀有度颜色系统 ==========
    private void LoadRarityData()
    {
        if (isRarityDataLoaded) return;

        try
        {
            if (LoadDataManager.Instance == null) return;

            var rarities = LoadDataManager.Instance.rarities;
            if (rarities == null) return;

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
        catch { }
    }

    private Color GetRarityColor(int rarityId)
    {
        LoadRarityData();

        if (rarityColorCache.TryGetValue(rarityId, out Color color)) return color;

        if (LoadDataManager.Instance != null)
        {
            var rarity = LoadDataManager.Instance.GetRarityById(rarityId);
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

    public int GetCurrentCharacterId() => currentCharacterId;

    // ========== Unity 生命周期 ==========
    private void OnEnable()
    {
        if (isInitialized && aniCtrl != null && currentCharacterId > 0)
        {
            if (characterAniDict.TryGetValue(currentCharacterId, out var aniData) && aniData.idleTexture != null)
            {
                aniCtrl.SetMainTexture(aniData.idleTexture);
                aniCtrl.SetSpriteSheetParams(1, aniData.idleColumns, aniData.idleSpeed);
                aniCtrl.SetSpriteSheetEnabled(false);
            }
        }

        if (fishTipAniCtrl == null) FindFishTipAniCtrl();
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
