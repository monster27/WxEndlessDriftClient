using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
//using Utils;

public partial class NetServerManager
{
    public void RequestPlayerSkins()
    {
        StartCoroutine(RequestPlayerSkinsCoroutine());
    }

    internal IEnumerator RequestPlayerSkinsCoroutine()
    {
        int playerId = _currentPlayerId;
        if (playerId <= 0)
        {
            Z_Logger.LogWarning("[NetServerManager] 请求皮肤数据失败：玩家ID无效");
            yield break;
        }

        string url = $"{serverUrl}/api/Player/skins/{playerId}";
        Z_Logger.Log($"[NetServerManager] 请求皮肤数据: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Z_Logger.Log($"[NetServerManager] 皮肤数据响应原始JSON: {json}");
                    
                    var response = JsonUtility.FromJson<SkinResponse>(json);
                    Z_Logger.Log($"[NetServerManager] 皮肤数据解析成功: success={response.success}, message={response.message}, dataCount={response.data?.Count ?? 0}");
                    
                    ParsePlayerSkinsResponse(response);
                }
                catch (System.Exception e)
                {
                    Z_Logger.LogError("[NetServerManager] 解析皮肤数据失败: " + e.Message);
                }
            }
            else
            {
                Z_Logger.LogError("[NetServerManager] 获取皮肤数据失败: " + request.error);
            }
        }
    }

    private void ParsePlayerSkinsResponse(SkinResponse response)
    {
        if (response == null || !response.success)
        {
            Z_Logger.LogWarning("[NetServerManager] 皮肤数据为空或失败: " + (response?.message ?? "未知错误"));
            return;
        }

        Dictionary<int, int> skins = new Dictionary<int, int>();
        if (response.data != null)
        {
            foreach (var skin in response.data)
            {
                skins[skin.slotType] = skin.skinId;
                Z_Logger.Log($"[NetServerManager] 解析皮肤: slotType={skin.slotType}, skinId={skin.skinId}");
            }
        }

        Z_Logger.Log($"[NetServerManager] 解析皮肤数据完成，共 {skins.Count} 个皮肤");

        // ✅ 关键：先把皮肤数据同步到 NetServerManager.equippedSkinsData，
        //    确保 IsItemEquipped 不依赖 SkinManager.Instance 即可正确判断装备状态
        UpdateEquippedSkinsData(skins);

        CommunicateEvent.Modify<Dictionary<int, int>>(CommunicateEvent.EVENT_SKIN_DATA_UPDATED, skins);

        // ✅ 移除EVENT_REFRESH_BAG，等待初始化完成统一刷新
        Z_Logger.Log("[NetServerManager] 皮肤数据已更新到 NetServerManager，等待初始化完成统一刷新");
    }

    // ✅ 防重复请求：记录上次装备请求的槽位、皮肤ID和时间
    private int _lastEquipSkinSlot = -1;
    private int _lastEquipSkinId = -1;
    private float _lastEquipSkinTime = 0f;
    private const float EQUIP_SKIN_COOLDOWN = 0.5f; // 500ms 冷却

    public void RequestEquipSkin(int slotType, int skinId)
    {
        // ✅ 防重复：同一槽位+皮肤ID在冷却时间内直接忽略
        if (slotType == _lastEquipSkinSlot && skinId == _lastEquipSkinId
            && Time.time - _lastEquipSkinTime < EQUIP_SKIN_COOLDOWN)
        {
            Z_Logger.Log($"[NetServerManager] 装备皮肤请求被防重复拦截: slotType={slotType}, skinId={skinId}（{EQUIP_SKIN_COOLDOWN}s内重复）");
            return;
        }

        _lastEquipSkinSlot = slotType;
        _lastEquipSkinId = skinId;
        _lastEquipSkinTime = Time.time;

        StartCoroutine(RequestEquipSkinCoroutine(slotType, skinId));
    }

    private IEnumerator RequestEquipSkinCoroutine(int slotType, int skinId)
    {
        int playerId = _currentPlayerId;
        if (playerId <= 0)
        {
            Z_Logger.LogWarning("[NetServerManager] 装备皮肤失败：玩家ID无效");
            yield break;
        }

        string url = $"{serverUrl}/api/Player/skins/{playerId}/equip";
        Z_Logger.Log($"[NetServerManager] 请求装备皮肤: {url}, slotType={slotType}, skinId={skinId}");

        var body = new Dictionary<string, object>
        {
            { "slotType", slotType },
            { "skinId", skinId }
        };

        string jsonBody = NetUtils.SerializeToJson(body);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        Z_Logger.Log($"[NetServerManager] 装备皮肤请求体: {jsonBody}");

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Z_Logger.Log($"[NetServerManager] 装备皮肤响应原始JSON: {json}");
                    
                    var response = JsonUtility.FromJson<SkinResponse>(json);
                    Z_Logger.Log($"[NetServerManager] 装备皮肤解析成功: success={response.success}, message={response.message}, dataCount={response.data?.Count ?? 0}");
                    
                    ParseEquipSkinResponse(response);
                }
                catch (System.Exception e)
                {
                    Z_Logger.LogError("[NetServerManager] 解析装备皮肤响应失败: " + e.Message);
                }
            }
            else
            {
                Z_Logger.LogError("[NetServerManager] 装备皮肤失败: " + request.error);
            }
        }
    }

    private void ParseEquipSkinResponse(SkinResponse response)
    {
        if (response == null || !response.success)
        {
            Z_Logger.LogWarning("[NetServerManager] 装备皮肤失败: " + (response?.message ?? "未知错误"));
            return;
        }

        Dictionary<int, int> skins = new Dictionary<int, int>();
        if (response.data != null)
        {
            foreach (var skin in response.data)
            {
                skins[skin.slotType] = skin.skinId;
                Z_Logger.Log($"[NetServerManager] 装备皮肤后: slotType={skin.slotType}, skinId={skin.skinId}");
            }
        }

        Z_Logger.Log($"[NetServerManager] 装备皮肤成功");

        // ✅ 同步到 NetServerManager.equippedSkinsData，保证 IsItemEquipped 数据正确
        UpdateEquippedSkinsData(skins);

        CommunicateEvent.Modify<Dictionary<int, int>>(CommunicateEvent.EVENT_SKIN_DATA_UPDATED, skins);

        CommunicateEvent.Modify(CommunicateEvent.EVENT_REFRESH_BAG);
        Z_Logger.Log("[NetServerManager] 装备皮肤成功，触发背包刷新事件");
    }

    [Serializable] private class SkinResponse
    {
        public bool success;
        public string message;
        public List<SkinData> data;
    }

    [Serializable] private class SkinData
    {
        public int slotType;
        public int skinId;
    }
}
