// ============================================================
// 文件: UI_EggBtn.cs
// 说明: 单个蛋按钮（对应一个蛋种类）
//       三个状态：未孵化 / 孵化中 / 孵化完毕
// 路径: Assets/Scripts/UI/
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_EggPrefab : MonoBehaviour
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    [Header("基础")]
    public Button button;
    public Image bg;
    public Image icon;
    public Text nameText;
    public Text countText;

    [Header("状态对象")]
    public GameObject unProduceObj;   // 未孵化
    public GameObject producingObj;   // 孵化中
    public GameObject endProduceObj;  // 孵化完毕

    [Header("孵化中")]
    public Slider progressSlider;
    public Text progressText;

    // ============================================================
    // 运行时数据
    // ============================================================

    private int _eggId = 0;
    private int _slotIndex = -1;
    private Action<int, int> _onClicked;   // (eggId, slotIndex)

    public int EggId => _eggId;
    public int SlotIndex => _slotIndex;

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(int eggId, int slotIndex, Action<int, int> onClicked)
    {
        _eggId = eggId;
        _slotIndex = slotIndex;
        _onClicked = onClicked;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClick);
        }
    }

    // ============================================================
    // 状态：空
    // ============================================================

    public void SetEmpty()
    {
        LoadIcon(0);

        if (bg != null) bg.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        if (nameText != null) nameText.text = "";
        if (countText != null) countText.text = "";

        SetStateObjects(false, false, false);
    }

    // ============================================================
    // 状态：未孵化
    // ============================================================

    public void SetUnproduced(int eggId, int invCount)
    {
        _eggId = eggId;

        LoadIcon(eggId);
        ApplyRarityBg(eggId);

        var eggData = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetEggById(eggId) : null;
        if (nameText != null) nameText.text = eggData?.name ?? $"蛋{eggId}";
        if (countText != null) countText.text = invCount > 0 ? $"x{invCount}" : "";

        SetStateObjects(true, false, false);
    }

    // ============================================================
    // 状态：孵化中 / 孵化完毕
    // ============================================================

    public void SetSlot(EggHatchSlotData slot, int invCount)
    {
        if (slot == null) return;

        _eggId = slot.eggId;

        LoadIcon(slot.eggId);
        ApplyRarityBg(slot.eggId);

        var eggData = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetEggById(slot.eggId) : null;
        if (nameText != null) nameText.text = eggData?.name ?? $"蛋{slot.eggId}";

        // 数量：孵化中不显示数量（数量已在孵化前扣了）
        if (countText != null) countText.text = "";

        if (slot.isCompleted)
        {
            SetStateObjects(false, false, true);
            if (progressText != null) progressText.text = "可领取";
            if (progressSlider != null) progressSlider.value = 1f;
        }
        else
        {
            SetStateObjects(false, true, false);
            UpdateRemainingSeconds(slot.remainingSeconds, false);
        }
    }

    // ============================================================
    // 倒计时刷新
    // ============================================================

    public void UpdateRemainingSeconds(int remaining, bool isCompleted)
    {
        if (isCompleted)
        {
            SetStateObjects(false, false, true);
            if (progressText != null) progressText.text = "可领取";
            if (progressSlider != null) progressSlider.value = 1f;
            return;
        }

        if (progressText != null)
        {
            int m = remaining / 60;
            int s = remaining % 60;
            progressText.text = m > 0 ? $"{m:D2}:{s:D2}" : $"{s}s";
        }

        // 进度条：需要总时长，从 EggData.hatchTime 读
        if (progressSlider != null)
        {
            var eggData = LoadDataManager.Instance != null ? LoadDataManager.Instance.GetEggById(_eggId) : null;
            int total = eggData?.hatchTime ?? 0;
            progressSlider.value = total > 0 ? Mathf.Clamp01(1f - (float)remaining / total) : 0f;
        }
    }

    // ============================================================
    // 状态对象控制
    // ============================================================

    private void SetStateObjects(bool un, bool producing, bool end)
    {
        if (unProduceObj != null) unProduceObj.SetActive(un);
        if (producingObj != null) producingObj.SetActive(producing);
        if (endProduceObj != null) endProduceObj.SetActive(end);
    }

    // ============================================================
    // Icon / 背景色
    // ============================================================

    private void LoadIcon(int eggId)
    {
        if (icon == null) return;

        // 参考鱼篓加载方式：Addressables 路径
        string path = eggId > 0
            ? $"UI/Icon/EggIcons/{eggId}"
            : "UI/Icon/EggIcons/0";

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

    // ============================================================
    // 点击
    // ============================================================

    private void OnButtonClick()
    {
        // 三个状态都隐藏 → 无反应
        bool allHidden =
            (unProduceObj == null || !unProduceObj.activeSelf) &&
            (producingObj == null || !producingObj.activeSelf) &&
            (endProduceObj == null || !endProduceObj.activeSelf);

        if (allHidden) return;

        _onClicked?.Invoke(_eggId, _slotIndex);
    }
}
