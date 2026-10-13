using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 消消乐流程控制层（状态机版 · 无协程）
/// 
/// 状态流转：
///   Idle → Swapping → PostSwap → Checking → Destroying → Checking（连锁循环）
///                                → InvalidSwapping → Idle
///                                → Shuffling → Idle
/// 
/// 性能优化：
///   - 无协程
///   - 复用 HashSet / Queue / List
///   - 时间显示节流
///   - 输入在非 Idle 状态锁死
/// </summary>
public class GameMatch3Manager : MonoBehaviour
{
    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 核心引用 =====")]
    [SerializeField] private GameMatch3View view;
    [SerializeField] private TextAsset configJson;

    [Header("===== 外部入参 =====")]
    [SerializeField] private int playerLevel = 1;

    private const string BEST_SCORE_KEY = "Match3_BestScore";

    // ============================================================
    // 状态机
    // ============================================================

    private enum Match3State
    {
        Idle,
        Swapping,
        PostSwap,
        InvalidSwapping,
        Checking,
        Destroying,
        Shuffling,
        GameOver,
    }

    private Match3State _state = Match3State.Idle;

    // ============================================================
    // 配置 / 数据
    // ============================================================

    private GameMatch3ConfigWrapper _config;

    private int[,] _board;
    private int _rows, _cols, _colorCount;

    private int _score;
    private float _timeLeft;
    private bool _isTimeUp;

    private int _hintCount, _refreshCount, _timeAddCount;

    private List<Vector2Int[]> _cachedMoves = new List<Vector2Int[]>();
    private int _hintIndex = -1;

    private Vector2Int? _selected;
    private int _bestScore;

    private int _lastTimeSecDisplayed = -1;

    // 交换参数
    private int _swapR1, _swapC1, _swapR2, _swapC2;
    private int _swapS1, _swapS2;

    // 连锁
    private int _combo;

    // 复用容器
    private readonly HashSet<Vector2Int> _clearSetBuffer = new HashSet<Vector2Int>();
    private readonly Queue<Vector2Int> _specialQueueBuffer = new Queue<Vector2Int>();
    private readonly List<Vector2Int> _clearListBuffer = new List<Vector2Int>();

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
    }

    private void Update()
    {
        TickTimer();

        switch (_state)
        {
            case Match3State.PostSwap: TickPostSwap(); break;
            case Match3State.Checking: TickChecking(); break;
        }
    }

    private void TickTimer()
    {
        if (_state == Match3State.GameOver) return;

        if (_timeLeft > 0f)
        {
            _timeLeft -= Time.deltaTime;
            if (_timeLeft < 0f) _timeLeft = 0f;
        }

        int sec = Mathf.CeilToInt(_timeLeft);
        if (sec != _lastTimeSecDisplayed)
        {
            _lastTimeSecDisplayed = sec;
            view?.SetTime(_timeLeft);
        }

        if (_timeLeft <= 0f && !_isTimeUp) _isTimeUp = true;

        if (_isTimeUp && _state == Match3State.Idle)
            EndGame();
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
            Z_Logger.LogError("[Match3Manager] configJson 未绑定");
            _config = new GameMatch3ConfigWrapper();
            return;
        }

        try
        {
            _config = JsonUtility.FromJson<GameMatch3ConfigWrapper>(configJson.text);
            if (_config == null) throw new Exception("null");
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
        PlayerPrefs.SetInt(BEST_SCORE_KEY, _bestScore);
        PlayerPrefs.Save();
    }

    public void StartGame(int level = 1)
    {
        playerLevel = Mathf.Max(1, level);

        _rows = _config.board.rows;
        _cols = _config.board.cols;
        _colorCount = _config.board.colorCount;

        _score = 0;
        _timeLeft = _config.time.initialTime;
        _isTimeUp = false;
        _selected = null;
        _cachedMoves.Clear();
        _hintIndex = -1;
        _lastTimeSecDisplayed = -1;
        _combo = 0;

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

        _state = Match3State.Idle;
        RefreshAllUI();
    }

    private void RefreshAllUI()
    {
        view?.SetScore(_score);
        view?.SetTime(_timeLeft);
        view?.SetLevel(playerLevel);
        view?.SetProps(_hintCount, _refreshCount, _timeAddCount);
    }

    // ============================================================
    // 玩家输入
    // ============================================================

    private void HandleTileClicked(int r, int c)
    {
        if (_state != Match3State.Idle) return;
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
        StartSwap(first.x, first.y, r, c);
    }

    private void HandleTileSwapped(int r1, int c1, int r2, int c2)
    {
        if (_state != Match3State.Idle) return;
        if (!IsInBoard(r1, c1) || !IsInBoard(r2, c2)) return;
        if (!IsAdjacent(r1, c1, r2, c2)) return;

        ClearAllSelections();
        ClearHintHighlight();

        StartSwap(r1, c1, r2, c2);
    }

    // ============================================================
    // 交换流程
    // ============================================================

    private void StartSwap(int r1, int c1, int r2, int c2)
    {
        _swapR1 = r1; _swapC1 = c1; _swapR2 = r2; _swapC2 = c2;

        int v1 = _board[r1, c1];
        int v2 = _board[r2, c2];
        _swapS1 = GameMatch3BoardLogic.GetSpecial(v1);
        _swapS2 = GameMatch3BoardLogic.GetSpecial(v2);

        GameMatch3BoardLogic.Swap(_board, r1, c1, r2, c2);

        _state = Match3State.Swapping;
        view.SetInputLocked(true);

        view.BeginSwapAnim(r1, c1, r2, c2, OnSwapAnimDone);
    }

    private void OnSwapAnimDone()
    {
        _state = Match3State.PostSwap;
    }

    private void TickPostSwap()
    {
        bool isSpecial1 = _swapS1 != GameMatch3BoardLogic.SPECIAL_NONE;
        bool isSpecial2 = _swapS2 != GameMatch3BoardLogic.SPECIAL_NONE;

        // ─────── A：两个都是特殊 → 组合 ───────
        if (isSpecial1 && isSpecial2)
        {
            ResolveSpecialComboLogic(_swapR1, _swapC1, _swapR2, _swapC2, _swapS1, _swapS2);

            if (_clearSetBuffer.Count > 0)
            {
                int gained = GameMatch3BoardLogic.CalcScore(_clearSetBuffer.Count, 1, _config.score);
                _score += gained;
                view.SetScore(_score);

                foreach (var p in _clearSetBuffer) _board[p.x, p.y] = 0;
                _clearListBuffer.Clear();
                _clearListBuffer.AddRange(_clearSetBuffer);

                _combo = 0;
                _state = Match3State.Destroying;
                view.BeginDestroyAnim(_clearListBuffer, OnDestroyAnimDone);
                return;
            }
        }

        // ─────── B：魔力鸟 + 普通 ───────
        bool hasMagic = _swapS1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD
                     || _swapS2 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD;

        if (hasMagic)
        {
            ResolveMagicBirdLogic();

            if (_clearSetBuffer.Count > 0)
            {
                int gained = GameMatch3BoardLogic.CalcSpecialScore(
                    GameMatch3BoardLogic.SPECIAL_MAGICBIRD,
                    _clearSetBuffer.Count, 1,
                    _config.score, _config.specialTiles);
                _score += gained;
                view.SetScore(_score);

                foreach (var p in _clearSetBuffer) _board[p.x, p.y] = 0;
                _clearListBuffer.Clear();
                _clearListBuffer.AddRange(_clearSetBuffer);

                _combo = 0;
                _state = Match3State.Destroying;
                view.BeginDestroyAnim(_clearListBuffer, OnDestroyAnimDone);
                return;
            }
        }

        // ─────── C：普通交换 ───────
        bool isValid = GameMatch3BoardLogic.HasMatchAt(_board, _swapR1, _swapC1)
                    || GameMatch3BoardLogic.HasMatchAt(_board, _swapR2, _swapC2);

        if (!isValid)
        {
            GameMatch3BoardLogic.Swap(_board, _swapR1, _swapC1, _swapR2, _swapC2);
            _state = Match3State.InvalidSwapping;
            view.BeginInvalidSwapAnim(_swapR1, _swapC1, _swapR2, _swapC2, OnInvalidSwapAnimDone);
            return;
        }

        _combo = 0;
        _state = Match3State.Checking;
    }

    private void OnInvalidSwapAnimDone()
    {
        _state = Match3State.Idle;
        view.SetInputLocked(false);
    }

    // ============================================================
    // 连锁循环
    // ============================================================

    private void TickChecking()
    {
        var matches = GameMatch3BoardLogic.FindAllMatches(_board);

        if (matches.Count == 0)
        {
            if (!GameMatch3BoardLogic.HasValidMove(_board))
            {
                GameMatch3BoardLogic.ShuffleBoard(_board, _colorCount);
                view.ClearAllSelected();
                view.UpdateAll(_board);
                _state = Match3State.Idle;
                view.SetInputLocked(false);
                return;
            }

            _state = Match3State.Idle;
            view.SetInputLocked(false);
            view.ClearHints();
            _cachedMoves.Clear();
            _hintIndex = -1;
            return;
        }

        _combo++;

        _clearSetBuffer.Clear();
        _specialQueueBuffer.Clear();

        foreach (var p in matches)
        {
            _clearSetBuffer.Add(p);
            int v = _board[p.x, p.y];
            if (GameMatch3BoardLogic.IsSpecial(v))
                _specialQueueBuffer.Enqueue(p);
        }

        ProcessSpecialTriggers(_specialQueueBuffer, _clearSetBuffer);

        var spawns = GameMatch3BoardLogic.FindSpecialSpawns(_board, matches);

        int gained = GameMatch3BoardLogic.CalcScore(_clearSetBuffer.Count, _combo, _config.score);
        _score += gained;
        view.SetScore(_score);

        if (_combo >= 2) view.ShowCombo(_combo);

        foreach (var p in _clearSetBuffer) _board[p.x, p.y] = 0;
        foreach (var s in spawns)
            _board[s.position.x, s.position.y] =
                GameMatch3BoardLogic.MakeValue(s.color, s.specialType);

        _clearListBuffer.Clear();
        _clearListBuffer.AddRange(_clearSetBuffer);

        _state = Match3State.Destroying;
        view.BeginDestroyAnim(_clearListBuffer, OnDestroyAnimDone);
    }

    private void OnDestroyAnimDone()
    {
        GameMatch3BoardLogic.DropTiles(_board);
        GameMatch3BoardLogic.RefillBoard(_board, _colorCount);

        view.UpdateAll(_board);
        _state = Match3State.Checking;
    }

    // ============================================================
    // ★ 特殊组合效果（重写 · 明确优先级）
    // ============================================================

    /// <summary>
    /// 两个特殊方块交换后的组合效果
    /// 
    /// 优先级（从上到下）：
    ///   1. 双魔力鸟 → 全屏
    ///   2. 魔力鸟 + 直线/爆炸 → 消同色 + 触发对方特殊效果
    ///   3. 直线 + 直线 → 十字
    ///   4. 直线 + 爆炸 → 十字 + 3×3
    ///   5. 爆炸 + 爆炸 → 5×5
    /// 
    /// 输出：填 _clearSetBuffer（所有要消除的格子）
    /// </summary>
    private void ResolveSpecialComboLogic(int r1, int c1, int r2, int c2, int s1, int s2)
    {
        _clearSetBuffer.Clear();
        _specialQueueBuffer.Clear();

        // ══════════════════════════════════════════════
        // 情况 1：双魔力鸟 → 全屏
        // ══════════════════════════════════════════════
        if (s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD
         && s2 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD)
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    _clearSetBuffer.Add(new Vector2Int(r, c));
            return;
        }

        // ══════════════════════════════════════════════
        // 情况 2：魔力鸟 + 直线/爆炸
        //   ① 消所有同色
        //   ② 明确触发对方特殊方块
        //   ③ 排除魔力鸟自己（避免又被随机消一次）
        // ══════════════════════════════════════════════
        if (s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD
         || s2 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD)
        {
            // 交换前的位置
            int magicOldR = (s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD) ? r1 : r2;
            int magicOldC = (s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD) ? c1 : c2;
            int otherOldR = (s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD) ? r2 : r1;
            int otherOldC = (s1 == GameMatch3BoardLogic.SPECIAL_MAGICBIRD) ? c2 : c1;

            // 交换后：
            //   魔力鸟在 (otherOldR, otherOldC)
            //   对方特殊方块在 (magicOldR, magicOldC)

            // ★ ① 对方方块的颜色（交换后在 magicOld 位置）
            int otherColor = GameMatch3BoardLogic.GetColor(_board[magicOldR, magicOldC]);

            // ★ ② 消同色 + 把同色范围内的特殊方块加入队列（排除魔力鸟自己）
            var sameColor = GameMatch3BoardLogic.CollectMagicBirdEffect(_board, otherColor);
            foreach (var p in sameColor)
            {
                _clearSetBuffer.Add(p);

                // 排除魔力鸟自己
                if (p.x == otherOldR && p.y == otherOldC) continue;

                // 同色范围内的特殊方块触发（递归）
                if (GameMatch3BoardLogic.IsSpecial(_board[p.x, p.y]))
                {
                    if (!_specialQueueBuffer.Contains(p))
                        _specialQueueBuffer.Enqueue(p);
                }
            }

            // ★ ③ 明确加入"对方特殊方块"并触发（不依赖同色扫描，防止颜色错位漏掉）
            var otherPos = new Vector2Int(magicOldR, magicOldC);
            _clearSetBuffer.Add(otherPos);
            if (!_specialQueueBuffer.Contains(otherPos))
                _specialQueueBuffer.Enqueue(otherPos);

            // ★ ④ 递归触发所有加入队列的特殊方块
            ProcessSpecialTriggers(_specialQueueBuffer, _clearSetBuffer);
            return;
        }

        // ══════════════════════════════════════════════
        // 情况 3~5：直线/爆炸 组合
        // ══════════════════════════════════════════════
        _clearSetBuffer.Add(new Vector2Int(r1, c1));
        _clearSetBuffer.Add(new Vector2Int(r2, c2));

        // 直线 + 直线 → 十字
        if (IsLineType(s1) && IsLineType(s2))
        {
            AddRow(_clearSetBuffer, r1);
            AddCol(_clearSetBuffer, c1);
        }
        // 直线 + 爆炸（或反过来）→ 十字 + 3×3
        else if (IsLineType(s1) && s2 == GameMatch3BoardLogic.SPECIAL_BOMB)
        {
            AddRow(_clearSetBuffer, r1);
            AddCol(_clearSetBuffer, c1);
            AddSquare(_clearSetBuffer, r2, c2, 1);
        }
        else if (s1 == GameMatch3BoardLogic.SPECIAL_BOMB && IsLineType(s2))
        {
            AddRow(_clearSetBuffer, r2);
            AddCol(_clearSetBuffer, c2);
            AddSquare(_clearSetBuffer, r1, c1, 1);
        }
        // 爆炸 + 爆炸 → 5×5
        else if (s1 == GameMatch3BoardLogic.SPECIAL_BOMB
              && s2 == GameMatch3BoardLogic.SPECIAL_BOMB)
        {
            AddSquare(_clearSetBuffer, r1, c1, 2);
        }

        // 通用遍历：范围内所有特殊方块触发
        foreach (var p in _clearSetBuffer)
        {
            if (!IsInBoard(p.x, p.y)) continue;
            if (GameMatch3BoardLogic.IsSpecial(_board[p.x, p.y]))
            {
                if (!_specialQueueBuffer.Contains(p))
                    _specialQueueBuffer.Enqueue(p);
            }
        }
        ProcessSpecialTriggers(_specialQueueBuffer, _clearSetBuffer);
    }

    // ============================================================
    // 魔力鸟 + 普通方块
    // ============================================================

    private void ResolveMagicBirdLogic()
    {
        _clearSetBuffer.Clear();
        _specialQueueBuffer.Clear();

        int v1 = _board[_swapR1, _swapC1];
        int v2 = _board[_swapR2, _swapC2];
        bool v1Magic = GameMatch3BoardLogic.IsMagicBird(v1);
        bool v2Magic = GameMatch3BoardLogic.IsMagicBird(v2);

        if (v1Magic && v2Magic)
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    _clearSetBuffer.Add(new Vector2Int(r, c));
            return;
        }

        // 交换前魔力鸟的位置
        int magicOldR = v1Magic ? _swapR1 : _swapR2;
        int magicOldC = v1Magic ? _swapC1 : _swapC2;

        // 交换后魔力鸟在对面
        int magicNewR = v1Magic ? _swapR2 : _swapR1;
        int magicNewC = v1Magic ? _swapC2 : _swapC1;

        // 目标颜色 = 交换后 (magicOld) 位置的颜色（也就是原来对面对面的颜色）
        int targetColor = GameMatch3BoardLogic.GetColor(_board[magicOldR, magicOldC]);

        var sameColor = GameMatch3BoardLogic.CollectMagicBirdEffect(_board, targetColor);
        foreach (var p in sameColor)
        {
            _clearSetBuffer.Add(p);

            // 同色范围内的特殊方块触发（排除魔力鸟自己）
            if (p.x == magicNewR && p.y == magicNewC) continue;
            if (GameMatch3BoardLogic.IsSpecial(_board[p.x, p.y]))
            {
                if (!_specialQueueBuffer.Contains(p))
                    _specialQueueBuffer.Enqueue(p);
            }
        }

        // 魔力鸟自己也要进 clearSet（但它不再触发）
        _clearSetBuffer.Add(new Vector2Int(magicNewR, magicNewC));

        // 递归触发
        ProcessSpecialTriggers(_specialQueueBuffer, _clearSetBuffer);
    }

    // ============================================================
    // 特殊方块递归触发
    // ============================================================

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
        Span<int> seen = stackalloc int[_colorCount + 1];
        int count = 0;

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                int col = GameMatch3BoardLogic.GetColor(_board[r, c]);
                if (col <= 0 || col > _colorCount) continue;
                if (seen[col] == 0) { seen[col] = 1; count++; }
            }

        if (count == 0) return 0;

        int idx = UnityEngine.Random.Range(0, count);
        for (int col = 1; col <= _colorCount; col++)
        {
            if (seen[col] == 0) continue;
            if (idx == 0) return col;
            idx--;
        }
        return 0;
    }

    // ============================================================
    // 辅助
    // ============================================================

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
    // 道具
    // ============================================================

    private void HandleHintClicked()
    {
        if (_state != Match3State.Idle) return;
        if (_hintCount <= 0) { LogDebug("提示次数已用完"); return; }

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
        if (_state != Match3State.Idle) return;
        if (_refreshCount <= 0) { LogDebug("刷新次数已用完"); return; }

        _refreshCount--;
        view.SetProps(_hintCount, _refreshCount, _timeAddCount);

        ClearAllSelections();
        ClearHintHighlight();

        GameMatch3BoardLogic.ShuffleBoard(_board, _colorCount);
        view.UpdateAll(_board);
    }

    private void HandleTimeAddClicked()
    {
        if (_state == Match3State.GameOver) return;
        if (_timeAddCount <= 0) { LogDebug("时间+次数已用完"); return; }

        _timeAddCount--;
        _timeLeft += _config.time.timeAddSeconds;
        if (_timeLeft > 0f) _isTimeUp = false;

        _lastTimeSecDisplayed = -1;
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

    private void ClearAllSelections()
    {
        _selected = null;
        view?.ClearAllSelected();
    }

    // ============================================================
    // 结算
    // ============================================================

    private void EndGame()
    {
        if (_state == Match3State.GameOver) return;
        _state = Match3State.GameOver;

        ClearAllSelections();
        view.SetInputLocked(true);

        SaveBestScore();
        view.ShowResult(_score, _bestScore);
    }

    private void HandleRetryClicked()
    {
        StartGame(playerLevel);
    }

    private void HandleCloseClicked()
    {
        gameObject.SetActive(false);
    }

    // ============================================================
    // 边界
    // ============================================================

    private bool IsInBoard(int r, int c) => r >= 0 && r < _rows && c >= 0 && c < _cols;
    private bool IsAdjacent(int r1, int c1, int r2, int c2)
        => Mathf.Abs(r1 - r2) + Mathf.Abs(c1 - c2) == 1;

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[Match3Manager] {msg}");
    }
}
