using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CollInfoPanel : MonoBehaviour
{
    public Button closeButton;
    public Button maskButton;
    public Text nameText;
    public Text descriptionText;
    public Text weatherText;
    public Text timeText;
    public Text baitText;
    public Text priceText;
    public Text maxWeightText;
    public Text catchCountText;
    public GameObject fishObj;

    private int currentEntryId;
    private bool isFish;

    void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
        if (maskButton != null)
        {
            maskButton.onClick.AddListener(Hide);
        }
        Hide();
    }

    public void ShowInfo(int entryId, bool isFishFlag)
    {
        currentEntryId = entryId;
        isFish = isFishFlag;

        if (isFish)
        {
            ShowFishInfo(entryId);
        }
        else
        {
            ShowNonFishInfo(entryId);
        }

        gameObject.SetActive(true);
    }

    private void ShowFishInfo(int fishId)
    {
        var fishData = LoadDataManager.Instance?.GetFishById(fishId);
        if (fishData != null)
        {
            nameText.text = fishData.name;
            descriptionText.text = fishData.description ?? "暂无描述";

            weatherText.text = GetWeatherNames(fishData.preferredWeatherIds);
            timeText.text = GetTimeNames(fishData.preferredTimeIds);
            baitText.text = GetBaitNames(fishData.preferredBaitIds);

            var itemData = LoadDataManager.Instance?.GetItemById(fishId);
            priceText.text = itemData != null ? itemData.sellPrice.ToString() : "0";

            float caughtMaxWeight = PlayerDataManager.Instance?.GetFishMaxWeight(fishId) ?? 0f;
            maxWeightText.text = caughtMaxWeight > 0 ? $"{caughtMaxWeight:F2}kg" : "0kg";
            catchCountText.text = (PlayerDataManager.Instance?.GetFishCatchCount(fishId) ?? 0).ToString();

            weatherText.gameObject.SetActive(true);
            timeText.gameObject.SetActive(true);
            baitText.gameObject.SetActive(true);
            priceText.gameObject.SetActive(true);
            maxWeightText.gameObject.SetActive(true);
            catchCountText.gameObject.SetActive(true);

            if (fishObj != null)
            {
                fishObj.SetActive(true);
            }
        }
    }

    private void ShowNonFishInfo(int entryId)
    {
        var itemData = LoadDataManager.Instance?.GetItemById(entryId);
        if (itemData != null)
        {
            nameText.text = itemData.name;
            descriptionText.text = itemData.description ?? "暂无描述";
        }
        else
        {
            nameText.text = "未知物品";
            descriptionText.text = "暂无描述";
        }

        weatherText.gameObject.SetActive(false);
        timeText.gameObject.SetActive(false);
        baitText.gameObject.SetActive(false);
        priceText.gameObject.SetActive(false);
        maxWeightText.gameObject.SetActive(false);
        catchCountText.gameObject.SetActive(false);

        if (fishObj != null)
        {
            fishObj.SetActive(false);
        }
    }

    private string GetWeatherNames(List<int> weatherIds)
    {
        if (weatherIds == null || weatherIds.Count == 0) return "不限";
        List<string> names = new List<string>();
        foreach (int id in weatherIds)
        {
            var weather = LoadDataManager.Instance?.GetWeatherById(id);
            if (weather != null)
            {
                names.Add(weather.name);
            }
        }
        return string.Join(", ", names);
    }

    private string GetTimeNames(List<int> timeIds)
    {
        if (timeIds == null || timeIds.Count == 0) return "不限";
        List<string> names = new List<string>();
        foreach (int id in timeIds)
        {
            var time = LoadDataManager.Instance?.GetTimeSlotById(id);
            if (time != null)
            {
                names.Add(time.name);
            }
        }
        return string.Join(", ", names);
    }

    private string GetBaitNames(List<int> baitIds)
    {
        if (baitIds == null || baitIds.Count == 0) return "不限";
        List<string> names = new List<string>();
        foreach (int id in baitIds)
        {
            var item = LoadDataManager.Instance?.GetItemById(id);
            if (item != null)
            {
                names.Add(item.name);
            }
        }
        return string.Join(", ", names);
    }

    private int GetCatchCount(int fishId)
    {
        return 0;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
