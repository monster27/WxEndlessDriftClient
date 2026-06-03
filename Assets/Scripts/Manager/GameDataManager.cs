// ==================== GameDataManager.cs ====================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

public class GameDataManager : SingletonMono<GameDataManager>
{
    public System.Action<int> OnFishCaught;
    
    public const string EVENT_FISHING_REQUEST = "FishingRequest";
    public const string EVENT_FISHING_RESPONSE = "FishingResponse";
    
    private bool isFishing = false;
    private int trashStreak = 0;
    
    protected override void Awake()
    {
        base.Awake();
    }
    
    public void Init()
    {
        CommunicateEvent.Register<Dictionary<string, object>>(EVENT_FISHING_RESPONSE, OnFishingResponse);
    }
    
    public void StartFishing()
    {
        if (isFishing)
        {
            Debug.Log("[GameDataManager] 正在钓鱼中，请勿重复操作");
            return;
        }
        
        isFishing = true;
        SendFishingRequest();
    }
    
    private void SendFishingRequest()
    {
        Dictionary<string, object> requestData = new Dictionary<string, object>
        {
            { "sceneId", EnvManager.Instance.currentSceneId },
            { "timeStatus", EnvManager.Instance.timeStatus.ToString() },
            { "weatherId", EnvManager.Instance.currentWeatherId },
            { "trashStreak", trashStreak }
        };
        
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine("[GameDataManager] 发送钓鱼请求:");
        logBuilder.AppendLine($"  场景ID: {EnvManager.Instance.currentSceneId}");
        logBuilder.AppendLine($"  时间: {EnvManager.Instance.timeStatus}");
        logBuilder.AppendLine($"  天气: {EnvManager.Instance.currentWeatherId}");
        logBuilder.AppendLine($"  垃圾连续次数: {trashStreak}");
        Debug.Log(logBuilder.ToString());
        
        CommunicateEvent.Modify(EVENT_FISHING_REQUEST, requestData);
    }
    
    private void OnFishingResponse(Dictionary<string, object> data)
    {
        HandleFishingResponse(data);
    }
    
    private void HandleFishingResponse(Dictionary<string, object> responseData)
    {
        if (responseData.TryGetValue("itemId", out object itemIdObj))
        {
            int itemId = System.Convert.ToInt32(itemIdObj);
            ProcessFishingResult(itemId);
            OnFishCaught?.Invoke(itemId);
        }
        
        isFishing = false;
    }
    
    private void ProcessFishingResult(int itemId)
    {
        bool isTrash = IsTrash(itemId);
        
        if (isTrash)
        {
            trashStreak++;
        }
        else
        {
            trashStreak = 0;
        }
        
        // 移除重复添加，因为SimulationServer已经添加了
        // AddToFishBag(itemId);
        UpdateFishBagUI();
        
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine("[GameDataManager] 钓鱼结果:");
        logBuilder.AppendLine($"  物品ID: {itemId}");
        logBuilder.AppendLine($"  是否垃圾: {isTrash}");
        logBuilder.AppendLine($"  垃圾连续次数: {trashStreak}");
        Debug.Log(logBuilder.ToString());
    }
    
    private bool IsTrash(int itemId)
    {
        return itemId >= 3001 && itemId <= 3003;
    }
    
    private void AddToFishBag(int itemId)
    {
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.AddFishToInventory(itemId, 1);
        }
    }
    
    private void UpdateFishBagUI()
    {
        if (UIManager.Instance != null && UIManager.Instance.fishBagView != null)
        {
            UIManager.Instance.fishBagView.RefreshItems();
        }
    }
    
    public int GetCurrentSceneId()
    {
        return EnvManager.Instance.currentSceneId;
    }
    
    public void SetSceneId(int sceneId)
    {
        EnvManager.Instance.currentSceneId = sceneId;
    }
    
    public int GetTrashStreak()
    {
        return trashStreak;
    }
    
    public void TestFishing()
    {
        StartFishing();
    }
}
