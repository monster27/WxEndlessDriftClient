using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
//using SharedModels;

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
    public DialogView dialogView;
    public CollectionView collectionView;

    private AsyncOperationHandle<GameObject> _adPrefabHandle;

    void OnDestroy()
    {
        AssetManager.ReleaseAddressable(_adPrefabHandle);
    }

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

        if (mapView != null)
        {
            mapView.BaseViewInit();
        }

        if (collectionView != null)
        {
            collectionView.BaseViewInit();
        }

        RegisterEvents();
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register("UI_OpenBag", OpenBag);
        CommunicateEvent.Register("UI_OpenFishBag", OpenFishBag);
        CommunicateEvent.Register("UI_OpenMall", OpenMall);
        CommunicateEvent.Register("UI_OpenEquipment", OpenEquipment);
        CommunicateEvent.Register("UI_OpenMap", OpenMap);
        CommunicateEvent.Register("UI_OpenCollection", OpenCollection);

        CommunicateEvent.Register<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, ShowTip);
        CommunicateEvent.Register<CommunicateEvent.AdvertisingRequest>(CommunicateEvent.EVENT_UI_SHOW_ADVERTISING, OnShowAdvertisingRequest);

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

    public void OpenMap()
    {
        if (mapView != null)
        {
            mapView.OpenMap();
        }
    }

    public void CloseMap()
    {
        if (mapView != null)
        {
            mapView.HideView();
        }
    }

    public void OpenCollection()
    {
        if (collectionView != null)
        {
            collectionView.OpenCollection();
        }
    }

    public void CloseCollection()
    {
        if (collectionView != null)
        {
            collectionView.CloseCollection();
        }
    }

    private void OnSceneSwitchRequest(Dictionary<string, object> data)
    {
        if (data == null || !data.ContainsKey("sceneId"))
        {
            Z_Logger.LogWarning("[GameUIManager] 场景切换请求数据无效");
            return;
        }

        int sceneId = (int)data["sceneId"];
        Z_Logger.Log($"[GameUIManager] 收到场景切换请求: {sceneId}");

        CommunicateEvent.Modify<int>("Server_SceneSwitch", sceneId);
    }

    public void ShowCatchResult(string itemName, float weight, Sprite icon, int starRatingId = 0, int itemId = 0, bool isFish = true, bool isFirstCatch = false)
    {
        if (mainGameView != null)
        {
            mainGameView.ShowCatchResult(itemName, weight, icon, starRatingId, itemId, isFish, isFirstCatch);
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
            AssetManager.LoadFromAddressables<GameObject>("Prefabs/UI/AdvertisingView", (prefab, handle) =>
            {
                _adPrefabHandle = handle;
                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, transform);
                    advertisingView = obj.GetComponent<AdvertisingView>();
                    if (advertisingView != null)
                    {
                        advertisingView.ShowAd(info, onConfirm, null, btnText);
                    }
                }
            });
        }
        else
        {
            advertisingView.ShowAd(info, onConfirm, null, btnText);
        }
    }

    public void ShowAdvertising(string info, int targetId, string btnText, System.Action<bool> onConfirmWithResult)
    {
        if (advertisingView == null)
        {
            AssetManager.LoadFromAddressables<GameObject>("Prefabs/UI/AdvertisingView", (prefab, handle) =>
            {
                _adPrefabHandle = handle;
                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, transform);
                    advertisingView = obj.GetComponent<AdvertisingView>();
                    if (advertisingView != null)
                    {
                        advertisingView.ShowAd(info, onConfirmWithResult, null, btnText);
                    }
                }
            });
        }
        else
        {
            advertisingView.ShowAd(info, onConfirmWithResult, null, btnText);
        }
    }

    public void UpdateFishCountDisplay(int currentCount, int maxCapacity)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateFishCount(currentCount, maxCapacity);
        }
    }

    public void UpdateBaitCountDisplay(int baitCount)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateBaitCount(baitCount);
        }
    }

    public void UpdateContinuousModeRemainingTime(float remainingTime)
    {
        if (mainGameView != null)
        {
            mainGameView.UpdateContinuousModeTime(remainingTime);
        }
    }
}
