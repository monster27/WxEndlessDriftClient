// ============================================================
// 文件: BaseFilterPanel.cs
// 说明: 通用过滤面板基类
//       - 只负责稀有度/星级/闪光/SkipSelected 的 Toggle 读取与回填
//       - 不绑定 Mask/Close，纯逻辑组件
//       - 子类只需继承并实现 SaveToServer / LoadFromServer
// 路径: Assets/Scripts/UI/
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class BaseFilterPanel : MonoBehaviour
{
    // ============================================================
    // Inspector 引用（子类复用，命名保持和 FishBagSelectPanel 一致）
    // ============================================================

    [Header("稀有度 Toggle")]
    public Toggle rarity201Tog;
    public Toggle rarity202Tog;
    public Toggle rarity203Tog;
    public Toggle rarity204Tog;
    public Toggle rarity205Tog;
    public Toggle rarity206Tog;

    [Header("星级 Toggle")]
    public Toggle starRate501Tog;
    public Toggle starRate502Tog;
    public Toggle starRate503Tog;
    public Toggle starRate504Tog;

    [Header("闪光 Toggle")]
    public Toggle notShineTog;
    public Toggle isShineTog;

    [Header("跳过已选中 Toggle")]
    public Toggle skipSelectedTog;

    // ============================================================
    // 生命周期
    // ============================================================

    protected virtual void Awake()
    {
        BindToggleEvents();
    }

    /// <summary>绑定所有 Toggle 的 onValueChanged</summary>
    private void BindToggleEvents()
    {
        Toggle[] toggles = {
            rarity201Tog, rarity202Tog, rarity203Tog, rarity204Tog, rarity205Tog, rarity206Tog,
            starRate501Tog, starRate502Tog, starRate503Tog, starRate504Tog,
            notShineTog, isShineTog,
            skipSelectedTog
        };

        foreach (var t in toggles)
        {
            if (t != null)
            {
                t.onValueChanged.RemoveAllListeners();
                t.onValueChanged.AddListener(_ => OnAnyToggleChanged());
            }
        }
    }

    /// <summary>任何一个 Toggle 变化时调用（子类可重写）</summary>
    protected virtual void OnAnyToggleChanged()
    {
        SaveSettings();
        SyncSettingsToServer();
    }

    // ============================================================
    // Filter 结构（通用）
    // ============================================================

    /// <summary>过滤配置数据结构</summary>
    public class FilterData
    {
        public bool rarity201;
        public bool rarity202;
        public bool rarity203;
        public bool rarity204;
        public bool rarity205;
        public bool rarity206;

        public bool starRate501;
        public bool starRate502;
        public bool starRate503;
        public bool starRate504;

        public bool notShine;
        public bool isShine;
        public bool skipSelected;
    }

    // ============================================================
    // 读取 / 回填
    // ============================================================

    /// <summary>从 Toggle 读出当前 Filter</summary>
    public FilterData GetCurrentFilter()
    {
        return new FilterData
        {
            rarity201 = rarity201Tog != null && rarity201Tog.isOn,
            rarity202 = rarity202Tog != null && rarity202Tog.isOn,
            rarity203 = rarity203Tog != null && rarity203Tog.isOn,
            rarity204 = rarity204Tog != null && rarity204Tog.isOn,
            rarity205 = rarity205Tog != null && rarity205Tog.isOn,
            rarity206 = rarity206Tog != null && rarity206Tog.isOn,

            starRate501 = starRate501Tog != null && starRate501Tog.isOn,
            starRate502 = starRate502Tog != null && starRate502Tog.isOn,
            starRate503 = starRate503Tog != null && starRate503Tog.isOn,
            starRate504 = starRate504Tog != null && starRate504Tog.isOn,

            notShine = notShineTog != null && notShineTog.isOn,
            isShine = isShineTog != null && isShineTog.isOn,
            skipSelected = skipSelectedTog != null && skipSelectedTog.isOn
        };
    }

    /// <summary>把 Filter 回填到 Toggle</summary>
    public void ApplyFilter(FilterData filter)
    {
        if (filter == null) return;

        SetToggle(rarity201Tog, filter.rarity201);
        SetToggle(rarity202Tog, filter.rarity202);
        SetToggle(rarity203Tog, filter.rarity203);
        SetToggle(rarity204Tog, filter.rarity204);
        SetToggle(rarity205Tog, filter.rarity205);
        SetToggle(rarity206Tog, filter.rarity206);

        SetToggle(starRate501Tog, filter.starRate501);
        SetToggle(starRate502Tog, filter.starRate502);
        SetToggle(starRate503Tog, filter.starRate503);
        SetToggle(starRate504Tog, filter.starRate504);

        SetToggle(notShineTog, filter.notShine);
        SetToggle(isShineTog, filter.isShine);
        SetToggle(skipSelectedTog, filter.skipSelected);
    }

    private void SetToggle(Toggle t, bool value)
    {
        if (t == null) return;
        t.onValueChanged.RemoveAllListeners();
        t.isOn = value;
        t.onValueChanged.AddListener(_ => OnAnyToggleChanged());
    }

    // ============================================================
    // 本地存储
    // ============================================================

    /// <summary>本地 PlayerPrefs Key 前缀（子类可重写区分鱼篓/宠物）</summary>
    protected virtual string PrefsKeyPrefix => "Filter_";

    public virtual void SaveSettings()
    {
        var f = GetCurrentFilter();

        PlayerPrefs.SetInt(PrefsKeyPrefix + "201", f.rarity201 ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "202", f.rarity202 ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "203", f.rarity203 ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "204", f.rarity204 ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "205", f.rarity205 ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "206", f.rarity206 ? 1 : 0);

        PlayerPrefs.SetInt(PrefsKeyPrefix + "501", f.starRate501 ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "502", f.starRate502 ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "503", f.starRate503 ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "504", f.starRate504 ? 1 : 0);

        PlayerPrefs.SetInt(PrefsKeyPrefix + "NotShine", f.notShine ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "IsShine", f.isShine ? 1 : 0);
        PlayerPrefs.SetInt(PrefsKeyPrefix + "SkipSelected", f.skipSelected ? 1 : 0);

        PlayerPrefs.Save();
    }

    public virtual void LoadSettings()
    {
        var f = new FilterData
        {
            rarity201 = PlayerPrefs.GetInt(PrefsKeyPrefix + "201", 0) == 1,
            rarity202 = PlayerPrefs.GetInt(PrefsKeyPrefix + "202", 0) == 1,
            rarity203 = PlayerPrefs.GetInt(PrefsKeyPrefix + "203", 0) == 1,
            rarity204 = PlayerPrefs.GetInt(PrefsKeyPrefix + "204", 0) == 1,
            rarity205 = PlayerPrefs.GetInt(PrefsKeyPrefix + "205", 0) == 1,
            rarity206 = PlayerPrefs.GetInt(PrefsKeyPrefix + "206", 0) == 1,

            starRate501 = PlayerPrefs.GetInt(PrefsKeyPrefix + "501", 0) == 1,
            starRate502 = PlayerPrefs.GetInt(PrefsKeyPrefix + "502", 0) == 1,
            starRate503 = PlayerPrefs.GetInt(PrefsKeyPrefix + "503", 0) == 1,
            starRate504 = PlayerPrefs.GetInt(PrefsKeyPrefix + "504", 0) == 1,

            notShine = PlayerPrefs.GetInt(PrefsKeyPrefix + "NotShine", 0) == 1,
            isShine = PlayerPrefs.GetInt(PrefsKeyPrefix + "IsShine", 0) == 1,
            skipSelected = PlayerPrefs.GetInt(PrefsKeyPrefix + "SkipSelected", 0) == 1
        };

        ApplyFilter(f);
    }

    // ============================================================
    // 服务器同步（子类实现）
    // ============================================================

    /// <summary>把当前 Filter 同步到服务器（子类实现）</summary>
    protected abstract void SyncSettingsToServer();

    /// <summary>从服务器拉取 Filter 并回填（子类实现）</summary>
    public abstract void FetchSettingsFromServer();

    /// <summary>面板打开时调用（默认：读本地 + 拉服务器）</summary>
    public virtual void OpenPanel()
    {
        gameObject.SetActive(true);
        LoadSettings();
        FetchSettingsFromServer();
    }

    /// <summary>面板关闭时调用（默认：存本地）</summary>
    public virtual void ClosePanel()
    {
        SaveSettings();
        gameObject.SetActive(false);
    }
}
