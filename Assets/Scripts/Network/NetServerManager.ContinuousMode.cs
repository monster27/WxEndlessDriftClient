using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager
{
    private bool isInContinuousMode = false;
    private float continuousModeRemainingTime = 0f;
    private int currentSceneBaitCount = 0;
    private long baitEndTime = 0;
    private bool baitEndTimeIsSeconds = false;

    private int GetCurrentSceneBaitCount() => playerInventory.TryGetValue(2501, out int count) ? count : 0;

    private IEnumerator FetchGameState()
    {
        if (!isConnected) yield break;
        yield return FetchGetJson<ContinuousModeStatus>("/api/game/continuous-mode/status", data =>
        {
            if (data == null) return;
            isInContinuousMode = data.isInContinuousMode;
            continuousModeRemainingTime = data.remainingTime;
        }, "连续模式状态");
    }

    private IEnumerator FetchBaitCount()
    {
        if (!isConnected) yield break;
        yield return FetchGetJson<BaitCountResponse>("/api/game/bait/count", data =>
        {
            if (data != null) currentSceneBaitCount = data.baitCount;
        }, "鱼饵数量");
    }

    // ========== 连续钓鱼模式 ==========

    private void OnConsumeBaitAndEnterContinuousMode() => StartCoroutine(AddBaitTimeCoroutine());

    private IEnumerator AddBaitTimeCoroutine()
    {
        string json = NetUtils.SerializeToJson(new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId }, { "addSeconds", 30 }
        });

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        using (var request = new UnityWebRequest(serverUrl + "/api/game/add-bait-time", "POST"))
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
                    var resp = JsonUtility.FromJson<AddBaitTimeResponse>(request.downloadHandler.text);
                    if (resp != null && resp.success)
                    {
                        if (ApplyContinuousModeResponse(resp.remainingTime, resp.baitEndTime))
                        {
                            OnContinuousModeEntered();
                            yield break;
                        }
                        yield break;
                    }
                    Logger.LogWarning($"[NetServerManager] 增加窝料时间失败: {resp?.message ?? "未知错误"}");
                    GameUIManager.ShowMessage(resp?.message ?? "操作失败");
                    yield break;
                }
                catch (Exception ex) { Logger.LogError($"[NetServerManager] 解析响应失败: {ex.Message}"); }
            }
        }

        yield return StartCoroutine(EnterContinuousModeWithBaitEndTimeCoroutine());
    }

    private IEnumerator EnterContinuousModeWithBaitEndTimeCoroutine()
    {
        using (var request = UnityWebRequest.PostWwwForm(serverUrl + "/api/game/enter-continuous-mode", ""))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Logger.LogError("[NetServerManager] 进入连续钓鱼模式请求失败: " + request.error);
                yield break;
            }

            try
            {
                var resp = JsonUtility.FromJson<EnterContinuousModeWithBaitEndTimeResponse>(request.downloadHandler.text);
                if (resp != null && resp.success)
                {
                    if (ApplyContinuousModeResponse(resp.remainingTime, resp.baitEndTime))
                        OnContinuousModeEntered();
                }
                else
                {
                    GameUIManager.ShowMessage("窝料不足，无法进入连续钓鱼模式");
                }
            }
            catch (Exception ex) { Logger.LogError($"[NetServerManager] 解析响应失败: {ex.Message}"); }
        }
    }

    /// <summary>统一处理连续模式响应，返回是否成功进入</summary>
    private bool ApplyContinuousModeResponse(float remainingTime, long newBaitEndTime)
    {
        if (remainingTime > 0)
        {
            continuousModeRemainingTime = remainingTime;
            isInContinuousMode = true;
            currentFishingMode = FishingMode.Continuous;
            baitEndTimeIsSeconds = newBaitEndTime <= 1000000000;
            if (!baitEndTimeIsSeconds) baitEndTime = newBaitEndTime;
            else baitEndTime = 0;
            return true;
        }

        if (newBaitEndTime > 0)
        {
            baitEndTime = newBaitEndTime;
            isInContinuousMode = true;
            currentFishingMode = FishingMode.Continuous;
            UpdateContinuousModeRemainingTime();
            return true;
        }

        return false;
    }

    private void OnContinuousModeEntered()
    {
        PlayerDataManager.Instance?.SyncInventoryFromServer();
        CommunicateEvent.Modify("Bag_RefreshItems");
        UpdateContinuousModeUI();
    }

    // ========== 剩余时间更新 ==========

    private void UpdateContinuousModeRemainingTime()
    {
        bool wasInContinuousMode = isInContinuousMode;
        float previousTime = continuousModeRemainingTime;

        if (baitEndTimeIsSeconds)
        {
            UpdateBySecondsCountdown();
        }
        else if (baitEndTime > 0)
        {
            UpdateByServerTimestamp();
        }
        else
        {
            ExitContinuousMode();
        }

        NotifyIfChanged(wasInContinuousMode, previousTime);
    }

    private void UpdateBySecondsCountdown()
    {
        continuousModeRemainingTime = Mathf.Max(0, continuousModeRemainingTime - Time.deltaTime);
        isInContinuousMode = continuousModeRemainingTime > 0;
        currentFishingMode = isInContinuousMode ? FishingMode.Continuous : FishingMode.Normal;
        if (!isInContinuousMode) baitEndTimeIsSeconds = false;
    }

    private void UpdateByServerTimestamp()
    {
        long currentServerTime = lastServerTime / 1000;
        if (currentServerTime <= 0) currentServerTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        float remaining = baitEndTime - currentServerTime;
        if (remaining > 0)
        {
            continuousModeRemainingTime = remaining;
            isInContinuousMode = true;
            currentFishingMode = FishingMode.Continuous;
        }
        else
        {
            ExitContinuousMode();
        }
    }

    private void ExitContinuousMode()
    {
        continuousModeRemainingTime = 0;
        isInContinuousMode = false;
        currentFishingMode = FishingMode.Normal;
        baitEndTime = 0;
        baitEndTimeIsSeconds = false;
    }

    private void NotifyIfChanged(bool wasInContinuousMode, float previousTime)
    {
        if (wasInContinuousMode != isInContinuousMode || Mathf.Abs(previousTime - continuousModeRemainingTime) > 0.1f)
            CommunicateEvent.Modify<float>("ContinuousModeTimeUpdated", continuousModeRemainingTime);
    }

    // ========== 状态同步 ==========

    public void SyncContinuousModeStatus() => StartCoroutine(SyncContinuousModeStatusCoroutine());

    private IEnumerator SyncContinuousModeStatusCoroutine()
    {
        yield return FetchGetJson<ContinuousModeStatus>("/api/game/continuous-mode/status", resp =>
        {
            if (resp == null) return;
            baitEndTime = resp.baitEndTime;
            UpdateContinuousModeRemainingTime();
            CommunicateEvent.Modify<float>("ContinuousModeTimeUpdated", continuousModeRemainingTime);
            UpdateContinuousModeUI();
        }, "连续模式状态");
    }

    private void UpdateContinuousModeUI()
    {
        CommunicateEvent.Modify<float>("ContinuousModeTimeUpdated", continuousModeRemainingTime);
        var countdownText = GameUIManager.Instance?.transform.Find("Canvas/BaitCountdownText")?.GetComponent<UnityEngine.UI.Text>();
        if (countdownText == null) return;
        if (isInContinuousMode && continuousModeRemainingTime > 0)
        {
            int m = (int)(continuousModeRemainingTime / 60), s = (int)(continuousModeRemainingTime % 60);
            countdownText.text = $"{m:00}:{s:00}";
        }
        else countdownText.text = "00:00";
    }

    // ========== 辅助数据类 ==========

    [Serializable] private class EnterContinuousModeWithBaitEndTimeResponse { public bool success; public string message; public float remainingTime; public long baitEndTime; }
    [Serializable] private class AddBaitTimeResponse { public bool success; public string message; public float remainingTime; public long baitEndTime; }
    [Serializable] private class ContinuousModeStatus { public bool isInContinuousMode; public float remainingTime; public long baitEndTime; }
    [Serializable] private class BaitCountResponse { public int baitCount; }
}