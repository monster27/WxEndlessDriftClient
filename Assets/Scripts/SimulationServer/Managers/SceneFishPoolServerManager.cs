using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 场景鱼库服务器管理器
/// 负责管理不同场景的鱼群数据，提供场景切换和鱼库查询功能
/// </summary>
public class SceneFishPoolServerManager
{
    /// <summary>当前场景ID</summary>
    public int CurrentSceneId { get; private set; } = 1;

    /// <summary>
    /// 初始化场景鱼库管理器
    /// </summary>
    public void Initialize()
    {
        Debug.Log("[SceneFishPoolServerManager] 场景鱼库服务器管理器初始化完成");
    }

    /// <summary>
    /// 切换场景
    /// </summary>
    /// <param name="sceneId">目标场景ID</param>
    public void SwitchScene(int sceneId)
    {
        CurrentSceneId = sceneId;
        Debug.Log($"[SceneFishPoolServerManager] 切换到场景: {sceneId}");
    }

    /// <summary>
    /// 获取指定场景的鱼库
    /// </summary>
    /// <param name="sceneId">场景ID</param>
    /// <returns>该场景的鱼类列表</returns>
    public List<FishData> GetSceneFishPool(int sceneId)
    {
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.fishes != null)
        {
            return new List<FishData>(LoadDataManager.Instance.fishes);
        }
        return new List<FishData>();
    }
}