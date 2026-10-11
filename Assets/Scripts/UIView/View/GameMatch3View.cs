using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 消消乐棋盘表现层
/// 
/// 输入逻辑：
///   - PointerDown：记录 _pressTile，视觉高亮
///   - PointerExit：若位移超过阈值 → 立即判断方向 → 触发 OnTileSwapped，并结束本次操作
///   - PointerUp：若操作已结束则忽略；否则视为轻点 → 触发 OnTileClicked
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
    [Tooltip("屏幕位移小于该像素值视为点击，大于则视为滑动")]
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

    private Sprite[] _tileSprites;
    private Sprite _lineOverlayH;
    private Sprite _lineOverlayV;
    private Sprite _bombOverlay;
    private Sprite _magicBird;
    private Sprite _highlight;
    private Sprite _select;

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

        if (useTestSprites)
        {
            LoadTestSprites(colorCount);
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
    // 棋盘生成 / 刷新
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
                var tile = Instantiate(tilePrefab, boardContainer);
                tile.Init(HandlePointerDown, HandlePointerExit, HandlePointerUp);
                tile.SetValue(board[r, c], GetIcon(board[r, c]), GetOverlay(board[r, c]), _magicBird);
                tile.SetSelectSprite(_select);
                _tiles[r, c] = tile;
            }
        }
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
                tile.SetSelectSprite(_select);
            }
    }

    private void ClearBoard()
    {
        if (_tiles != null)
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    if (_tiles[r, c] != null) Destroy(_tiles[r, c].gameObject);
        }

        if (boardContainer != null)
        {
            for (int i = boardContainer.childCount - 1; i >= 0; i--)
                Destroy(boardContainer.GetChild(i).gameObject);
        }

        _tiles = null;
        _pressTile = null;
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

    /// <summary>
    /// 指针移出该格子 → 若位移超阈值，立即判断方向触发交换
    /// </summary>
    private void HandlePointerExit(UI_GameMatch3GridPrefab tile, Vector2 delta)
    {
        if (_inputLocked) return;
        if (_pressTile != tile) return;

        // 位移不够 → 不动，等 PointerUp
        if (delta.magnitude < dragThreshold) return;

        var source = _pressTile;
        _pressTile = null;
        source.SetSelected(false);
        source.ForceEndPress();   // 结束按下状态

        var coord = FindCoord(source);
        if (coord == null) return;

        Vector2Int dir = GetDragDirection(delta);
        if (dir == Vector2Int.zero) return;

        int tr = coord.Value.x + dir.x;
        int tc = coord.Value.y + dir.y;

        if (tr < 0 || tr >= _rows || tc < 0 || tc >= _cols) return;

        // 立即触发交换，本次操作结束
        OnTileSwapped?.Invoke(coord.Value.x, coord.Value.y, tr, tc);
    }

    /// <summary>
    /// 松手 → 若操作还未结束，视为轻点
    /// </summary>
    private void HandlePointerUp(UI_GameMatch3GridPrefab tile, Vector2 delta)
    {
        if (_pressTile != tile) return;

        var source = _pressTile;
        _pressTile = null;

        var coord = FindCoord(source);
        if (coord == null)
        {
            source.SetSelected(false);
            return;
        }

        // 松手时位移小于阈值 → 视为点击（不清除选中，交给 Manager 决定）
        OnTileClicked?.Invoke(coord.Value.x, coord.Value.y);
    }

    /// <summary>
    /// 屏幕 delta → 网格方向
    /// 
    /// 网格坐标：row 向下增大，col 向右增大
    /// 屏幕坐标：x 向右增大，y 向上增大
    /// 
    /// 屏幕 x > 0 → col + 1
    /// 屏幕 x < 0 → col - 1
    /// 屏幕 y > 0 → row - 1
    /// 屏幕 y < 0 → row + 1
    /// </summary>
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

        _tiles[r1, c1] = b;
        _tiles[r2, c2] = a;
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
            foreach (var p in positions)
            {
                var tile = GetTile(p.x, p.y);
                if (tile != null) tile.Rect.localScale = Vector3.one * Mathf.Max(0.01f, k);
            }
            yield return null;
        }

        foreach (var p in positions)
        {
            var tile = GetTile(p.x, p.y);
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

    public void SetTileSelected(int r, int c, bool selected) { GetTile(r, c)?.SetSelected(selected); }

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
    // UI 刷新
    // ============================================================

    public void SetScore(int score) { if (scoreText != null) scoreText.text = score.ToString(); }

    public void SetTime(float time)
    {
        if (timeText == null) return;
        int total = Mathf.CeilToInt(time);
        timeText.text = $"{total / 60:00}:{total % 60:00}";
    }

    public void SetLevel(int level) { if (levelText != null) levelText.text = $"等级 {level}"; }

    public void SetProps(int hint, int refresh, int timeAdd)
    {
        if (hintBtnText != null) hintBtnText.text = $"提示({hint})";
        if (refreshBtnText != null) refreshBtnText.text = $"刷新({refresh})";
        if (timeAddBtnText != null) timeAddBtnText.text = $"时间+({timeAdd})";

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

    private Vector2Int? FindCoord(UI_GameMatch3GridPrefab tile)
    {
        if (_tiles == null || tile == null) return null;
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (_tiles[r, c] == tile) return new Vector2Int(r, c);
        return null;
    }

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[Match3View] {msg}");
    }
}
