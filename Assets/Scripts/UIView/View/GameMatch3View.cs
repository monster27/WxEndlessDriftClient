using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 消消乐棋盘表现层（状态机版 · 无协程）
/// 
/// 动画系统：
///   - 单槽位动画（同一时刻只播一个动画）
///   - Update 驱动，完成后回调
///   - 零协程、零 GC
/// 
/// 优化：
///   - 对象池（ResetForReuse / CleanupForPool）
///   - _tileIndex 字典 O(1)
///   - AA 资源只加载一次
///   - Text 更新带缓存
/// </summary>
public class GameMatch3View : MonoBehaviour
{
    // ============================================================
    // 引用
    // ============================================================

    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 棋盘容器 =====")]
    [SerializeField] private RectTransform boardContainer;
    [SerializeField] private UI_GameMatch3GridPrefab tilePrefab;

    [Header("===== 顶部状态栏 =====")]
    [SerializeField] private Text levelText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text timeText;

    [Header("===== 道具按钮 =====")]
    [SerializeField] private Button hintBtn;
    [SerializeField] private Text hintBtnText;
    [SerializeField] private Button refreshBtn;
    [SerializeField] private Text refreshBtnText;
    [SerializeField] private Button timeAddBtn;
    [SerializeField] private Text timeAddBtnText;

    [Header("===== 结算面板 =====")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultScoreText;
    [SerializeField] private Text resultBestText;
    [SerializeField] private Button retryBtn;
    [SerializeField] private Button closeBtn;

    [Header("===== Combo 提示 =====")]
    [SerializeField] private Text comboText;

    // ============================================================
    // 美术 - 测试
    // ============================================================

    [Header("===== 美术 - 测试 Sprite =====")]
    [SerializeField] private bool useTestSprites = false;
    [SerializeField] private List<Sprite> testTileSprites = new List<Sprite>();
    [SerializeField] private Sprite testLineOverlayH;
    [SerializeField] private Sprite testLineOverlayV;
    [SerializeField] private Sprite testBombOverlay;
    [SerializeField] private Sprite testMagicBird;
    [SerializeField] private Sprite testHighlight;
    [SerializeField] private Sprite testSelect;

    // ============================================================
    // 美术 - AA
    // ============================================================

    [Header("===== 美术 - AA 加载路径 =====")]
    [SerializeField] private string tileIconPathFormat = "UI/Icon/GameMatch3Icons/{0}";
    [SerializeField] private string lineOverlayHPath = "UI/Icon/GameMatch3Icons/overlay_line_h";
    [SerializeField] private string lineOverlayVPath = "UI/Icon/GameMatch3Icons/overlay_line_v";
    [SerializeField] private string bombOverlayPath = "UI/Icon/GameMatch3Icons/overlay_bomb";
    [SerializeField] private string magicBirdPath = "UI/Icon/GameMatch3Icons/magicbird";
    [SerializeField] private string highlightPath = "UI/Icon/GameMatch3Icons/highlight";
    [SerializeField] private string selectPath = "UI/Icon/GameMatch3Icons/select";
    [SerializeField] private float loadTimeout = 5f;

    // ============================================================
    // 输入参数
    // ============================================================

    [Header("===== 输入参数 =====")]
    [SerializeField] private float dragThreshold = 20f;

    // ============================================================
    // 动画参数
    // ============================================================

    [Header("===== 动画参数 =====")]
    [SerializeField] private float swapDuration = 0.15f;
    [SerializeField] private float invalidSwapDuration = 0.20f;
    [SerializeField] private float destroyDuration = 0.15f;
    [SerializeField] private float comboShowDuration = 0.8f;

    // ============================================================
    // 事件
    // ============================================================

    public event Action<int, int> OnTileClicked;
    public event Action<int, int, int, int> OnTileSwapped;
    public event Action OnHintClicked;
    public event Action OnRefreshClicked;
    public event Action OnTimeAddClicked;
    public event Action OnRetryClicked;
    public event Action OnCloseClicked;

    // ============================================================
    // 内部状态
    // ============================================================

    private UI_GameMatch3GridPrefab[,] _tiles;
    private int _rows, _cols;

    // 字典 O(1)
    private readonly Dictionary<UI_GameMatch3GridPrefab, Vector2Int> _tileIndex =
        new Dictionary<UI_GameMatch3GridPrefab, Vector2Int>();

    // 对象池
    private readonly Queue<UI_GameMatch3GridPrefab> _tilePool =
        new Queue<UI_GameMatch3GridPrefab>();

    // 美术资源
    private bool _artLoaded;
    private Sprite[] _tileSprites;
    private Sprite _lineOverlayH;
    private Sprite _lineOverlayV;
    private Sprite _bombOverlay;
    private Sprite _magicBird;
    private Sprite _highlight;
    private Sprite _select;

    // 文字缓存
    private int _lastTimeSec = -1;
    private int _lastScore = int.MinValue;
    private int _lastLevel = int.MinValue;
    private int _lastHint = int.MinValue;
    private int _lastRefresh = int.MinValue;
    private int _lastTimeAdd = int.MinValue;

    // Combo
    private float _comboTimer;

    // 输入
    private UI_GameMatch3GridPrefab _pressTile;
    private bool _inputLocked;

    // ────────── 动画槽 ──────────
    private enum AnimType { None, Swap, InvalidSwap, Destroy }

    private AnimType _animType = AnimType.None;
    private float _animElapsed;
    private float _animDuration;
    private Action _animOnComplete;

    // Swap / InvalidSwap 共用参数
    private UI_GameMatch3GridPrefab _animA, _animB;
    private Vector2 _animPosA, _animPosB;
    private int _animR1, _animC1, _animR2, _animC2;

    // Destroy 参数（复用列表，零分配）
    private readonly List<Vector2Int> _destroyPositions = new List<Vector2Int>();

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (hintBtn != null) hintBtn.onClick.AddListener(OnHintBtnClick);
        if (refreshBtn != null) refreshBtn.onClick.AddListener(OnRefreshBtnClick);
        if (timeAddBtn != null) timeAddBtn.onClick.AddListener(OnTimeAddBtnClick);
        if (retryBtn != null) retryBtn.onClick.AddListener(OnRetryBtnClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseBtnClick);

        if (resultPanel != null) resultPanel.SetActive(false);
        if (comboText != null) comboText.gameObject.SetActive(false);
    }

    // 命名方法，避免 lambda 分配闭包
    private void OnHintBtnClick() { OnHintClicked?.Invoke(); }
    private void OnRefreshBtnClick() { OnRefreshClicked?.Invoke(); }
    private void OnTimeAddBtnClick() { OnTimeAddClicked?.Invoke(); }
    private void OnRetryBtnClick() { OnRetryClicked?.Invoke(); }
    private void OnCloseBtnClick() { OnCloseClicked?.Invoke(); }

    // ============================================================
    // Update：驱动动画槽 + Combo 计时
    // ============================================================

    private void Update()
    {
        TickAnimation();

        if (comboText != null && comboText.gameObject.activeSelf)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f) comboText.gameObject.SetActive(false);
        }
    }

    private void TickAnimation()
    {
        if (_animType == AnimType.None) return;

        _animElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_animElapsed / _animDuration);

        switch (_animType)
        {
            case AnimType.Swap: TickSwapAnim(t); break;
            case AnimType.InvalidSwap: TickInvalidSwapAnim(t); break;
            case AnimType.Destroy: TickDestroyAnim(t); break;
        }

        if (t < 1f) return;

        // 动画完成
        if (_animType == AnimType.Swap)
        {
            // 同步交换数组引用和索引
            _tiles[_animR1, _animC1] = _animB;
            _tiles[_animR2, _animC2] = _animA;
            _tileIndex[_animA] = new Vector2Int(_animR2, _animC2);
            _tileIndex[_animB] = new Vector2Int(_animR1, _animC1);
        }

        var cb = _animOnComplete;
        _animType = AnimType.None;
        _animOnComplete = null;
        cb?.Invoke();
    }

    private void TickSwapAnim(float t)
    {
        if (_animA == null || _animB == null) return;
        float k = EaseInOutQuad(t);
        _animA.Rect.anchoredPosition = Vector2.Lerp(_animPosA, _animPosB, k);
        _animB.Rect.anchoredPosition = Vector2.Lerp(_animPosB, _animPosA, k);
    }

    private void TickInvalidSwapAnim(float t)
    {
        if (_animA == null || _animB == null) return;

        const float half = 0.5f;
        if (t < half)
        {
            float k = EaseInOutQuad(t / half);
            _animA.Rect.anchoredPosition = Vector2.Lerp(_animPosA, _animPosB, k);
            _animB.Rect.anchoredPosition = Vector2.Lerp(_animPosB, _animPosA, k);
        }
        else
        {
            float k = EaseInOutQuad((t - half) / half);
            _animA.Rect.anchoredPosition = Vector2.Lerp(_animPosB, _animPosA, k);
            _animB.Rect.anchoredPosition = Vector2.Lerp(_animPosA, _animPosB, k);
        }
    }

    private void TickDestroyAnim(float t)
    {
        float k = 1f - t;
        k = k * k; // 缓出，更快消失
        float scale = Mathf.Max(0.01f, k);

        for (int i = 0; i < _destroyPositions.Count; i++)
        {
            var tile = GetTile(_destroyPositions[i].x, _destroyPositions[i].y);
            if (tile != null) tile.Rect.localScale = Vector3.one * scale;
        }
    }

    private static float EaseInOutQuad(float t)
        => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

    // ============================================================
    // 动画接口（供 Manager 调用）
    // ============================================================

    public void BeginSwapAnim(int r1, int c1, int r2, int c2, Action onComplete)
    {
        var a = GetTile(r1, c1);
        var b = GetTile(r2, c2);
        if (a == null || b == null)
        {
            onComplete?.Invoke();
            return;
        }

        _animR1 = r1; _animC1 = c1; _animR2 = r2; _animC2 = c2;
        _animA = a; _animB = b;
        _animPosA = a.Rect.anchoredPosition;
        _animPosB = b.Rect.anchoredPosition;

        _animType = AnimType.Swap;
        _animElapsed = 0f;
        _animDuration = swapDuration;
        _animOnComplete = onComplete;
    }

    public void BeginInvalidSwapAnim(int r1, int c1, int r2, int c2, Action onComplete)
    {
        var a = GetTile(r1, c1);
        var b = GetTile(r2, c2);
        if (a == null || b == null)
        {
            onComplete?.Invoke();
            return;
        }

        _animR1 = r1; _animC1 = c1; _animR2 = r2; _animC2 = c2;
        _animA = a; _animB = b;
        _animPosA = a.Rect.anchoredPosition;
        _animPosB = b.Rect.anchoredPosition;

        _animType = AnimType.InvalidSwap;
        _animElapsed = 0f;
        _animDuration = invalidSwapDuration;
        _animOnComplete = onComplete;
    }

    public void BeginDestroyAnim(List<Vector2Int> positions, Action onComplete)
    {
        _destroyPositions.Clear();
        if (positions != null) _destroyPositions.AddRange(positions);

        _animType = AnimType.Destroy;
        _animElapsed = 0f;
        _animDuration = destroyDuration;
        _animOnComplete = onComplete;
    }

    /// <summary>
    /// 立即更新所有 tile 的显示（下落后调用）
    /// </summary>
    public void UpdateAll(int[,] board)
    {
        if (_tiles == null) return;

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                var tile = _tiles[r, c];
                if (tile == null) continue;
                tile.Rect.localScale = Vector3.one;
                tile.SetValue(board[r, c], GetIcon(board[r, c]), GetOverlay(board[r, c]), _magicBird);
            }
    }

    // ============================================================
    // 初始化
    // ============================================================

    public void InitBoard(int rows, int cols, int colorCount)
    {
        _rows = rows;
        _cols = cols;

        if (_artLoaded) return;

        if (useTestSprites)
        {
            LoadTestSprites(colorCount);
            _artLoaded = true;
        }
        else
        {
            StartCoroutine(LoadArtCoroutine(colorCount));
        }
    }

    private void LoadTestSprites(int colorCount)
    {
        _tileSprites = new Sprite[colorCount + 1];

        for (int i = 1; i <= colorCount; i++)
        {
            int listIndex = i - 1;
            if (listIndex < testTileSprites.Count)
                _tileSprites[i] = testTileSprites[listIndex];
            else
                Z_Logger.LogWarning($"[Match3View] 测试 Sprite 不足：缺少颜色 {i}");
        }

        _lineOverlayH = testLineOverlayH;
        _lineOverlayV = testLineOverlayV;
        _bombOverlay = testBombOverlay;
        _magicBird = testMagicBird;
        _highlight = testHighlight;
        _select = testSelect;
    }

    // 唯一的协程：加载 AA 资源（开局一次，不影响游戏循环）
    private System.Collections.IEnumerator LoadArtCoroutine(int colorCount)
    {
        _tileSprites = new Sprite[colorCount + 1];

        for (int i = 1; i <= colorCount; i++)
        {
            int idx = i;
            string path = string.Format(tileIconPathFormat, idx);
            yield return StartCoroutine(LoadSpriteCoroutine(path, s => _tileSprites[idx] = s));
        }

        yield return StartCoroutine(LoadSpriteCoroutine(lineOverlayHPath, s => _lineOverlayH = s));
        yield return StartCoroutine(LoadSpriteCoroutine(lineOverlayVPath, s => _lineOverlayV = s));
        yield return StartCoroutine(LoadSpriteCoroutine(bombOverlayPath, s => _bombOverlay = s));
        yield return StartCoroutine(LoadSpriteCoroutine(magicBirdPath, s => _magicBird = s));
        yield return StartCoroutine(LoadSpriteCoroutine(highlightPath, s => _highlight = s));
        yield return StartCoroutine(LoadSpriteCoroutine(selectPath, s => _select = s));

        _artLoaded = true;
    }

    private System.Collections.IEnumerator LoadSpriteCoroutine(string path, Action<Sprite> onLoaded)
    {
        if (string.IsNullOrEmpty(path)) { onLoaded?.Invoke(null); yield break; }

        bool done = false;
        Sprite result = null;

        AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
        {
            result = sprite;
            done = true;
        });

        float timer = 0f;
        while (!done && timer < loadTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        onLoaded?.Invoke(result);
    }

    // ============================================================
    // 棋盘生成 / 清理（对象池）
    // ============================================================

    public void BuildBoard(int[,] board)
    {
        ClearBoard();

        if (boardContainer == null || tilePrefab == null)
        {
            Z_Logger.LogError("[Match3View] boardContainer 或 tilePrefab 未绑定");
            return;
        }

        _tiles = new UI_GameMatch3GridPrefab[_rows, _cols];

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                var tile = GetTileFromPool();
                tile.Init(HandlePointerDown, HandlePointerExit, HandlePointerUp);
                tile.SetValue(board[r, c], GetIcon(board[r, c]), GetOverlay(board[r, c]), _magicBird);
                tile.SetSelectSprite(_select);
                _tiles[r, c] = tile;
                _tileIndex[tile] = new Vector2Int(r, c);
            }
        }
    }

    private UI_GameMatch3GridPrefab GetTileFromPool()
    {
        UI_GameMatch3GridPrefab tile;

        if (_tilePool.Count > 0)
        {
            tile = _tilePool.Dequeue();
        }
        else
        {
            tile = Instantiate(tilePrefab, boardContainer);
        }

        tile.transform.SetParent(boardContainer, false);
        tile.ResetForReuse();
        tile.gameObject.SetActive(true);
        return tile;
    }

    private void ReturnTileToPool(UI_GameMatch3GridPrefab tile)
    {
        if (tile == null) return;

        tile.CleanupForPool();
        tile.gameObject.SetActive(false);
        _tilePool.Enqueue(tile);
    }

    public void ClearBoard()
    {
        if (_tiles != null)
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    if (_tiles[r, c] != null)
                        ReturnTileToPool(_tiles[r, c]);

            _tiles = null;
        }

        _tileIndex.Clear();
        _pressTile = null;
    }

    // ============================================================
    // 美术资源查询
    // ============================================================

    private Sprite GetIcon(int value)
    {
        if (GameMatch3BoardLogic.IsMagicBird(value)) return _magicBird;
        int color = GameMatch3BoardLogic.GetColor(value);
        if (color <= 0 || _tileSprites == null || color >= _tileSprites.Length) return null;
        return _tileSprites[color];
    }

    private Sprite GetOverlay(int value)
    {
        int sp = GameMatch3BoardLogic.GetSpecial(value);
        return sp switch
        {
            GameMatch3BoardLogic.SPECIAL_LINE_H => _lineOverlayH,
            GameMatch3BoardLogic.SPECIAL_LINE_V => _lineOverlayV,
            GameMatch3BoardLogic.SPECIAL_BOMB => _bombOverlay,
            _ => null
        };
    }

    // ============================================================
    // 输入
    // ============================================================

    public void SetInputLocked(bool locked)
    {
        _inputLocked = locked;

        if (locked && _pressTile != null)
        {
            _pressTile.SetSelected(false);
            _pressTile.ForceEndPress();
            _pressTile = null;
        }
    }

    private void HandlePointerDown(UI_GameMatch3GridPrefab tile)
    {
        if (_inputLocked) return;
        if (_tiles == null || tile == null) return;
        if (_pressTile != null) return;

        _pressTile = tile;
        tile.SetSelected(true);
    }

    private void HandlePointerExit(UI_GameMatch3GridPrefab tile, Vector2 delta)
    {
        if (_inputLocked) return;
        if (_pressTile != tile) return;
        if (delta.magnitude < dragThreshold) return;

        var source = _pressTile;
        _pressTile = null;
        source.SetSelected(false);
        source.ForceEndPress();

        if (!_tileIndex.TryGetValue(source, out var coord)) return;

        Vector2Int dir = GetDragDirection(delta);
        if (dir == Vector2Int.zero) return;

        int tr = coord.x + dir.x;
        int tc = coord.y + dir.y;

        if (tr < 0 || tr >= _rows || tc < 0 || tc >= _cols) return;

        OnTileSwapped?.Invoke(coord.x, coord.y, tr, tc);
    }

    private void HandlePointerUp(UI_GameMatch3GridPrefab tile, Vector2 delta)
    {
        if (_pressTile != tile) return;

        var source = _pressTile;
        _pressTile = null;

        if (!_tileIndex.TryGetValue(source, out var coord))
        {
            source.SetSelected(false);
            return;
        }

        OnTileClicked?.Invoke(coord.x, coord.y);
    }

    private Vector2Int GetDragDirection(Vector2 delta)
    {
        if (delta.magnitude < dragThreshold) return Vector2Int.zero;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? new Vector2Int(0, 1) : new Vector2Int(0, -1);
        else
            return delta.y > 0 ? new Vector2Int(-1, 0) : new Vector2Int(1, 0);
    }

    // ============================================================
    // 提示 / 选中 / Combo
    // ============================================================

    public void ShowHint(int r, int c) { GetTile(r, c)?.SetHighlight(true, _highlight); }
    public void ClearHint(int r, int c) { GetTile(r, c)?.SetHighlight(false, null); }

    public void ClearHints()
    {
        if (_tiles == null) return;
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                _tiles[r, c]?.SetHighlight(false, null);
    }

    public void SetTileSelected(int r, int c, bool selected)
        => GetTile(r, c)?.SetSelected(selected);

    public void ClearAllSelected()
    {
        if (_tiles == null) return;
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                _tiles[r, c]?.SetSelected(false);
    }

    public void ShowCombo(int combo)
    {
        if (comboText == null) return;
        comboText.text = $"Combo x{combo}";
        comboText.gameObject.SetActive(true);
        _comboTimer = comboShowDuration;
    }

    // ============================================================
    // UI 刷新（带缓存）
    // ============================================================

    public void SetScore(int score)
    {
        if (scoreText == null || score == _lastScore) return;
        _lastScore = score;
        scoreText.text = score.ToString();
    }

    public void SetTime(float time)
    {
        if (timeText == null) return;

        int sec = Mathf.CeilToInt(time);
        if (sec == _lastTimeSec) return;
        _lastTimeSec = sec;
        timeText.text = $"{sec / 60:00}:{sec % 60:00}";
    }

    public void SetLevel(int level)
    {
        if (levelText == null || level == _lastLevel) return;
        _lastLevel = level;
        levelText.text = $"等级 {level}";
    }

    public void SetProps(int hint, int refresh, int timeAdd)
    {
        if (hintBtnText != null && hint != _lastHint)
        {
            _lastHint = hint;
            hintBtnText.text = $"提示({hint})";
        }
        if (refreshBtnText != null && refresh != _lastRefresh)
        {
            _lastRefresh = refresh;
            refreshBtnText.text = $"刷新({refresh})";
        }
        if (timeAddBtnText != null && timeAdd != _lastTimeAdd)
        {
            _lastTimeAdd = timeAdd;
            timeAddBtnText.text = $"时间+({timeAdd})";
        }

        if (hintBtn != null) hintBtn.interactable = hint > 0;
        if (refreshBtn != null) refreshBtn.interactable = refresh > 0;
        if (timeAddBtn != null) timeAddBtn.interactable = timeAdd > 0;
    }

    public void ShowResult(int score, int best)
    {
        if (resultPanel != null) resultPanel.SetActive(true);
        if (resultScoreText != null) resultScoreText.text = score.ToString();
        if (resultBestText != null) resultBestText.text = best.ToString();
    }

    public void HideResult()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // ============================================================
    // 内部
    // ============================================================

    private UI_GameMatch3GridPrefab GetTile(int r, int c)
    {
        if (_tiles == null) return null;
        if (r < 0 || r >= _rows || c < 0 || c >= _cols) return null;
        return _tiles[r, c];
    }

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[Match3View] {msg}");
    }
}
