// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SimulationServer 组件管理和辅助方法部分
/// </summary>
public partial class SimulationServer : MonoBehaviour
{
    #region 组件管理

    /// <summary>
    /// 装备钓鱼组件
    /// </summary>
    /// <param name="config">组件配置</param>
    /// <param name="level">组件等级（默认1级）</param>
    public void EquipComponent(FishingComponentConfig config, int level = 1)
    {
        playerAbilityManager?.EquipComponent(config, level);
        Debug.LogFormat("<color=orange>[SimulationServer] 装备组件: ID={0}, 等级={1}</color>", config.id, level);
    }

    /// <summary>
    /// 卸下钓鱼组件
    /// </summary>
    /// <param name="componentId">组件ID</param>
    public void UnequipComponent(int componentId)
    {
        playerAbilityManager?.UnequipComponentById(componentId);
        Debug.LogFormat("<color=orange>[SimulationServer] 卸下组件: ID={0}</color>", componentId);
    }

    /// <summary>
    /// 升级组件等级
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <param name="newLevel">新等级</param>
    public void UpgradeComponentLevel(int componentId, int newLevel)
    {
        playerAbilityManager?.UpgradeComponent(componentId, newLevel);
        Debug.LogFormat("<color=orange>[SimulationServer] 升级组件: ID={0}, 新等级={1}</color>", componentId, newLevel);
    }

    /// <summary>
    /// 获取已装备的组件列表
    /// </summary>
    /// <returns>已装备组件列表</returns>
    public List<PlayerFishingComponent> GetEquippedComponents()
    {
        return playerAbilityManager?.GetAllEquippedComponents() ?? new List<PlayerFishingComponent>();
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取钓鱼算法服务器管理器
    /// </summary>
    public FishingAlgorithmServerManager GetFishingAlgorithmManager()
    {
        return fishingAlgorithmManager;
    }

    /// <summary>
    /// 获取场景鱼池服务器管理器
    /// </summary>
    public SceneFishPoolServerManager GetSceneFishPoolManager()
    {
        return sceneFishPoolManager;
    }

    #endregion
}
*/