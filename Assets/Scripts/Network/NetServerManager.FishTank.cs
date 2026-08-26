// ============================================================
// 文件: NetServerManager.FishTank.cs
// 说明: 鱼缸系统网络请求 - 只负责收发数据，不缓存
// 路径: Assets/Scripts/Network/
// ============================================================

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public partial class NetServerManager
{
    // ============================================================
    // 公开属性（从 PlayerDataManager 读取）
    // ============================================================

    /// <summary>
    /// 获取鱼缸列表（从 PlayerDataManager 转换）
    /// </summary>
    public List<FishTankInfoData> FishTankList
    {
        get
        {
            var result = new List<FishTankInfoData>();
            if (PlayerDataManager.Instance != null)
            {
                var statuses = PlayerDataManager.Instance.GetAllFishTankStatusOrdered();
                foreach (var s in statuses)
                {
                    var config = LoadDataManager.Instance?.GetFishTankConfig(s.tankId);
                    result.Add(new FishTankInfoData
                    {
                        tankId = s.tankId,
                        name = config?.name ?? $"鱼缸{s.tankId}",
                        type = config?.type ?? "normal",
                        purchaseCost = config?.purchaseCost ?? 0,
                        isUnlocked = s.isUnlocked,
                        level = s.level,
                        capacity = s.capacity,
                        currentCount = s.currentCount,
                        remainingSpace = s.remainingSpace
                    });
                }
            }
            return result;
        }
    }

    public bool IsFishTankDataLoaded => PlayerDataManager.Instance != null;

    /// <summary>
    /// 获取指定鱼缸的状态（从 PlayerDataManager 读取）
    /// </summary>
    public FishTankStatusData GetFishTankStatus(int tankId)
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.GetFishTankStatus(tankId);
        return null;
    }

    /// <summary>
    /// 获取指定鱼缸是否解锁
    /// </summary>
    public bool IsFishTankUnlocked(int tankId)
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.IsFishTankUnlocked(tankId);
        return false;
    }

    /// <summary>
    /// 获取指定鱼缸容量
    /// </summary>
    public int GetFishTankCapacity(int tankId)
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.GetFishTankCapacity(tankId);
        return 10;
    }

    /// <summary>
    /// 获取鱼篓数据（从 PlayerDataManager）
    /// </summary>
    public List<FishDetailData> GetFishBagList()
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.GetFishBagList();
        return new List<FishDetailData>();
    }

    // ============================================================
    // ✅ 网络请求 - 获取鱼篓数据
    // ============================================================

    public void FetchPlayerFishBag(Action<bool, List<FishDetailData>> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] FetchPlayerFishBag: playerId={_currentPlayerId}");
        StartCoroutine(FetchPlayerFishBagCoroutine(onComplete));
    }

    // NetServerManager.FishTank.cs - FetchPlayerFishBagCoroutine

    private IEnumerator FetchPlayerFishBagCoroutine(Action<bool, List<FishDetailData>> onComplete = null)
    {
        Z_Logger.Log("[NetServerManager] FetchPlayerFishBagCoroutine 开始");
        if (!CheckNetworkConnection())
        {
            Z_Logger.LogWarning("[NetServerManager] 网络未连接");
            onComplete?.Invoke(false, null);
            yield break;
        }

        string url = ServerUrls.Player.FishBagById(_currentPlayerId);
        string fullUrl = serverUrl + url;
        Z_Logger.Log($"[NetServerManager] 请求鱼篓数据: {fullUrl}");

        bool isCompleted = false;
        List<FishDetailData> result = null;

        using (UnityWebRequest request = UnityWebRequest.Get(fullUrl))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string rawJson = request.downloadHandler.text;
                Z_Logger.Log($"[NetServerManager] 鱼篓原始响应: {rawJson}");

                try
                {
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<FishBagResponse>(rawJson);

                    // ✅ 判断 items 是否有数据（服务器返回的是 {"items":[...]}，没有 success 字段）
                    if (response != null && response.items != null && response.items.Count > 0)
                    {
                        result = response.items;

                        foreach (var fish in result)
                        {
                            fish.location = 0;
                            fish.tankId = 0;
                        }

                        Z_Logger.Log($"[NetServerManager] 鱼篓数据获取成功，共 {result.Count} 条鱼");

                        if (PlayerDataManager.Instance != null)
                        {
                            // ✅ capacity 用本地已有的值
                            PlayerDataManager.Instance.UpdateFishBagFromResponse(result, fishBagCapacity);
                        }

                        onComplete?.Invoke(true, result);
                    }
                    else
                    {
                        Z_Logger.LogWarning($"[NetServerManager] 获取鱼篓数据失败: items为空或null");
                        onComplete?.Invoke(false, null);
                    }
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析鱼篓数据异常: {e.Message}");
                    Z_Logger.LogError($"[NetServerManager] 原始JSON: {rawJson}");
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 请求鱼篓数据失败: {request.error}");
                onComplete?.Invoke(false, null);
            }

            isCompleted = true;
        }

        while (!isCompleted)
            yield return null;

        Z_Logger.Log("[NetServerManager] FetchPlayerFishBagCoroutine 结束");
    }

    // ============================================================
    // ✅ 网络请求 - 获取所有鱼缸列表
    // ============================================================

    public void FetchAllFishTanks(Action<bool> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] FetchAllFishTanks: playerId={_currentPlayerId}");
        StartCoroutine(FetchAllFishTanksCoroutine(onComplete));
    }

    private IEnumerator FetchAllFishTanksCoroutine(Action<bool> onComplete = null)
    {
        Z_Logger.Log("[NetServerManager] FetchAllFishTanksCoroutine 开始");
        if (!CheckNetworkConnection())
        {
            Z_Logger.LogWarning("[NetServerManager] 网络未连接");
            onComplete?.Invoke(false);
            yield break;
        }

        string url = ServerUrls.FishTank.List(_currentPlayerId);
        Z_Logger.Log($"[NetServerManager] 请求鱼缸列表: {url}");

        bool isCompleted = false;
        List<FishTankInfoData> tankInfos = null;

        yield return FetchGetJson<FishTankListResponse>(
            url,
            data =>
            {
                if (data != null && data.success)
                {
                    tankInfos = data.tanks ?? new List<FishTankInfoData>();
                    Z_Logger.Log($"[NetServerManager] 鱼缸列表响应: count={tankInfos.Count}");

                    // ✅ 打印每个鱼缸信息
                    foreach (var tank in tankInfos)
                    {
                        Z_Logger.Log($"[NetServerManager] 鱼缸 {tank.tankId}: name={tank.name}, isUnlocked={tank.isUnlocked}, currentCount={tank.currentCount}");
                    }

                    // ✅ 获取每个鱼缸的详细状态
                    StartCoroutine(FetchAllTankDetails(tankInfos, success =>
                    {
                        Z_Logger.Log($"[NetServerManager] 鱼缸详情加载完成: success={success}");
                        onComplete?.Invoke(success);
                        isCompleted = true;
                    }));
                }
                else
                {
                    Z_Logger.LogWarning("[NetServerManager] 获取鱼缸列表失败");
                    onComplete?.Invoke(false);
                    isCompleted = true;
                }
            },
            "鱼缸列表"
        );

        while (!isCompleted)
            yield return null;

        Z_Logger.Log("[NetServerManager] FetchAllFishTanksCoroutine 结束");
    }

    private IEnumerator FetchAllTankDetails(List<FishTankInfoData> tankInfos, Action<bool> onComplete)
    {
        if (tankInfos == null || tankInfos.Count == 0)
        {
            Z_Logger.LogWarning("[NetServerManager] 没有鱼缸数据");

            // ✅ 创建默认鱼缸
            if (PlayerDataManager.Instance != null)
            {
                var defaultTanks = new List<FishTankStatusResponse>
            {
                new FishTankStatusResponse
                {
                    success = true,
                    tankId = 1,
                    Name = "特殊鱼缸",
                    Type = "special",
                    PurchaseCost = 0,
                    isUnlocked = true,
                    level = 1,
                    capacity = 10,
                    currentCount = 0,
                    remainingSpace = 10,
                    items = new List<FishDetailData>()
                }
            };
                PlayerDataManager.Instance.UpdateFishTankFromResponse(defaultTanks);
                Z_Logger.Log("[NetServerManager] 默认鱼缸已创建");
            }
            onComplete?.Invoke(true);
            yield break;
        }

        var results = new List<FishTankStatusResponse>();
        int completed = 0;
        int total = tankInfos.Count;

        foreach (var info in tankInfos)
        {
            StartCoroutine(FetchSingleTankStatusCoroutine(info.tankId, response =>
            {
                if (response != null)
                {
                    Z_Logger.Log($"[NetServerManager] 鱼缸 {info.tankId} 详情获取成功");
                    results.Add(response);
                }
                else
                {
                    Z_Logger.LogWarning($"[NetServerManager] 鱼缸 {info.tankId} 详情获取失败");
                }
                completed++;
            }));
        }

        float waitTime = 0f;
        while (completed < total && waitTime < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (completed < total)
            Z_Logger.LogWarning($"[NetServerManager] 鱼缸详情请求超时: {completed}/{total}");

        // ✅ 关键修复：将结果存入 PlayerDataManager
        if (PlayerDataManager.Instance != null)
        {
            if (results.Count > 0)
            {
                PlayerDataManager.Instance.UpdateFishTankFromResponse(results);
                Z_Logger.Log($"[NetServerManager] 鱼缸数据已存入 PlayerDataManager: {results.Count} 个");
            }
            else
            {
                // ✅ 如果所有请求都失败，创建默认鱼缸
                Z_Logger.LogWarning("[NetServerManager] 所有鱼缸详情请求失败，创建默认鱼缸");
                var defaultTanks = new List<FishTankStatusResponse>
            {
                new FishTankStatusResponse
                {
                    success = true,
                    tankId = 1,
                    Name = "特殊鱼缸",
                    Type = "special",
                    PurchaseCost = 0,
                    isUnlocked = true,
                    level = 1,
                    capacity = 10,
                    currentCount = 0,
                    remainingSpace = 10,
                    items = new List<FishDetailData>()
                }
            };
                PlayerDataManager.Instance.UpdateFishTankFromResponse(defaultTanks);
            }
        }

        onComplete?.Invoke(true);
    }

    // ============================================================
    // ✅ 网络请求 - 获取单个鱼缸状态
    // ============================================================

    public void FetchFishTankStatus(int tankId, Action<bool> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] FetchFishTankStatus: playerId={_currentPlayerId}, tankId={tankId}");
        StartCoroutine(FetchSingleTankStatusCoroutine(tankId, response =>
        {
            if (response != null && PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.UpdateSingleFishTankFromResponse(response);
                onComplete?.Invoke(true);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        }));
    }

    // NetServerManager.FishTank.cs - FetchSingleTankStatusCoroutine

    // NetServerManager.FishTank.cs - FetchSingleTankStatusCoroutine
    private IEnumerator FetchSingleTankStatusCoroutine(int tankId, Action<FishTankStatusResponse> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            Z_Logger.LogWarning("[NetServerManager] 网络未连接");
            onComplete?.Invoke(null);
            yield break;
        }

        string url = ServerUrls.FishTank.Status(_currentPlayerId, tankId);
        Z_Logger.Log($"[NetServerManager] 请求鱼缸状态: {url}");

        bool isCompleted = false;
        FishTankStatusResponse result = null;

        yield return FetchGetJson<FishTankStatusResponse>(
            url,
            data =>
            {
                if (data != null && data.success)
                {
                    // ✅ 在这里加日志 - 服务器返回的原始数据（反序列化后）
                    Z_Logger.Log($"[NetServerManager] 收到鱼缸 {tankId} 响应: items={data.items?.Count ?? 0}");
                    if (data.items != null)
                    {
                        foreach (var fish in data.items)
                        {
                            Z_Logger.Log($"[NetServerManager]   服务器返回鱼: id={fish.id}, fishId={fish.fishId}, location={fish.location}, tankId={fish.tankId}");
                        }
                    }

                    // ✅ 如果服务器没有返回 location，客户端手动填充
                    if (data.items != null)
                    {
                        foreach (var fish in data.items)
                        {
                            if (fish.location == 0 && fish.tankId == 0)
                            {
                                fish.location = 1;
                                fish.tankId = tankId;
                                Z_Logger.Log($"[NetServerManager]   手动填充: id={fish.id}, location={fish.location}, tankId={fish.tankId}");
                            }
                        }
                    }

                    Z_Logger.Log($"[NetServerManager] 鱼缸 {tankId} 状态响应: isUnlocked={data.isUnlocked}, items={data.items?.Count ?? 0}");
                    result = data;
                }
                else
                {
                    Z_Logger.LogWarning($"[NetServerManager] 获取鱼缸 {tankId} 状态失败");
                }
                isCompleted = true;
            },
            $"鱼缸{tankId}状态"
        );

        while (!isCompleted)
            yield return null;

        onComplete?.Invoke(result);
        Z_Logger.Log($"[NetServerManager] FetchSingleTankStatusCoroutine 结束, tankId={tankId}");
    }

    // ============================================================
    // ✅ 网络请求 - 解锁鱼缸
    // ============================================================

    public void UnlockFishTank(int tankId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] UnlockFishTank: playerId={_currentPlayerId}, tankId={tankId}");
        StartCoroutine(UnlockFishTankCoroutine(tankId, onComplete));
    }

    private IEnumerator UnlockFishTankCoroutine(int tankId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] UnlockFishTankCoroutine 开始, tankId={tankId}");
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        string url = ServerUrls.FishTank.Unlock(_currentPlayerId, tankId);
        UnityWebRequest request = UnityWebRequest.PostWwwForm(serverUrl + url, "");
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<FishTankOperationResponse>(request.downloadHandler.text);
            Z_Logger.Log($"[NetServerManager] 解锁鱼缸响应: success={response?.success}");

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataUpdated();
            }
            else
            {
                responseMessage = response?.message ?? "解锁失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 解锁请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage);
        Z_Logger.Log($"[NetServerManager] UnlockFishTankCoroutine 结束");
    }

    // ============================================================
    // ✅ 网络请求 - 升级鱼缸
    // ============================================================

    public void UpgradeFishTank(int tankId, Action<bool, string, int, int> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] UpgradeFishTank: playerId={_currentPlayerId}, tankId={tankId}");
        StartCoroutine(UpgradeFishTankCoroutine(tankId, onComplete));
    }

    private IEnumerator UpgradeFishTankCoroutine(int tankId, Action<bool, string, int, int> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] UpgradeFishTankCoroutine 开始, tankId={tankId}");
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0, 0);
            yield break;
        }

        string url = ServerUrls.FishTank.Upgrade(_currentPlayerId, tankId);
        UnityWebRequest request = UnityWebRequest.PostWwwForm(serverUrl + url, "");
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";
        int newLevel = 0;
        int newCapacity = 0;

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<FishTankUpgradeResponse>(request.downloadHandler.text);
            Z_Logger.Log($"[NetServerManager] 升级鱼缸响应: success={response?.success}");

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;
                newLevel = response.level;
                newCapacity = response.capacity;

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataUpdated();
            }
            else
            {
                responseMessage = response?.message ?? "升级失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 升级请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage, newLevel, newCapacity);
        Z_Logger.Log($"[NetServerManager] UpgradeFishTankCoroutine 结束");
    }

    // ============================================================
    // ✅ 网络请求 - 从鱼篓放入鱼缸
    // ============================================================

    public void MoveFishFromBagToTank(int tankId, int fishItemId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] MoveFishFromBagToTank: playerId={_currentPlayerId}, tankId={tankId}, fishItemId={fishItemId}");
        StartCoroutine(MoveFishFromBagToTankCoroutine(tankId, fishItemId, onComplete));
    }

    private IEnumerator MoveFishFromBagToTankCoroutine(int tankId, int fishItemId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log("[NetServerManager] MoveFishFromBagToTankCoroutine 开始");
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            { "PlayerId", _currentPlayerId },
            { "TankId", tankId },
            { "FishItemId", fishItemId }
        };

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        string url = serverUrl + ServerUrls.FishTank.MoveBagToTank;

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<FishTankMoveResponse>(request.downloadHandler.text);
            Z_Logger.Log($"[NetServerManager] 放入鱼缸响应: success={response?.success}");

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataUpdated();
            }
            else
            {
                responseMessage = response?.message ?? "放入失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 放入鱼缸请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage);
        Z_Logger.Log("[NetServerManager] MoveFishFromBagToTankCoroutine 结束");
    }

    // ============================================================
    // ✅ 网络请求 - 从鱼缸取出到鱼篓
    // ============================================================

    public void MoveFishFromTankToBag(int fishItemId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] MoveFishFromTankToBag: playerId={_currentPlayerId}, fishItemId={fishItemId}");
        StartCoroutine(MoveFishFromTankToBagCoroutine(fishItemId, onComplete));
    }

    private IEnumerator MoveFishFromTankToBagCoroutine(int fishItemId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log("[NetServerManager] MoveFishFromTankToBagCoroutine 开始");
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            { "PlayerId", _currentPlayerId },
            { "FishItemId", fishItemId }
        };

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        string url = serverUrl + ServerUrls.FishTank.MoveTankToBag;

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";
        int tankId = 1;

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<FishTankMoveResponse>(request.downloadHandler.text);
            Z_Logger.Log($"[NetServerManager] 取出鱼响应: success={response?.success}");

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;

                if (PlayerDataManager.Instance != null)
                    tankId = PlayerDataManager.Instance.FindTankIdByFishItemId(fishItemId);

                if (tankId > 0)
                    yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));

                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));
                yield return StartCoroutine(FetchAllFishTanksCoroutine(null));

                NotifyDataUpdated();
            }
            else
            {
                responseMessage = response?.message ?? "取出失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 取出鱼请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage);
        Z_Logger.Log("[NetServerManager] MoveFishFromTankToBagCoroutine 结束");
    }

    // ============================================================
    // ✅ 网络请求 - 鱼缸转移到鱼缸
    // ============================================================

    public void MoveFishFromTankToTank(int fromTankId, int toTankId, int fishItemId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] MoveFishFromTankToTank: fromTankId={fromTankId}, toTankId={toTankId}, fishItemId={fishItemId}");
        StartCoroutine(MoveFishFromTankToTankCoroutine(fromTankId, toTankId, fishItemId, onComplete));
    }

    private IEnumerator MoveFishFromTankToTankCoroutine(int fromTankId, int toTankId, int fishItemId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log("[NetServerManager] MoveFishFromTankToTankCoroutine 开始");
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            { "PlayerId", _currentPlayerId },
            { "FromTankId", fromTankId },
            { "ToTankId", toTankId },
            { "FishItemId", fishItemId }
        };

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        string url = serverUrl + ServerUrls.FishTank.MoveTankToTank;

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<FishTankOperationResponse>(request.downloadHandler.text);
            Z_Logger.Log($"[NetServerManager] 鱼缸转移响应: success={response?.success}");

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(fromTankId, null));
                yield return StartCoroutine(FetchSingleTankStatusCoroutine(toTankId, null));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataUpdated();
            }
            else
            {
                responseMessage = response?.message ?? "转移失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 鱼缸转移请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage);
        Z_Logger.Log("[NetServerManager] MoveFishFromTankToTankCoroutine 结束");
    }

    // ============================================================
    // ✅ 网络请求 - 批量从鱼篓放入鱼缸
    // ============================================================

    public void BatchMoveFishFromBagToTank(int tankId, List<int> fishItemIds, Action<bool, string, int> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] BatchMoveFishFromBagToTank: tankId={tankId}, count={fishItemIds?.Count ?? 0}");
        StartCoroutine(BatchMoveFishFromBagToTankCoroutine(tankId, fishItemIds, onComplete));
    }

    private IEnumerator BatchMoveFishFromBagToTankCoroutine(int tankId, List<int> fishItemIds, Action<bool, string, int> onComplete = null)
    {
        Z_Logger.Log("[NetServerManager] BatchMoveFishFromBagToTankCoroutine 开始");
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0);
            yield break;
        }

        if (fishItemIds == null || fishItemIds.Count == 0)
        {
            onComplete?.Invoke(false, "请选择要放入的鱼", 0);
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            { "PlayerId", _currentPlayerId },
            { "TankId", tankId },
            { "FishItemIds", fishItemIds }
        };

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        string url = serverUrl + ServerUrls.FishTank.BatchMoveBagToTank;

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";
        int movedCount = 0;

        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<FishTankBatchMoveResponse>(request.downloadHandler.text);
            Z_Logger.Log($"[NetServerManager] 批量放入响应: success={response?.success}");

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;
                movedCount = response.movedCount;

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataUpdated();
            }
            else
            {
                responseMessage = response?.message ?? "批量放入失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 批量放入请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage, movedCount);
        Z_Logger.Log("[NetServerManager] BatchMoveFishFromBagToTankCoroutine 结束");
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    /// <summary>
    /// 通知数据已更新 - 只触发统一事件
    /// </summary>
    private void NotifyDataUpdated()
    {
        Z_Logger.Log("[NetServerManager] 通知鱼缸数据已更新");

        // ✅ 先更新数据版本号
        if (PlayerDataManager.Instance != null)
        {
            // 强制触发数据更新
            PlayerDataManager.Instance.ForceNotifyDataChanged();
        }

        // 直接触发UI刷新
        CommunicateEvent.Modify("FishTankChanged");
        CommunicateEvent.Modify("PlayerDataUpdated");

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.RefreshFishTankData();
        }
    }

    // ============================================================
    // 事件处理器
    // ============================================================

    // ============================================================
    // NetServerManager.FishTank.cs - 修改 OnFishTankOpen
    // ============================================================

    private void OnFishTankOpen()
    {
        Z_Logger.Log("[NetServerManager] OnFishTankOpen: 请求鱼缸和鱼篓数据");
        if (!_isEnabled) return;

        int completed = 0;
        int total = 2;

        FetchAllFishTanks(success =>
        {
            completed++;
            Z_Logger.Log($"[NetServerManager] 鱼缸数据加载完成: success={success}, completed={completed}/{total}");
            if (completed >= total)
            {
                // ✅ 强制触发数据更新通知
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.ForceNotifyDataChanged();
                }
                Z_Logger.Log("[NetServerManager] 所有数据请求完成，触发 FishTankDataReady");
                CommunicateEvent.Modify("FishTankDataReady");
            }
        });

        FetchPlayerFishBag((success, fishList) =>
        {
            completed++;
            Z_Logger.Log($"[NetServerManager] 鱼篓数据加载完成: success={success}, count={fishList?.Count ?? 0}, completed={completed}/{total}");
            if (completed >= total)
            {
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.ForceNotifyDataChanged();
                }
                Z_Logger.Log("[NetServerManager] 所有数据请求完成，触发 FishTankDataReady");
                CommunicateEvent.Modify("FishTankDataReady");
            }
        });
    }

    /// <summary>
    /// 检查数据是否就绪，就绪则触发事件
    /// </summary>
    private void CheckAndNotifyDataReady(bool tankLoaded, bool bagLoaded)
    {
        if (tankLoaded && bagLoaded)
        {
            Z_Logger.Log("[NetServerManager] 鱼缸和鱼篓数据都已加载完成，触发 FishTankDataReady");

            // ✅ 只触发一个事件
            CommunicateEvent.Modify("FishTankDataReady");
        }
    }

    private void OnSyncFishTankStatus()
    {
        Z_Logger.Log("[NetServerManager] OnSyncFishTankStatus: 请求同步鱼缸状态");
        if (!_isEnabled) return;

        FetchAllFishTanks(success =>
        {
            if (success)
                Z_Logger.Log($"[NetServerManager] 同步鱼缸状态成功");
            else
                Z_Logger.LogWarning("[NetServerManager] 同步鱼缸状态失败");
        });
    }

    private void OnUnlockFishTankRequest(int tankId)
    {
        Z_Logger.Log($"[NetServerManager] OnUnlockFishTankRequest: tankId={tankId}");
        if (!_isEnabled) return;

        UnlockFishTank(tankId, (success, message) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    private void OnUpgradeFishTankRequest(int tankId)
    {
        Z_Logger.Log($"[NetServerManager] OnUpgradeFishTankRequest: tankId={tankId}");
        if (!_isEnabled) return;

        UpgradeFishTank(tankId, (success, message, level, capacity) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    private void OnMoveFishFromBagToTankRequest(int tankId, int fishItemId)
    {
        Z_Logger.Log($"[NetServerManager] OnMoveFishFromBagToTankRequest: tankId={tankId}");
        if (!_isEnabled) return;

        MoveFishFromBagToTank(tankId, fishItemId, (success, message) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    private void OnMoveFishFromTankToBagRequest(int tankItemId)
    {
        Z_Logger.Log($"[NetServerManager] OnMoveFishFromTankToBagRequest: tankItemId={tankItemId}");
        if (!_isEnabled) return;

        MoveFishFromTankToBag(tankItemId, (success, message) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    private void OnBatchMoveFishFromBagToTankRequest(int tankId, List<int> fishItemIds)
    {
        Z_Logger.Log($"[NetServerManager] OnBatchMoveFishFromBagToTankRequest: tankId={tankId}");
        if (!_isEnabled) return;

        BatchMoveFishFromBagToTank(tankId, fishItemIds, (success, message, count) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    // ============================================================
    // 数据类
    // ============================================================

    [Serializable]
    public class FishBagResponse
    {
        public bool success;
        public List<FishDetailData> items;
        public int capacity;
        public int count;
    }

    [Serializable]
    public class FishTankInfoData
    {
        public int tankId;
        public string name;
        public string type;
        public int purchaseCost;
        public bool isUnlocked;
        public int level;
        public int capacity;
        public int currentCount;
        public int remainingSpace;
    }

    [Serializable]
    public class FishTankStatusData
    {
        public int tankId;
        public bool isUnlocked;
        public int level;
        public int capacity;
        public int currentCount;
        public int remainingSpace;
        public List<FishDetailData> items;
    }

    [Serializable]
    public class FishTankUpgradeInfo
    {
        public bool isUnlocked;
        public int currentLevel;
        public int currentCapacity;
        public int nextLevel;
        public int nextCapacity;
        public int upgradeCost;
        public bool canUpgrade;
        public bool isMaxLevel;
    }

    [Serializable]
    private class FishTankListResponse
    {
        public bool success;
        public List<FishTankInfoData> tanks;
    }

    [Serializable]
    private class FishTankOperationResponse
    {
        public bool success;
        public string message;
    }

    [Serializable]
    private class FishTankUpgradeResponse
    {
        public bool success;
        public string message;
        public int level;
        public int capacity;
    }

    [Serializable]
    private class FishTankMoveResponse
    {
        public bool success;
        public string message;
        public int fishTankCount;
        public int fishTankCapacity;
        public int bagCount;
        public int bagCapacity;
    }

    [Serializable]
    private class FishTankBatchMoveResponse
    {
        public bool success;
        public string message;
        public int movedCount;
        public int fishTankCount;
        public int fishTankCapacity;
        public int bagCapacity;
    }

    // ============================================================
    // 兼容旧代码（过时，将在后续版本移除）
    // 解决 Events.cs 和 Init.cs 中的引用错误
    // ============================================================

    [Obsolete("请使用 FishTankList 属性")]
    private List<FishTankInfoData> _fishTankList => FishTankList;

    //[Obsolete("请使用 PlayerDataManager 检查数据是否加载")]
    //private bool _fishTankDataLoaded => PlayerDataManager.Instance != null;

    [Obsolete("请使用 PlayerDataManager.GetFishTankStatus")]
    private Dictionary<int, FishTankStatusData> _fishTankStatusCache
    {
        get
        {
            var dict = new Dictionary<int, FishTankStatusData>();
            if (PlayerDataManager.Instance != null)
            {
                var statuses = PlayerDataManager.Instance.GetAllFishTankStatus();
                foreach (var s in statuses)
                {
                    dict[s.tankId] = s;
                }
            }
            return dict;
        }
    }
}
