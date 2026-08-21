using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;
//using SharedModels;
//using Z_Logger = Utils.Z_Logger;

public partial class NetServerManager
{
    public void UnlockSkill(int skillId, System.Action<bool> callback)
    {
        StartCoroutine(UnlockSkillCoroutine(skillId, callback));
    }

    private IEnumerator UnlockSkillCoroutine(int skillId, System.Action<bool> callback)
    {
        string url = serverUrl + ServerUrls.Skill.Unlock;
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"ComponentId\":{skillId}}}";

        Z_Logger.Log($"[NetServerManager] 解锁技能请求: {jsonData}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Z_Logger.Log($"[NetServerManager] 解锁技能响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<UnlockSkillResponse>(responseText);
                    if (response != null && response.success)
                {
                    Z_Logger.Log($"[NetServerManager] 成功解锁技能 {skillId}");

                    equipmentLevelMap[skillId] = 1;
                    Z_Logger.Log($"[NetServerManager] 设置技能等级缓存: skillId={skillId}, level=1");

                    // ✅ 更新本地背包数据，使 IsSkillObtained 返回正确结果
                    if (response.inventory != null)
                    {
                        var inventoryDict = new Dictionary<int, int>();
                        foreach (var item in response.inventory)
                        {
                            inventoryDict[item.key] = item.value;
                        }
                        playerInventory = inventoryDict;
                        Z_Logger.Log($"[NetServerManager] 解锁技能后背包数据已更新: {playerInventory.Count} 个物品");

                        if (PlayerDataManager.Instance != null)
                        {
                            PlayerDataManager.Instance.UpdateInventoryFromServer(playerInventory);
                        }
                    }
                    else
                    {
                        // 如果服务器未返回背包数据，主动同步
                        if (PlayerDataManager.Instance != null)
                        {
                            PlayerDataManager.Instance.SyncInventoryFromServer();
                        }
                    }

                    // ✅ 触发刷新事件，刷新技能列表界面
                    CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, (skillId, 1));
                    CommunicateEvent.Modify("Equipment_Refresh");

                    callback?.Invoke(true);
                }
                    else
                    {
                        Z_Logger.LogWarning($"[NetServerManager] 解锁技能失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false);
                    }
                }
                catch (System.Exception ex)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析解锁技能响应失败: {ex.Message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 解锁技能请求失败: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// 解锁技能槽位（通过看广告）
    /// </summary>
    public void UnlockSkillSlot(int slot, System.Action<bool> callback)
    {
        StartCoroutine(UnlockSkillSlotCoroutine(slot, callback));
    }

    private IEnumerator UnlockSkillSlotCoroutine(int slot, System.Action<bool> callback)
    {
        string url = serverUrl + ServerUrls.Skill.UnlockSlot;
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"Slot\":{slot}}}";

        Z_Logger.Log($"[NetServerManager] 解锁技能槽位请求: {jsonData}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Z_Logger.Log($"[NetServerManager] 解锁技能槽位响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<UnlockSkillSlotResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Z_Logger.Log($"[NetServerManager] 成功解锁技能槽位 {slot}");

                        if (slot == 1)
                            skill1SlotUnlocked = true;
                        else if (slot == 2)
                            skill2SlotUnlocked = true;

                        // ✅ 触发装备刷新事件
                        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, ((int)EquipmentSlotType.Skill2, 0));
                        CommunicateEvent.Modify("Equipment_Refresh");

                        callback?.Invoke(true);
                    }
                    else
                    {
                        Z_Logger.LogWarning($"[NetServerManager] 解锁技能槽位失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false);
                    }
                }
                catch (System.Exception ex)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析解锁技能槽位响应失败: {ex.Message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 解锁技能槽位请求失败: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    public void UpgradeSkill(int skillId, int newLevel, System.Action<bool> callback)
    {
        StartCoroutine(UpgradeSkillCoroutine(skillId, newLevel, callback));
    }

    private IEnumerator UpgradeSkillCoroutine(int skillId, int newLevel, System.Action<bool> callback)
    {
        string url = serverUrl + ServerUrls.Skill.Upgrade;
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"ComponentId\":{skillId},\"NewLevel\":{newLevel}}}";

        Z_Logger.Log($"[NetServerManager] 升级技能请求: {jsonData}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Z_Logger.Log($"[NetServerManager] 升级技能响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<SkillUpgradeResponse>(responseText);
                    if (response != null && response.success)
                {
                    Z_Logger.Log($"[NetServerManager] 成功升级技能 {skillId} 到等级 {response.level}");

                    equipmentLevelMap[skillId] = response.level;
                    Z_Logger.Log($"[NetServerManager] 更新技能等级缓存: skillId={skillId}, level={response.level}");

                    if (response.gold > 0)
                    {
                        playerGold = response.gold;
                        Z_Logger.Log($"[NetServerManager] 升级技能后金币: {playerGold}");

                        CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, new Dictionary<string, object>
                        {
                            { "gold", playerGold },
                            { "add", 0 },
                            { "reduce", 0 }
                        });
                        CommunicateEvent.Modify<int>(CommunicateEvent.EVENT_GOLD_CHANGED, playerGold);
                    }
                    else
                    {
                        Z_Logger.Log("[NetServerManager] 服务器响应未包含金币，触发同步");
                        CommunicateEvent.Modify(CommunicateEvent.EVENT_SYNC_GOLD);
                    }

                    CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, ((int)EquipmentSlotType.Skill1, skillId));

                    callback?.Invoke(true);
                }
                    else
                    {
                        Z_Logger.LogWarning($"[NetServerManager] 升级技能失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false);
                    }
                }
                catch (System.Exception ex)
                {
                    Z_Logger.LogError($"[NetServerManager] 解析升级技能响应失败: {ex.Message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Z_Logger.LogError($"[NetServerManager] 升级技能请求失败: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    [System.Serializable]
    private class SkillUpgradeResponse
    {
        public bool success;
        public string message;
        public int level;
        public int gold;
    }

    [System.Serializable]
    private class SkillInventoryItem
    {
        public int key;
        public int value;
    }

    [System.Serializable]
    private class UnlockSkillResponse
    {
        public bool success;
        public string message;
        public List<SkillInventoryItem> inventory;
    }

    [System.Serializable]
    private class UnlockSkillSlotResponse
    {
        public bool success;
        public string message;
        public bool skill1SlotUnlocked;
        public bool skill2SlotUnlocked;
    }
}