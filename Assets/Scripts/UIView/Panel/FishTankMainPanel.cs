// 路径：Assets/Scripts/UIView/Panel/FishTankMainPanel.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static PlayerDataManager;

public class FishTankMainPanel : MonoBehaviour
{
    [Header("===== 鱼缸区域(UI RectTransform) =====")]
    [SerializeField] private RectTransform totalAreaRect;
    [SerializeField] private RectTransform bottomAreaRect;

    [Header("===== 容器 =====")]
    [SerializeField] private RectTransform fishContainer;
    [SerializeField] private RectTransform baitContainer;
    [SerializeField] private RectTransform decorationContainer;

    [Header("===== 鱼预制体 =====")]
    [SerializeField] private GameObject fishPrefab;
    [SerializeField] private GameObject baitPrefab;

    [Header("===== 鱼行为参数（像素单位） =====")]
    [SerializeField] private float baseHeight = 30f;
    [SerializeField] private float uniformScale = 1f;
    [SerializeField] private float directionChangeIntervalMin = 2f;
    [SerializeField] private float directionChangeIntervalMax = 8f;

    [Header("===== 物理参数（像素/秒） =====")]
    [SerializeField] private float moveSpeedMin = 30f;
    [SerializeField] private float moveSpeedMax = 80f;
    [SerializeField] private float verticalSpeedRatio = 0.4f;
    [Range(0f, 1f)]
    [SerializeField] private float verticalMoveProbability = 0.3f;
    [SerializeField] private float accelerationMin = 2.5f;
    [SerializeField] private float accelerationMax = 5.0f;
    [SerializeField] private float dragForce = 0.8f;
    [SerializeField] private float chargeDurationMin = 0.4f;
    [SerializeField] private float chargeDurationMax = 1f;
    [SerializeField] private float chargeScaleX = 0.6f;
    [SerializeField] private float chargeScaleY = 1.35f;
    [SerializeField] private float chargeSpeedRatio = 0.15f;
    [SerializeField] private float sprintDurationMin = 1.5f;
    [SerializeField] private float sprintDurationMax = 3.5f;

    [Header("===== 鱼饵系统 =====")]
    [SerializeField] private float baitTriggerRadius = 500f;      // 鱼发现鱼饵的距离（像素）
    [SerializeField] private float baitEatRadius = 40f;           // 鱼真正吃掉鱼饵的距离（像素）
    [SerializeField, Range(0.05f, 2f)]
    private float baitFallSpeedRatio = 0.25f;                     // 每秒下落占区域高度比例
    [SerializeField] private float baitChaseDurationMin = 0.5f;
    [SerializeField] private float baitChaseDurationMax = 0.8f;
    [SerializeField] private float baitChaseSpeedMultiplier = 5f;
    [SerializeField] private float baitScale = 1f;
    [SerializeField] private int baitPoolInitSize = 5;

    [Header("===== 对象池 =====")]
    [SerializeField] private int fishPoolInitialCapacity = 10;

    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    [Header("===== 顶部UI =====")]
    [SerializeField] private Button leftBtn;
    [SerializeField] private Button rightBtn;
    [SerializeField] private Text tankNameText;
    [SerializeField] private Text capacityText;
    [SerializeField] private Text harvestText;
    [SerializeField] private Button lockBtn;
    [SerializeField] private GameObject lockIcon;

    [Header("===== 底部功能按钮 =====")]
    [SerializeField] private Button manageBtn;
    [SerializeField] private Button decorationBtn;

    [Header("===== 子面板引用 =====")]
    [SerializeField] private FishTankManagerPanel managerPanel;
    [SerializeField] private FishTankDecorationPanel decorationPanel;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private int _currentTankIndex = 0;
    private bool _isDecorationMode = false;
    private bool _isManagerOpen = false;
    private bool _isInitialized = false;

    private List<UI_FishTankFish> _fullScreenSwimList = new List<UI_FishTankFish>();
    private List<UI_FishTankFish> _fullScreenStaticList = new List<UI_FishTankFish>();
    private List<UI_FishTankFish> _bottomSwimList = new List<UI_FishTankFish>();
    private List<UI_FishTankFish> _bottomStaticList = new List<UI_FishTankFish>();

    private List<FishDetailData> _currentDisplayingFish = new List<FishDetailData>();
    private List<FishDetailData> _pendingFishData = null;
    private Coroutine _createCoroutine = null;

    private FishObjectPool _fishPool;
    private BaitObjectPool _baitPool;

    private Dictionary<string, UI_FishTankDecPrefab> _decorationItems = new Dictionary<string, UI_FishTankDecPrefab>();
    private string _selectedDecInstanceId = null;

    private Coroutine _updateCoroutine;

    private Rect _totalRect;
    private Rect _bottomRect;

    public bool EnableDebugLog => enableDebugLog;
    public Rect TotalRect => _totalRect;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void Awake()
    {
        // fishContainer
        if (fishContainer == null)
        {
            GameObject go = new GameObject("FishContainer");
            go.transform.SetParent(transform);
            fishContainer = go.AddComponent<RectTransform>();
            fishContainer.anchorMin = Vector2.zero;
            fishContainer.anchorMax = Vector2.one;
            fishContainer.sizeDelta = Vector2.zero;
            fishContainer.anchoredPosition = Vector2.zero;
        }

        // baitContainer
        if (baitContainer == null)
        {
            GameObject go = new GameObject("BaitContainer");
            go.transform.SetParent(transform);
            baitContainer = go.AddComponent<RectTransform>();
            baitContainer.anchorMin = Vector2.zero;
            baitContainer.anchorMax = Vector2.one;
            baitContainer.sizeDelta = Vector2.zero;
            baitContainer.anchoredPosition = Vector2.zero;
        }

        // decorationContainer
        if (decorationContainer == null)
        {
            GameObject go = new GameObject("DecorationContainer");
            go.transform.SetParent(transform);
            decorationContainer = go.AddComponent<RectTransform>();
            decorationContainer.anchorMin = Vector2.zero;
            decorationContainer.anchorMax = Vector2.one;
            decorationContainer.sizeDelta = Vector2.zero;
            decorationContainer.gameObject.SetActive(false);
        }

        _fishPool = new FishObjectPool(fishPrefab, fishContainer, fishPoolInitialCapacity, this);
        _baitPool = new BaitObjectPool(baitPrefab, baitContainer, baitPoolInitSize, this, _totalRect, baitFallSpeedRatio, baitScale);

        BindUIEvents();
        SetupClickHandler();

        if (managerPanel != null)
        {
            managerPanel.gameObject.SetActive(false);
            managerPanel.SetTransferCallback(OnFishTransferRequest);
            managerPanel.SetUnlockCallback(OnUnlockRequest);
        }
        if (decorationPanel != null)
            decorationPanel.gameObject.SetActive(false);

        _isInitialized = true;
        LogDebug("Awake 完成");
    }

    private void OnEnable()
    {
        RegisterEvents();
        if (_isInitialized) UpdateRects();
    }

    private void OnDisable() => UnregisterEvents();

    private void OnDestroy()
    {
        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        _fishPool?.Clear();
        _baitPool?.Clear();
        ClearAllFish();
        ClearAllDecorations();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void RegisterEvents()
    {
        UnregisterEvents();
        CommunicateEvent.Register(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
        CommunicateEvent.Register(FishTankMessage.DecorationDataUpdated.ToString(), OnDecorationDataUpdated);
        CommunicateEvent.Register(FishTankMessage.ShowDecOperator.ToString(), OnShowDecOperator);
        CommunicateEvent.Register(FishTankMessage.HideDecOperator.ToString(), OnHideDecOperator);
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
        CommunicateEvent.Unregister(FishTankMessage.DecorationDataUpdated.ToString(), OnDecorationDataUpdated);
        CommunicateEvent.Unregister(FishTankMessage.ShowDecOperator.ToString(), OnShowDecOperator);
        CommunicateEvent.Unregister(FishTankMessage.HideDecOperator.ToString(), OnHideDecOperator);
    }

    private void BindUIEvents()
    {
        if (leftBtn != null) leftBtn.onClick.AddListener(OnLeftClick);
        if (rightBtn != null) rightBtn.onClick.AddListener(OnRightClick);
        if (manageBtn != null) manageBtn.onClick.AddListener(ToggleManagerPanel);
        if (decorationBtn != null) decorationBtn.onClick.AddListener(ToggleDecorationMode);
        if (lockBtn != null) lockBtn.onClick.AddListener(OnLockClick);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void SetupClickHandler()
    {
        if (totalAreaRect == null)
        {
            Debug.LogWarning("[FishTankMainPanel] totalAreaRect 未绑定，无法添加点击监听");
            return;
        }

        var img = totalAreaRect.GetComponent<Image>();
        if (img == null) img = totalAreaRect.gameObject.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = true;

        var trigger = totalAreaRect.GetComponent<EventTrigger>();
        if (trigger == null) trigger = totalAreaRect.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        var entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((data) =>
        {
            PointerEventData ped = data as PointerEventData;
            if (ped == null) return;

            Vector2 localPoint;
            Camera cam = ped.pressEventCamera;
            if (cam == null) cam = Camera.main;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                totalAreaRect, ped.position, cam, out localPoint))
            {
                Vector3 worldPos = totalAreaRect.TransformPoint(localPoint);
                SpawnBaitAtPosition(worldPos);
            }
        });
        trigger.triggers.Add(entry);

        LogDebug("已为 totalAreaRect 添加点击监听");
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    /// <summary>
    /// 在点击位置生成鱼饵（鱼饵挂 baitContainer 下）
    /// </summary>
    public void SpawnBaitAtPosition(Vector3 worldPosition)
    {
        if (baitPrefab == null)
        {
            Debug.LogError("[FishTankMainPanel] baitPrefab 未绑定！");
            return;
        }

        Vector3 localPos = baitContainer.InverseTransformPoint(worldPosition);
        localPos.z = 0;

        float margin = 10f;
        localPos.x = Mathf.Clamp(localPos.x, _totalRect.xMin + margin, _totalRect.xMax - margin);
        localPos.y = Mathf.Clamp(localPos.y, _totalRect.yMin + margin, _totalRect.yMax - margin);

        GameObject bait = _baitPool.Get(localPos);
        if (bait == null) return;

        Debug.Log($"[FishTankMainPanel] 生成鱼饵, worldPos={worldPosition}, localPos={localPos}");
        CheckNearbyFish(bait);
    }

    public void ReturnBait(GameObject bait)
    {
        if (bait == null) return;
        _baitPool.RemoveBait(bait);
    }

    /// <summary>
    /// 清空当前所有鱼饵（切换鱼缸时调用）
    /// </summary>
    private void ClearAllBaits()
    {
        _baitPool?.ClearAll();
        LogDebug("清空所有鱼饵");
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void OpenPanel()
    {
        gameObject.SetActive(true);
        if (!_isInitialized) return;
        UpdateRects();
        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateLoop());
            LogDebug("更新循环已启动");
        }
        RefreshAll();
        CommunicateEvent.Modify(FishTankMessage.OpenFishTank.ToString());
    }

    public void ClosePanel()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
        CloseFishTank();

        // 关闭时清空所有鱼饵
        ClearAllBaits();

        if (managerPanel != null) managerPanel.ClosePanel();
        if (decorationPanel != null) decorationPanel.gameObject.SetActive(false);
        SetFishVisible(true);
        _isManagerOpen = false;
        _isDecorationMode = false;
        decorationContainer.gameObject.SetActive(false);
        ClearAllDecorations();
        gameObject.SetActive(false);
        CommunicateEvent.Modify(FishTankMessage.CloseFishTank.ToString());
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void ToggleDecorationMode()
    {
        _isDecorationMode = !_isDecorationMode;
        if (_isDecorationMode)
        {
            SetFishVisible(false);
            decorationContainer.gameObject.SetActive(true);
            if (managerPanel != null && managerPanel.gameObject.activeSelf) managerPanel.ClosePanel();
            LoadEquippedDecorations();
            if (decorationPanel != null && decorationPanel.gameObject.activeSelf) decorationPanel.RefreshData();
        }
        else
        {
            decorationContainer.gameObject.SetActive(false);
            SetFishVisible(true);
            ClearAllDecorations();
            CommunicateEvent.Modify(FishTankMessage.HideDecOperator.ToString());
        }
        CommunicateEvent.Modify(_isDecorationMode ? FishTankMessage.EnterDecorationMode.ToString() : FishTankMessage.ExitDecorationMode.ToString());
    }

    private void LoadEquippedDecorations()
    {
        ClearAllDecorations();
        var equipped = PlayerDataService.Instance?.GetEquippedDecorations(_currentTankIndex + 1);
        if (equipped == null) return;
        if (equipped.TryGetValue(80, out var list80))
        {
            foreach (var info in list80)
                CreateDecorationUI(info, 80);
        }
        if (equipped.TryGetValue(81, out var list81))
        {
            foreach (var info in list81)
                CreateDecorationUI(info, 81);
        }
        LogDebug($"加载了 {_decorationItems.Count} 个装饰");
    }

    private void CreateDecorationUI(DecorationEquipInfo info, int category)
    {
        GameObject decoGo = new GameObject($"Decoration_{info.Id}");
        decoGo.transform.SetParent(decorationContainer, false);
        var rect = decoGo.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(info.PositionX, info.PositionY);
        rect.sizeDelta = new Vector2(100, 100);

        var image = decoGo.AddComponent<Image>();
        var itemData = LoadDataManager.Instance?.GetItemById(info.DecorationId);
        if (itemData != null && !string.IsNullOrEmpty(itemData.iconPath))
        {
            AssetManager.LoadFromAddressables<Sprite>(itemData.iconPath, (sprite, handle) =>
            {
                if (sprite != null) image.sprite = sprite;
            });
        }

        var clickHandler = decoGo.AddComponent<DecoClickHandler>();
        clickHandler.Init(info.Id.ToString(), info.DecorationId, category, _currentTankIndex + 1);
        _decorationItems[info.Id.ToString()] = null;
    }

    private void ClearAllDecorations()
    {
        foreach (Transform child in decorationContainer)
            Destroy(child.gameObject);
        _decorationItems.Clear();
        _selectedDecInstanceId = null;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void RefreshAll()
    {
        if (!gameObject.activeInHierarchy) return;
        RefreshTopUI();
        RefreshFishTank();
        if (managerPanel != null && managerPanel.gameObject.activeSelf) managerPanel.RefreshData();
        if (decorationPanel != null && decorationPanel.gameObject.activeSelf) decorationPanel.RefreshData();
        if (_isDecorationMode) LoadEquippedDecorations();
    }

    private void RefreshTopUI()
    {
        var tanks = GetTankList();
        if (tanks == null || tanks.Count == 0)
        {
            SetTopUIEmpty();
            return;
        }

        if (_currentTankIndex >= tanks.Count) _currentTankIndex = tanks.Count - 1;
        if (_currentTankIndex < 0) _currentTankIndex = 0;

        var tank = tanks[_currentTankIndex];
        if (tank == null) { SetTopUIEmpty(); return; }

        var fishList = GetCurrentTankFish();
        var config = GetTankConfig(tank.tankId);

        if (tankNameText != null) tankNameText.text = config?.name ?? $"鱼缸{tank.tankId}";
        if (capacityText != null) capacityText.text = $"{fishList.Count}/{tank.capacity}";

        if (harvestText != null)
        {
            if (config?.type == "special" && tank.isUnlocked && fishList.Count > 0)
            {
                harvestText.text = $"每小时: {fishList.Count * 10} 金币";
                harvestText.gameObject.SetActive(true);
            }
            else harvestText.gameObject.SetActive(false);
        }

        if (lockIcon != null) lockIcon.SetActive(!tank.isUnlocked);
        if (lockBtn != null) lockBtn.gameObject.SetActive(!tank.isUnlocked);

        if (leftBtn != null) leftBtn.interactable = tanks.Count > 1;
        if (rightBtn != null) rightBtn.interactable = tanks.Count > 1;
    }

    private void SetTopUIEmpty()
    {
        if (tankNameText != null) tankNameText.text = "暂无鱼缸";
        if (capacityText != null) capacityText.text = "0/0";
        if (harvestText != null) harvestText.gameObject.SetActive(false);
        if (lockIcon != null) lockIcon.SetActive(false);
        if (lockBtn != null) lockBtn.gameObject.SetActive(false);
        if (leftBtn != null) leftBtn.interactable = false;
        if (rightBtn != null) rightBtn.interactable = false;
    }

    private void RefreshFishTank()
    {
        var tank = GetCurrentTank();
        if (tank == null || !tank.isUnlocked)
        {
            SetFishData(null);
            CloseFishTank();
            return;
        }

        var fishList = GetCurrentTankFish();
        SetFishData(fishList);
        OpenFishTank();
    }

    private List<FishTankStatusData> GetTankList()
    {
        return PlayerDataService.Instance?.GetTankList() ?? new List<FishTankStatusData>();
    }

    private FishTankStatusData GetCurrentTank()
    {
        var tanks = GetTankList();
        if (tanks.Count == 0) return null;
        if (_currentTankIndex >= tanks.Count) _currentTankIndex = tanks.Count - 1;
        return tanks[_currentTankIndex];
    }

    private List<FishDetailData> GetCurrentTankFish()
    {
        var tank = GetCurrentTank();
        if (tank == null) return new List<FishDetailData>();
        return PlayerDataService.Instance?.GetTankFishList(tank.tankId) ?? new List<FishDetailData>();
    }

    private FishTankConfig GetTankConfig(int tankId)
    {
        return LoadDataManager.Instance?.GetFishTankConfig(tankId);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void SetFishData(List<FishDetailData> fishList)
    {
        if (fishList == null || fishList.Count == 0)
        {
            ClearFish();
            return;
        }
        _pendingFishData = new List<FishDetailData>(fishList);
        LogDebug($"待处理鱼数据: {_pendingFishData.Count}");
    }

    public void ClearFish()
    {
        ClearAllFish();
        _currentDisplayingFish.Clear();
        _pendingFishData = null;
        if (_createCoroutine != null)
        {
            StopCoroutine(_createCoroutine);
            _createCoroutine = null;
        }
    }

    public void OpenFishTank()
    {
        if (_updateCoroutine == null)
            _updateCoroutine = StartCoroutine(UpdateLoop());
    }

    public void CloseFishTank() { }

    public void SetFishVisible(bool visible)
    {
        if (fishContainer != null)
            fishContainer.gameObject.SetActive(visible);
    }

    private void Update()
    {
        if (_pendingFishData != null && _createCoroutine == null)
        {
            List<FishDetailData> fishList = _pendingFishData;
            _pendingFishData = null;
            if (IsSameFishList(fishList, _currentDisplayingFish))
            {
                LogDebug("数据相同，跳过重建");
                return;
            }
            LogDebug($"启动重建协程，数量: {fishList.Count}");
            _createCoroutine = StartCoroutine(RebuildFish(fishList));
        }
    }

    private IEnumerator RebuildFish(List<FishDetailData> fishList)
    {
        _currentDisplayingFish = new List<FishDetailData>(fishList);
        ClearAllFish();
        yield return StartCoroutine(CreateFishCoroutine(fishList));
        _createCoroutine = null;
        LogDebug($"重建完成，总鱼数: {GetTotalFishCount()}");
    }

    private IEnumerator CreateFishCoroutine(List<FishDetailData> fishList)
    {
        foreach (var fishDetail in fishList)
        {
            if (fishDetail == null) continue;
            var fishData = LoadDataManager.Instance?.GetFishById(fishDetail.fishId);
            if (fishData == null) continue;

            string iconPath = LoadDataManager.Instance?.GetItemById(fishDetail.fishId)?.iconPath;
            if (string.IsNullOrEmpty(iconPath)) continue;
            string loadPath = fishDetail.isShiny ? iconPath + "_s" : iconPath;

            bool loaded = false;
            Sprite sprite = null;
            AssetManager.LoadFromAddressables<Sprite>(loadPath, (s, handle) =>
            {
                sprite = s;
                loaded = true;
            });

            float timeout = 3f;
            float timer = 0f;
            while (!loaded && timer < timeout)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            if (sprite == null) continue;

            var fish = _fishPool.Get();
            if (fish == null) continue;

            fish.SetBaseHeight(baseHeight);
            fish.UniformScale = uniformScale * UnityEngine.Random.Range(0.8f, 1.2f);
            fish.EnableDebugLog = enableDebugLog;
            fish.SetTexture(sprite.texture);

            var speciesType = LoadDataManager.Instance.GetFishSpeciesType(fishData.fishSpeciesId);
            UpdateRects();
            fish.totalAreaRect = _totalRect;
            fish.bottomAreaRect = _bottomRect;

            float accel = UnityEngine.Random.Range(accelerationMin, accelerationMax);
            fish.SetPhysicsParams(
                moveSpeedMin, moveSpeedMax,
                verticalSpeedRatio, verticalMoveProbability,
                accel, dragForce,
                chargeDurationMin, chargeDurationMax,
                chargeScaleX, chargeScaleY,
                chargeSpeedRatio,
                sprintDurationMin, sprintDurationMax
            );

            Vector2 spawnPos = GetRandomPosInRect(_totalRect);
            switch (speciesType)
            {
                case FishSpeciesType.FullScreenSwim:
                    fish.SetFullScreenSwim(moveSpeedMin, moveSpeedMax,
                        directionChangeIntervalMin, directionChangeIntervalMax,
                        spawnPos);
                    _fullScreenSwimList.Add(fish);
                    break;
                case FishSpeciesType.FullScreenStatic:
                    fish.SetFullScreenStatic();
                    _fullScreenStaticList.Add(fish);
                    break;
                case FishSpeciesType.BottomSwim:
                    fish.SetBottomSwim(moveSpeedMin * 0.5f, moveSpeedMax * 0.6f,
                        directionChangeIntervalMin, directionChangeIntervalMax);
                    _bottomSwimList.Add(fish);
                    break;
                case FishSpeciesType.BottomStatic:
                    fish.SetBottomStatic();
                    _bottomStaticList.Add(fish);
                    break;
            }
        }
        UpdateRects();
        LogDebug($"CreateFishCoroutine 完成，全屏游动鱼: {_fullScreenSwimList.Count}");
    }

    private bool IsSameFishList(List<FishDetailData> list1, List<FishDetailData> list2)
    {
        if (list1 == null && list2 == null) return true;
        if (list1 == null || list2 == null) return false;
        if (list1.Count != list2.Count) return false;
        var ids1 = new HashSet<int>();
        foreach (var f in list1) if (f != null) ids1.Add(f.id);
        var ids2 = new HashSet<int>();
        foreach (var f in list2) if (f != null) ids2.Add(f.id);
        return ids1.SetEquals(ids2);
    }

    private void ClearAllFish()
    {
        foreach (var fish in _fullScreenSwimList) if (fish) _fishPool.Return(fish);
        foreach (var fish in _fullScreenStaticList) if (fish) _fishPool.Return(fish);
        foreach (var fish in _bottomSwimList) if (fish) _fishPool.Return(fish);
        foreach (var fish in _bottomStaticList) if (fish) _fishPool.Return(fish);
        _fullScreenSwimList.Clear();
        _fullScreenStaticList.Clear();
        _bottomSwimList.Clear();
        _bottomStaticList.Clear();
    }

    private int GetTotalFishCount()
    {
        return _fullScreenSwimList.Count + _fullScreenStaticList.Count +
               _bottomSwimList.Count + _bottomStaticList.Count;
    }

    private Vector2 GetRandomPosInRect(Rect rect)
    {
        float margin = 10f;
        float x = UnityEngine.Random.Range(rect.xMin + margin, rect.xMax - margin);
        float y = UnityEngine.Random.Range(rect.yMin + margin, rect.yMax - margin);
        return new Vector2(x, y);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void UpdateRects()
    {
        if (totalAreaRect != null && fishContainer != null)
        {
            Vector3[] corners = new Vector3[4];
            totalAreaRect.GetWorldCorners(corners);
            Vector3 local0 = fishContainer.InverseTransformPoint(corners[0]);
            Vector3 local2 = fishContainer.InverseTransformPoint(corners[2]);
            _totalRect = new Rect(local0.x, local0.y, local2.x - local0.x, local2.y - local0.y);
            LogDebug($"计算 _totalRect (fishContainer 本地): {_totalRect}");
        }

        if (bottomAreaRect != null && fishContainer != null)
        {
            Vector3[] corners = new Vector3[4];
            bottomAreaRect.GetWorldCorners(corners);
            Vector3 local0 = fishContainer.InverseTransformPoint(corners[0]);
            Vector3 local2 = fishContainer.InverseTransformPoint(corners[2]);
            _bottomRect = new Rect(local0.x, local0.y, local2.x - local0.x, local2.y - local0.y);
        }

        foreach (var fish in _fullScreenSwimList) if (fish) { fish.totalAreaRect = _totalRect; fish.bottomAreaRect = _bottomRect; }
        foreach (var fish in _fullScreenStaticList) if (fish) { fish.totalAreaRect = _totalRect; fish.bottomAreaRect = _bottomRect; }
        foreach (var fish in _bottomSwimList) if (fish) { fish.totalAreaRect = _totalRect; fish.bottomAreaRect = _bottomRect; }
        foreach (var fish in _bottomStaticList) if (fish) { fish.totalAreaRect = _totalRect; fish.bottomAreaRect = _bottomRect; }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private IEnumerator UpdateLoop()
    {
        int frame = 0;
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!gameObject.activeSelf) continue;
            frame++;
            if (enableDebugLog && frame % 60 == 0)
                LogDebug($"更新循环运行中，鱼数: {_fullScreenSwimList.Count}");

            if (_isDecorationMode) continue;

            UpdateBaits();

            foreach (var fish in _fullScreenSwimList)
                fish?.UpdateFullScreenSwim(directionChangeIntervalMin, directionChangeIntervalMax);
            foreach (var fish in _fullScreenStaticList)
                fish?.UpdateFullScreenStatic();
            foreach (var fish in _bottomSwimList)
                fish?.UpdateBottomSwim(directionChangeIntervalMin, directionChangeIntervalMax);
            foreach (var fish in _bottomStaticList)
                fish?.UpdateBottomStatic();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void UpdateBaits()
    {
        _baitPool.UpdateAllBaits(_totalRect, CheckNearbyFish, CheckBaitConsumption);
    }

    private void CheckNearbyFish(GameObject bait)
    {
        if (bait == null || _isDecorationMode) return;
        var baitComp = bait.GetComponent<UI_FishTankBait>();
        if (baitComp == null || !baitComp.IsActive) return;

        // 世界坐标 → fishContainer 本地坐标（与鱼同一坐标系）
        Vector3 baitWorld = baitComp.GetWorldPosition();
        Vector3 baitLocalInFish = fishContainer.InverseTransformPoint(baitWorld);
        Vector2 baitPos = new Vector2(baitLocalInFish.x, baitLocalInFish.y);

        foreach (var fish in _fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf || fish.IsChasingBait) continue;
            var fishRect = fish.GetComponent<RectTransform>();
            if (fishRect == null) continue;

            float dist = Vector2.Distance(fishRect.anchoredPosition, baitPos);
            if (dist <= baitTriggerRadius)
            {
                float duration = UnityEngine.Random.Range(baitChaseDurationMin, baitChaseDurationMax);
                fish.StartChasingBait(baitPos, duration, baitChaseSpeedMultiplier);
                LogDebug($"鱼 {fish.UniqueId} 开始追逐，距离 {dist:F1}");
            }
        }
    }

    private void CheckBaitConsumption(GameObject bait)
    {
        if (bait == null) return;
        var baitComp = bait.GetComponent<UI_FishTankBait>();
        if (baitComp == null || !baitComp.IsActive) return;

        Vector3 baitWorld = baitComp.GetWorldPosition();
        Vector3 baitLocalInFish = fishContainer.InverseTransformPoint(baitWorld);
        Vector2 baitPos = new Vector2(baitLocalInFish.x, baitLocalInFish.y);

        foreach (var fish in _fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf || !fish.IsChasingBait) continue;
            var fishRect = fish.GetComponent<RectTransform>();
            if (fishRect == null) continue;

            float dist = Vector2.Distance(fishRect.anchoredPosition, baitPos);
            if (dist < baitEatRadius)
            {
                LogDebug($"鱼饵被 {fish.UniqueId} 吃掉，距离 {dist:F1}");
                _baitPool.RemoveBait(bait);
                fish.ResetFishState();
                break;
            }
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void OnLeftClick()
    {
        var tanks = GetTankList();
        if (tanks.Count <= 1) return;

        // ★ 切换前清空所有鱼饵
        ClearAllBaits();

        _currentTankIndex = (_currentTankIndex - 1 + tanks.Count) % tanks.Count;
        CommunicateEvent.Modify(FishTankMessage.SwitchTank.ToString(), _currentTankIndex);
        RefreshAll();
        if (managerPanel != null && managerPanel.gameObject.activeSelf)
            managerPanel.OnTankSwitched(_currentTankIndex);
    }

    private void OnRightClick()
    {
        var tanks = GetTankList();
        if (tanks.Count <= 1) return;

        // ★ 切换前清空所有鱼饵
        ClearAllBaits();

        _currentTankIndex = (_currentTankIndex + 1) % tanks.Count;
        CommunicateEvent.Modify(FishTankMessage.SwitchTank.ToString(), _currentTankIndex);
        RefreshAll();
        if (managerPanel != null && managerPanel.gameObject.activeSelf)
            managerPanel.OnTankSwitched(_currentTankIndex);
    }

    private void OnLockClick()
    {
        var tank = GetCurrentTank();
        if (tank == null || tank.isUnlocked) return;
        var config = GetTankConfig(tank.tankId);
        if (config == null) return;
        GameUIManager.Instance?.ShowDialog(
            $"花费 {config.purchaseCost} 金币解锁 {config.name}？",
            DialogType.Info,
            () =>
            {
                CommunicateEvent.Modify(FishTankMessage.UnlockTank.ToString(), tank.tankId);
                GameUIManager.ShowMessage("解锁请求已发送");
            }
        );
    }

    private void ToggleManagerPanel()
    {
        _isManagerOpen = !_isManagerOpen;
        if (managerPanel != null)
        {
            if (_isManagerOpen)
            {
                managerPanel.OpenPanel();
                if (_isDecorationMode) ToggleDecorationMode();
            }
            else
                managerPanel.ClosePanel();
        }
        CommunicateEvent.Modify(FishTankMessage.ToggleManagerPanel.ToString());
    }

    private void OnFishTransferRequest(FishDetailData fishData, FishTankStoreData fromContainer, FishTankStoreData toContainer)
    {
        if (fishData == null || toContainer == null) return;
        var transferData = new TransferData
        {
            FishData = fishData,
            FromIndex = fromContainer?.IsBag == true ? 0 : (fromContainer?.TankId ?? 0) + 1,
            ToIndex = toContainer.IsBag ? 0 : toContainer.TankId + 1,
            IsFromBag = fromContainer?.IsBag ?? false,
            IsToBag = toContainer.IsBag
        };
        CommunicateEvent.Modify(FishTankMessage.TransferFish.ToString(), transferData);
    }

    private void OnUnlockRequest(int tankId)
    {
        var config = GetTankConfig(tankId);
        if (config == null) return;
        GameUIManager.Instance?.ShowDialog(
            $"花费 {config.purchaseCost} 金币解锁 {config.name}？",
            DialogType.Info,
            () =>
            {
                CommunicateEvent.Modify(FishTankMessage.UnlockTank.ToString(), tankId);
                GameUIManager.ShowMessage("解锁请求已发送");
            }
        );
    }

    private void OnDataUpdated()
    {
        if (gameObject.activeInHierarchy) RefreshAll();
    }

    private void OnDecorationDataUpdated()
    {
        if (gameObject.activeInHierarchy && _isDecorationMode) LoadEquippedDecorations();
    }

    private void OnShowDecOperator() { }

    private void OnHideDecOperator() { }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private class FishObjectPool
    {
        private GameObject _prefab;
        private Transform _parent;
        private FishTankMainPanel _panel;
        private Queue<UI_FishTankFish> _pool = new Queue<UI_FishTankFish>();
        private List<UI_FishTankFish> _allObjects = new List<UI_FishTankFish>();

        public FishObjectPool(GameObject prefab, Transform parent, int capacity, FishTankMainPanel panel)
        {
            _prefab = prefab;
            _parent = parent;
            _panel = panel;
            for (int i = 0; i < capacity; i++) CreateNewObject();
        }

        private UI_FishTankFish CreateNewObject()
        {
            GameObject go = GameObject.Instantiate(_prefab, _parent);
            go.SetActive(false);
            var fish = go.GetComponent<UI_FishTankFish>();
            if (fish == null) fish = go.AddComponent<UI_FishTankFish>();
            _allObjects.Add(fish);
            _pool.Enqueue(fish);
            return fish;
        }

        public UI_FishTankFish Get()
        {
            UI_FishTankFish fish;
            if (_pool.Count > 0) fish = _pool.Dequeue();
            else fish = CreateNewObject();
            fish.gameObject.SetActive(true);
            return fish;
        }

        public void Return(UI_FishTankFish fish)
        {
            if (fish == null) return;
            fish.gameObject.SetActive(false);
            if (!_pool.Contains(fish) && _allObjects.Contains(fish))
                _pool.Enqueue(fish);
        }

        public void Clear()
        {
            foreach (var fish in _allObjects) if (fish != null) GameObject.Destroy(fish.gameObject);
            _pool.Clear();
            _allObjects.Clear();
        }
    }

    private class BaitObjectPool
    {
        private GameObject _prefab;
        private Transform _parent;
        private FishTankMainPanel _panel;
        private Rect _totalRect;
        private float _fallSpeedRatio;
        private float _scale;
        private Queue<GameObject> _pool = new Queue<GameObject>();
        private List<GameObject> _allObjects = new List<GameObject>();
        private Queue<GameObject> _activeBaits = new Queue<GameObject>();

        public BaitObjectPool(GameObject prefab, Transform parent, int capacity, FishTankMainPanel panel, Rect totalRect, float fallSpeedRatio, float scale)
        {
            _prefab = prefab;
            _parent = parent;
            _panel = panel;
            _totalRect = totalRect;
            _fallSpeedRatio = fallSpeedRatio;
            _scale = scale;
            for (int i = 0; i < capacity; i++) CreateNewBait();
        }

        private GameObject CreateNewBait()
        {
            if (_prefab == null)
            {
                Debug.LogError("[BaitObjectPool] baitPrefab 为空，无法创建鱼饵");
                return null;
            }
            GameObject go = GameObject.Instantiate(_prefab, _parent);
            go.SetActive(false);
            var bait = go.GetComponent<UI_FishTankBait>();
            if (bait == null) bait = go.AddComponent<UI_FishTankBait>();
            bait.Init(_panel, _totalRect, _fallSpeedRatio, _scale);
            _allObjects.Add(go);
            _pool.Enqueue(go);
            return go;
        }

        public GameObject Get(Vector3 position)
        {
            GameObject bait;
            if (_pool.Count > 0) bait = _pool.Dequeue();
            else
            {
                bait = CreateNewBait();
                if (bait != null) bait = _pool.Dequeue();
            }
            if (bait == null) return null;
            bait.SetActive(true);
            var comp = bait.GetComponent<UI_FishTankBait>();
            if (comp != null) comp.ResetBait(position);
            _activeBaits.Enqueue(bait);
            return bait;
        }

        public void Return(GameObject bait)
        {
            if (bait == null) return;
            var comp = bait.GetComponent<UI_FishTankBait>();
            if (comp != null) comp.Deactivate();
            bait.SetActive(false);
            if (!_pool.Contains(bait) && _allObjects.Contains(bait))
                _pool.Enqueue(bait);
        }

        public void RemoveBait(GameObject bait)
        {
            Queue<GameObject> newQueue = new Queue<GameObject>();
            while (_activeBaits.Count > 0)
            {
                var current = _activeBaits.Dequeue();
                if (current == bait) Return(bait);
                else newQueue.Enqueue(current);
            }
            _activeBaits = newQueue;
        }

        public void ClearAll()
        {
            while (_activeBaits.Count > 0)
            {
                var bait = _activeBaits.Dequeue();
                if (bait != null) Return(bait);
            }
        }

        public void Clear()
        {
            ClearAll();
            foreach (var bait in _allObjects) if (bait != null) GameObject.Destroy(bait);
            _pool.Clear();
            _allObjects.Clear();
        }

        public void UpdateAllBaits(Rect totalRect, Action<GameObject> checkNearby, Action<GameObject> checkConsumption)
        {
            List<GameObject> baitsList = new List<GameObject>(_activeBaits);
            foreach (var bait in baitsList)
            {
                if (bait == null) continue;
                var comp = bait.GetComponent<UI_FishTankBait>();
                if (comp != null)
                {
                    comp.UpdateBait();
                    if (!comp.IsActive) continue;
                    checkNearby?.Invoke(bait);
                    checkConsumption?.Invoke(bait);
                }
            }
        }
    }

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankMainPanel] {msg}");
    }
}

public class DecoClickHandler : MonoBehaviour
{
    private string _recordId;
    private int _decorationId;
    private int _category;
    private int _tankId;

    public void Init(string recordId, int decId, int category, int tankId)
    {
        _recordId = recordId;
        _decorationId = decId;
        _category = category;
        _tankId = tankId;
    }

    private void OnMouseDown()
    {
        var data = new SelectDecorationData
        {
            TankId = _tankId,
            RecordId = int.Parse(_recordId),
            Category = _category,
            ScreenPosition = Input.mousePosition
        };
        CommunicateEvent.Modify(FishTankMessage.SelectDecoration.ToString(), data);
    }
}
