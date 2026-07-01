using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 地图视图 - 用于切换场景
/// </summary>
public class MapView : BagViewBase
{
    public Button scene101Btn;  // 切换到场景101按钮
    public Button scene102Btn;  // 切换到场景102按钮

    public override void Init()
    {
        if (isInitialized) return;
        base.Init();

        RegisterEvents();
        BindButtons();

        isInitialized = true;
    }

    private void RegisterEvents()
    {
        // 注册场景切换响应事件
        CommunicateEvent.Register<Dictionary<string, object>>("SceneSwitchResponse", OnSceneSwitchResponse);
    }

    private void BindButtons()
    {
        if (scene101Btn != null)
        {
            scene101Btn.onClick.AddListener(() => OnSceneButtonClick(101));
        }

        if (scene102Btn != null)
        {
            scene102Btn.onClick.AddListener(() => OnSceneButtonClick(102));
        }
    }

    /// <summary>
    /// 场景按钮点击处理
    /// </summary>
    private void OnSceneButtonClick(int sceneId)
    {
        Debug.Log($"[MapView] 点击切换场景: {sceneId}");

        // 发送切换场景请求
        var requestData = new Dictionary<string, object>
        {
            { "sceneId", sceneId }
        };

        CommunicateEvent.Modify<Dictionary<string, object>>("SceneSwitchRequest", requestData);
    }

    /// <summary>
    /// 场景切换响应处理
    /// </summary>
    private void OnSceneSwitchResponse(Dictionary<string, object> data)
    {
        if (data == null) return;

        bool success = data.ContainsKey("success") && (bool)data["success"];
        int sceneId = data.ContainsKey("sceneId") ? (int)data["sceneId"] : 0;

        if (success)
        {
            Debug.Log($"[MapView] 场景切换成功: {sceneId}");
            // 可以在这里添加成功提示或关闭地图界面
            CloseBag(); // 继承自BagViewBase的关闭方法
        }
        else
        {
            string message = data.ContainsKey("message") ? (string)data["message"] : "切换失败";
            Debug.LogWarning($"[MapView] 场景切换失败: {message}");
            GameUIManager.ShowMessage(message);
        }
    }

    public void OpenMap()
    {
        gameObject.SetActive(true);
        SendEvent();
    }

    private void SendEvent()
    {
        CommunicateEvent.Modify("Map_Open");
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister<Dictionary<string, object>>("SceneSwitchResponse", OnSceneSwitchResponse);
    }
}