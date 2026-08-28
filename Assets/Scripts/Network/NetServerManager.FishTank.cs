// ============================================================
// 文件: NetServerManager.FishTank.cs
// 说明: 鱼缸系统网络请求 - 只负责收发数据
// 路径: Assets/Scripts/Network/
// ============================================================

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using static PlayerDataManager;
using Newtonsoft.Json;

public partial class NetServerManager
{
    // ============================================================
    // 公开属性（从 PlayerDataManager 读取）
    // ============================================================

    public bool IsFishTankDataLoaded => PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsFishDataLoaded;

    public List<FishTankStatusData> FishTankList
    {
        get
        {
            if (PlayerDataManager.Instance != null)
                return PlayerDataManager.Instance.GetAllFishTankStatusOrdered();
            return new List<FishTankStatusData>();
        }
    }

    public FishTankStatusData GetFishTankStatus(int tankId)
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.GetFishTankStatus(tankId);
        return null;
    }

    public bool IsFishTankUnlocked(int tankId)
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.IsFishTankUnlocked(tankId);
        return false;
    }

    public List<FishDetailData> GetFishBagList()
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.GetFishBagList();
        return new List<FishDetailData>();
    }

    public int GetFishTankCapacity(int tankId)
    {
        if (PlayerDataManager.Instance != null)
            return PlayerDataManager.Instance.GetFishTankCapacity(tankId);
        return 10;
    }

    public int GetFishTankLevel(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        return status?.level ?? 1;
    }

    public int GetFishTankCount(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        return status?.currentCount ?? 0;
    }

    public int GetFishTankRemainingSpace(int tankId)
    {
        var status = GetFishTankStatus(tankId);
        return status?.remainingSpace ?? 0;
    }

    // ============================================================
    // 辅助方法 - URL 处理
    // ============================================================

    private string GetFullUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        if (url.StartsWith("http://") || url.StartsWith("https://"))
            return url;

        string baseUrl = serverUrl.TrimEnd('/');
        string relativeUrl = url.TrimStart('/');
        return baseUrl + "/" + relativeUrl;
    }

    // ============================================================
    // ✅ 网络请求 - 获取鱼篓数据
    // ============================================================

    public void FetchPlayerFishBag(Action<bool> onComplete = null)
    {
        StartCoroutine(FetchPlayerFishBagCoroutine(onComplete));
    }

    private IEnumerator FetchPlayerFishBagCoroutine(Action<bool> onComplete = null)
    {
        List<FishDetailData> fishList = null;
        int capacity = 0;
        bool dataSuccess = false;

        yield return FetchGetJson<InventoryResponse>(ServerUrls.Player.FishBagById(_currentPlayerId), data =>
        {
            if (data?.items != null)
            {
                fishList = new List<FishDetailData>();
                foreach (var item in data.items)
                {
                    fishList.Add(new FishDetailData
                    {
                        id = item.id,
                        fishId = item.key,
                        weight = item.weight,
                        starRatingId = item.starRatingId,
                        isShiny = item.isShiny,
                        isLocked = item.isLocked,
                        calculatedPrice = item.calculatedPrice,
                        caughtTimestamp = item.caughtTimestamp,
                        location = 0,
                        tankId = 0
                    });
                }
                dataSuccess = true;
            }
        }, "鱼篓数据");

        if (!dataSuccess || fishList == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        yield return FetchGetJson<CapacityResponse>(ServerUrls.Inventory.FishCapacityById(_currentPlayerId), capData =>
        {
            if (capData != null)
                capacity = capData.capacity;
        }, "鱼篓容量");

        if (capacity <= 0)
            capacity = fishBagCapacity;

        fishBagCapacity = capacity;

        fishBagDetailData.Clear();
        fishInventory.Clear();
        foreach (var fish in fishList)
        {
            if (!fishInventory.ContainsKey(fish.fishId))
                fishInventory[fish.fishId] = 0;
            fishInventory[fish.fishId]++;

            if (!fishBagDetailData.ContainsKey(fish.fishId))
                fishBagDetailData[fish.fishId] = new List<FishDetailData>();
            fishBagDetailData[fish.fishId].Add(fish);
        }

        PlayerDataManager.Instance?.UpdateFishDetailData(fishBagDetailData);

        int total = GetTotalFishCount();
        isFishBagFull = total >= fishBagCapacity;

        Z_Logger.Log($"[NetServerManager] 鱼篓数据加载完成: {fishInventory.Count} 种鱼，总数量: {total}，容量: {fishBagCapacity}，已满: {isFishBagFull}");

        onComplete?.Invoke(true);
    }

    // ============================================================
    // ✅ 网络请求 - 获取所有鱼缸列表
    // ============================================================

    public void FetchAllFishTanks(Action<bool> onComplete = null)
    {
        StartCoroutine(FetchAllFishTanksCoroutine(onComplete));
    }

    private IEnumerator FetchAllFishTanksCoroutine(Action<bool> onComplete = null)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = GetFullUrl(ServerUrls.FishTank.List(_currentPlayerId));
        Z_Logger.Log($"[NetServerManager] 请求鱼缸列表: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                FishTankListResponse data = null;
                try
                {
                    data = JsonConvert.DeserializeObject<FishTankListResponse>(json);
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析鱼缸列表异常: {e.Message}");
                    onComplete?.Invoke(false);
                    yield break;
                }

                if (data != null && data.success)
                {
                    var tankInfos = data.tanks ?? new List<FishTankInfoData>();
                    yield return StartCoroutine(FetchAllTankDetails(tankInfos, onComplete));
                }
                else
                {
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 请求鱼缸列表失败: {request.error}");
                onComplete?.Invoke(false);
            }
        }
    }

    private IEnumerator FetchAllTankDetails(List<FishTankInfoData> tankInfos, Action<bool> onComplete)
    {
        if (tankInfos == null || tankInfos.Count == 0)
        {
            CreateDefaultTank();
            NotifyDataLoaded();
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
                    results.Add(response);
                completed++;
            }));
        }

        float waitTime = 0f;
        while (completed < total && waitTime < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (results.Count > 0)
        {
            if (PlayerDataManager.Instance != null)
                PlayerDataManager.Instance.UpdateFishTankFromResponse(results);
        }
        else
        {
            CreateDefaultTank();
        }

        NotifyDataLoaded();
        onComplete?.Invoke(true);
    }

    // ============================================================
    // ✅ 网络请求 - 获取单个鱼缸状态
    // ============================================================

    public void FetchFishTankStatus(int tankId, Action<bool> onComplete = null)
    {
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

    private IEnumerator FetchSingleTankStatusCoroutine(int tankId, Action<FishTankStatusResponse> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(null);
            yield break;
        }

        string url = GetFullUrl(ServerUrls.FishTank.Status(_currentPlayerId, tankId));
        Z_Logger.Log($"[NetServerManager] 请求鱼缸状态: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                Z_Logger.Log($"[NetServerManager] 鱼缸状态原始响应: {json}");

                FishTankStatusResponse data = null;
                try
                {
                    data = JsonConvert.DeserializeObject<FishTankStatusResponse>(json);
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析鱼缸状态异常: {e.Message}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                if (data != null && data.success)
                {
                    if (data.items != null)
                    {
                        foreach (var fish in data.items)
                        {
                            if (fish.location == 0 && fish.tankId == 0)
                            {
                                fish.location = 1;
                                fish.tankId = tankId;
                            }
                        }
                    }
                    onComplete?.Invoke(data);
                }
                else
                {
                    Z_Logger.LogWarning($"[NetServerManager] 鱼缸状态响应 success=false 或 data=null");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 请求鱼缸状态失败: {request.error}");
                onComplete?.Invoke(null);
            }
        }
    }

    // ============================================================
    // ✅ 网络请求 - 解锁鱼缸
    // ============================================================

    public void UnlockFishTank(int tankId, Action<bool, string> onComplete = null)
    {
        StartCoroutine(UnlockFishTankCoroutine(tankId, onComplete));
    }

    private IEnumerator UnlockFishTankCoroutine(int tankId, Action<bool, string> onComplete = null)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        string url = GetFullUrl(ServerUrls.FishTank.Unlock(_currentPlayerId, tankId));
        Z_Logger.Log($"[NetServerManager] 解锁鱼缸请求: {url}");

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            FishTankOperationResponse response = null;
            try
            {
                response = JsonConvert.DeserializeObject<FishTankOperationResponse>(json);
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[NetServerManager] 解析解锁响应异常: {e.Message}");
                responseMessage = "解析响应失败";
                request.Dispose();
                onComplete?.Invoke(false, responseMessage);
                yield break;
            }

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;

                // ★ 修改：拉取后更新数据
                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, resp =>
                {
                    if (resp != null && PlayerDataManager.Instance != null)
                        PlayerDataManager.Instance.UpdateSingleFishTankFromResponse(resp);
                }));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataLoaded();
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
    }

    // ============================================================
    // ✅ 网络请求 - 升级鱼缸
    // ============================================================

    public void UpgradeFishTank(int tankId, Action<bool, string, int, int> onComplete = null)
    {
        StartCoroutine(UpgradeFishTankCoroutine(tankId, onComplete));
    }

    private IEnumerator UpgradeFishTankCoroutine(int tankId, Action<bool, string, int, int> onComplete = null)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0, 0);
            yield break;
        }

        string url = GetFullUrl(ServerUrls.FishTank.Upgrade(_currentPlayerId, tankId));
        Z_Logger.Log($"[NetServerManager] 升级鱼缸请求: {url}");

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";
        int newLevel = 0;
        int newCapacity = 0;

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            FishTankUpgradeResponse response = null;
            try
            {
                response = JsonConvert.DeserializeObject<FishTankUpgradeResponse>(json);
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[NetServerManager] 解析升级响应异常: {e.Message}");
                responseMessage = "解析响应失败";
                request.Dispose();
                onComplete?.Invoke(false, responseMessage, 0, 0);
                yield break;
            }

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;
                newLevel = response.level;
                newCapacity = response.capacity;

                // ★ 修改：拉取后更新数据
                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, resp =>
                {
                    if (resp != null && PlayerDataManager.Instance != null)
                        PlayerDataManager.Instance.UpdateSingleFishTankFromResponse(resp);
                }));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataLoaded();
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
    }

    // ============================================================
    // ✅ 网络请求 - 从鱼篓放入鱼缸（★ 修改：更新数据）
    // ============================================================

    public void MoveFishFromBagToTank(int tankId, int fishItemId, Action<bool, string> onComplete = null)
    {
        Z_Logger.Log($"[NetServerManager] MoveFishFromBagToTank: tankId={tankId}, fishItemId={fishItemId}");
        StartCoroutine(MoveFishFromBagToTankCoroutine(tankId, fishItemId, onComplete));
    }

    private IEnumerator MoveFishFromBagToTankCoroutine(int tankId, int fishItemId, Action<bool, string> onComplete = null)
    {
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

        string url = GetFullUrl(ServerUrls.FishTank.MoveBagToTank);
        Z_Logger.Log($"[NetServerManager] 放入鱼缸请求: {url}");

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
            string responseJson = request.downloadHandler.text;
            FishTankMoveResponse response = null;
            try
            {
                response = JsonConvert.DeserializeObject<FishTankMoveResponse>(responseJson);
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[NetServerManager] 解析放入响应异常: {e.Message}");
                responseMessage = "解析响应失败";
                request.Dispose();
                onComplete?.Invoke(false, responseMessage);
                yield break;
            }

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;

                // ★ 修改：拉取鱼缸状态并更新到 PlayerDataManager
                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, resp =>
                {
                    if (resp != null && PlayerDataManager.Instance != null)
                        PlayerDataManager.Instance.UpdateSingleFishTankFromResponse(resp);
                }));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataLoaded();
            }
            else
            {
                responseMessage = response?.message ?? "放入失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 放入请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage);
    }

    // ============================================================
    // ✅ 网络请求 - 从鱼缸取出到鱼篓（★ 修改：更新数据）
    // ============================================================

    public void MoveFishFromTankToBag(int fishItemId, Action<bool, string> onComplete = null)
    {
        StartCoroutine(MoveFishFromTankToBagCoroutine(fishItemId, onComplete));
    }

    private IEnumerator MoveFishFromTankToBagCoroutine(int fishItemId, Action<bool, string> onComplete = null)
    {
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

        string url = GetFullUrl(ServerUrls.FishTank.MoveTankToBag);
        Z_Logger.Log($"[NetServerManager] 取出鱼请求: {url}");

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool isSuccess = false;
        string responseMessage = "";
        int tankId = -1;

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseJson = request.downloadHandler.text;
            FishTankMoveResponse response = null;
            try
            {
                response = JsonConvert.DeserializeObject<FishTankMoveResponse>(responseJson);
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[NetServerManager] 解析取出响应异常: {e.Message}");
                responseMessage = "解析响应失败";
                request.Dispose();
                onComplete?.Invoke(false, responseMessage);
                yield break;
            }

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;

                // 找到鱼所在的鱼缸ID
                if (PlayerDataManager.Instance != null)
                    tankId = PlayerDataManager.Instance.FindTankIdByFishItemId(fishItemId);

                // ★ 修改：拉取鱼缸状态并更新
                if (tankId > 0)
                {
                    yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, resp =>
                    {
                        if (resp != null && PlayerDataManager.Instance != null)
                            PlayerDataManager.Instance.UpdateSingleFishTankFromResponse(resp);
                    }));
                }
                else
                {
                    // 如果无法确定，拉取所有鱼缸
                    yield return StartCoroutine(FetchAllFishTanksCoroutine(null));
                }

                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataLoaded();
            }
            else
            {
                responseMessage = response?.message ?? "取出失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 取出请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage);
    }

    // ============================================================
    // ✅ 网络请求 - 鱼缸转移到鱼缸（★ 修改：更新数据）
    // ============================================================

    public void MoveFishFromTankToTank(int fromTankId, int toTankId, int fishItemId, Action<bool, string> onComplete = null)
    {
        StartCoroutine(MoveFishFromTankToTankCoroutine(fromTankId, toTankId, fishItemId, onComplete));
    }

    private IEnumerator MoveFishFromTankToTankCoroutine(int fromTankId, int toTankId, int fishItemId, Action<bool, string> onComplete = null)
    {
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

        string url = GetFullUrl(ServerUrls.FishTank.MoveTankToTank);
        Z_Logger.Log($"[NetServerManager] 鱼缸转移请求: {url}");

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
            string responseJson = request.downloadHandler.text;
            FishTankOperationResponse response = null;
            try
            {
                response = JsonConvert.DeserializeObject<FishTankOperationResponse>(responseJson);
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[NetServerManager] 解析转移响应异常: {e.Message}");
                responseMessage = "解析响应失败";
                request.Dispose();
                onComplete?.Invoke(false, responseMessage);
                yield break;
            }

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;

                // ★ 修改：拉取两个鱼缸状态并更新
                yield return StartCoroutine(FetchSingleTankStatusCoroutine(fromTankId, resp =>
                {
                    if (resp != null && PlayerDataManager.Instance != null)
                        PlayerDataManager.Instance.UpdateSingleFishTankFromResponse(resp);
                }));
                yield return StartCoroutine(FetchSingleTankStatusCoroutine(toTankId, resp =>
                {
                    if (resp != null && PlayerDataManager.Instance != null)
                        PlayerDataManager.Instance.UpdateSingleFishTankFromResponse(resp);
                }));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataLoaded();
            }
            else
            {
                responseMessage = response?.message ?? "转移失败";
            }
        }
        else
        {
            Z_Logger.LogError($"[NetServerManager] 转移请求失败: {request.error}");
            responseMessage = request.error;
        }

        request.Dispose();
        onComplete?.Invoke(isSuccess, responseMessage);
    }

    // ============================================================
    // ✅ 网络请求 - 批量从鱼篓放入鱼缸
    // ============================================================

    public void BatchMoveFishFromBagToTank(int tankId, List<int> fishItemIds, Action<bool, string, int> onComplete = null)
    {
        StartCoroutine(BatchMoveFishFromBagToTankCoroutine(tankId, fishItemIds, onComplete));
    }

    private IEnumerator BatchMoveFishFromBagToTankCoroutine(int tankId, List<int> fishItemIds, Action<bool, string, int> onComplete = null)
    {
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

        string url = GetFullUrl(ServerUrls.FishTank.BatchMoveBagToTank);
        Z_Logger.Log($"[NetServerManager] 批量放入请求: {url}");

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
            string responseJson = request.downloadHandler.text;
            FishTankBatchMoveResponse response = null;
            try
            {
                response = JsonConvert.DeserializeObject<FishTankBatchMoveResponse>(responseJson);
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[NetServerManager] 解析批量放入响应异常: {e.Message}");
                responseMessage = "解析响应失败";
                request.Dispose();
                onComplete?.Invoke(false, responseMessage, 0);
                yield break;
            }

            if (response != null && response.success)
            {
                isSuccess = true;
                responseMessage = response.message;
                movedCount = response.movedCount;

                // ★ 修改：拉取鱼缸状态并更新
                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, resp =>
                {
                    if (resp != null && PlayerDataManager.Instance != null)
                        PlayerDataManager.Instance.UpdateSingleFishTankFromResponse(resp);
                }));
                yield return StartCoroutine(FetchPlayerFishBagCoroutine(null));

                NotifyDataLoaded();
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
    }

    // ============================================================
    // ✅ 辅助方法
    // ============================================================

    private void CreateDefaultTank()
    {
        if (PlayerDataManager.Instance == null) return;

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

    private void NotifyDataLoaded()
    {
        Z_Logger.Log("[NetServerManager] 数据加载完成，通知 Service");
        CommunicateEvent.Modify(FishTankMessage.DataLoaded.ToString());
    }

    // ============================================================
    // ✅ 事件处理器（由外部调用）
    // ============================================================

    public void OnFishTankOpen()
    {
        if (!_isEnabled) return;

        Z_Logger.Log("[NetServerManager] OnFishTankOpen: 请求鱼缸和鱼篓数据");

        int completed = 0;
        int total = 2;

        FetchAllFishTanks(success =>
        {
            completed++;
            if (completed >= total)
                NotifyDataLoaded();
        });

        FetchPlayerFishBag(success =>
        {
            completed++;
            if (completed >= total)
                NotifyDataLoaded();
        });
    }

    public void OnSyncFishTankStatus()
    {
        Z_Logger.Log("[NetServerManager] OnSyncFishTankStatus: 请求同步鱼缸状态");
        if (!_isEnabled) return;

        FetchAllFishTanks(success =>
        {
            if (success)
                Z_Logger.Log("[NetServerManager] 同步鱼缸状态成功");
            else
                Z_Logger.LogWarning("[NetServerManager] 同步鱼缸状态失败");
        });
    }

    public void OnUnlockFishTankRequest(int tankId)
    {
        Z_Logger.Log($"[NetServerManager] OnUnlockFishTankRequest: tankId={tankId}");
        if (!_isEnabled) return;

        UnlockFishTank(tankId, (success, message) =>
        {
            if (success)
            {
                GameUIManager.Instance?.ShowTip("解锁成功！"); // ✅ 添加成功提示
            }
            else
            {
                GameUIManager.Instance?.ShowTip(message);
            }
        });
    }

    public void OnUpgradeFishTankRequest(int tankId)
    {
        Z_Logger.Log($"[NetServerManager] OnUpgradeFishTankRequest: tankId={tankId}");
        if (!_isEnabled) return;

        UpgradeFishTank(tankId, (success, message, level, capacity) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    public void OnMoveFishFromBagToTankRequest(int tankId, int fishItemId)
    {
        Z_Logger.Log($"[NetServerManager] OnMoveFishFromBagToTankRequest: tankId={tankId}, fishItemId={fishItemId}");
        if (!_isEnabled) return;

        MoveFishFromBagToTank(tankId, fishItemId, (success, message) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    public void OnMoveFishFromTankToBagRequest(int fishItemId)
    {
        Z_Logger.Log($"[NetServerManager] OnMoveFishFromTankToBagRequest: fishItemId={fishItemId}");
        if (!_isEnabled) return;

        MoveFishFromTankToBag(fishItemId, (success, message) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    public void OnBatchMoveFishFromBagToTankRequest(int tankId, List<int> fishItemIds)
    {
        Z_Logger.Log($"[NetServerManager] OnBatchMoveFishFromBagToTankRequest: tankId={tankId}, count={fishItemIds?.Count ?? 0}");
        if (!_isEnabled) return;

        BatchMoveFishFromBagToTank(tankId, fishItemIds, (success, message, count) =>
        {
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
        });
    }

    // ============================================================
    // 数据类定义
    // ============================================================

    [Serializable]
    public class FishBagResponse
    {
        [JsonProperty("success")]
        public bool success;
        [JsonProperty("items")]
        public List<FishDetailData> items;
        [JsonProperty("capacity")]
        public int capacity;
        [JsonProperty("count")]
        public int count;
    }

    [Serializable]
    public class FishTankInfoData
    {
        [JsonProperty("tankId")]
        public int tankId;
        [JsonProperty("name")]
        public string name;
        [JsonProperty("type")]
        public string type;
        [JsonProperty("purchaseCost")]
        public int purchaseCost;
        [JsonProperty("isUnlocked")]
        public bool isUnlocked;
        [JsonProperty("level")]
        public int level;
        [JsonProperty("capacity")]
        public int capacity;
        [JsonProperty("currentCount")]
        public int currentCount;
        [JsonProperty("remainingSpace")]
        public int remainingSpace;
    }

    [Serializable]
    public class FishTankUpgradeInfo
    {
        [JsonProperty("isUnlocked")]
        public bool isUnlocked;
        [JsonProperty("currentLevel")]
        public int currentLevel;
        [JsonProperty("currentCapacity")]
        public int currentCapacity;
        [JsonProperty("nextLevel")]
        public int nextLevel;
        [JsonProperty("nextCapacity")]
        public int nextCapacity;
        [JsonProperty("upgradeCost")]
        public int upgradeCost;
        [JsonProperty("canUpgrade")]
        public bool canUpgrade;
        [JsonProperty("isMaxLevel")]
        public bool isMaxLevel;
    }

    [Serializable]
    private class FishTankListResponse
    {
        [JsonProperty("success")]
        public bool success;
        [JsonProperty("tanks")]
        public List<FishTankInfoData> tanks;
    }

    [Serializable]
    private class FishTankOperationResponse
    {
        [JsonProperty("success")]
        public bool success;
        [JsonProperty("message")]
        public string message;
    }

    [Serializable]
    private class FishTankUpgradeResponse
    {
        [JsonProperty("success")]
        public bool success;
        [JsonProperty("message")]
        public string message;
        [JsonProperty("level")]
        public int level;
        [JsonProperty("capacity")]
        public int capacity;
    }

    [Serializable]
    private class FishTankMoveResponse
    {
        [JsonProperty("success")]
        public bool success;
        [JsonProperty("message")]
        public string message;
        [JsonProperty("fishTankCount")]
        public int fishTankCount;
        [JsonProperty("fishTankCapacity")]
        public int fishTankCapacity;
        [JsonProperty("bagCount")]
        public int bagCount;
        [JsonProperty("bagCapacity")]
        public int bagCapacity;
    }

    [Serializable]
    private class FishTankBatchMoveResponse
    {
        [JsonProperty("success")]
        public bool success;
        [JsonProperty("message")]
        public string message;
        [JsonProperty("movedCount")]
        public int movedCount;
        [JsonProperty("fishTankCount")]
        public int fishTankCount;
        [JsonProperty("fishTankCapacity")]
        public int fishTankCapacity;
        [JsonProperty("bagCapacity")]
        public int bagCapacity;
    }
}
