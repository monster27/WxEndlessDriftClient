using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 鱼缸视图 - 管理和控制
/// </summary>
public class FishTankView : BaseView
{
    [Header("鱼缸区域(与Container同级)")]
    public RectTransform moveArea;
    public RectTransform bottomArea;

    [Header("鱼容器")]
    public RectTransform fishContainer;

    [Header("四个行为类型列表")]
    public List<FishTankFishPrefab> fullScreenSwimList = new List<FishTankFishPrefab>();
    public List<FishTankFishPrefab> fullScreenStaticList = new List<FishTankFishPrefab>();
    public List<FishTankFishPrefab> bottomSwimList = new List<FishTankFishPrefab>();
    public List<FishTankFishPrefab> bottomStaticList = new List<FishTankFishPrefab>();

    [Header("临时图片列表")]
    public List<Sprite> tempFullScreenSwimSprites = new List<Sprite>();
    public List<Sprite> tempFullScreenStaticSprites = new List<Sprite>();
    public List<Sprite> tempBottomSwimSprites = new List<Sprite>();
    public List<Sprite> tempBottomStaticSprites = new List<Sprite>();

    [Header("鱼预制体")]
    public GameObject fishPrefab;

    [Header("水平移动")]
    public float moveSpeedMin = 30f;
    public float moveSpeedMax = 80f;
    public float directionChangeIntervalMin = 2f;
    public float directionChangeIntervalMax = 6f;

    [Header("垂直移动(正弦波动-鱼鳍摆动)")]
    public float floatAmplitude = 5f;
    public float floatSpeedMin = 0.5f;
    public float floatSpeedMax = 1.5f;

    [Header("垂直移动(上浮/下潜速度)")]
    public float verticalSpeedMin = 20f;
    public float verticalSpeedMax = 50f;

    [Header("全屏移动鱼类生成范围")]
    [Range(0.1f, 1f)]
    public float fullScreenSwimSpawnRange = 1f; // 0-1之间的比例，从顶部开始计算

    [Header("UI")]
    public Button refreshBtn;
    public Text fishCountText;

    private bool _isInitialized;
    private Coroutine _updateCoroutine;

    private Rect _moveRect;
    private Rect _bottomRect;

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        InitFishTank();
        BindButtonListeners();

        isInitialized = true;
        _isInitialized = true;
    }

    private void InitFishTank()
    {
        if (fishContainer == null)
        {
            GameObject go = new GameObject("FishContainer");
            go.transform.SetParent(transform);
            fishContainer = go.AddComponent<RectTransform>();
            fishContainer.anchorMin = Vector2.zero;
            fishContainer.anchorMax = Vector2.one;
            fishContainer.offsetMin = Vector2.zero;
            fishContainer.offsetMax = Vector2.zero;
            fishContainer.pivot = new Vector2(0.5f, 0.5f);
            fishContainer.localScale = Vector3.one;
        }

        InitAreas();
        UpdateRects();
        CreateAllFish();

        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateLoop());
        }

        UpdateFishCount();
    }

    private void InitAreas()
    {
        if (moveArea == null)
        {
            GameObject go = new GameObject("MoveArea");
            go.transform.SetParent(transform);
            moveArea = go.AddComponent<RectTransform>();
            moveArea.anchorMin = Vector2.zero;
            moveArea.anchorMax = Vector2.one;
            moveArea.offsetMin = Vector2.zero;
            moveArea.offsetMax = Vector2.zero;
            moveArea.pivot = new Vector2(0.5f, 0.5f);
            moveArea.localScale = Vector3.one;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.8f, 0.05f);
            img.raycastTarget = false;
        }

        if (bottomArea == null)
        {
            GameObject go = new GameObject("BottomArea");
            go.transform.SetParent(transform);
            bottomArea = go.AddComponent<RectTransform>();
            bottomArea.anchorMin = new Vector2(0, 0);
            bottomArea.anchorMax = new Vector2(1, 0.3f);
            bottomArea.offsetMin = Vector2.zero;
            bottomArea.offsetMax = Vector2.zero;
            bottomArea.pivot = new Vector2(0.5f, 0.5f);
            bottomArea.localScale = Vector3.one;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.6f, 0.4f, 0.2f, 0.1f);
            img.raycastTarget = false;
        }
    }

    private void UpdateRects()
    {
        if (moveArea != null)
        {
            Vector3[] corners = new Vector3[4];
            moveArea.GetWorldCorners(corners);
            Vector2 min = fishContainer.InverseTransformPoint(corners[0]);
            Vector2 max = fishContainer.InverseTransformPoint(corners[2]);
            _moveRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        if (bottomArea != null)
        {
            Vector3[] corners = new Vector3[4];
            bottomArea.GetWorldCorners(corners);
            Vector2 min = fishContainer.InverseTransformPoint(corners[0]);
            Vector2 max = fishContainer.InverseTransformPoint(corners[2]);
            _bottomRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        foreach (var fish in fullScreenSwimList) SetFishParams(fish);
        foreach (var fish in fullScreenStaticList) SetFishParams(fish);
        foreach (var fish in bottomSwimList) SetFishParams(fish);
        foreach (var fish in bottomStaticList) SetFishParams(fish);
    }

    private void SetFishParams(FishTankFishPrefab fish)
    {
        if (fish != null)
        {
            fish.moveAreaRect = _moveRect;
            fish.bottomAreaRect = _bottomRect;
        }
    }

    #region 创建鱼

    private void CreateAllFish()
    {
        ClearAllFish();

        foreach (var sprite in tempFullScreenSwimSprites)
            if (sprite != null) CreateFish(sprite, FishSpeciesType.FullScreenSwim);

        foreach (var sprite in tempFullScreenStaticSprites)
            if (sprite != null) CreateFish(sprite, FishSpeciesType.FullScreenStatic);

        foreach (var sprite in tempBottomSwimSprites)
            if (sprite != null) CreateFish(sprite, FishSpeciesType.BottomSwim);

        foreach (var sprite in tempBottomStaticSprites)
            if (sprite != null) CreateFish(sprite, FishSpeciesType.BottomStatic);
    }

    private void CreateFish(Sprite sprite, FishSpeciesType type)
    {
        if (fishPrefab == null)
        {
            Debug.LogError("[FishTankView] fishPrefab 为空");
            return;
        }

        GameObject go = Instantiate(fishPrefab, fishContainer);
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        FishTankFishPrefab fish = go.GetComponent<FishTankFishPrefab>();
        if (fish == null) fish = go.AddComponent<FishTankFishPrefab>();

        FishSpeciesData data = new FishSpeciesData
        {
            id = (int)type,
            name = type.ToString(),
            type = type.ToString()
        };

        fish.Init(data);
        fish.SetSprite(sprite);
        SetFishParams(fish);
        SetBehavior(fish, sprite);

        AddToList(fish, type);
    }

    /// <summary>
    /// 获取区域内的随机位置（支持生成范围限制，从顶部开始计算）
    /// </summary>
    private Vector2 GetRandomPosInRectWithRange(Rect rect, float range)
    {
        float x = Random.Range(rect.xMin + 30f, rect.xMax - 30f);
        // 从顶部开始计算：范围0.75表示从顶部向下75%的区域
        // yMax = rect.yMax - 30f (顶部边界)
        // yMin = rect.yMax - (rect.yMax - rect.yMin) * range + 30f (范围底部)
        float yMax = rect.yMax - 30f;
        float yMin = rect.yMax - (rect.yMax - rect.yMin) * range + 30f;
        if (yMin > yMax) yMin = yMax;
        float y = Random.Range(yMin, yMax);
        return new Vector2(x, y);
    }

    private void SetBehavior(FishTankFishPrefab fish, Sprite sprite)
    {
        float verticalSpeed = Random.Range(verticalSpeedMin, verticalSpeedMax);

        switch (fish.speciesType)
        {
            case FishSpeciesType.FullScreenSwim:
                // 使用带范围限制的随机位置（从顶部开始）
                Vector2 swimPos = GetRandomPosInRectWithRange(_moveRect, fullScreenSwimSpawnRange);
                fish.SetFullScreenSwim(moveSpeedMin, moveSpeedMax, floatAmplitude,
                    floatSpeedMin, floatSpeedMax, directionChangeIntervalMin, directionChangeIntervalMax, verticalSpeed, swimPos);
                fish.SetSpeedRange(moveSpeedMin, moveSpeedMax, verticalSpeedMin, verticalSpeedMax);
                break;
            case FishSpeciesType.FullScreenStatic:
                fish.SetFullScreenStatic();
                break;
            case FishSpeciesType.BottomSwim:
                fish.SetBottomSwim(moveSpeedMin, moveSpeedMax, floatAmplitude,
                    floatSpeedMin, floatSpeedMax, directionChangeIntervalMin, directionChangeIntervalMax, verticalSpeed);
                fish.SetSpeedRange(moveSpeedMin * 0.5f, moveSpeedMax * 0.7f, verticalSpeedMin * 0.5f, verticalSpeedMax * 0.5f);
                break;
            case FishSpeciesType.BottomStatic:
                fish.SetBottomStatic();
                break;
        }
    }

    private void AddToList(FishTankFishPrefab fish, FishSpeciesType type)
    {
        switch (type)
        {
            case FishSpeciesType.FullScreenSwim: fullScreenSwimList.Add(fish); break;
            case FishSpeciesType.FullScreenStatic: fullScreenStaticList.Add(fish); break;
            case FishSpeciesType.BottomSwim: bottomSwimList.Add(fish); break;
            case FishSpeciesType.BottomStatic: bottomStaticList.Add(fish); break;
        }
    }

    private void ClearAllFish()
    {
        foreach (var f in fullScreenSwimList) if (f) Destroy(f.gameObject);
        foreach (var f in fullScreenStaticList) if (f) Destroy(f.gameObject);
        foreach (var f in bottomSwimList) if (f) Destroy(f.gameObject);
        foreach (var f in bottomStaticList) if (f) Destroy(f.gameObject);

        fullScreenSwimList.Clear();
        fullScreenStaticList.Clear();
        bottomSwimList.Clear();
        bottomStaticList.Clear();
    }

    #endregion

    #region 更新循环

    private IEnumerator UpdateLoop()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!gameObject.activeSelf) continue;

            foreach (var f in fullScreenSwimList) f?.UpdateFullScreenSwim(directionChangeIntervalMin, directionChangeIntervalMax);
            foreach (var f in fullScreenStaticList) f?.UpdateFullScreenStatic();
            foreach (var f in bottomSwimList) f?.UpdateBottomSwim(directionChangeIntervalMin, directionChangeIntervalMax);
            foreach (var f in bottomStaticList) f?.UpdateBottomStatic();
        }
    }

    #endregion

    #region 按钮事件

    private void BindButtonListeners()
    {
        if (refreshBtn != null) refreshBtn.onClick.AddListener(OnRefreshClick);
    }

    public void OnRefreshClick()
    {
        foreach (var f in fullScreenSwimList) if (f) SetBehavior(f, null);
        foreach (var f in fullScreenStaticList) if (f) SetBehavior(f, null);
        foreach (var f in bottomSwimList) if (f) SetBehavior(f, null);
        foreach (var f in bottomStaticList) if (f) SetBehavior(f, null);
    }

    #endregion

    #region 公共方法

    public void UpdateFishCount()
    {
        if (fishCountText != null)
        {
            int total = fullScreenSwimList.Count + fullScreenStaticList.Count +
                       bottomSwimList.Count + bottomStaticList.Count;
            fishCountText.text = $"鱼数量: {total}";
        }
    }

    public void OpenFishTank()
    {
        gameObject.SetActive(true);
        if (!_isInitialized) BaseViewInit();
        else { UpdateRects(); OnRefreshClick(); }
    }

    public void CloseFishTank()
    {
        gameObject.SetActive(false);
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
    }

    public void ReloadFishTank()
    {
        CreateAllFish();
        UpdateFishCount();
    }

    #endregion

    private void OnDestroy()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
        ClearAllFish();
    }

    private void OnEnable()
    {
        if (_isInitialized) UpdateRects();
    }
}
