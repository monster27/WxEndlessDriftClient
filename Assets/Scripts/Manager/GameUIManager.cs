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

    public void Init()
    {
        if (mainGameView != null)
        {
            mainGameView.Init();
        }

        if (bagView != null)
        {
            bagView.Init();
        }

        if (fishBagView != null)
        {
            fishBagView.Init();
        }

        if (equipmentView != null)
        {
            equipmentView.Init();
        }

        RegisterEvents();
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register("UI_OpenBag", OpenBag);
        CommunicateEvent.Register("UI_OpenFishBag", OpenFishBag);
        CommunicateEvent.Register("UI_OpenMall", OpenMall);
        CommunicateEvent.Register("UI_OpenEquipment", OpenEquipment);

        CommunicateEvent.Register<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, ShowTip);
        CommunicateEvent.Register<CommunicateEvent.AdvertisingRequest>(CommunicateEvent.EVENT_UI_SHOW_ADVERTISING, OnShowAdvertisingRequest);
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
            bagView.CloseBag();
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

    public void ShowCatchResult(string itemName, float weight, Sprite icon)
    {
        if (mainGameView != null)
        {
            mainGameView.ShowCatchResult(itemName, weight, icon);
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
}