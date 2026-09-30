// ============================================================
// 文件: NetServerManager.Insect.cs
// 说明: 昆虫系统网络请求（列表、卖一只、批量卖）
// 路径: Assets/Scripts/Network/
// ============================================================

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

public partial class NetServerManager
{
    // ============================================================
    // 缓存数据
    // ============================================================

    /// <summary>当前缓存的昆虫列表</summary>
    private List<PlayerInsectData> playerInsectsCache = new List<PlayerInsectData>();

    /// <summary>公开访问器</summary>
    public List<PlayerInsectData> PlayerInsects => playerInsectsCache;

    // ============================================================
    // 昆虫列表
    // ============================================================

    public void FetchPlayerInsects(Action<bool, List<PlayerInsectData>> onComplete = null)
    {
        StartCoroutine(FetchPlayerInsectsCoroutine(onComplete));
    }

    private IEnumerator FetchPlayerInsectsCoroutine(Action<bool, List<PlayerInsectData>> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, null);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/insects";
        Z_Logger.Log($"[NetServerManager] 获取昆虫列表: {url}");

        yield return FetchGetJson<InsectsListResponse>(url, data =>
        {
            if (data != null && data.success)
            {
                playerInsectsCache = data.insects ?? new List<PlayerInsectData>();
                Z_Logger.Log($"[NetServerManager] 昆虫列表加载完成，共 {playerInsectsCache.Count} 只");

                // 同步到 PlayerDataManager
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.UpdatePlayerInsects(playerInsectsCache);
                }

                CommunicateEvent.Modify("Insects_Updated");
                onComplete?.Invoke(true, playerInsectsCache);
            }
            else
            {
                Z_Logger.LogWarning("[NetServerManager] 获取昆虫列表失败");
                onComplete?.Invoke(false, null);
            }
        }, "昆虫列表");
    }

    // ============================================================
    // 卖一只
    // ============================================================

    public void SellInsect(int insectInstanceId, Action<bool, string, int> onComplete = null)
    {
        StartCoroutine(SellInsectCoroutine(insectInstanceId, onComplete));
    }

    private IEnumerator SellInsectCoroutine(int insectInstanceId, Action<bool, string, int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/insects/{insectInstanceId}/sell";
        Z_Logger.Log($"[NetServerManager] 卖昆虫: instanceId={insectInstanceId}");

        yield return SendRequest<SellInsectResponse>(url, null, resp =>
        {
            if (resp != null && resp.success)
            {
                playerInsectsCache.RemoveAll(i => i.insectInstanceId == insectInstanceId);

                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.RemoveInsect(insectInstanceId);
                }

                Z_Logger.Log($"[NetServerManager] 卖昆虫成功，获得 {resp.goldEarned} 金币");

                StartCoroutine(FetchPlayerGold());

                CommunicateEvent.Modify("Insects_Updated");
                onComplete?.Invoke(true, resp.message, resp.goldEarned);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "卖出失败", 0);
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败", 0);
        }, forcePost: true);
    }

    // ============================================================
    // 批量卖
    // ============================================================

    public void SellInsectsBatch(List<int> instanceIds, Action<bool, string, int> onComplete = null)
    {
        StartCoroutine(SellInsectsBatchCoroutine(instanceIds, onComplete));
    }

    private IEnumerator SellInsectsBatchCoroutine(List<int> instanceIds, Action<bool, string, int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0);
            yield break;
        }

        if (instanceIds == null || instanceIds.Count == 0)
        {
            onComplete?.Invoke(false, "请选择要出售的昆虫", 0);
            yield break;
        }

        string url = $"/api/player/{_currentPlayerId}/insects/sell-batch";
        var requestData = new Dictionary<string, object>
        {
            { "instanceIds", instanceIds }
        };

        Z_Logger.Log($"[NetServerManager] 批量卖昆虫: count={instanceIds.Count}");

        yield return SendRequest<SellInsectResponse>(url, requestData, resp =>
        {
            if (resp != null && resp.success)
            {
                playerInsectsCache.RemoveAll(i => instanceIds.Contains(i.insectInstanceId));

                if (PlayerDataManager.Instance != null)
                {
                    foreach (var id in instanceIds)
                    {
                        PlayerDataManager.Instance.RemoveInsect(id);
                    }
                }

                Z_Logger.Log($"[NetServerManager] 批量卖昆虫成功，获得 {resp.goldEarned} 金币");
                StartCoroutine(FetchPlayerGold());

                CommunicateEvent.Modify("Insects_Updated");
                onComplete?.Invoke(true, resp.message, resp.goldEarned);
            }
            else
            {
                onComplete?.Invoke(false, resp?.message ?? "批量卖出失败", 0);
            }
        }, err =>
        {
            onComplete?.Invoke(false, "网络请求失败", 0);
        }, forcePost: true);
    }

    // ============================================================
    // 响应数据类
    // ============================================================

    [Serializable]
    private class InsectsListResponse
    {
        public bool success;
        public List<PlayerInsectData> insects;
    }

    [Serializable]
    private class SellInsectResponse
    {
        public bool success;
        public string message;
        public int goldEarned;
    }
}
