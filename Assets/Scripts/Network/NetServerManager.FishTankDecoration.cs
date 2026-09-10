// ============================================================
// 文件: NetServerManager.FishTankDecoration.cs
// 说明: 鱼缸装饰系统网络请求
// 路径: Assets/Scripts/Network/
// ============================================================

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using static PlayerDataManager;

public partial class NetServerManager
{
    // ============================================================
    // 公开接口
    // ============================================================

    /// <summary>
    /// 获取玩家已拥有的所有装饰ID列表（从背包）
    /// </summary>
    public void FetchOwnedDecorations(Action<List<int>> onComplete = null)
    {
        StartCoroutine(FetchOwnedDecorationsCoroutine(onComplete));
    }

    /// <summary>
    /// 获取指定鱼缸各槽位的装备实例列表（含变换数据）
    /// </summary>
    public void FetchEquippedStatus(int tankId, Action<Dictionary<int, List<DecorationEquipInfo>>> onComplete = null)
    {
        StartCoroutine(FetchEquippedStatusCoroutine(tankId, onComplete));
    }

    /// <summary>
    /// 装备装饰到指定鱼缸的槽位（每次创建新实例）
    /// </summary>
    public void EquipDecoration(int tankId, int slotType, int decorationId,
        float? posX = null, float? posY = null, float? posZ = null,
        float? scaleX = null, float? scaleY = null, float? scaleZ = null,
        float? rotX = null, float? rotY = null, float? rotZ = null,
        Action<bool, string, int> onComplete = null)
    {
        StartCoroutine(EquipDecorationCoroutine(tankId, slotType, decorationId,
            posX, posY, posZ, scaleX, scaleY, scaleZ, rotX, rotY, rotZ, onComplete));
    }

    /// <summary>
    /// 按槽位卸下装饰（卸下该槽位最早的一个实例，保留兼容）
    /// </summary>
    public void UnequipDecoration(int tankId, int slotType, Action<bool, string> onComplete = null)
    {
        StartCoroutine(UnequipDecorationCoroutine(tankId, slotType, onComplete));
    }

    /// <summary>
    /// 按记录ID卸下装饰（推荐使用，精确控制）
    /// </summary>
    public void UnEquipDecoration(int tankId, int recordId, Action<bool, string> onComplete = null)
    {
        StartCoroutine(UnequipDecorationByIdCoroutine(recordId, onComplete));
    }

    /// <summary>
    /// 镜像装饰（通过 update-transform 修改 RotationY）
    /// </summary>
    public void MirrorDecoration(int tankId, int recordId, float rotationY, Action<bool, string> onComplete = null)
    {
        StartCoroutine(UpdateTransformCoroutine(recordId,
            null, null, null,
            null, null, null,
            null, rotationY, null,
            onComplete));
    }

    /// <summary>
    /// 移动装饰（通过 update-transform 修改 PositionX / PositionY）
    /// </summary>
    public void MoveDecoration(int tankId, int recordId, float posX, float posY, Action<bool, string> onComplete = null)
    {
        StartCoroutine(UpdateTransformCoroutine(recordId,
            posX, posY, null,
            null, null, null,
            null, null, null,
            onComplete));
    }

    /// <summary>
    /// 应用纹理装饰（82/83/84）—— 由于这三类每类只能装备 1 个，装备成功即已应用，无需额外网络请求
    /// </summary>
    public void ApplyTextureDecoration(int tankId, int category, int decorationId, Action<bool, string> onComplete = null)
    {
        onComplete?.Invoke(true, "应用成功");
    }

    // ============================================================
    // 协程实现
    // ============================================================

    private IEnumerator FetchOwnedDecorationsCoroutine(Action<List<int>> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(new List<int>());
            yield break;
        }

        string url = GetFullUrl(ServerUrls.FishTankDecoration.List(_currentPlayerId));
        Z_Logger.Log($"[NetServerManager] 获取已拥有装饰: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                try
                {
                    var response = JsonConvert.DeserializeObject<DecorationListResponse>(json);
                    if (response != null && response.success)
                    {
                        Z_Logger.Log($"[NetServerManager] 已拥有装饰数量: {response.decorationIds?.Count ?? 0}");
                        onComplete?.Invoke(response.decorationIds ?? new List<int>());
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析已拥有装饰列表失败: {e.Message}");
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 获取已拥有装饰失败: {request.error}");
            }
        }

        onComplete?.Invoke(new List<int>());
    }

    private IEnumerator FetchEquippedStatusCoroutine(int tankId, Action<Dictionary<int, List<DecorationEquipInfo>>> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(new Dictionary<int, List<DecorationEquipInfo>>());
            yield break;
        }

        string url = GetFullUrl(ServerUrls.FishTankDecoration.Equipped(_currentPlayerId, tankId));
        Z_Logger.Log($"[NetServerManager] 获取鱼缸{tankId}装备状态: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                try
                {
                    var response = JsonConvert.DeserializeObject<EquippedStatusResponse>(json);
                    if (response != null && response.success)
                    {
                        var result = response.data ?? new Dictionary<int, List<DecorationEquipInfo>>();
                        int totalCount = 0;
                        foreach (var kv in result) totalCount += kv.Value.Count;
                        Z_Logger.Log($"[NetServerManager] 鱼缸{tankId}装备状态加载完成，{result.Count} 个槽位，共 {totalCount} 个实例");
                        onComplete?.Invoke(result);
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析装备状态失败: {e.Message}");
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 获取装备状态失败: {request.error}");
            }
        }

        onComplete?.Invoke(new Dictionary<int, List<DecorationEquipInfo>>());
    }

    private IEnumerator EquipDecorationCoroutine(int tankId, int slotType, int decorationId,
        float? posX, float? posY, float? posZ,
        float? scaleX, float? scaleY, float? scaleZ,
        float? rotX, float? rotY, float? rotZ,
        Action<bool, string, int> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接", 0);
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            { "PlayerId", _currentPlayerId },
            { "TankId", tankId },
            { "SlotType", slotType },
            { "DecorationId", decorationId }
        };

        // 只有 80/81 才传递变换数据
        if (slotType == 80 || slotType == 81)
        {
            if (posX.HasValue) requestData["PositionX"] = posX.Value;
            if (posY.HasValue) requestData["PositionY"] = posY.Value;
            if (posZ.HasValue) requestData["PositionZ"] = posZ.Value;
            if (scaleX.HasValue) requestData["ScaleX"] = scaleX.Value;
            if (scaleY.HasValue) requestData["ScaleY"] = scaleY.Value;
            if (scaleZ.HasValue) requestData["ScaleZ"] = scaleZ.Value;
            if (rotX.HasValue) requestData["RotationX"] = rotX.Value;
            if (rotY.HasValue) requestData["RotationY"] = rotY.Value;
            if (rotZ.HasValue) requestData["RotationZ"] = rotZ.Value;
        }

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        string url = GetFullUrl(ServerUrls.FishTankDecoration.Equip);

        Z_Logger.Log($"[NetServerManager] 装备装饰请求: tankId={tankId}, slot={slotType}, decoId={decorationId}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;
                try
                {
                    var response = JsonConvert.DeserializeObject<EquipOperationResponse>(responseJson);
                    if (response != null && response.success)
                    {
                        Z_Logger.Log($"[NetServerManager] 装备装饰成功: {response.message}, 新ID={response.newRecordId}");
                        onComplete?.Invoke(true, response.message, response.newRecordId);
                        yield break;
                    }
                    else
                    {
                        Z_Logger.LogWarning($"[NetServerManager] 装备装饰失败: {response?.message ?? "未知错误"}");
                        onComplete?.Invoke(false, response?.message ?? "装备失败", 0);
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析装备响应失败: {e.Message}");
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 装备请求失败: {request.error}");
            }
        }

        onComplete?.Invoke(false, "网络请求失败", 0);
    }

    private IEnumerator UnequipDecorationCoroutine(int tankId, int slotType, Action<bool, string> onComplete)
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
            { "SlotType", slotType }
        };

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        string url = GetFullUrl(ServerUrls.FishTankDecoration.Unequip);

        Z_Logger.Log($"[NetServerManager] 卸下装饰请求: tankId={tankId}, slot={slotType}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;
                try
                {
                    var response = JsonConvert.DeserializeObject<OperationResponse>(responseJson);
                    if (response != null && response.success)
                    {
                        Z_Logger.Log($"[NetServerManager] 卸下装饰成功: {response.message}");
                        onComplete?.Invoke(true, response.message);
                        yield break;
                    }
                    else
                    {
                        Z_Logger.LogWarning($"[NetServerManager] 卸下装饰失败: {response?.message ?? "未知错误"}");
                        onComplete?.Invoke(false, response?.message ?? "卸下失败");
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析卸下响应失败: {e.Message}");
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 卸下请求失败: {request.error}");
            }
        }

        onComplete?.Invoke(false, "网络请求失败");
    }

    private IEnumerator UnequipDecorationByIdCoroutine(int recordId, Action<bool, string> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            { "PlayerId", _currentPlayerId },
            { "RecordId", recordId }
        };

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        string url = GetFullUrl(ServerUrls.FishTankDecoration.UnequipById);

        Z_Logger.Log($"[NetServerManager] 按ID卸下装饰请求: recordId={recordId}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;
                try
                {
                    var response = JsonConvert.DeserializeObject<OperationResponse>(responseJson);
                    if (response != null && response.success)
                    {
                        Z_Logger.Log($"[NetServerManager] 按ID卸下装饰成功: {response.message}");
                        onComplete?.Invoke(true, response.message);
                        yield break;
                    }
                    else
                    {
                        Z_Logger.LogWarning($"[NetServerManager] 按ID卸下装饰失败: {response?.message ?? "未知错误"}");
                        onComplete?.Invoke(false, response?.message ?? "卸下失败");
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析按ID卸下响应失败: {e.Message}");
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 按ID卸下请求失败: {request.error}");
            }
        }

        onComplete?.Invoke(false, "网络请求失败");
    }

    /// <summary>
    /// 更新装饰变换的统一协程（移动 / 镜像共用）
    /// </summary>
    private IEnumerator UpdateTransformCoroutine(
        int recordId,
        float? posX, float? posY, float? posZ,
        float? scaleX, float? scaleY, float? scaleZ,
        float? rotX, float? rotY, float? rotZ,
        Action<bool, string> onComplete)
    {
        if (!CheckNetworkConnection())
        {
            onComplete?.Invoke(false, "网络未连接");
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            { "PlayerId", _currentPlayerId },
            { "RecordId", recordId }
        };

        if (posX.HasValue) requestData["PositionX"] = posX.Value;
        if (posY.HasValue) requestData["PositionY"] = posY.Value;
        if (posZ.HasValue) requestData["PositionZ"] = posZ.Value;
        if (scaleX.HasValue) requestData["ScaleX"] = scaleX.Value;
        if (scaleY.HasValue) requestData["ScaleY"] = scaleY.Value;
        if (scaleZ.HasValue) requestData["ScaleZ"] = scaleZ.Value;
        if (rotX.HasValue) requestData["RotationX"] = rotX.Value;
        if (rotY.HasValue) requestData["RotationY"] = rotY.Value;
        if (rotZ.HasValue) requestData["RotationZ"] = rotZ.Value;

        string json = NetUtils.SerializeToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        string url = GetFullUrl(ServerUrls.FishTankDecoration.UpdateTransform);

        Z_Logger.Log($"[NetServerManager] 更新装饰变换请求: recordId={recordId}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;
                try
                {
                    var response = JsonConvert.DeserializeObject<OperationResponse>(responseJson);
                    if (response != null && response.success)
                    {
                        Z_Logger.Log($"[NetServerManager] 更新变换成功: {response.message}");
                        onComplete?.Invoke(true, response.message);
                        yield break;
                    }
                    else
                    {
                        Z_Logger.LogWarning($"[NetServerManager] 更新变换失败: {response?.message ?? "未知错误"}");
                        onComplete?.Invoke(false, response?.message ?? "更新失败");
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析更新变换响应失败: {e.Message}");
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 更新变换请求失败: {request.error}");
            }
        }

        onComplete?.Invoke(false, "网络请求失败");
    }

    // ============================================================
    // 数据类定义
    // ============================================================

    [Serializable]
    private class DecorationListResponse
    {
        public bool success;
        public List<int> decorationIds;
    }

    [Serializable]
    private class EquippedStatusResponse
    {
        public bool success;
        public Dictionary<int, List<DecorationEquipInfo>> data;
    }

    [Serializable]
    private class OperationResponse
    {
        public bool success;
        public string message;
    }

    [Serializable]
    private class EquipOperationResponse
    {
        public bool success;
        public string message;
        public int newRecordId;
    }
}
