using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SharedModels;

public class GameUIManager : SingletonMonoFromScene<GameUIManager>
{
    public MainGameView mainGameView;
    public BagView bagView;
    public FishBagView fishBagView;
    public MallView mallView;
    public TipView tipView;
    public EquipmentView equipmentView;
    public AdvertisingView advertisingView;
    public MapView mapView;
    public DialogView dialogView;  // ✅ 新增

    public void Init()
    {
        if (mainGameView != null)
        {
            mainGameView.BaseViewInit();
        }

        if (bagView != null)
        {
            bagView.BaseViewInit();
        }

        if (fishBagView != null)
        {
            fishBagView.BaseViewInit();
        }

        if (equipmentView != null)
        {
            equipmentView.Init();
        }

        if (mapView != null)  // ✅ 新增
        {
            mapView.BaseViewInit();
        }

        RegisterEvents();
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register("UI_OpenBag", OpenBag);
        CommunicateEvent.Register("UI_OpenFishBag", OpenFishBag);
        CommunicateEvent.Register("UI_OpenMall", OpenMall);
        CommunicateEvent.Register("UI_OpenEquipment", OpenEquipment);
        CommunicateEvent.Register("UI_OpenMap", OpenMap);  // ✅ 新增

        CommunicateEvent.Register<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, ShowTip);
        CommunicateEvent.Register<CommunicateEvent.AdvertisingRequest>(CommunicateEvent.EVENT_UI_SHOW_ADVERTISING, OnShowAdvertisingRequest);

        // ✅ 新增：注册场景切换请求事件
        CommunicateEvent.Register<Dictionary<string, object>>("SceneSwitchRequest", OnSceneSwitchRequest);
    }

    private void OnShowAdvertisingRequest(CommunicateEvent.AdvertisingRequest request)
    {
        ShowAdvertising(request.info, request.targetId, request.btnText, (bool success) =>
        {
            CommunicateEvent.OnCallback(request.callbackId, success);
        });
    }

    public void InitTimeNameDic()
    {

    }

    public void UpdateMainViewTimee(TimeStatus status, string timeName)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateTime(status, timeName);
        }
    }

    public void UpdateMainViewWeather(int weatherId, string weatherName)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateWeather(weatherId, weatherName);
        }
    }

    public void OpenBag()
    {
        if (bagView != null)
        {
            bagView.OpenBag();
        }
    }

    public void CloseBag()
    {
        if (bagView != null)
        {
            bagView.HideView();
        }
    }

    public void OpenFishBag()
    {
        if (fishBagView != null)
        {
            fishBagView.OpenFishBag();
        }
    }

    public void CloseFishBag()
    {
        if (fishBagView != null)
        {
            fishBagView.CloseFishBag();
        }
    }

    public void OpenMall()
    {
        if (mallView != null)
        {
            mallView.OpenMall();
        }
    }

    public void CloseMall()
    {
        if (mallView != null)
        {
            mallView.CloseMall();
        }
    }

    public void OpenEquipment()
    {
        if (equipmentView != null)
        {
            equipmentView.Show();
        }
    }

    public void CloseEquipment()
    {
        if (equipmentView != null)
        {
            equipmentView.Hide();
        }
    }

    // ✅ 新增：打开地图
    public void OpenMap()
    {
        if (mapView != null)
        {
            mapView.OpenMap();
        }
    }

    // ✅ 新增：关闭地图
    public void CloseMap()
    {
        if (mapView != null)
        {
            mapView.HideView();
        }
    }

    // ✅ 新增：场景切换请求处理
    private void OnSceneSwitchRequest(Dictionary<string, object> data)
    {
        if (data == null || !data.ContainsKey("sceneId"))
        {
            Debug.LogWarning("[GameUIManager] 场景切换请求数据无效");
            return;
        }

        int sceneId = (int)data["sceneId"];
        Debug.Log($"[GameUIManager] 收到场景切换请求: {sceneId}");

        // 发送到服务器处理
        CommunicateEvent.Modify<int>("Server_SceneSwitch", sceneId);
    }

    public void ShowCatchResult(string itemName, float weight, Sprite icon, int starRatingId = 0, int itemId = 0, bool isFish = true)
    {
        if (mainGameView != null)
        {
            mainGameView.ShowCatchResult(itemName, weight, icon, starRatingId, itemId, isFish);
        }
    }

    public void UpdateGoldDisplay(int goldAmount)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateGold(goldAmount);
        }
    }

    public void ShowTip(string message)
    {
        if (tipView != null)
        {
            tipView.ShowTip(message);
        }
    }

    public void ShowDialog(string message, DialogType type = DialogType.Warning, System.Action onConfirm = null)
    {
        if (dialogView != null)
        {
            dialogView.Show(message, type, onConfirm);
        }
    }

    public void HideDialog()
    {
        if (dialogView != null)
        {
            dialogView.Hide();
        }
    }

    public static void ShowWarningMessage(string message)
    {
        if (Instance != null)
        {
            Instance.ShowDialog(message, DialogType.Warning);
        }
    }

    public static void ShowInfoMessage(string message, System.Action onConfirm = null)
    {
        if (Instance != null)
        {
            Instance.ShowDialog(message, DialogType.Info, onConfirm);
        }
    }

    public static void ShowMessage(string message)
    {
        if (Instance != null)
        {
            Instance.ShowTip(message);
        }
    }

    public void ShowAdvertising(string info, int targetId, string btnText, System.Action onConfirm)
    {
        if (advertisingView == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/AdvertisingView");
            if (prefab != null)
            {
                GameObject obj = Instantiate(prefab, transform);
                advertisingView = obj.GetComponent<AdvertisingView>();
            }
        }

        if (advertisingView != null)
        {
            advertisingView.ShowAd(info, onConfirm, null, btnText);
        }
    }

    public void ShowAdvertising(string info, int targetId, string btnText, System.Action<bool> onConfirmWithResult)
    {
        if (advertisingView == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/AdvertisingView");
            if (prefab != null)
            {
                GameObject obj = Instantiate(prefab, transform);
                advertisingView = obj.GetComponent<AdvertisingView>();
            }
        }

        if (advertisingView != null)
        {
            advertisingView.ShowAd(info, onConfirmWithResult, null, btnText);
        }
    }

    /// <summary>
    /// 更新鱼篓数量显示
    /// </summary>
    public void UpdateFishCountDisplay(int currentCount, int maxCapacity)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateFishCount(currentCount, maxCapacity);
        }
    }

    /// <summary>
    /// 更新窝料数量显示
    /// </summary>
    public void UpdateBaitCountDisplay(int baitCount)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateBaitCount(baitCount);
        }
    }

    /// <summary>
    /// 更新连续钓鱼模式剩余时间
    /// </summary>
    public void UpdateContinuousModeRemainingTime(float remainingTime)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateContinuousModeTime(remainingTime);
        }
    }
}