using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 消消乐流程控制层
/// </summary>
public class Match3Manager : MonoBehaviour
{
    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 核心引用 =====")]
    [SerializeField] private GameMatch3View view;
    [SerializeField] private TextAsset configJson;

    [Header("===== 外部入参 =====")]
    [SerializeField] private int playerLevel = 1;

    private const string BEST_SCORE_KEY = "Match3_BestScore";

    private GameMatch3ConfigWrapper _config;

    private int[,] _board;
    private int _rows, _cols, _colorCount;

    private int _score;
    private float _timeLeft;
    private bool _isResolving;
    private bool _isGameOver;
    private bool _isTimeUp;

    private int _hintCount, _refreshCount, _timeAddCount;

    private List<Vector2Int[]> _cachedMoves = new List<Vector2Int[]>();
    private int _hintIndex = -1;

    private Vector2Int? _selected;
    private int _bestScore;
    private Coroutine _swapCoroutine;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        LoadConfig();
        LoadBestScore();
        SubscribeViewEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeViewEvents();
        if (_swapCoroutine != null) { StopCoroutine(_swapCoroutine); _swapCoroutine = null; }
    }

    private void Update()
    {
        if (_isGameOver) return;

        if (_timeLeft > 0f)
        {
            _timeLeft -= Time.deltaTime;
            if (_timeLeft < 0f) _timeLeft = 0f;
        }

        view?.SetTime(_timeLeft);

        if (_timeLeft <= 0f && !_isTimeUp) _isTimeUp = true;

        if (_isTimeUp && !_isResolving) EndGame();
    }

    // ============================================================
    // 事件订阅
    // ============================================================

    private void SubscribeViewEvents()
    {
        if (view == null) return;

        view.OnTileClicked += HandleTileClicked;
        view.OnTileSwapped += HandleTileSwapped;
        view.OnHintClicked += HandleHintClicked;
        view.OnRefreshClicked += HandleRefreshClicked;
        view.OnTimeAddClicked += HandleTimeAddClicked;
        view.OnRetryClicked += HandleRetryClicked;
        view.OnCloseClicked += HandleCloseClicked;
    }

    private void UnsubscribeViewEvents()
    {
        if (view == null) return;

        view.OnTileClicked -= HandleTileClicked;
        view.OnTileSwapped -= HandleTileSwapped;
        view.OnHintClicked -= HandleHintClicked;
        view.OnRefreshClicked -= HandleRefreshClicked;
        view.OnTimeAddClicked -= HandleTimeAddClicked;
        view.OnRetryClicked -= HandleRetryClicked;
        view.OnCloseClicked -= HandleCloseClicked;
    }

    // ============================================================
    // 初始化
    // ============================================================

    private void LoadConfig()
    {
        if (configJson == null)
        {
            Z_Logger.LogError("[Match3Manager] configJson 未绑定，使用默认配置");
            _config = new GameMatch3ConfigWrapper();
            return;
        }

        try
        {
            _config = JsonUtility.FromJson<GameMatch3ConfigWrapper>(configJson.text);
            if (_config == null) throw new Exception("解析结果为 null");
        }
        catch (Exception e)
        {
            Z_Logger.LogError($"[Match3Manager] JSON 解析失败: {e.Message}");
            _config = new GameMatch3ConfigWrapper();
        }
    }

    private void LoadBestScore() { _bestScore = PlayerPrefs.GetInt(BEST_SCORE_KEY, 0); }

    private void SaveBestScore()
    {
        if (_score <= _bestScore) return;
        _bestScore = _score;
        // TODO: 后期迁移到服务器
        PlayerPrefs.SetInt(BEST_SCORE_KEY, _bestScore);
        PlayerPrefs.Save();
    }

    // ============================================================
    // 开一局
    // ============================================================

    public void StartGame(int level = 1)
    {
        playerLevel = Mathf.Max(1, level);

        _rows = _config.board.rows;
        _cols = _config.board.cols;
        _colorCount = _config.board.colorCount;

        _score = 0;
        _timeLeft = _config.time.initialTime;
        _isResolving = false;
        _isGameOver = false;
        _isTimeUp = false;
        _selected = null;
        _cachedMoves.Clear();
        _hintIndex = -1;

        _hintCount = _config.props.hintBaseCount * playerLevel;
        _refreshCount = _config.props.refreshBaseCount * playerLevel;
        _timeAddCount = _config.props.timeAddBaseCount * playerLevel;

        _board = GameMatch3BoardLogic.GenerateBoard(_rows, _cols, _colorCount);

        if (view != null)
        {
            view.InitBoard(_rows, _cols, _colorCount);
            view.BuildBoard(_board);
            view.ClearHints();
            view.ClearAllSelected();
            view.HideResult();
            view.SetInputLocked(false);
        }

        RefreshAllUI();
        LogDebug("游戏开始");
    }

    private void RefreshAllUI()
    {
        view?.SetScore(_score);
        view?.SetTime(_timeLeft);
        view?.SetLevel(playerLevel);
        view?.SetProps(_hintCount, _refreshCount, _timeAddCount);
    }

    // ============================================================
    // 玩家点击（轻点）
    // ============================================================

    private void HandleTileClicked(int r, int c)
    {
        if (_isGameOver || _isResolving) return;
        if (!IsInBoard(r, c)) return;

        ClearHintHighlight();

        if (_selected == null)
        {
            _selected = new Vector2Int(r, c);
            view.SetTileSelected(r, c, true);
            return;
        }

        var first = _selected.Value;

        if (first.x == r && first.y == c)
        {
            view.SetTileSelected(first.x, first.y, false);
            _selected = null;
            return;
        }

        if (!IsAdjacent(first.x, first.y, r, c))
        {
            view.SetTileSelected(first.x, first.y, false);
            _selected = new Vector2Int(r, c);
            view.SetTileSelected(r, c, true);
            return;
        }

        view.SetTileSelected(first.x, first.y, false);
        _selected = null;
        StartSwapRoutine(first.x, first.y, r, c);
    }

    // ============================================================
    // 玩家滑动
    // ============================================================

    private void HandleTileSwapped(int r1, int c1, int r2, int c2)
    {
        if (_isGameOver || _isResolving) return;
        if (!IsInBoard(r1, c1) || !IsInBoard(r2, c2)) return;
        if (!IsAdjacent(r1, c1, r2, c2)) return;

        // 清除所有选中（滑动时可能有前一次点击残留的选中态）
        ClearAllSelections();
        ClearHintHighlight();

        StartSwapRoutine(r1, c1, r2, c2);
    }

    // ============================================================
    // 交换流程
    // ============================================================

    private void StartSwapRoutine(int r1, int c1, int r2, int c2)
    {
        if (_swapCoroutine != null) StopCoroutine(_swapCoroutine);
        _swapCoroutine = StartCoroutine(TrySwapRoutine(r1, c1, r2, c2));
    }

    private IEnumerator TrySwapRoutine(int r1, int c1, int r2, int c2)
    {
        _isResolving = true;
        view.SetInputLocked(true);

        // ★ 进入交换流程前，清空所有选中态
        ClearAllSelections();
        ClearHintHighlight();

        int v1 = _board[r1, c1];
        int v2 = _board[r2, c2];
        int s1 = GameMatch3BoardLogic.GetSpecial(v1);
        int s2 = GameMatch3BoardLogic.GetSpecial(v2);

        bool isSpecial1 = s1 != GameMatch3BoardLogic.SPECIAL_NONE;
        bool isSpecial2 = s2 != GameMatch3BoardLogic.SPECIAL_NONE;

        // 逻辑交换
        GameMatch3BoardLogic.Swap(_board, r1, c1, r2, c2);

        // ─────────────────────────────────────────────────────────
        // 情况 A：两个都是特殊方块 → 组合效果
        // ─────────────────────────────────────────────────────────
        if (isSpecial1 && isSpecial2)
        {
            yield return StartCoroutine(view.PlaySwapAnim(r1, c1, r2, c2));
            yield return StartCoroutine(ResolveSpecialCombo(r1, c1, r2, c2, s1, s2));
            yield return StartCoroutine(ResolveLoop());

            FinishSwapRoutine();
            yield break;
        }

        // ─────────────────────────────────────────────────────────
        // 情况 B：魔力鸟 + 普通
        // ─────────────────────────────────────────────────────────
        bool hasMagic = s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD
                     || s2 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD;

        if (hasMagic)
        {
            yield return StartCoroutine(view.PlaySwapAnim(r1, c1, r2, c2));
            yield return StartCoroutine(ResolveMagicBirdSwap(r1, c1, r2, c2));
            yield return StartCoroutine(ResolveLoop());

            FinishSwapRoutine();
            yield break;
        }

        // ─────────────────────────────────────────────────────────
        // 情况 C：普通交换 → 检查三连
        // ─────────────────────────────────────────────────────────
        bool isValid = GameMatch3BoardLogic.HasMatchAt(_board, r1, c1)
                    || GameMatch3BoardLogic.HasMatchAt(_board, r2, c2);

        if (!isValid)
        {
            GameMatch3BoardLogic.Swap(_board, r1, c1, r2, c2);
            yield return StartCoroutine(view.PlayInvalidSwapAnim(r1, c1, r2, c2));

            FinishSwapRoutine();
            yield break;
        }

        yield return StartCoroutine(view.PlaySwapAnim(r1, c1, r2, c2));
        yield return StartCoroutine(ResolveLoop());

        FinishSwapRoutine();
    }

    /// <summary>
    /// 统一结束交换流程
    /// </summary>
    private void FinishSwapRoutine()
    {
        // ★ 再次保证所有选中态清空
        ClearAllSelections();

        _isResolving = false;
        view.SetInputLocked(false);
        _swapCoroutine = null;
    }

    // ============================================================
    // 魔力鸟 + 普通方块
    // ============================================================

    private IEnumerator ResolveMagicBirdSwap(int r1, int c1, int r2, int c2)
    {
        int v1 = _board[r1, c1];
        int v2 = _board[r2, c2];
        bool v1Magic = GameMatch3BoardLogic.IsMagicBird(v1);
        bool v2Magic = GameMatch3BoardLogic.IsMagicBird(v2);

        List<Vector2Int> toClear = new List<Vector2Int>();

        if (v1Magic && v2Magic)
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    toClear.Add(new Vector2Int(r, c));
        }
        else
        {
            int targetColor = v1Magic
                ? GameMatch3BoardLogic.GetColor(v2)
                : GameMatch3BoardLogic.GetColor(v1);

            toClear = GameMatch3BoardLogic.CollectMagicBirdEffect(_board, targetColor);
            toClear.Add(v1Magic ? new Vector2Int(r1, c1) : new Vector2Int(r2, c2));
        }

        if (toClear.Count == 0) yield break;

        int gained = GameMatch3BoardLogic.CalcSpecialScore(
            GameMatch3BoardLogic.SPECIAL_MAGICBIRD, toClear.Count, 1,
            _config.score, _config.specialTiles);
        _score += gained;
        view.SetScore(_score);

        foreach (var p in toClear) _board[p.x, p.y] = 0;

        yield return StartCoroutine(view.PlayDestroyAnim(toClear));

        GameMatch3BoardLogic.DropTiles(_board);
        GameMatch3BoardLogic.RefillBoard(_board, _colorCount);

        yield return StartCoroutine(view.PlayDropRefillAnim(_board));
    }

    // ============================================================
    // 特殊 + 特殊 组合
    // ============================================================

    private IEnumerator ResolveSpecialCombo(int r1, int c1, int r2, int c2, int s1, int s2)
    {
        var clearSet = new HashSet<Vector2Int>();
        var specialQueue = new Queue<Vector2Int>();

        clearSet.Add(new Vector2Int(r1, c1));
        clearSet.Add(new Vector2Int(r2, c2));

        // 情况 1：双魔力鸟 → 全屏
        if (s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD
         && s2 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD)
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    clearSet.Add(new Vector2Int(r, c));
        }
        // 情况 2：魔力鸟 + 其他特殊
        else if (s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD
              || s2 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD)
        {
            int otherColor = s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD
                ? GameMatch3BoardLogic.GetColor(_board[r1, c1])
                : GameMatch3BoardLogic.GetColor(_board[r2, c2]);

            var sameColor = GameMatch3BoardLogic.CollectMagicBirdEffect(_board, otherColor);
            foreach (var p in sameColor)
            {
                clearSet.Add(p);

                if (GameMatch3BoardLogic.IsSpecial(_board[p.x, p.y]))
                    specialQueue.Enqueue(p);
            }
        }
        // 情况 3：直线 + 直线 → 十字
        else if (IsLineType(s1) && IsLineType(s2))
        {
            AddRow(clearSet, r1);
            AddCol(clearSet, c1);
        }
        // 情况 4：直线 + 爆炸 → 十字 + 3×3
        else if (IsLineType(s1) && s2 == GameMatch3BoardLogic.SPECIAL_BOMB)
        {
            AddRow(clearSet, r1);
            AddCol(clearSet, c1);
            AddSquare(clearSet, r2, c2, 1);
        }
        else if (s1 == GameMatch3BoardLogic.SPECIAL_BOMB && IsLineType(s2))
        {
            AddRow(clearSet, r2);
            AddCol(clearSet, c2);
            AddSquare(clearSet, r1, c1, 1);
        }
        // 情况 5：爆炸 + 爆炸 → 5×5
        else if (s1 == GameMatch3BoardLogic.SPECIAL_BOMB
              && s2 == GameMatch3BoardLogic.SPECIAL_BOMB)
        {
            AddSquare(clearSet, r1, c1, 2);
        }

        foreach (var p in clearSet)
        {
            if (!IsInBoard(p.x, p.y)) continue;
            if (GameMatch3BoardLogic.IsSpecial(_board[p.x, p.y]))
            {
                if (!specialQueue.Contains(p))
                    specialQueue.Enqueue(p);
            }
        }

        ProcessSpecialTriggers(specialQueue, clearSet);

        int gained = GameMatch3BoardLogic.CalcScore(clearSet.Count, 1, _config.score);
        _score += gained;
        view.SetScore(_score);

        foreach (var p in clearSet) _board[p.x, p.y] = 0;

        yield return StartCoroutine(view.PlayDestroyAnim(new List<Vector2Int>(clearSet)));

        GameMatch3BoardLogic.DropTiles(_board);
        GameMatch3BoardLogic.RefillBoard(_board, _colorCount);

        yield return StartCoroutine(view.PlayDropRefillAnim(_board));
    }

    private bool IsLineType(int s)
        => s == GameMatch3BoardLogic.SPECIAL_LINE_H
        || s == GameMatch3BoardLogic.SPECIAL_LINE_V;

    private void AddRow(HashSet<Vector2Int> set, int row)
    {
        for (int c = 0; c < _cols; c++) set.Add(new Vector2Int(row, c));
    }

    private void AddCol(HashSet<Vector2Int> set, int col)
    {
        for (int r = 0; r < _rows; r++) set.Add(new Vector2Int(r, col));
    }

    private void AddSquare(HashSet<Vector2Int> set, int centerR, int centerC, int radius)
    {
        for (int dr = -radius; dr <= radius; dr++)
            for (int dc = -radius; dc <= radius; dc++)
            {
                int r = centerR + dr, c = centerC + dc;
                if (IsInBoard(r, c)) set.Add(new Vector2Int(r, c));
            }
    }

    // ============================================================
    // 连锁循环
    // ============================================================

    private IEnumerator ResolveLoop()
    {
        int combo = 0;

        // ★ 进入连锁前，清空所有选中态
        ClearAllSelections();
        ClearHintHighlight();

        while (true)
        {
            var matches = GameMatch3BoardLogic.FindAllMatches(_board);
            if (matches.Count == 0) break;

            combo++;

            // ★ 每轮消除前再清一次，防止动画期间用户点击产生的选中残留
            ClearAllSelections();

            var clearSet = new HashSet<Vector2Int>();
            var specialQueue = new Queue<Vector2Int>();

            foreach (var p in matches)
            {
                clearSet.Add(p);
                int v = _board[p.x, p.y];
                if (GameMatch3BoardLogic.IsSpecial(v))
                    specialQueue.Enqueue(p);
            }

            ProcessSpecialTriggers(specialQueue, clearSet);

            var spawns = GameMatch3BoardLogic.FindSpecialSpawns(_board, matches);

            int gained = GameMatch3BoardLogic.CalcScore(clearSet.Count, combo, _config.score);
            _score += gained;
            view.SetScore(_score);

            if (combo >= 2) view.ShowCombo(combo);

            foreach (var p in clearSet) _board[p.x, p.y] = 0;

            foreach (var s in spawns)
                _board[s.position.x, s.position.y] =
                    GameMatch3BoardLogic.MakeValue(s.color, s.specialType);

            yield return StartCoroutine(view.PlayDestroyAnim(new List<Vector2Int>(clearSet)));

            GameMatch3BoardLogic.DropTiles(_board);
            GameMatch3BoardLogic.RefillBoard(_board, _colorCount);

            yield return StartCoroutine(view.PlayDropRefillAnim(_board));
        }

        // 死局 → 洗牌
        if (!GameMatch3BoardLogic.HasValidMove(_board))
        {
            LogDebug("死局，自动洗牌");
            yield return StartCoroutine(ShuffleRoutine());
        }

        // ★ 连锁结束后，清空所有状态
        ClearAllSelections();
        view.ClearHints();
        _cachedMoves.Clear();
        _hintIndex = -1;
    }

    private void ProcessSpecialTriggers(Queue<Vector2Int> queue, HashSet<Vector2Int> clearSet)
    {
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            int v = _board[p.x, p.y];
            int special = GameMatch3BoardLogic.GetSpecial(v);

            List<Vector2Int> effectArea = null;

            switch (special)
            {
                case GameMatch3BoardLogic.SPECIAL_LINE_H:
                    effectArea = GameMatch3BoardLogic.CollectLineHEffect(_board, p);
                    break;

                case GameMatch3BoardLogic.SPECIAL_LINE_V:
                    effectArea = GameMatch3BoardLogic.CollectLineVEffect(_board, p);
                    break;

                case GameMatch3BoardLogic.SPECIAL_BOMB:
                    effectArea = GameMatch3BoardLogic.CollectBombEffect(_board, p);
                    break;

                case GameMatch3BoardLogic.SPECIAL_MAGICBIRD:
                    int targetColor = PickRandomColorOnBoard();
                    if (targetColor > 0)
                        effectArea = GameMatch3BoardLogic.CollectMagicBirdEffect(_board, targetColor);
                    break;
            }

            if (effectArea == null) continue;

            foreach (var pos in effectArea)
            {
                if (!IsInBoard(pos.x, pos.y)) continue;

                if (clearSet.Add(pos))
                {
                    int nv = _board[pos.x, pos.y];
                    if (GameMatch3BoardLogic.IsSpecial(nv))
                        queue.Enqueue(pos);
                }
            }
        }
    }

    private int PickRandomColorOnBoard()
    {
        var colors = new HashSet<int>();
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                int col = GameMatch3BoardLogic.GetColor(_board[r, c]);
                if (col > 0) colors.Add(col);
            }

        if (colors.Count == 0) return 0;

        int idx = UnityEngine.Random.Range(0, colors.Count);
        foreach (var col in colors)
        {
            if (idx == 0) return col;
            idx--;
        }
        return 0;
    }

    // ============================================================
    // 洗牌
    // ============================================================

    private IEnumerator ShuffleRoutine()
    {
        GameMatch3BoardLogic.ShuffleBoard(_board, _colorCount);

        // ★ 洗牌前也清空选中
        ClearAllSelections();

        if (view != null)
            yield return StartCoroutine(view.PlayShuffleAnim(_board));
    }

    // ============================================================
    // 道具
    // ============================================================

    private void HandleHintClicked()
    {
        if (_isGameOver || _isResolving) return;
        if (_hintCount <= 0) { LogDebug("提示次数已用完"); return; }

        // ★ 提示时也要清空选中（避免选中框和提示框叠在一起）
        ClearAllSelections();

        if (_cachedMoves.Count == 0)
        {
            _cachedMoves = GameMatch3BoardLogic.FindAllValidMoves(_board);
            _hintIndex = -1;
        }
        if (_cachedMoves.Count == 0) { LogDebug("无可行解"); return; }

        if (_hintIndex >= 0 && _hintIndex < _cachedMoves.Count)
        {
            var prev = _cachedMoves[_hintIndex];
            view.ClearHint(prev[0].x, prev[0].y);
            view.ClearHint(prev[1].x, prev[1].y);
        }

        _hintIndex = (_hintIndex + 1) % _cachedMoves.Count;
        var cur = _cachedMoves[_hintIndex];
        view.ShowHint(cur[0].x, cur[0].y);
        view.ShowHint(cur[1].x, cur[1].y);

        _hintCount--;
        view.SetProps(_hintCount, _refreshCount, _timeAddCount);
    }

    private void HandleRefreshClicked()
    {
        if (_isGameOver || _isResolving) return;
        if (_refreshCount <= 0) { LogDebug("刷新次数已用完"); return; }

        _refreshCount--;
        view.SetProps(_hintCount, _refreshCount, _timeAddCount);

        // ★ 刷新前清空选中
        ClearAllSelections();
        ClearHintHighlight();

        StartCoroutine(ShuffleRoutine());
    }

    private void HandleTimeAddClicked()
    {
        if (_isGameOver) return;
        if (_timeAddCount <= 0) { LogDebug("时间+次数已用完"); return; }

        _timeAddCount--;
        _timeLeft += _config.time.timeAddSeconds;
        if (_timeLeft > 0f) _isTimeUp = false;

        view.SetTime(_timeLeft);
        view.SetProps(_hintCount, _refreshCount, _timeAddCount);
    }

    private void ClearHintHighlight()
    {
        if (_hintIndex >= 0 && _hintIndex < _cachedMoves.Count)
        {
            var prev = _cachedMoves[_hintIndex];
            view.ClearHint(prev[0].x, prev[0].y);
            view.ClearHint(prev[1].x, prev[1].y);
        }
        _hintIndex = -1;
    }

    /// <summary>
    /// ★ 统一清空所有选中态
    /// </summary>
    private void ClearAllSelections()
    {
        _selected = null;
        if (view != null) view.ClearAllSelected();
    }

    // ============================================================
    // 结算
    // ============================================================

    private void EndGame()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        // ★ 结算前清空选中
        ClearAllSelections();

        SaveBestScore();
        view.ShowResult(_score, _bestScore);
        LogDebug($"游戏结束，本局={_score}，最高={_bestScore}");
    }

    private void HandleRetryClicked()
    {
        if (_swapCoroutine != null) { StopCoroutine(_swapCoroutine); _swapCoroutine = null; }
        StartGame(playerLevel);
    }

    private void HandleCloseClicked()
    {
        gameObject.SetActive(false);
        // TODO: 通知宿主关闭
    }

    // ============================================================
    // 辅助
    // ============================================================

    private bool IsInBoard(int r, int c) => r >= 0 && r < _rows && c >= 0 && c < _cols;

    private bool IsAdjacent(int r1, int c1, int r2, int c2)
        => Mathf.Abs(r1 - r2) + Mathf.Abs(c1 - c2) == 1;

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[Match3Manager] {msg}");
    }
}
