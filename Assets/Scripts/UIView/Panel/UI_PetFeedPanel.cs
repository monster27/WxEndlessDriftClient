// ============================================================
// 文件: UI_PetFeedPanel.cs
// 说明: 喂食面板（每条鱼一个按钮）
// 路径: Assets/Scripts/UI/
// ============================================================

using System;
using System.Collections;
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
    private Action _onFed;   // ✅ 新增：喂食成功回调

    private List<UI_PetFeedPrefab> _feedPrefabs = new List<UI_PetFeedPrefab>();
    private List<FishDetailData> _fishList = new List<FishDetailData>();
    private int _selectedFishId = -1;   // FishDetailData.id

    private int _remainingSeconds = 0;
    private bool _autoFeedEnabled = false;
    private Coroutine _timerCoroutine;

    private bool _isOpen = false;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (feedBtn != null) feedBtn.onClick.AddListener(OnFeedClick);
        if (selectConfigBtn != null) selectConfigBtn.onClick.AddListener(OnSelectConfigClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(Close);

        Z_Logger.Log("[UI_PetFeedPanel] Awake 完成");
    }

    private void OnEnable()
    {
        CommunicateEvent.Register("FishBagDataUpdated", OnFishBagUpdated);
        Z_Logger.Log("[UI_PetFeedPanel] OnEnable，注册 FishBagDataUpdated");
    }

    private void OnDisable()
    {
        CommunicateEvent.Unregister("FishBagDataUpdated", OnFishBagUpdated);
        Z_Logger.Log("[UI_PetFeedPanel] OnDisable，注销 FishBagDataUpdated");
    }

    // ============================================================
    // 事件回调
    // ============================================================

    private void OnFishBagUpdated()
    {
        Z_Logger.Log($"[UI_PetFeedPanel] OnFishBagUpdated: _isOpen={_isOpen}, activeSelf={gameObject.activeSelf}");
        if (_isOpen && gameObject.activeSelf)
        {
            RefreshFishList();
        }
    }

    // ============================================================
    // 打开 / 关闭
    // ============================================================

    /// <summary>
    /// 打开喂食面板
    /// </summary>
    /// <param name="petInstanceId">要喂的宠物实例ID</param>
    /// <param name="onClosed">面板关闭时的回调</param>
    /// <param name="onFed">喂食成功时的回调（✅ 新增）</param>
    public void Open(int petInstanceId, Action onClosed = null, Action onFed = null)
    {
        _petInstanceId = petInstanceId;
        _onClosed = onClosed;
        _onFed = onFed;
        _selectedFishId = -1;
        _isOpen = true;

        Z_Logger.Log($"[UI_PetFeedPanel] Open: petInstanceId={petInstanceId}");

        gameObject.SetActive(true);

        if (NetServerManager.Instance != null)
        {
            // 拉自动喂食状态
            NetServerManager.Instance.FetchAutoFeedStatus((success, data) =>
            {
                Z_Logger.Log($"[UI_PetFeedPanel] FetchAutoFeedStatus 回调: success={success}, " +
                             $"level={data?.level}, enabled={data?.enabled}, remaining={data?.remainingSeconds}");
                if (success && data != null)
                {
                    _remainingSeconds = data.remainingSeconds;
                    _autoFeedEnabled = data.enabled;
                    UpdateAutoFeedTimer();
                }
            });

            // ✅ 主动拉一次最新鱼篓数据，确保打开时不是旧数据
            NetServerManager.Instance.FetchPlayerFishBag();
        }
        else
        {
            Z_Logger.LogWarning("[UI_PetFeedPanel] Open: NetServerManager.Instance 为 null");
        }

        RefreshFishList();
        StartTimerCoroutine();
    }

    public void Close()
    {
        Z_Logger.Log($"[UI_PetFeedPanel] Close: petInstanceId={_petInstanceId}");
        _isOpen = false;
        StopTimerCoroutine();
        gameObject.SetActive(false);
        _onClosed?.Invoke();
    }

    // ============================================================
    // 鱼列表刷新
    // ============================================================

    private void RefreshFishList()
    {
        _fishList = GetAllFishDetailList();

        Z_Logger.Log($"[UI_PetFeedPanel] RefreshFishList: 共 {_fishList.Count} 条鱼");

        // 按 caughtTimestamp 升序
        _fishList.Sort((a, b) => a.caughtTimestamp.CompareTo(b.caughtTimestamp));

        EnsureFeedPrefabs(_fishList.Count);

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

        _selectedFishId = -1;
    }

    private List<FishDetailData> GetAllFishDetailList()
    {
        var result = new List<FishDetailData>();
        if (PlayerDataManager.Instance == null)
        {
            Z_Logger.LogWarning("[UI_PetFeedPanel] GetAllFishDetailList: PlayerDataManager.Instance 为 null");
            return result;
        }

        var dict = PlayerDataManager.Instance.GetFishDetailData();
        if (dict == null)
        {
            Z_Logger.LogWarning("[UI_PetFeedPanel] GetAllFishDetailList: GetFishDetailData() 返回 null");
            return result;
        }

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
        foreach (var p in _feedPrefabs)
        {
            if (p != null && p != prefab)
            {
                p.SetSelection(false);
            }
        }

        _selectedFishId = prefab.IsSelected ? prefab.FishDetailId : -1;

        Z_Logger.Log($"[UI_PetFeedPanel] OnFishSelected: fishDetailId={prefab.FishDetailId}, isSelected={prefab.IsSelected}, _selectedFishId={_selectedFishId}");
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

        Z_Logger.Log($"[UI_PetFeedPanel] OnFeedClick: petInstanceId={_petInstanceId}, fishInstanceId={_selectedFishId}");

        NetServerManager.Instance.FeedPet(_petInstanceId, _selectedFishId,
            (success, message, restored, hunger) =>
            {
                Z_Logger.Log($"[UI_PetFeedPanel] FeedPet 回调: success={success}, message={message}, restored={restored}, currentHunger={hunger}");

                ShowTip(message);
                if (success)
                {
                    // 刷新鱼列表（鱼被消耗了）
                    RefreshFishList();
                    _selectedFishId = -1;

                    // ✅ 通知外部（UI_PetInfoPanel）刷新
                    Z_Logger.Log("[UI_PetFeedPanel] 触发 _onFed 回调");
                    _onFed?.Invoke();
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
        Z_Logger.Log("[UI_PetFeedPanel] 倒计时协程启动");
    }

    private void StopTimerCoroutine()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
            Z_Logger.Log("[UI_PetFeedPanel] 倒计时协程停止");
        }
    }

    private IEnumerator TimerTick()
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

        Z_Logger.Log("[UI_PetFeedPanel] OnSelectConfigClick: 打开过滤面板");
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
