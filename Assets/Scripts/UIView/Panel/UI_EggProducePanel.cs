// ============================================================
// 文件: UI_EggProducePanel.cs
// 说明: 蛋操作面板（孵化 / 卖 / 跳过 / 破壳 / 提升稀有度）
// 路径: Assets/Scripts/UI/
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_EggProducePanel : MonoBehaviour
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    [Header("基础")]
    public Image bg;
    public Image icon;
    public Text nameText;
    public Text priceText;
    public List<Text> priceTextLst;

    [Header("进度")]
    public Slider progressSlider;
    public Text progressText;

    [Header("状态对象")]
    public GameObject unProduceObj;
    public GameObject producingObj;
    public GameObject endProduceObj;

    [Header("按钮")]
    public Button hatchBtn;
    public Button sellBtn;
    public Button skipBtn;
    public Button breakEggBtn;
    public Button upgradeRarityBtn;
    public Button closeBtn;

    [Header("引用")]
    public UI_EggHatchSuccessPanel hatchSuccessPanel;

    // ============================================================
    // 运行时数据
    // ============================================================

    private int _eggId = 0;
    private int _slotIndex = -1;
    private EggHatchSlotData _slot;
    private Action _onClosed;

    private float _timerAccumulator = 0f;
    private int _localRemaining = -1;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (closeBtn != null) closeBtn.onClick.AddListener(Close);

        if (hatchBtn != null) hatchBtn.onClick.AddListener(OnHatchClick);
        if (sellBtn != null) sellBtn.onClick.AddListener(OnSellClick);
        if (skipBtn != null) skipBtn.onClick.AddListener(OnSkipClick);
        if (breakEggBtn != null) breakEggBtn.onClick.AddListener(OnBreakEggClick);
        if (upgradeRarityBtn != null) upgradeRarityBtn.onClick.AddListener(OnUpgradeRarityClick);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (_localRemaining <= 0) return;

        _timerAccumulator += Time.deltaTime;
        if (_timerAccumulator >= 1f)
        {
            _timerAccumulator -= 1f;
            _localRemaining--;
            if (_localRemaining < 0) _localRemaining = 0;

            UpdateProgressDisplay(_localRemaining, _localRemaining <= 0);
        }
    }

    // ============================================================
    // 打开
    // ============================================================

    public void Open(int eggId, int slotIndex, Action onClosed = null)
    {
        _eggId = eggId;
        _slotIndex = slotIndex;
        _onClosed = onClosed;

        gameObject.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        _onClosed?.Invoke();
    }

    // ============================================================
    // 刷新显示
    // ============================================================

    private void Refresh()
    {
        // 从 PlayerDataManager 找槽位
        _slot = null;
        if (PlayerDataManager.Instance != null)
        {
            var slot = PlayerDataManager.Instance.GetHatchSlot(_slotIndex);
            if (slot != null && slot.isOccupied && !slot.isClaimed && slot.eggId == _eggId)
            {
                _slot = slot;
            }
        }

        var eggData = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetEggById(_eggId) : null;

        // 图标 / 名字 / 价格
        LoadIcon(_eggId);
        ApplyRarityBg(_eggId);

        if (nameText != null) nameText.text = eggData?.name ?? $"蛋{_eggId}";

        int sellPrice = GetEggSellPrice(_eggId);
        if (priceText != null) priceText.text = sellPrice > 0 ? $"{sellPrice}" : "--";

        if (priceTextLst != null && priceTextLst.Count > 0)
        {
            for (int i = 0; i < priceTextLst.Count; i++)
            {
                priceTextLst[i].text = sellPrice > 0 ? $"{sellPrice}" : "--";
            }
        }

        // 状态
        if (_slot != null)
        {
            // 孵化中 / 孵化完毕
            if (_slot.isCompleted)
            {
                SetStateObjects(false, false, true);
                if (progressText != null) progressText.text = "可领取";
                if (progressSlider != null) progressSlider.value = 1f;

                // 按钮：破壳 + 提升（非最高稀有度）
                SetButtons(false, false, false, true, CanUpgradeRarity(eggData));
            }
            else
            {
                SetStateObjects(false, true, false);
                _localRemaining = _slot.remainingSeconds;
                UpdateProgressDisplay(_localRemaining, false);

                // 按钮：跳过
                SetButtons(false, false, true, false, false);
            }
        }
        else
        {
            // 未孵化
            SetStateObjects(true, false, false);
            _localRemaining = -1;

            // 按钮：孵化 + 卖
            SetButtons(true, true, false, false, false);
        }
    }

    private void SetStateObjects(bool un, bool producing, bool end)
    {
        if (unProduceObj != null) unProduceObj.SetActive(un);
        if (producingObj != null) producingObj.SetActive(producing);
        if (endProduceObj != null) endProduceObj.SetActive(end);
    }

    private void SetButtons(bool hatch, bool sell, bool skip, bool breakEgg, bool upgradeRarity)
    {
        if (hatchBtn != null) hatchBtn.gameObject.SetActive(hatch);
        if (sellBtn != null) sellBtn.gameObject.SetActive(sell);
        if (skipBtn != null) skipBtn.gameObject.SetActive(skip);
        if (breakEggBtn != null) breakEggBtn.gameObject.SetActive(breakEgg);
        if (upgradeRarityBtn != null) upgradeRarityBtn.gameObject.SetActive(upgradeRarity);
    }

    private void UpdateProgressDisplay(int remaining, bool isCompleted)
    {
        if (isCompleted)
        {
            SetStateObjects(false, false, true);
            if (progressText != null) progressText.text = "可领取";
            if (progressSlider != null) progressSlider.value = 1f;

            // 切按钮：破壳 + 提升
            var eggData = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetEggById(_eggId) : null;
            SetButtons(false, false, false, true, CanUpgradeRarity(eggData));
            return;
        }

        if (progressText != null)
        {
            int m = remaining / 60;
            int s = remaining % 60;
            progressText.text = m > 0 ? $"{m:D2}:{s:D2}" : $"{s}s";
        }

        if (progressSlider != null)
        {
            var eggData = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetEggById(_eggId) : null;
            int total = eggData?.hatchTime ?? 0;
            progressSlider.value = total > 0 ? Mathf.Clamp01(1f - (float)remaining / total) : 0f;
        }
    }

    // ============================================================
    // 按钮：孵化
    // ============================================================

    private void OnHatchClick()
    {
        if (NetServerManager.Instance == null) return;

        var net = NetServerManager.Instance;
        net.StartEggHatch(_slotIndex, _eggId, (success, message, slot) =>
        {
            if (success)
            {
                ShowTip("开始孵化");
                Refresh();
            }
            else
            {
                ShowTip(message);
            }
        });
    }

    // ============================================================
    // 按钮：卖蛋
    // ============================================================

    private void OnSellClick()
    {
        if (NetServerManager.Instance == null) return;

        int invCount = PlayerDataManager.Instance != null
            ? PlayerDataManager.Instance.GetItemQuantity(_eggId) : 0;
        if (invCount <= 0)
        {
            ShowTip("背包没有该蛋");
            return;
        }

        int unitPrice = GetEggSellPrice(_eggId);
        string desc = $"确定出售 1 个「{LoadDataManager.Instance?.GetEggName(_eggId)}」？\n获得 {unitPrice} 金币";

        GameUIManager.ShowInfoMessage(desc, () =>
        {
            NetServerManager.Instance.SellEgg(_eggId, 1, (success, message, gold) =>
            {
                if (success)
                {
                    ShowTip(message);
                    Refresh();
                    // 刷新 EggView
                    _onClosed?.Invoke();
                }
                else
                {
                    ShowTip(message);
                }
            });
        });
    }

    // ============================================================
    // 按钮：跳过孵化
    // ============================================================

    private void OnSkipClick()
    {
        if (NetServerManager.Instance == null) return;

        var eggData = LoadDataManager.Instance?.GetEggById(_eggId);
        if (eggData == null) return;

        int skipCost = eggData.skipCost;
        int currentGold = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_GOLD, 0);

        if (currentGold >= skipCost)
        {
            // 金币够 → 直接跳过
            NetServerManager.Instance.SkipEggHatch(_slotIndex, false, OnSkipResult);
        }
        else
        {
            // 金币不足 → 广告
            string adInfo = $"金币不足！跳过孵化需要{skipCost}金币，观看广告可免费跳过！";
            GameUIManager.Instance.ShowAdvertising(adInfo, _eggId, "看广告跳过", (bool adSuccess) =>
            {
                if (adSuccess)
                {
                    NetServerManager.Instance.SkipEggHatch(_slotIndex, true, OnSkipResult);
                }
                else
                {
                    ShowTip("广告未完成");
                }
            });
        }
    }

    private void OnSkipResult(bool success, string message, bool needAd, EggHatchSlotData slot)
    {
        if (success)
        {
            ShowTip("已跳过孵化");
            Refresh();
        }
        else if (needAd)
        {
            // 服务器要求看广告（金币不足）
            string adInfo = "金币不足，观看广告可免费跳过！";
            GameUIManager.Instance.ShowAdvertising(adInfo, _eggId, "看广告跳过", (bool adSuccess) =>
            {
                if (adSuccess)
                {
                    NetServerManager.Instance.SkipEggHatch(_slotIndex, true, OnSkipResult);
                }
                else
                {
                    ShowTip("广告未完成");
                }
            });
        }
        else
        {
            ShowTip(message);
        }
    }

    // ============================================================
    // 按钮：破壳领宠物
    // ============================================================

    private void OnBreakEggClick()
    {
        if (NetServerManager.Instance == null) return;

        NetServerManager.Instance.ClaimEggHatch(_slotIndex, (success, message, pet) =>
        {
            if (success)
            {
                ShowTip("孵化成功");
                Close();
                // 通知 EggView 弹宠物面板
                NotifyHatchSuccess(pet);
            }
            else
            {
                ShowTip(message);
            }
        });
    }

    // ============================================================
    // 按钮：提升孵化稀有度
    // ============================================================

    private void OnUpgradeRarityClick()
    {
        if (NetServerManager.Instance == null) return;

        var eggData = LoadDataManager.Instance?.GetEggById(_eggId);
        if (eggData == null) return;

        int cost = eggData.upgradeRarityCost;
        int currentGold = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_GOLD, 0);

        if (currentGold >= cost)
        {
            NetServerManager.Instance.UpgradeHatchedPetRarity(_slotIndex, false, OnUpgradeRarityResult);
        }
        else
        {
            string adInfo = $"金币不足！提升稀有度需要{cost}金币，观看广告可免费提升！";
            GameUIManager.Instance.ShowAdvertising(adInfo, _eggId, "看广告提升", (bool adSuccess) =>
            {
                if (adSuccess)
                {
                    NetServerManager.Instance.UpgradeHatchedPetRarity(_slotIndex, true, OnUpgradeRarityResult);
                }
                else
                {
                    ShowTip("广告未完成");
                }
            });
        }
    }

    private void OnUpgradeRarityResult(bool success, string message, bool needAd, PlayerPetData pet)
    {
        if (success)
        {
            ShowTip("提升成功");
            Close();
            NotifyHatchSuccess(pet);
        }
        else if (needAd)
        {
            string adInfo = "金币不足，观看广告可免费提升！";
            GameUIManager.Instance.ShowAdvertising(adInfo, _eggId, "看广告提升", (bool adSuccess) =>
            {
                if (adSuccess)
                {
                    NetServerManager.Instance.UpgradeHatchedPetRarity(_slotIndex, true, OnUpgradeRarityResult);
                }
                else
                {
                    ShowTip("广告未完成");
                }
            });
        }
        else
        {
            ShowTip(message);
        }
    }

    // ============================================================
    // 通知 EggView 显示孵化成功
    // ============================================================

    private void NotifyHatchSuccess(PlayerPetData pet)
    {
        if (hatchSuccessPanel != null && pet != null)
        {
            hatchSuccessPanel.Open(pet);
            return;
        }

        // 兜底：通过 GameUIManager 打开 EggView 的 hatchSuccessPanel
        if (GameUIManager.Instance != null && GameUIManager.Instance.eggView != null)
        {
            GameUIManager.Instance.eggView.ShowHatchSuccess(pet);
        }
    }

    // ============================================================
    // 辅助
    // ============================================================

    private bool CanUpgradeRarity(EggData eggData)
    {
        if (eggData == null) return false;
        return eggData.upgradeRarityCost > 0;
    }

    private int GetEggSellPrice(int eggId)
    {
        var item = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetItemById(eggId) : null;
        return item?.sellPrice ?? 0;
    }

    private void LoadIcon(int eggId)
    {
        if (icon == null) return;

        string path = eggId > 0 ? $"UI/Icon/EggIcons/{eggId}" : "UI/Icon/EggIcons/0";
        AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
        {
            if (icon != null) icon.sprite = sprite;
        });
    }

    private void ApplyRarityBg(int eggId)
    {
        if (bg == null) return;

        var eggData = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetEggById(eggId) : null;
        if (eggData == null) { bg.color = Color.white; return; }

        var rarity = LoadDataManager.Instance.GetRarityById(eggData.rarityId);
        if (rarity != null && !string.IsNullOrEmpty(rarity.colorCode))
        {
            if (ColorUtility.TryParseHtmlString(rarity.colorCode, out Color c))
            {
                bg.color = c;
                return;
            }
        }
        bg.color = Color.white;
    }

    private void ShowTip(string msg)
    {
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, msg);
    }
}
