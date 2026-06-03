using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 数据事件系统 - 用于解耦View层和数据层
/// View层通过订阅事件获取数据，而不是直接调用SimulationServer
/// </summary>
public class DataEventSystem : MonoBehaviour
{
    private static DataEventSystem _instance;
    public static DataEventSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                // 尝试查找已存在的实例
                GameObject existing = GameObject.Find("DataEventSystem");
                if (existing != null)
                {
                    _instance = existing.GetComponent<DataEventSystem>();
                }
                else
                {
                    GameObject go = new GameObject("DataEventSystem");
                    _instance = go.AddComponent<DataEventSystem>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        // 如果已有实例，销毁重复的
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        // 清理事件订阅
        OnGoldChanged = null;
        OnCharacterDataChanged = null;
        OnEquipChanged = null;
        OnItemQuantityChanged = null;
        OnContinuousModeChanged = null;
        OnSkillObtained = null;
        OnSkillLevelChanged = null;
        OnInventoryChanged = null;
        OnComponentLevelChanged = null;
        OnCharacterObtained = null;
        OnEquipSlotChanged = null;
        OnMallDataChanged = null;

        // 如果销毁的是单例实例，重置引用
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // 数据变更事件
    public event Action<int> OnGoldChanged;
    public event Action<int, int, int> OnCharacterDataChanged; // level, currentExp, requiredExp
    public event Action<int, int> OnEquipChanged; // slotType, itemId
    public event Action<int, int> OnItemQuantityChanged; // itemId, quantity
    public event Action<bool, float> OnContinuousModeChanged; // isActive, remainingTime
    public event Action<int> OnSkillObtained; // skillId
    public event Action<int, int> OnSkillLevelChanged; // skillId, level
    public event Action<Dictionary<int, int>> OnInventoryChanged; // 背包数据变更
    public event Action<int, int> OnComponentLevelChanged; // itemId, level
    public event Action<int, bool> OnCharacterObtained; // characterId, isObtained
    public event Action<int, int> OnEquipSlotChanged; // slotType, itemId
    public event Action<List<object>> OnMallDataChanged; // 商城数据变更

    /// <summary>
    /// 触发金币变更事件
    /// </summary>
    public void TriggerGoldChanged(int gold)
    {
        OnGoldChanged?.Invoke(gold);
    }

    /// <summary>
    /// 触发人物数据变更事件
    /// </summary>
    public void TriggerCharacterDataChanged(int level, int currentExp, int requiredExp)
    {
        OnCharacterDataChanged?.Invoke(level, currentExp, requiredExp);
    }

    /// <summary>
    /// 触发装备变更事件
    /// </summary>
    public void TriggerEquipChanged(int slotType, int itemId)
    {
        OnEquipChanged?.Invoke(slotType, itemId);
    }

    /// <summary>
    /// 触发物品数量变更事件
    /// </summary>
    public void TriggerItemQuantityChanged(int itemId, int quantity)
    {
        OnItemQuantityChanged?.Invoke(itemId, quantity);
    }

    /// <summary>
    /// 触发连续钓鱼模式变更事件
    /// </summary>
    public void TriggerContinuousModeChanged(bool isActive, float remainingTime)
    {
        OnContinuousModeChanged?.Invoke(isActive, remainingTime);
    }

    /// <summary>
    /// 触发技能获取事件
    /// </summary>
    public void TriggerSkillObtained(int skillId)
    {
        OnSkillObtained?.Invoke(skillId);
    }

    /// <summary>
    /// 触发技能等级变更事件
    /// </summary>
    public void TriggerSkillLevelChanged(int skillId, int level)
    {
        OnSkillLevelChanged?.Invoke(skillId, level);
    }

    /// <summary>
    /// 触发背包数据变更事件
    /// </summary>
    public void TriggerInventoryChanged(Dictionary<int, int> inventory)
    {
        OnInventoryChanged?.Invoke(inventory);
    }

    /// <summary>
    /// 触发组件等级变更事件
    /// </summary>
    public void TriggerComponentLevelChanged(int itemId, int level)
    {
        OnComponentLevelChanged?.Invoke(itemId, level);
    }

    /// <summary>
    /// 触发人物获取状态变更事件
    /// </summary>
    public void TriggerCharacterObtained(int characterId, bool isObtained)
    {
        OnCharacterObtained?.Invoke(characterId, isObtained);
    }

    /// <summary>
    /// 触发装备槽位变更事件
    /// </summary>
    public void TriggerEquipSlotChanged(int slotType, int itemId)
    {
        OnEquipSlotChanged?.Invoke(slotType, itemId);
    }

    /// <summary>
    /// 触发商城数据变更事件
    /// </summary>
    public void TriggerMallDataChanged(List<object> mallItems)
    {
        OnMallDataChanged?.Invoke(mallItems);
    }
}