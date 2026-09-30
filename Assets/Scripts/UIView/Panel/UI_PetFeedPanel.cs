// ============================================================
// 文件: UI_PetFeedPanel.cs
// 说明: 喂食面板（每条鱼一个按钮）
// 路径: Assets/Scripts/UI/
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_PetFeedPanel : MonoBehaviour
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    public Transform container;
    public GameObject feedPrefab;

    public Button feedBtn;
    public Button selectConfigBtn;
    public Button closeBtn;

    public Text autoFeedTimerText;

    public PetAutoFeedFilterPanel filterPanel;

    // ============================================================
    // 运行时数据
    // ============================================================

    private int _petInstanceId = -1;
    private Action _onClosed;

    private List<UI_PetFeedPrefab> _feedPrefabs = new List<UI_PetFeedPrefab>();
    private List<FishDetailData> _fishList = new List<FishDetailData>();
    private int _selectedFishId = -1;   // FishDetailData.id

    private int _remainingSeconds = 0;
    private bool _autoFeedEnabled = false;
    private Coroutine _timerCoroutine;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (feedBtn != null) feedBtn.onClick.AddListener(OnFeedClick);
        if (selectConfigBtn != null) selectConfigBtn.onClick.AddListener(OnSelectConfigClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(Close);
    }

    // ============================================================
    // 打开 / 关闭
    // ============================================================

    public void Open(int petInstanceId, Action onClosed = null)
    {
        _petInstanceId = petInstanceId;
        _onClosed = onClosed;
        _selectedFishId = -1;

        gameObject.SetActive(true);

        // 拉自动喂食状态
        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.FetchAutoFeedStatus((success, data) =>
            {
                if (success && data != null)
                {
                    _remainingSeconds = data.remainingSeconds;
                    _autoFeedEnabled = data.enabled;
                    UpdateAutoFeedTimer();
                }
            });
        }

        RefreshFishList();
        StartTimerCoroutine();
    }

    public void Close()
    {
        StopTimerCoroutine();
        gameObject.SetActive(false);
        _onClosed?.Invoke();
    }

    // ============================================================
    // 鱼列表刷新
    // ============================================================

    private void RefreshFishList()
    {
        // 从 PlayerDataManager 拿所有鱼详情
        _fishList = GetAllFishDetailList();

        // 按 caughtTimestamp 升序
        _fishList.Sort((a, b) => a.caughtTimestamp.CompareTo(b.caughtTimestamp));

        // 池
        EnsureFeedPrefabs(_fishList.Count);

        // 填数据
        for (int i = 0; i < _feedPrefabs.Count; i++)
        {
            if (i < _fishList.Count)
            {
                _feedPrefabs[i].gameObject.SetActive(true);
                _feedPrefabs[i].Init(_fishList[i], OnFishSelected);
                _feedPrefabs[i].SetSelection(false);
            }
            else
            {
                _feedPrefabs[i].gameObject.SetActive(false);
            }
        }
    }

    private List<FishDetailData> GetAllFishDetailList()
    {
        var result = new List<FishDetailData>();
        if (PlayerDataManager.Instance == null) return result;

        var dict = PlayerDataManager.Instance.GetFishDetailData();
        if (dict == null) return result;

        foreach (var kv in dict)
        {
            if (kv.Value == null) continue;
            foreach (var fish in kv.Value)
            {
                // 只显示鱼篓里的鱼（location == 0）
                if (fish.location == 0 && !fish.isLocked)
                {
                    result.Add(fish);
                }
            }
        }

        return result;
    }

    private void EnsureFeedPrefabs(int count)
    {
        while (_feedPrefabs.Count < count)
        {
            if (feedPrefab == null || container == null)
            {
                Z_Logger.LogError("[UI_PetFeedPanel] feedPrefab 或 container 未绑定");
                return;
            }

            var go = Instantiate(feedPrefab, container);
            var prefab = go.GetComponent<UI_PetFeedPrefab>();
            if (prefab == null)
            {
                Z_Logger.LogError("[UI_PetFeedPanel] feedPrefab 上没有 UI_PetFeedPrefab 组件");
                Destroy(go);
                return;
            }
            _feedPrefabs.Add(prefab);
        }
    }

    // ============================================================
    // 单选
    // ============================================================

    private void OnFishSelected(UI_PetFeedPrefab prefab)
    {
        // 单选：其他全部取消
        foreach (var p in _feedPrefabs)
        {
            if (p != null && p != prefab)
            {
                p.SetSelection(false);
            }
        }

        _selectedFishId = prefab.IsSelected ? prefab.FishDetailId : -1;
    }

    // ============================================================
    // 喂食
    // ============================================================

    private void OnFeedClick()
    {
        if (_selectedFishId < 0)
        {
            ShowTip("请选择要喂的鱼");
            return;
        }

        NetServerManager.Instance.FeedPet(_petInstanceId, _selectedFishId, (success, message, restored, hunger) =>
        {
            ShowTip(message);
            if (success)
            {
                // 刷新鱼篓
                NetServerManager.Instance.FetchPlayerFishBag();
                RefreshFishList();
                _selectedFishId = -1;
            }
        });
    }

    // ============================================================
    // 自动喂食倒计时
    // ============================================================

    private void StartTimerCoroutine()
    {
        StopTimerCoroutine();
        _timerCoroutine = StartCoroutine(TimerTick());
    }

    private void StopTimerCoroutine()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }
    }

    private System.Collections.IEnumerator TimerTick()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (_remainingSeconds > 0) _remainingSeconds--;
            UpdateAutoFeedTimer();
        }
    }

    private void UpdateAutoFeedTimer()
    {
        if (autoFeedTimerText == null) return;

        if (!_autoFeedEnabled)
        {
            autoFeedTimerText.text = "自动喂食关闭";
            return;
        }

        autoFeedTimerText.text = FormatTime(_remainingSeconds);
    }

    private string FormatTime(int seconds)
    {
        int h = seconds / 3600;
        int m = (seconds % 3600) / 60;
        int s = seconds % 60;
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    // ============================================================
    // 过滤配置
    // ============================================================

    private void OnSelectConfigClick()
    {
        if (filterPanel == null)
        {
            Z_Logger.LogWarning("[UI_PetFeedPanel] filterPanel 未绑定");
            return;
        }

        filterPanel.OpenPanel();
    }

    // ============================================================
    // 工具
    // ============================================================

    private void ShowTip(string msg)
    {
        CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, msg);
    }
}