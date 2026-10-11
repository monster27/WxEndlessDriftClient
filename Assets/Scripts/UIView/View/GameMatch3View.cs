using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 消消乐棋盘表现层（性能优化版）
/// 
/// 优化点：
///   1. 对象池复用 tile
///   2. FindCoord 用字典 O(1)
///   3. 美术资源只加载一次
///   4. 文字只在变化时更新（在 Manager 里）
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

    // ★ 反向索引：tile → 坐标，O(1) 查
    private Dictionary<UI_GameMatch3GridPrefab, Vector2Int> _tileIndex =
        new Dictionary<UI_GameMatch3GridPrefab, Vector2Int>();

    // ★ 对象池
    private Queue<UI_GameMatch3GridPrefab> _tilePool = new Queue<UI_GameMatch3GridPrefab>();

    // 美术资源（只加载一次）
    private bool _artLoaded;
    private Sprite[] _tileSprites;
    private Sprite _lineOverlayH;
    private Sprite _lineOverlayV;
    private Sprite _bombOverlay;
    private Sprite _magicBird;
    private Sprite _highlight;
    private Sprite _select;

    // ★ 文字缓存（避免每帧赋值）
    private int _lastScore = int.MinValue;
    private int _lastTimeSec = int.MinValue;
    private int _lastLevel = int.MinValue;
    private int _lastHint = int.MinValue;
    private int _lastRefresh = int.MinValue;
    private int _lastTimeAdd = int.MinValue;

    private Coroutine _comboCoroutine;

    // 输入
    private UI_GameMatch3GridPrefab _pressTile;
    private bool _inputLocked;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (hintBtn != null) hintBtn.onClick.AddListener(() => OnHintClicked?.Invoke());
        if (refreshBtn != null) refreshBtn.onClick.AddListener(() => OnRefreshClicked?.Invoke());
        if (timeAddBtn != null) timeAddBtn.onClick.AddListener(() => OnTimeAddClicked?.Invoke());
        if (retryBtn != null) retryBtn.onClick.AddListener(() => OnRetryClicked?.Invoke());
        if (closeBtn != null) closeBtn.onClick.AddListener(() => OnCloseClicked?.Invoke());

        if (resultPanel != null) resultPanel.SetActive(false);
        if (comboText != null) comboText.gameObject.SetActive(false);
    }

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

    // ============================================================
    // 初始化
    // ============================================================

    public void InitBoard(int rows, int cols, int colorCount)
    {
        _rows = rows;
        _cols = cols;

        // ★ 只在第一次加载美术资源
        if (_artLoaded) return;

        if (useTestSprites)
        {
            LoadTestSprites(colorCount);
            _artLoaded = true;
            LogDebug("使用测试 Sprite");
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

    private IEnumerator LoadArtCoroutine(int colorCount)
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
        LogDebug("AA 美术资源加载完成");
    }

    private IEnumerator LoadSpriteCoroutine(string path, Action<Sprite> onLoaded)
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

        if (result == null)
            Z_Logger.LogWarning($"[Match3View] AA 加载失败或超时: {path}");

        onLoaded?.Invoke(result);
    }

    // ============================================================
    // 棋盘生成 / 刷新（对象池版）
    // ============================================================

    public void BuildBoard(int[,] board)
    {
        if (boardContainer == null || tilePrefab == null)
        {
            Z_Logger.LogError("[Match3View] boardContainer 或 tilePrefab 未绑定");
            return;
        }

        // 首次创建数组
        if (_tiles == null)
            _tiles = new UI_GameMatch3GridPrefab[_rows, _cols];

        _tileIndex.Clear();
        _pressTile = null;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                var tile = GetOrCreateTile();
                tile.transform.SetParent(boardContainer, false);
                tile.Init(HandlePointerDown, HandlePointerExit, HandlePointerUp);
                tile.SetValue(board[r, c], GetIcon(board[r, c]), GetOverlay(board[r, c]), _magicBird);
                tile.SetSelectSprite(_select);
                _tiles[r, c] = tile;
                _tileIndex[tile] = new Vector2Int(r, c);
            }
        }
    }

    /// <summary>
    /// 从对象池取一个 tile，没有就创建
    /// </summary>
    private UI_GameMatch3GridPrefab GetOrCreateTile()
    {
        if (_tilePool.Count > 0)
        {
            var tile = _tilePool.Dequeue();
            tile.gameObject.SetActive(true);
            return tile;
        }
        return Instantiate(tilePrefab, boardContainer);
    }

    /// <summary>
    /// 回收所有 tile 到对象池
    /// </summary>
    private void RecycleAllTiles()
    {
        if (_tiles == null) return;

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                var tile = _tiles[r, c];
                if (tile == null) continue;
                tile.gameObject.SetActive(false);
                _tilePool.Enqueue(tile);
                _tiles[r, c] = null;
            }

        _tileIndex.Clear();
        _pressTile = null;
    }

    public void UpdateAll(int[,] board)
    {
        if (_tiles == null) return;

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                var tile = _tiles[r, c];
                if (tile == null) continue;
                tile.SetValue(board[r, c], GetIcon(board[r, c]), GetOverlay(board[r, c]), _magicBird);
            }
    }

    /// <summary>
    /// 外部调用：回收所有 tile（StartGame 时用）
    /// </summary>
    public void ClearBoard()
    {
        RecycleAllTiles();
    }

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
    // 输入处理
    // ============================================================

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
    // 动画
    // ============================================================

    public IEnumerator PlaySwapAnim(int r1, int c1, int r2, int c2)
    {
        var a = GetTile(r1, c1);
        var b = GetTile(r2, c2);
        if (a == null || b == null) yield break;

        var posA = a.Rect.anchoredPosition;
        var posB = b.Rect.anchoredPosition;

        float duration = 0.15f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            a.Rect.anchoredPosition = Vector2.Lerp(posA, posB, k);
            b.Rect.anchoredPosition = Vector2.Lerp(posB, posA, k);
            yield return null;
        }
        a.Rect.anchoredPosition = posB;
        b.Rect.anchoredPosition = posA;

        // 更新数组和索引
        _tiles[r1, c1] = b;
        _tiles[r2, c2] = a;
        _tileIndex[a] = new Vector2Int(r2, c2);
        _tileIndex[b] = new Vector2Int(r1, c1);
    }

    public IEnumerator PlayInvalidSwapAnim(int r1, int c1, int r2, int c2)
    {
        var a = GetTile(r1, c1);
        var b = GetTile(r2, c2);
        if (a == null || b == null) yield break;

        var posA = a.Rect.anchoredPosition;
        var posB = b.Rect.anchoredPosition;

        float t = 0f;
        float dur = 0.1f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            a.Rect.anchoredPosition = Vector2.Lerp(posA, posB, k);
            b.Rect.anchoredPosition = Vector2.Lerp(posB, posA, k);
            yield return null;
        }

        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            a.Rect.anchoredPosition = Vector2.Lerp(posB, posA, k);
            b.Rect.anchoredPosition = Vector2.Lerp(posA, posB, k);
            yield return null;
        }

        a.Rect.anchoredPosition = posA;
        b.Rect.anchoredPosition = posB;
    }

    public IEnumerator PlayDestroyAnim(List<Vector2Int> positions)
    {
        if (positions == null || positions.Count == 0) yield break;

        float duration = 0.15f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = 1f - (t / duration);
            for (int i = 0; i < positions.Count; i++)
            {
                var tile = GetTile(positions[i].x, positions[i].y);
                if (tile != null) tile.Rect.localScale = Vector3.one * Mathf.Max(0.01f, k);
            }
            yield return null;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            var tile = GetTile(positions[i].x, positions[i].y);
            if (tile != null) tile.Rect.localScale = Vector3.one;
        }
    }

    public IEnumerator PlayDropRefillAnim(int[,] newBoard)
    {
        UpdateAll(newBoard);
        yield return null;
    }

    public IEnumerator PlayShuffleAnim(int[,] newBoard)
    {
        UpdateAll(newBoard);
        yield return null;
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

        if (_comboCoroutine != null) StopCoroutine(_comboCoroutine);
        _comboCoroutine = StartCoroutine(HideComboRoutine());
    }

    private IEnumerator HideComboRoutine()
    {
        yield return new WaitForSeconds(0.8f);
        if (comboText != null) comboText.gameObject.SetActive(false);
    }

    // ============================================================
    // UI 刷新（带缓存，避免无意义重建）
    // ============================================================

    public void SetScore(int score)
    {
        if (scoreText == null) return;
        if (score == _lastScore) return;   // ★ 无变化直接返回
        _lastScore = score;
        scoreText.text = score.ToString();
    }

    public void SetTime(float time)
    {
        if (timeText == null) return;

        int total = Mathf.CeilToInt(time);
        if (total == _lastTimeSec) return;  // ★ 秒数没变直接返回
        _lastTimeSec = total;
        timeText.text = $"{total / 60:00}:{total % 60:00}";
    }

    public void SetLevel(int level)
    {
        if (levelText == null) return;
        if (level == _lastLevel) return;
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
