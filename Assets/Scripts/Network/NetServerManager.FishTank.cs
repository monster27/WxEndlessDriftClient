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
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = GetFullUrl(ServerUrls.Player.FishBagById(_currentPlayerId));
        Z_Logger.Log($"[NetServerManager] 请求鱼篓数据: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                FishBagResponse response = null;

                try
                {
                    response = Newtonsoft.Json.JsonConvert.DeserializeObject<FishBagResponse>(json);
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析鱼篓数据异常: {e.Message}");
                    onComplete?.Invoke(false);
                    yield break;
                }

                if (response != null && response.items != null)
                {
                    // ✅ 标记鱼篓数据
                    foreach (var fish in response.items)
                    {
                        fish.location = 0;
                        fish.tankId = 0;
                    }

                    // ✅ 存入 PlayerDataManager（使用主文件的 fishDetailData）
                    if (PlayerDataManager.Instance != null)
                    {
                        // 先清空旧的鱼篓数据，再添加新数据
                        PlayerDataManager.Instance.UpdateFishBagFromResponse(response.items, fishBagCapacity);
                    }

                    onComplete?.Invoke(true);
                }
                else
                {
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 请求鱼篓数据失败: {request.error}");
                onComplete?.Invoke(false);
            }
        }
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
                    data = JsonUtility.FromJson<FishTankListResponse>(json);
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
                FishTankStatusResponse data = null;

                try
                {
                    data = JsonUtility.FromJson<FishTankStatusResponse>(json);
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
                response = JsonUtility.FromJson<FishTankOperationResponse>(json);
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

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));
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
                response = JsonUtility.FromJson<FishTankUpgradeResponse>(json);
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

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));
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
    // ✅ 网络请求 - 从鱼篓放入鱼缸
    // ============================================================

    public void MoveFishFromBagToTank(int tankId, int fishItemId, Action<bool, string> onComplete = null)
    {
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
                response = JsonUtility.FromJson<FishTankMoveResponse>(responseJson);
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

                // ✅ 同步更新本地数据
                if (PlayerDataManager.Instance != null)
                {
                    // 从鱼篓移除
                    PlayerDataManager.Instance.RemoveFishFromBag(fishItemId);
                    // 添加到鱼缸（需要从服务器重新获取鱼数据，或者从缓存中获取）
                    // 这里通过重新拉取数据来保证一致性
                }

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));
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
    // ✅ 网络请求 - 从鱼缸取出到鱼篓
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
                response = JsonUtility.FromJson<FishTankMoveResponse>(responseJson);
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

                if (PlayerDataManager.Instance != null)
                    tankId = PlayerDataManager.Instance.FindTankIdByFishItemId(fishItemId);

                if (tankId > 0)
                    yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));

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
    // ✅ 网络请求 - 鱼缸转移到鱼缸
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
                response = JsonUtility.FromJson<FishTankOperationResponse>(responseJson);
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

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(fromTankId, null));
                yield return StartCoroutine(FetchSingleTankStatusCoroutine(toTankId, null));
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
                response = JsonUtility.FromJson<FishTankBatchMoveResponse>(responseJson);
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

                yield return StartCoroutine(FetchSingleTankStatusCoroutine(tankId, null));
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
            if (!success)
                GameUIManager.Instance?.ShowTip(message);
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
}
