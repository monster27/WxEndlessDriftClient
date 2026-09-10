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

    [Header("===== 预制体 =====")]
    [SerializeField] private GameObject fishPrefab;
    [SerializeField] private GameObject baitPrefab;
    [SerializeField] private GameObject decPrefab;

    [Header("===== 82/83/84 贴图 Image =====")]
    [SerializeField] private Image backgroundBorderImage;
    [SerializeField] private Image backgroundBottomImage;
    [SerializeField] private Image backgroundImage;

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
    [SerializeField] private float baitTriggerRadius = 500f;
    [SerializeField] private float baitEatRadius = 40f;
    [SerializeField, Range(0.05f, 2f)]
    private float baitFallSpeedRatio = 0.25f;
    [SerializeField] private float baitChaseDurationMin = 0.5f;
    [SerializeField] private float baitChaseDurationMax = 0.8f;
    [SerializeField] private float baitChaseSpeedMultiplier = 5f;
    [SerializeField] private float baitScale = 1f;
    [SerializeField] private int baitPoolInitSize = 5;

    [Header("===== 对象池 =====")]
    [SerializeField] private int fishPoolInitialCapacity = 10;

    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private int _currentTankIndex = 0;
    private bool _hasInitialized = false;
    private bool _isDecorationMode = false;

    private List<UI_FishTankFish> _fullScreenSwimList = new List<UI_FishTankFish>();
    private List<UI_FishTankFish> _fullScreenStaticList = new List<UI_FishTankFish>();
    private List<UI_FishTankFish> _bottomSwimList = new List<UI_FishTankFish>();
    private List<UI_FishTankFish> _bottomStaticList = new List<UI_FishTankFish>();

    private List<FishDetailData> _currentDisplayingFish = new List<FishDetailData>();
    private List<FishDetailData> _pendingFishData = null;
    private Coroutine _createCoroutine = null;

    private FishObjectPool _fishPool;
    private BaitObjectPool _baitPool;
    private Coroutine _updateCoroutine;

    private Rect _totalRect;
    private Rect _bottomRect;

    private Dictionary<int, UI_FishTankDec> _decorationInstances = new Dictionary<int, UI_FishTankDec>();
    private Sprite _defaultBorderSprite;
    private Sprite _defaultBottomSprite;
    private Sprite _defaultBackgroundSprite;

    /// <summary>
    /// 装饰被点击时，由外部（View）注册的回调
    /// </summary>
    public Action<UI_FishTankDec> OnDecorationClickedCallback { get; set; }

    public bool EnableDebugLog => enableDebugLog;
    public Rect TotalRect => _totalRect;
    public bool IsDecorationMode => _isDecorationMode;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 初始化
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void Init(bool isEnableDebug = false)
    {
        if (_hasInitialized) return;
        enableDebugLog = isEnableDebug;

        if (backgroundBorderImage != null) _defaultBorderSprite = backgroundBorderImage.sprite;
        if (backgroundBottomImage != null) _defaultBottomSprite = backgroundBottomImage.sprite;
        if (backgroundImage != null) _defaultBackgroundSprite = backgroundImage.sprite;

        if (fishPrefab != null && fishContainer != null)
            _fishPool = new FishObjectPool(fishPrefab, fishContainer, fishPoolInitialCapacity, this);

        if (baitPrefab != null && baitContainer != null)
            _baitPool = new BaitObjectPool(baitPrefab, baitContainer, baitPoolInitSize, this, _totalRect, baitFallSpeedRatio, baitScale);

        SetupClickHandler();
        EnsureDecorationOnTop();
        _hasInitialized = true;
        Debug.Log("[FishTankMainPanel] Init 完成");
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 打开/关闭
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        if (!_hasInitialized) Init(enableDebugLog);
        UpdateRects();
        if (_updateCoroutine == null)
            _updateCoroutine = StartCoroutine(UpdateLoop());
    }

    public void ClosePanel()
    {
        if (_updateCoroutine != null) { StopCoroutine(_updateCoroutine); _updateCoroutine = null; }
        ClearAllBaits();
        ClearFish();
        ClearAllDecorations();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        _fishPool?.Clear();
        _baitPool?.Clear();
        ClearAllFish();
        ClearAllDecorations();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 可见性
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void SetFishVisible(bool visible)
    {
        if (fishContainer != null) fishContainer.gameObject.SetActive(visible);
    }

    public void SetBaitVisible(bool visible)
    {
        if (baitContainer != null) baitContainer.gameObject.SetActive(visible);
    }


    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装饰模式开关
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 切换装饰模式
    /// true  = 装饰可点，鱼缸区域不可点（不生成鱼饵）
    /// false = 装饰不可点，鱼缸区域可点（生成鱼饵）
    /// </summary>
    public void SetDecorationMode(bool isDecorationMode)
    {
        _isDecorationMode = isDecorationMode;

        // 遍历所有装饰实例，切换 Button.interactable
        foreach (var kv in _decorationInstances)
        {
            if (kv.Value != null) kv.Value.SetInteractable(isDecorationMode);
        }

        // 鱼缸区域 raycastTarget：装饰模式下关闭
        SetFishTankClickable(!isDecorationMode);

        LogDebug($"SetDecorationMode: {isDecorationMode}, 装饰实例数={_decorationInstances.Count}");
    }

    /// <summary>
    /// 设置鱼缸点击区域是否可点击（生成鱼饵）
    /// </summary>
    public void SetFishTankClickable(bool clickable)
    {
        if (totalAreaRect == null) return;
        var img = totalAreaRect.GetComponent<Image>();
        if (img != null) img.raycastTarget = clickable;
    }

    /// <summary>
    /// 确保 decorationContainer 在 totalAreaRect 之上
    /// </summary>
    public void EnsureDecorationOnTop()
    {
        if (totalAreaRect == null || decorationContainer == null) return;
        if (totalAreaRect.parent != decorationContainer.parent) return;

        int areaIdx = totalAreaRect.GetSiblingIndex();
        int decIdx = decorationContainer.GetSiblingIndex();
        if (decIdx < areaIdx)
        {
            decorationContainer.SetSiblingIndex(areaIdx);
            LogDebug($"调整 decorationContainer siblingIndex: {decIdx} -> {areaIdx}");
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 点击生成鱼饵
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void SetupClickHandler()
    {
        if (totalAreaRect == null) return;

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
            // 装饰模式下不生成鱼饵
            if (_isDecorationMode) return;

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
    }

    public void SpawnBaitAtPosition(Vector3 worldPosition)
    {
        if (baitPrefab == null || _baitPool == null || baitContainer == null) return;

        Vector3 localPos = baitContainer.InverseTransformPoint(worldPosition);
        localPos.z = 0;

        float margin = 10f;
        localPos.x = Mathf.Clamp(localPos.x, _totalRect.xMin + margin, _totalRect.xMax - margin);
        localPos.y = Mathf.Clamp(localPos.y, _totalRect.yMin + margin, _totalRect.yMax - margin);

        GameObject bait = _baitPool.Get(localPos);
        if (bait == null) return;
        CheckNearbyFish(bait);
    }

    public void ReturnBait(GameObject bait)
    {
        if (bait == null || _baitPool == null) return;
        _baitPool.RemoveBait(bait);
    }

    public void ClearAllBaits()
    {
        _baitPool?.ClearAll();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 鱼群
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void RefreshFishTank(int tankIndex)
    {
        _currentTankIndex = tankIndex;

        var tanks = PlayerDataService.Instance?.GetTankList() ?? new List<FishTankStatusData>();
        if (tanks.Count == 0) { ClearFish(); return; }
        if (_currentTankIndex >= tanks.Count) _currentTankIndex = tanks.Count - 1;
        if (_currentTankIndex < 0) _currentTankIndex = 0;

        var tank = tanks[_currentTankIndex];
        if (tank == null || !tank.isUnlocked) { ClearFish(); return; }

        var fishList = PlayerDataService.Instance?.GetTankFishList(tank.tankId) ?? new List<FishDetailData>();
        SetFishData(fishList);
    }

    public void SetFishData(List<FishDetailData> fishList)
    {
        if (fishList == null || fishList.Count == 0) { ClearFish(); return; }
        _pendingFishData = new List<FishDetailData>(fishList);
    }

    public void ClearFish()
    {
        ClearAllFish();
        _currentDisplayingFish.Clear();
        _pendingFishData = null;
        if (_createCoroutine != null) { StopCoroutine(_createCoroutine); _createCoroutine = null; }
    }

    private void Update()
    {
        if (_pendingFishData != null && _createCoroutine == null)
        {
            List<FishDetailData> fishList = _pendingFishData;
            _pendingFishData = null;
            if (IsSameFishList(fishList, _currentDisplayingFish)) return;
            _createCoroutine = StartCoroutine(RebuildFish(fishList));
        }
    }

    private IEnumerator RebuildFish(List<FishDetailData> fishList)
    {
        _currentDisplayingFish = new List<FishDetailData>(fishList);
        ClearAllFish();
        yield return StartCoroutine(CreateFishCoroutine(fishList));
        _createCoroutine = null;
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
            while (!loaded && timer < timeout) { yield return null; timer += Time.deltaTime; }
            if (sprite == null) continue;

            var fish = _fishPool.Get();
            if (fish == null) continue;

            var speciesType = LoadDataManager.Instance.GetFishSpeciesType(fishData.fishSpeciesId);

            FishSpeciesData fishSpeciesData = new FishSpeciesData
            {
                id = (int)speciesType,
                name = speciesType.ToString(),
                type = speciesType.ToString()
            };
            fish.Init(fishSpeciesData, null);

            fish.SetBaseHeight(baseHeight);
            fish.UniformScale = uniformScale * UnityEngine.Random.Range(0.8f, 1.2f);
            fish.EnableDebugLog = enableDebugLog;
            fish.SetTexture(sprite.texture);

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
                        directionChangeIntervalMin, directionChangeIntervalMax, spawnPos);
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
        foreach (var fish in _fullScreenSwimList) if (fish && _fishPool != null) _fishPool.Return(fish);
        foreach (var fish in _fullScreenStaticList) if (fish && _fishPool != null) _fishPool.Return(fish);
        foreach (var fish in _bottomSwimList) if (fish && _fishPool != null) _fishPool.Return(fish);
        foreach (var fish in _bottomStaticList) if (fish && _fishPool != null) _fishPool.Return(fish);
        _fullScreenSwimList.Clear();
        _fullScreenStaticList.Clear();
        _bottomSwimList.Clear();
        _bottomStaticList.Clear();
    }

    private Vector2 GetRandomPosInRect(Rect rect)
    {
        float margin = 10f;
        float x = UnityEngine.Random.Range(rect.xMin + margin, rect.xMax - margin);
        float y = UnityEngine.Random.Range(rect.yMin + margin, rect.yMax - margin);
        return new Vector2(x, y);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 更新循环
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void UpdateRects()
    {
        if (totalAreaRect != null && fishContainer != null)
        {
            Vector3[] corners = new Vector3[4];
            totalAreaRect.GetWorldCorners(corners);
            Vector3 local0 = fishContainer.InverseTransformPoint(corners[0]);
            Vector3 local2 = fishContainer.InverseTransformPoint(corners[2]);
            _totalRect = new Rect(local0.x, local0.y, local2.x - local0.x, local2.y - local0.y);
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

    private IEnumerator UpdateLoop()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!gameObject.activeSelf) continue;

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

    private void UpdateBaits()
    {
        _baitPool?.UpdateAllBaits(_totalRect, CheckNearbyFish, CheckBaitConsumption);
    }

    private void CheckNearbyFish(GameObject bait)
    {
        if (bait == null) return;
        var baitComp = bait.GetComponent<UI_FishTankBait>();
        if (baitComp == null || !baitComp.IsActive) return;

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
                _baitPool.RemoveBait(bait);
                fish.ResetFishState();
                break;
            }
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装饰渲染
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void RenderDecorations(int tankId)
    {
        ClearAllDecorations();

        var equipped = PlayerDataService.Instance?.GetEquippedDecorations(tankId);
        if (equipped == null) return;

        if (equipped.TryGetValue(80, out var list80))
            foreach (var info in list80) CreateDecorationInstance(info, 80, tankId);

        if (equipped.TryGetValue(81, out var list81))
            foreach (var info in list81) CreateDecorationInstance(info, 81, tankId);

        ApplyTexture(equipped, 82, backgroundBorderImage, _defaultBorderSprite);
        ApplyTexture(equipped, 83, backgroundBottomImage, _defaultBottomSprite);
        ApplyTexture(equipped, 84, backgroundImage, _defaultBackgroundSprite);
    }

    /// <summary>
    /// 从 decPrefab 实例化一个装饰
    /// </summary>
    private void CreateDecorationInstance(DecorationEquipInfo info, int category, int tankId)
    {
        if (decorationContainer == null || decPrefab == null) return;

        GameObject go = Instantiate(decPrefab, decorationContainer);
        go.name = $"Decoration_{info.Id}";

        var rect = go.GetComponent<RectTransform>();
        if (rect == null) rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(info.PositionX, info.PositionY);

        if (rect.sizeDelta.x <= 0.01f || rect.sizeDelta.y <= 0.01f)
            rect.sizeDelta = new Vector2(100f, 100f);

        var img = go.GetComponent<Image>();
        if (img == null) img = go.GetComponentInChildren<Image>();
        if (img != null) img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.GetComponentInChildren<Button>();
        if (btn != null)
        {
            if (btn.targetGraphic == null && img != null) btn.targetGraphic = img;
        }

        var decComp = go.GetComponent<UI_FishTankDec>();
        if (decComp == null) decComp = go.AddComponent<UI_FishTankDec>();

        decComp.Init(info.Id, category, tankId, info.DecorationId, OnDecorationInstanceClicked);

        // ★ 根据当前模式设置交互
        decComp.SetInteractable(_isDecorationMode);

        decComp.SetFlip(Mathf.Approximately(info.RotationY, 180f));

        _decorationInstances[info.Id] = decComp;
    }

    private void OnDecorationInstanceClicked(UI_FishTankDec dec)
    {
        if (!_isDecorationMode) return;
        if (dec == null) return;

        OnDecorationClickedCallback?.Invoke(dec);
    }

    private void ApplyTexture(Dictionary<int, List<DecorationEquipInfo>> equipped, int category, Image target, Sprite defaultSprite)
    {
        if (target == null) return;

        if (equipped != null && equipped.TryGetValue(category, out var list) && list.Count > 0)
        {
            var info = list[0];
            var itemData = LoadDataManager.Instance?.GetItemById(info.DecorationId);
            if (itemData != null && !string.IsNullOrEmpty(itemData.iconPath))
            {
                AssetManager.LoadFromAddressables<Sprite>(itemData.iconPath, (s, handle) =>
                {
                    if (s != null && target != null) target.sprite = s;
                });
                return;
            }
        }

        target.sprite = defaultSprite;
    }

    public void ClearAllDecorations()
    {
        foreach (var kv in _decorationInstances)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        _decorationInstances.Clear();
    }

    public void MoveDecorationInstance(int recordId, float dx, float dy)
    {
        if (_decorationInstances.TryGetValue(recordId, out var dec) && dec != null)
        {
            var rect = dec.Rect;
            if (rect != null) rect.anchoredPosition += new Vector2(dx, dy);
        }
    }

    public RectTransform GetDecorationRect(int recordId)
    {
        if (_decorationInstances.TryGetValue(recordId, out var dec) && dec != null)
            return dec.Rect;
        return null;
    }

    public UI_FishTankDec GetDecorationInstance(int recordId)
    {
        _decorationInstances.TryGetValue(recordId, out var dec);
        return dec;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 对象池（鱼）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private class FishObjectPool
    {
        private GameObject _prefab;
        private Transform _parent;
        private FishTankMainPanel _panel;
        private Queue<UI_FishTankFish> _pool = new Queue<UI_FishTankFish>();
        private List<UI_FishTankFish> _allObjects = new List<UI_FishTankFish>();

        public FishObjectPool(GameObject prefab, Transform parent, int capacity, FishTankMainPanel panel)
        {
            _prefab = prefab; _parent = parent; _panel = panel;
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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 对象池（鱼饵）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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
            _prefab = prefab; _parent = parent; _panel = panel;
            _totalRect = totalRect; _fallSpeedRatio = fallSpeedRatio; _scale = scale;
            for (int i = 0; i < capacity; i++) CreateNewBait();
        }

        private GameObject CreateNewBait()
        {
            if (_prefab == null) { Z_Logger.LogError("[BaitObjectPool] baitPrefab 为空"); return null; }
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
            else { bait = CreateNewBait(); if (bait != null) bait = _pool.Dequeue(); }
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
