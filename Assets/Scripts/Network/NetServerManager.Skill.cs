using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager : SingletonMono<NetServerManager>
{
    public void UnlockSkill(int skillId, System.Action<bool> callback)
    {
        StartCoroutine(UnlockSkillCoroutine(skillId, callback));
    }

    private IEnumerator UnlockSkillCoroutine(int skillId, System.Action<bool> callback)
    {
        string url = serverUrl + "/api/player/skills/unlock";
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"ComponentId\":{skillId}}}";

        Logger.Log($"[NetServerManager] 解锁技能请求: {jsonData}");

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
                Logger.Log($"[NetServerManager] 解锁技能响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<AddItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功解锁技能 {skillId}");
                        callback?.Invoke(true);
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 解锁技能失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析解锁技能响应失败: {ex.Message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 解锁技能请求失败: {request.error}");
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
        string url = serverUrl + "/api/player/skills/upgrade";
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"ComponentId\":{skillId},\"NewLevel\":{newLevel}}}";

        Logger.Log($"[NetServerManager] 升级技能请求: {jsonData}");

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
                Logger.Log($"[NetServerManager] 升级技能响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<AddItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功升级技能 {skillId} 到等级 {newLevel}");
                        callback?.Invoke(true);
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 升级技能失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析升级技能响应失败: {ex.Message}");
                    callback?.Invoke(false);
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 升级技能请求失败: {request.error}");
                callback?.Invoke(false);
            }
        }
    }
}