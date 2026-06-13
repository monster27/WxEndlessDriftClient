using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using SharedModels;

public class PlayerAniManager : SingletonMono<PlayerAniManager>
{
    public PlayerAniCtrl aniCtrl;

    private Action pendingCallback;
    private float animationTimer = 0f;
    private bool isWaitingForAnimation = false;

    private Dictionary<int, CharacterAniData> characterAniDict = new Dictionary<int, CharacterAniData>();
    private int currentCharacterId = 0;

    private const string ANI_PATH_PREFIX = "JsonData/PlayerAni/Ani/";

    public void Init()
    {
        if (aniCtrl == null)
        {
            aniCtrl = GetComponent<PlayerAniCtrl>();
            if (aniCtrl == null)
            {
                aniCtrl = FindObjectOfType<PlayerAniCtrl>();
                if (aniCtrl != null)
                {
                    Debug.Log("[PlayerAniManager] 通过 FindObjectOfType 找到 PlayerAniCtrl");
                }
            }
        }
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
            // 网络模式：从服务器获取人物配置
            Debug.Log("[PlayerAniManager] 网络模式，使用默认人物配置进行预加载");
            configList = GetDefaultCharacterConfigs();
        }
        else if (CharacterServerManager.Instance != null)
        {
            // 离线模式：使用本地 CharacterServerManager
            configList = CharacterServerManager.Instance.GetAllCharacterConfigs();
        }

        // 如果以上两种方式都失败，使用默认人物配置作为后备
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

    /// <summary>
    /// 获取默认人物配置（用于网络模式下的预加载）
    /// </summary>
    private List<CharacterConfig> GetDefaultCharacterConfigs()
    {
        return new List<CharacterConfig>
        {
            new CharacterConfig { id = 3401, name = "默认人物", description = "默认角色" }
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

        // 确保动画数据已加载
        if (!characterAniDict.ContainsKey(characterId))
        {
            Debug.Log($"[PlayerAniManager] SwitchCharacter - 加载人物动画数据: {characterId}");
            LoadCharacterAnimation(characterId);
        }

        // 确保动画数据存在
        if (!characterAniDict.ContainsKey(characterId))
        {
            Debug.LogWarning($"[PlayerAniManager] SwitchCharacter - 人物动画数据加载失败: {characterId}");
            return;
        }

        currentCharacterId = characterId;

        if (aniCtrl != null)
        {
            var aniData = characterAniDict[characterId];
            // 先设置动画参数（列数和速度），再设置纹理
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

        if (aniCtrl != null)
        {
            // 如果鱼篓已满，不播放拉钩动画
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsFishBagFull())
            {
                Debug.Log("[PlayerAniManager] 鱼篓已满，不播放拉钩动画");
                callback?.Invoke();
                return;
            }

            // 如果已经在播放拉钩动画，忽略新请求
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
        if (aniCtrl != null)
        {
            aniCtrl.PlayIdleAnimation();
            Debug.Log("[PlayerAniManager] Switched from Lazy to Idle animation");
        }
    }

    public void PlayLazyAnimation()
    {
        Debug.Log("[PlayerAniManager] PlayLazyAnimation called");
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
}

public class CharacterAniData
{
    public int characterId;
    public Texture2D idleTexture;
    public Texture2D lazyTexture;
    public Texture2D reelTexture;
    
    // 动画参数
    public int idleColumns = 4;
    public float idleSpeed = 15f;
    public int reelColumns = 4;
    public float reelSpeed = 20f;
    public int lazyColumns = 4;
    public float lazySpeed = 18f;
}

