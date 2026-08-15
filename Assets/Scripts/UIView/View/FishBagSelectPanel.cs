using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FishBagSelectPanel : BaseView
{
    public Button enableAutoSellBtn;
    public Button disableAutoSellBtn;
    public Button upgradeFishBagBtn;

    public Toggle rarity201Tog;
    public Toggle rarity202Tog;
    public Toggle rarity203Tog;
    public Toggle rarity204Tog;
    public Toggle rarity205Tog;
    public Toggle rarity206Tog;

    public Toggle starRate501Tog;
    public Toggle starRate502Tog;
    public Toggle starRate503Tog;
    public Toggle starRate504Tog;

    public Toggle notShineTog;
    public Toggle isShineTog;

    public Toggle skipSelectedTog;

    private bool _isAutoSellEnabled = false;

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();
        BindButtonListeners();
        LoadSettings();
        isInitialized = true;
    }

    private void BindButtonListeners()
    {
        if (enableAutoSellBtn != null)
        {
            enableAutoSellBtn.onClick.AddListener(OnEnableAutoSellClick);
        }

        if (disableAutoSellBtn != null)
        {
            disableAutoSellBtn.onClick.AddListener(OnDisableAutoSellClick);
        }

        if (upgradeFishBagBtn != null)
        {
            upgradeFishBagBtn.onClick.AddListener(OnUpgradeFishBagClick);
        }

        // 移除互斥逻辑，两个Toggle独立
        if (notShineTog != null)
        {
            notShineTog.onValueChanged.AddListener((_) => SaveSettingsAndSync());
        }

        if (isShineTog != null)
        {
            isShineTog.onValueChanged.AddListener((_) => SaveSettingsAndSync());
        }

        RegisterToggleEvents();
    }

    private void RegisterToggleEvents()
    {
        Toggle[] toggles = {
            rarity201Tog, rarity202Tog, rarity203Tog, rarity204Tog, rarity205Tog, rarity206Tog,
            starRate501Tog, starRate502Tog, starRate503Tog, starRate504Tog,
            skipSelectedTog
        };

        foreach (var toggle in toggles)
        {
            if (toggle != null)
            {
                toggle.onValueChanged.AddListener((_) => SaveSettingsAndSync());
            }
        }
    }

    private void OnEnableAutoSellClick()
    {
        var netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            netManager.ToggleAutoSell(true, (success) =>
            {
                if (success)
                {
                    _isAutoSellEnabled = true;
                    PlayerPrefs.SetInt("FishBag_AutoSellEnabled", 1);
                    PlayerPrefs.Save();
                    ShowTip("自动出售已开启");
                    CommunicateEvent.Modify("FishBagDataUpdated");
                }
                else
                {
                    ShowTip("开启自动出售失败");
                }
            });
        }
    }

    private void OnDisableAutoSellClick()
    {
        var netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            netManager.ToggleAutoSell(false, (success) =>
            {
                if (success)
                {
                    _isAutoSellEnabled = false;
                    PlayerPrefs.SetInt("FishBag_AutoSellEnabled", 0);
                    PlayerPrefs.Save();
                    ShowTip("自动出售已关闭");
                    CommunicateEvent.Modify("FishBagDataUpdated");
                }
                else
                {
                    ShowTip("关闭自动出售失败");
                }
            });
        }
    }

    private void OnUpgradeFishBagClick()
    {
        var netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            netManager.FetchFishBagLevel(data =>
            {
                if (data != null)
                {
                    if (!data.canUpgrade)
                    {
                        ShowTip("鱼篓已达到最高等级");
                        return;
                    }

                    netManager.UpgradeFishBag((success, message) =>
                    {
                        if (success)
                        {
                            ShowTip("鱼篓升级成功");
                        }
                        else
                        {
                            ShowTip("升级失败: " + message);
                        }
                    });
                }
            });
        }
    }

    private void ShowTip(string message)
    {
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, message);
    }

    public void LoadSettings()
    {
        LoadRarityToggles();
        LoadStarToggles();
        LoadShineToggles();
        skipSelectedTog.isOn = PlayerPrefs.GetInt("FishBag_SkipSelected", 0) == 1;
        _isAutoSellEnabled = PlayerPrefs.GetInt("FishBag_AutoSellEnabled", 0) == 1;

        FetchSettingsFromServer();
    }

    private void SaveSettingsAndSync()
    {
        SaveSettings();
        SyncSettingsToServer();
    }

    public void SaveSettings()
    {
        SaveRarityToggles();
        SaveStarToggles();
        SaveShineToggles();
        PlayerPrefs.SetInt("FishBag_SkipSelected", skipSelectedTog.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SyncSettingsToServer()
    {
        var netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            var request = new NetServerManager.FishBagFilterConfigRequest
            {
                isAutoSellEnabled = _isAutoSellEnabled,
                rarity201 = rarity201Tog.isOn,
                rarity202 = rarity202Tog.isOn,
                rarity203 = rarity203Tog.isOn,
                rarity204 = rarity204Tog.isOn,
                rarity205 = rarity205Tog.isOn,
                rarity206 = rarity206Tog.isOn,
                starRate501 = starRate501Tog.isOn,
                starRate502 = starRate502Tog.isOn,
                starRate503 = starRate503Tog.isOn,
                starRate504 = starRate504Tog.isOn,
                notShine = notShineTog.isOn,
                isShine = isShineTog.isOn,
                skipSelected = skipSelectedTog.isOn
            };

            netManager.SaveFilterConfig(request, (success) =>
            {
                if (success)
                {
                    Debug.Log("[FishBagSelectPanel] 鱼篓筛选配置已同步到服务器");
                }
                else
                {
                    Debug.LogError("[FishBagSelectPanel] 鱼篓筛选配置同步失败");
                }
            });
        }
    }

    private void FetchSettingsFromServer()
    {
        var netManager = NetServerManager.Instance;
        if (netManager != null)
        {
            netManager.FetchFilterConfig(response =>
            {
                if (response != null)
                {
                    _isAutoSellEnabled = response.isAutoSellEnabled;
                    rarity201Tog.isOn = response.rarity201;
                    rarity202Tog.isOn = response.rarity202;
                    rarity203Tog.isOn = response.rarity203;
                    rarity204Tog.isOn = response.rarity204;
                    rarity205Tog.isOn = response.rarity205;
                    rarity206Tog.isOn = response.rarity206;
                    starRate501Tog.isOn = response.starRate501;
                    starRate502Tog.isOn = response.starRate502;
                    starRate503Tog.isOn = response.starRate503;
                    starRate504Tog.isOn = response.starRate504;
                    notShineTog.isOn = response.notShine;
                    isShineTog.isOn = response.isShine;
                    skipSelectedTog.isOn = response.skipSelected;

                    PlayerPrefs.SetInt("FishBag_AutoSellEnabled", _isAutoSellEnabled ? 1 : 0);
                    PlayerPrefs.SetInt("FishBag_SkipSelected", skipSelectedTog.isOn ? 1 : 0);
                    PlayerPrefs.Save();

                    Debug.Log("[FishBagSelectPanel] 从服务器获取鱼篓筛选配置成功");
                }
            });
        }
    }

    private void LoadRarityToggles()
    {
        SetToggleFromPrefs(rarity201Tog, "FishBag_Rarity_201");
        SetToggleFromPrefs(rarity202Tog, "FishBag_Rarity_202");
        SetToggleFromPrefs(rarity203Tog, "FishBag_Rarity_203");
        SetToggleFromPrefs(rarity204Tog, "FishBag_Rarity_204");
        SetToggleFromPrefs(rarity205Tog, "FishBag_Rarity_205");
        SetToggleFromPrefs(rarity206Tog, "FishBag_Rarity_206");
    }

    private void SaveRarityToggles()
    {
        SaveToggleToPrefs(rarity201Tog, "FishBag_Rarity_201");
        SaveToggleToPrefs(rarity202Tog, "FishBag_Rarity_202");
        SaveToggleToPrefs(rarity203Tog, "FishBag_Rarity_203");
        SaveToggleToPrefs(rarity204Tog, "FishBag_Rarity_204");
        SaveToggleToPrefs(rarity205Tog, "FishBag_Rarity_205");
        SaveToggleToPrefs(rarity206Tog, "FishBag_Rarity_206");
    }

    private void LoadStarToggles()
    {
        SetToggleFromPrefs(starRate501Tog, "FishBag_Star_501");
        SetToggleFromPrefs(starRate502Tog, "FishBag_Star_502");
        SetToggleFromPrefs(starRate503Tog, "FishBag_Star_503");
        SetToggleFromPrefs(starRate504Tog, "FishBag_Star_504");
    }

    private void SaveStarToggles()
    {
        SaveToggleToPrefs(starRate501Tog, "FishBag_Star_501");
        SaveToggleToPrefs(starRate502Tog, "FishBag_Star_502");
        SaveToggleToPrefs(starRate503Tog, "FishBag_Star_503");
        SaveToggleToPrefs(starRate504Tog, "FishBag_Star_504");
    }

    private void LoadShineToggles()
    {
        // 分别读取两个独立开关
        if (notShineTog != null)
            notShineTog.isOn = PlayerPrefs.GetInt("FishBag_NotShine", 0) == 1;
        if (isShineTog != null)
            isShineTog.isOn = PlayerPrefs.GetInt("FishBag_IsShine", 0) == 1;
    }

    private void SaveShineToggles()
    {
        // 分别保存两个独立开关
        if (notShineTog != null)
            PlayerPrefs.SetInt("FishBag_NotShine", notShineTog.isOn ? 1 : 0);
        if (isShineTog != null)
            PlayerPrefs.SetInt("FishBag_IsShine", isShineTog.isOn ? 1 : 0);
    }

    private void SetToggleFromPrefs(Toggle toggle, string key)
    {
        if (toggle != null)
        {
            toggle.isOn = PlayerPrefs.GetInt(key, 0) == 1;
        }
    }

    private void SaveToggleToPrefs(Toggle toggle, string key)
    {
        if (toggle != null)
        {
            PlayerPrefs.SetInt(key, toggle.isOn ? 1 : 0);
        }
    }

    public FishBagFilter GetCurrentFilter()
    {
        FishBagFilter filter = new FishBagFilter();

        filter.selectedRarities = new List<int>();
        AddRarityIfSelected(filter.selectedRarities, rarity201Tog, 201);
        AddRarityIfSelected(filter.selectedRarities, rarity202Tog, 202);
        AddRarityIfSelected(filter.selectedRarities, rarity203Tog, 203);
        AddRarityIfSelected(filter.selectedRarities, rarity204Tog, 204);
        AddRarityIfSelected(filter.selectedRarities, rarity205Tog, 205);
        AddRarityIfSelected(filter.selectedRarities, rarity206Tog, 206);

        filter.selectedStars = new List<int>();
        AddStarIfSelected(filter.selectedStars, starRate501Tog, 501);
        AddStarIfSelected(filter.selectedStars, starRate502Tog, 502);
        AddStarIfSelected(filter.selectedStars, starRate503Tog, 503);
        AddStarIfSelected(filter.selectedStars, starRate504Tog, 504);

        // 直接读取两个独立的Toggle状态
        filter.showShiny = isShineTog != null && isShineTog.isOn;
        filter.showNotShiny = notShineTog != null && notShineTog.isOn;

        filter.skipSelected = skipSelectedTog.isOn;

        return filter;
    }

    private void AddRarityIfSelected(List<int> list, Toggle toggle, int value)
    {
        if (toggle != null && toggle.isOn)
        {
            list.Add(value);
        }
    }

    private void AddStarIfSelected(List<int> list, Toggle toggle, int value)
    {
        if (toggle != null && toggle.isOn)
        {
            list.Add(value);
        }
    }

    public void TogglePanel()
    {
        gameObject.SetActive(!gameObject.activeSelf);
        if (gameObject.activeSelf)
        {
            LoadSettings();
        }
        else
        {
            SaveSettings();
        }
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        SaveSettings();
    }
}

[System.Serializable]
public class FishBagFilter
{
    public List<int> selectedRarities;
    public List<int> selectedStars;
    public bool showShiny;      // 是否显示闪光鱼
    public bool showNotShiny;   // 是否显示非闪光鱼
    public bool skipSelected;
}
