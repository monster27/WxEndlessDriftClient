using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using SharedModels;

public class PlayerAniManager : SingletonMono<PlayerAniManager>
{
    [Header("拖拽赋值（推荐）")]
    [SerializeField] private PlayerAniCtrl aniCtrl;

    private Action pendingCallback;
    private float animationTimer = 0f;
    private bool isWaitingForAnimation = false;

    private Dictionary<int, CharacterAniData> characterAniDict = new Dictionary<int, CharacterAniData>();
    private int currentCharacterId = 0;

    private const string ANI_PATH_PREFIX = "JsonData/PlayerAni/Ani/";

    // ========== 获取 aniCtrl 的统一方法 ==========
    private PlayerAniCtrl GetAniCtrl()
    {
        // 1. 优先使用拖拽赋值
        if (aniCtrl != null)
        {
            return aniCtrl;
        }

        // 2. 备用：通过 Tag 查找 Player 物体上的 PlayerAniCtrl
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            aniCtrl = playerObj.GetComponent<PlayerAniCtrl>();
            if (aniCtrl == null)
            {
                aniCtrl = playerObj.GetComponentInChildren<PlayerAniCtrl>();
            }
            if (aniCtrl != null)
            {
                Debug.Log($"[PlayerAniManager] 通过 Tag('Player') 找到 PlayerAniCtrl，所在物体: {playerObj.name}");
                return aniCtrl;
            }
        }

        // 3. 最后备用：在整个场景中查找
        aniCtrl = FindObjectOfType<PlayerAniCtrl>();
        if (aniCtrl != null)
        {
            Debug.Log($"[PlayerAniManager] 通过 FindObjectOfType 找到 PlayerAniCtrl，所在物体: {aniCtrl.gameObject.name}");
            return aniCtrl;
        }

        Debug.LogWarning("[PlayerAniManager] 无法找到 PlayerAniCtrl 组件！请拖拽赋值或确保场景中有 Tag 为 'Player' 的物体挂载 PlayerAniCtrl。");
        return null;
    }

    protected override void Awake()
    {
        PlayerAniCtrl savedAniCtrl = aniCtrl;

        base.Awake();

        if (savedAniCtrl != null)
        {
            aniCtrl = savedAniCtrl;
            Debug.Log("[PlayerAniManager] Awake - 从序列化引用恢复 PlayerAniCtrl");
        }
        else
        {
            GetAniCtrl();
        }

        if (aniCtrl != null)
        {
            Debug.Log("[PlayerAniManager] Awake - 获取到 PlayerAniCtrl 引用");
        }
        else
        {
            Debug.LogWarning("[PlayerAniManager] Awake - 未找到 PlayerAniCtrl，将在 Init 时再次尝试");
        }
    }

    public void Init()
    {
        // 获取 aniCtrl 引用
        PlayerAniCtrl ctrl = GetAniCtrl();

        if (ctrl == null)
        {
            Debug.LogError("[PlayerAniManager] Init - 无法找到 PlayerAniCtrl 组件！请拖拽赋值或确保场景中有 Tag 为 'Player' 的物体。");
            return;
        }

        aniCtrl = ctrl;

        PreloadAllCharacterAnimations();

        if (aniCtrl != null && characterAniDict.Count > 0)
        {
            SwitchCharacter(3401);
        }

        Debug.Log("[PlayerAniManager] 初始化完成，已预加载 " + characterAniDict.Count + " 个人物的动画资源");
    }

    private void PreloadAllCharacterAnimations()
    {
        List<CharacterConfig> configList = null;

        // 检查是否处于网络模式
        if (NetServerManager.Instance != null && NetServerManager.Instance.IsConnected)
        {
            Debug.Log("[PlayerAniManager] 网络模式，使用默认人物配置进行预加载");
            configList = GetDefaultCharacterConfigs();
        }
        else if (CharacterServerManager.Instance != null)
        {
            configList = CharacterServerManager.Instance.GetAllCharacterConfigs();
        }

        if (configList == null || configList.Count == 0)
        {
            Debug.LogWarning("[PlayerAniManager] 未找到人物配置，使用默认人物配置");
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
            new CharacterConfig { id = 3401, name = "默认人物", description = "默认角色" },
            new CharacterConfig { id = 3402, name = "钓鱼大师", description = "大师角色" }
        };
    }

    private void LoadCharacterAnimation(int characterId)
    {
        CharacterAniData aniData;

        if (characterAniDict.ContainsKey(characterId))
        {
            aniData = characterAniDict[characterId];
        }
        else
        {
            aniData = new CharacterAniData();
            aniData.characterId = characterId;
            characterAniDict[characterId] = aniData;
        }

        CharacterConfig characterConfig = GetCharacterConfig(characterId);
        if (characterConfig != null)
        {
            aniData.idleColumns = characterConfig.idleColumns;
            aniData.idleSpeed = characterConfig.idleSpeed;
            aniData.reelColumns = characterConfig.reelColumns;
            aniData.reelSpeed = characterConfig.reelSpeed;
            aniData.lazyColumns = characterConfig.lazyColumns;
            aniData.lazySpeed = characterConfig.lazySpeed;
            Debug.Log($"[PlayerAniManager] 从人物配置加载动画参数 - characterId: {characterId}, idle: {aniData.idleColumns}x{aniData.idleSpeed}, reel: {aniData.reelColumns}x{aniData.reelSpeed}, lazy: {aniData.lazyColumns}x{aniData.lazySpeed}");
        }

        string basePath = ANI_PATH_PREFIX + characterId;

        Texture2D idleTex = Resources.Load<Texture2D>(basePath + "/Idle");
        Texture2D lazyTex = Resources.Load<Texture2D>(basePath + "/Lazy");
        Texture2D reelTex = Resources.Load<Texture2D>(basePath + "/Reel");

        if (idleTex != null)
        {
            aniData.idleTexture = idleTex;
            Debug.Log($"[PlayerAniManager] 加载人物 {characterId} Idle 动画成功");
        }
        else
        {
            Debug.LogWarning($"[PlayerAniManager] 人物 {characterId} Idle 动画加载失败: {basePath}/Idle");
        }

        if (lazyTex != null)
        {
            aniData.lazyTexture = lazyTex;
            Debug.Log($"[PlayerAniManager] 人物 {characterId} Lazy 动画成功");
        }
        else
        {
            Debug.LogWarning($"[PlayerAniManager] 人物 {characterId} Lazy 动画加载失败: {basePath}/Lazy");
        }

        if (reelTex != null)
        {
            aniData.reelTexture = reelTex;
            Debug.Log($"[PlayerAniManager] 人物 {characterId} Reel 动画成功");
        }
        else
        {
            Debug.LogWarning($"[PlayerAniManager] 人物 {characterId} Reel 动画加载失败: {basePath}/Reel");
        }
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

    public void SwitchCharacter(int characterId)
    {
        if (currentCharacterId == characterId)
        {
            Debug.Log($"[PlayerAniManager] SwitchCharacter - 人物ID相同，无需切换: {characterId}");
            return;
        }

        // 获取 aniCtrl 引用
        PlayerAniCtrl ctrl = GetAniCtrl();
        if (ctrl == null)
        {
            Debug.LogError($"[PlayerAniManager] SwitchCharacter - 无法获取 PlayerAniCtrl，无法切换人物动画: {characterId}");
            return;
        }
        aniCtrl = ctrl;

        // 确保动画数据已加载
        if (!characterAniDict.ContainsKey(characterId))
        {
            Debug.Log($"[PlayerAniManager] SwitchCharacter - 加载人物动画数据: {characterId}");
            LoadCharacterAnimation(characterId);
        }

        if (!characterAniDict.ContainsKey(characterId))
        {
            Debug.LogWarning($"[PlayerAniManager] SwitchCharacter - 人物动画数据加载失败: {characterId}");
            return;
        }

        currentCharacterId = characterId;

        if (aniCtrl != null)
        {
            var aniData = characterAniDict[characterId];
            aniCtrl.SetAnimationParams(aniData.idleColumns, aniData.idleSpeed, aniData.reelColumns, aniData.reelSpeed, aniData.lazyColumns, aniData.lazySpeed);
            aniCtrl.SetCharacterTextures(aniData.idleTexture, aniData.lazyTexture, aniData.reelTexture);
            Debug.Log($"[PlayerAniManager] 切换人物动画: characterId={characterId}, 参数已更新");
        }
        else
        {
            Debug.LogWarning($"[PlayerAniManager] SwitchCharacter - aniCtrl is null, 无法更新人物动画: {characterId}");
        }
    }

    public CharacterAniData GetCharacterAniData(int characterId)
    {
        if (characterAniDict.ContainsKey(characterId))
        {
            return characterAniDict[characterId];
        }
        return null;
    }

    public int GetCurrentCharacterId()
    {
        return currentCharacterId;
    }

    public void PlayIdleAnimation()
    {
        Debug.Log("[PlayerAniManager] PlayIdleAnimation called");

        PlayerAniCtrl ctrl = GetAniCtrl();
        if (ctrl == null)
        {
            Debug.LogWarning("[PlayerAniManager] PlayIdleAnimation - 无法获取 PlayerAniCtrl");
            return;
        }
        aniCtrl = ctrl;

        if (aniCtrl != null)
        {
            aniCtrl.PlayIdleAnimation();
            Debug.Log("[PlayerAniManager] PlayIdleAnimation executed");
        }
        else
        {
            Debug.LogWarning("[PlayerAniManager] aniCtrl is null, cannot play idle animation");
        }
    }

    public void PlayReelAnimation(float duration, Action callback)
    {
        Debug.Log($"[PlayerAniManager] PlayReelAnimation with callback, duration: {duration}");

        PlayerAniCtrl ctrl = GetAniCtrl();
        if (ctrl == null)
        {
            Debug.LogWarning("[PlayerAniManager] PlayReelAnimation - 无法获取 PlayerAniCtrl");
            callback?.Invoke();
            return;
        }
        aniCtrl = ctrl;

        if (aniCtrl != null)
        {
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsFishBagFull())
            {
                Debug.Log("[PlayerAniManager] 鱼篓已满，不播放拉钩动画");
                callback?.Invoke();
                return;
            }

            if (isWaitingForAnimation)
            {
                Debug.Log("[PlayerAniManager] 已有拉钩动画正在播放，忽略新请求");
                callback?.Invoke();
                return;
            }

            aniCtrl.PlayReelAnimation();
            pendingCallback = callback;
            animationTimer = duration;
            isWaitingForAnimation = true;
        }
        else
        {
            callback?.Invoke();
        }
    }

    public void PlayReelAnimationWithTwoIds(int detectedFishId, int actualItemId, float struggleTime, bool isTrash, Action callback)
    {
        Debug.Log($"[PlayerAniManager] PlayReelAnimationWithTwoIds called - 第一个ID={detectedFishId}, 第二个ID={actualItemId}, 挣扎时间={struggleTime}秒, 是否垃圾={isTrash}");

        PlayReelAnimation(struggleTime, () => {
            Debug.Log("[PlayerAniManager] 拉杆动画结束，调用回调");
            callback?.Invoke();
        });
    }

    public void SwitchFromLazyToIdle()
    {
        Debug.Log("[PlayerAniManager] SwitchFromLazyToIdle called");

        PlayerAniCtrl ctrl = GetAniCtrl();
        if (ctrl == null)
        {
            Debug.LogWarning("[PlayerAniManager] SwitchFromLazyToIdle - 无法获取 PlayerAniCtrl");
            return;
        }
        aniCtrl = ctrl;

        if (aniCtrl != null)
        {
            aniCtrl.PlayIdleAnimation();
            Debug.Log("[PlayerAniManager] Switched from Lazy to Idle animation");
        }
    }

    public void PlayLazyAnimation()
    {
        Debug.Log("[PlayerAniManager] PlayLazyAnimation called");

        PlayerAniCtrl ctrl = GetAniCtrl();
        if (ctrl == null)
        {
            Debug.LogWarning("[PlayerAniManager] PlayLazyAnimation - 无法获取 PlayerAniCtrl");
            return;
        }
        aniCtrl = ctrl;

        if (aniCtrl != null)
        {
            aniCtrl.PlayLazyAnimation();
            Debug.Log("[PlayerAniManager] PlayLazyAnimation executed");
        }
        else
        {
            Debug.LogWarning("[PlayerAniManager] aniCtrl is null, cannot play lazy animation");
        }
    }

    void Update()
    {
        if (isWaitingForAnimation)
        {
            animationTimer -= Time.deltaTime;
            if (animationTimer <= 0f)
            {
                isWaitingForAnimation = false;
                pendingCallback?.Invoke();
                pendingCallback = null;
            }
        }
    }

    private void OnEnable()
    {
        // 场景切换后，如果 aniCtrl 丢失，重新获取
        if (aniCtrl == null)
        {
            GetAniCtrl();
            if (aniCtrl != null)
            {
                Debug.Log("[PlayerAniManager] OnEnable - 重新获取到 PlayerAniCtrl 引用");
            }
        }
    }

    // ========== 编辑器辅助 ==========
    private void OnValidate()
    {
        // 在 Inspector 中如果拖拽了 aniCtrl，直接使用
        if (aniCtrl != null)
        {
            Debug.Log("[PlayerAniManager] 已通过拖拽赋值 PlayerAniCtrl");
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