using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 消消乐棋盘纯逻辑层
/// 
/// 数据编码：value = color | (special << 8)
///   - color:   0 = 空, 1..N = 颜色索引
///   - special: 0 = 普通
///              1 = 横向直线（消除整行）
///              2 = 竖向直线（消除整列）
///              3 = 爆炸（3×3 区域）
///              4 = 魔力鸟（消除同色）
/// </summary>
public static class GameMatch3BoardLogic
{
    // ============================================================
    // 常量
    // ============================================================

    public const int SPECIAL_NONE = 0;
    public const int SPECIAL_LINE_H = 1;
    public const int SPECIAL_LINE_V = 2;
    public const int SPECIAL_BOMB = 3;
    public const int SPECIAL_MAGICBIRD = 4;

    private const int COLOR_MASK = 0xFF;
    private const int SPECIAL_SHIFT = 8;
    private const int SPECIAL_MASK = 0xFF;

    private static readonly Vector2Int[] FourDirs =
    {
        new Vector2Int(-1, 0), new Vector2Int(1, 0),
        new Vector2Int(0, -1), new Vector2Int(0, 1),
    };

    // ============================================================
    // 1. 编码 / 解码
    // ============================================================

    public static int MakeValue(int color, int special = SPECIAL_NONE)
        => (color & COLOR_MASK) | ((special & SPECIAL_MASK) << SPECIAL_SHIFT);

    public static int GetColor(int value) => value & COLOR_MASK;
    public static int GetSpecial(int value) => (value >> SPECIAL_SHIFT) & SPECIAL_MASK;
    public static bool IsEmpty(int value) => value == 0;
    public static bool IsSpecial(int value) => GetSpecial(value) != SPECIAL_NONE;
    public static bool IsMagicBird(int value) => GetSpecial(value) == SPECIAL_MAGICBIRD;
    public static bool IsColorTile(int value) => !IsEmpty(value) && GetColor(value) != 0;

    // ============================================================
    // 2. 生成棋盘
    // ============================================================

    public static int[,] GenerateBoard(int rows, int cols, int colorCount, int maxRetry = 100)
    {
        if (colorCount < 3) colorCount = 3;

        int[,] board = null;
        for (int i = 0; i < maxRetry; i++)
        {
            board = CreateRandomBoard(rows, cols, colorCount);
            if (HasValidMove(board)) return board;
        }
        return board;
    }

    private static int[,] CreateRandomBoard(int rows, int cols, int colorCount)
    {
        var board = new int[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var forbidden = new HashSet<int>();

                if (c >= 2)
                {
                    int a = GetColor(board[r, c - 1]);
                    int b = GetColor(board[r, c - 2]);
                    if (a == b && a != 0) forbidden.Add(a);
                }
                if (r >= 2)
                {
                    int a = GetColor(board[r - 1, c]);
                    int b = GetColor(board[r - 2, c]);
                    if (a == b && a != 0) forbidden.Add(a);
                }

                board[r, c] = MakeValue(PickColorExcluding(forbidden, colorCount));
            }
        }
        return board;
    }

    private static int PickColorExcluding(HashSet<int> forbidden, int colorCount)
    {
        for (int i = 0; i < 20; i++)
        {
            int color = UnityEngine.Random.Range(1, colorCount + 1);
            if (!forbidden.Contains(color)) return color;
        }
        for (int color = 1; color <= colorCount; color++)
            if (!forbidden.Contains(color)) return color;
        return 1;
    }

    public static void RemoveInitialMatches(int[,] board, int colorCount)
    {
        int guard = 0;
        while (guard++ < 100)
        {
            var matches = FindAllMatches(board);
            if (matches.Count == 0) return;
            foreach (var pos in matches)
                PickNonMatchingColorAt(board, pos.x, pos.y, colorCount);
        }
    }

    private static void PickNonMatchingColorAt(int[,] board, int r, int c, int colorCount)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);
        var forbidden = new HashSet<int>();

        void TryAdd(int a, int b)
        {
            if (a == b && a != 0) forbidden.Add(a);
        }

        if (c >= 2) TryAdd(GetColor(board[r, c - 1]), GetColor(board[r, c - 2]));
        if (c + 2 < cols) TryAdd(GetColor(board[r, c + 1]), GetColor(board[r, c + 2]));
        if (r >= 2) TryAdd(GetColor(board[r - 1, c]), GetColor(board[r - 2, c]));
        if (r + 2 < rows) TryAdd(GetColor(board[r + 1, c]), GetColor(board[r + 2, c]));
        if (c >= 1 && c + 1 < cols) TryAdd(GetColor(board[r, c - 1]), GetColor(board[r, c + 1]));
        if (r >= 1 && r + 1 < rows) TryAdd(GetColor(board[r - 1, c]), GetColor(board[r + 1, c]));

        board[r, c] = MakeValue(PickColorExcluding(forbidden, colorCount));
    }

    // ============================================================
    // 3. 匹配检测
    // ============================================================

    public static bool HasMatchAt(int[,] board, int r, int c)
    {
        int v = board[r, c];
        if (IsEmpty(v)) return false;

        int color = GetColor(v);
        if (color == 0) return false;

        int rows = board.GetLength(0), cols = board.GetLength(1);

        int count = 1;
        for (int i = c - 1; i >= 0 && SameColor(board[r, i], color); i--) count++;
        for (int i = c + 1; i < cols && SameColor(board[r, i], color); i++) count++;
        if (count >= 3) return true;

        count = 1;
        for (int i = r - 1; i >= 0 && SameColor(board[i, c], color); i--) count++;
        for (int i = r + 1; i < rows && SameColor(board[i, c], color); i++) count++;
        return count >= 3;
    }

    private static bool SameColor(int a, int b)
    {
        if (IsEmpty(a) || IsEmpty(b)) return false;
        int ca = GetColor(a), cb = GetColor(b);
        if (ca == 0 || cb == 0) return false;
        return ca == cb;
    }

    public static List<Vector2Int> FindAllMatches(int[,] board)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);
        var result = new HashSet<Vector2Int>();

        for (int r = 0; r < rows; r++)
        {
            int start = 0;
            while (start < cols)
            {
                int color = GetColor(board[r, start]);
                if (color == 0) { start++; continue; }

                int end = start + 1;
                while (end < cols && SameColor(board[r, end], color)) end++;

                if (end - start >= 3)
                    for (int i = start; i < end; i++) result.Add(new Vector2Int(r, i));

                start = end;
            }
        }

        for (int c = 0; c < cols; c++)
        {
            int start = 0;
            while (start < rows)
            {
                int color = GetColor(board[start, c]);
                if (color == 0) { start++; continue; }

                int end = start + 1;
                while (end < rows && SameColor(board[end, c], color)) end++;

                if (end - start >= 3)
                    for (int i = start; i < end; i++) result.Add(new Vector2Int(i, c));

                start = end;
            }
        }

        return new List<Vector2Int>(result);
    }

    // ============================================================
    // 4. 特殊方块生成
    // ============================================================

    public class SpecialSpawnInfo
    {
        public Vector2Int position;
        public int specialType;
        public int color;
    }

    public static List<SpecialSpawnInfo> FindSpecialSpawns(int[,] board, List<Vector2Int> matches)
    {
        var result = new List<SpecialSpawnInfo>();
        if (matches == null || matches.Count == 0) return result;

        var matchSet = new HashSet<Vector2Int>(matches);
        var visited = new HashSet<Vector2Int>();

        foreach (var start in matches)
        {
            if (visited.Contains(start)) continue;

            int groupColor = GetColor(board[start.x, start.y]);
            if (groupColor == 0) continue;

            var group = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                group.Add(cur);

                foreach (var dir in FourDirs)
                {
                    var next = cur + dir;
                    if (next.x < 0 || next.x >= board.GetLength(0)) continue;
                    if (next.y < 0 || next.y >= board.GetLength(1)) continue;
                    if (visited.Contains(next) || !matchSet.Contains(next)) continue;
                    if (GetColor(board[next.x, next.y]) != groupColor) continue;

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            AnalyzeGroup(board, group, groupColor, result);
        }

        return result;
    }

    private static void AnalyzeGroup(int[,] board, List<Vector2Int> group, int color, List<SpecialSpawnInfo> result)
    {
        var groupSet = new HashSet<Vector2Int>(group);
        int rows = board.GetLength(0), cols = board.GetLength(1);

        int maxH = 0, maxV = 0;
        List<Vector2Int> bestHRun = null, bestVRun = null;

        var visitedH = new HashSet<Vector2Int>();
        var visitedV = new HashSet<Vector2Int>();

        // 横向扫连续段
        foreach (var p in group)
        {
            if (visitedH.Contains(p)) continue;
            if (groupSet.Contains(new Vector2Int(p.x, p.y - 1))) continue;

            var run = new List<Vector2Int>();
            int c = p.y;
            while (groupSet.Contains(new Vector2Int(p.x, c)))
            {
                run.Add(new Vector2Int(p.x, c));
                visitedH.Add(new Vector2Int(p.x, c));
                c++;
            }

            if (run.Count > maxH) { maxH = run.Count; bestHRun = run; }
        }

        // 纵向扫连续段
        foreach (var p in group)
        {
            if (visitedV.Contains(p)) continue;
            if (groupSet.Contains(new Vector2Int(p.x - 1, p.y))) continue;

            var run = new List<Vector2Int>();
            int r = p.x;
            while (groupSet.Contains(new Vector2Int(r, p.y)))
            {
                run.Add(new Vector2Int(r, p.y));
                visitedV.Add(new Vector2Int(r, p.y));
                r++;
            }

            if (run.Count > maxV) { maxV = run.Count; bestVRun = run; }
        }

        // 5 连 → 魔力鸟，位置取中点
        if (maxH >= 5 || maxV >= 5)
        {
            var run = maxH >= 5 ? bestHRun : bestVRun;
            var mid = run[run.Count / 2];
            result.Add(new SpecialSpawnInfo
            {
                position = mid,
                specialType = SPECIAL_MAGICBIRD,
                color = 0,
            });
            return;
        }

        // 4 连 → 直线特效，位置取中点，方向区分 H / V
        if (maxH == 4 || maxV == 4)
        {
            var run = maxH == 4 ? bestHRun : bestVRun;
            var mid = run[run.Count / 2];
            result.Add(new SpecialSpawnInfo
            {
                position = mid,
                specialType = maxH == 4 ? SPECIAL_LINE_H : SPECIAL_LINE_V,
                color = color,
            });
            return;
        }

        // L / T / 十字 → 爆炸，位置取拐点
        if (group.Count >= 5)
        {
            Vector2Int cornerPos = group[group.Count / 2];
            foreach (var p in group)
            {
                bool hasH = groupSet.Contains(new Vector2Int(p.x, p.y - 1))
                         || groupSet.Contains(new Vector2Int(p.x, p.y + 1));
                bool hasV = groupSet.Contains(new Vector2Int(p.x - 1, p.y))
                         || groupSet.Contains(new Vector2Int(p.x + 1, p.y));
                if (hasH && hasV) { cornerPos = p; break; }
            }
            result.Add(new SpecialSpawnInfo
            {
                position = cornerPos,
                specialType = SPECIAL_BOMB,
                color = color,
            });
        }
    }

    // ============================================================
    // 5. 死局检测 / 交换
    // ============================================================

    public static bool HasValidMove(int[,] board)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (IsMagicBird(board[r, c])) return true;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (c + 1 < cols && CheckSwapMatch(board, r, c, r, c + 1)) return true;
                if (r + 1 < rows && CheckSwapMatch(board, r, c, r + 1, c)) return true;
            }
        }
        return false;
    }

    private static bool CheckSwapMatch(int[,] board, int r1, int c1, int r2, int c2)
    {
        Swap(board, r1, c1, r2, c2);
        bool ok = HasMatchAt(board, r1, c1) || HasMatchAt(board, r2, c2);
        Swap(board, r1, c1, r2, c2);
        return ok;
    }

    public static void Swap(int[,] board, int r1, int c1, int r2, int c2)
    {
        int t = board[r1, c1];
        board[r1, c1] = board[r2, c2];
        board[r2, c2] = t;
    }

    public static List<Vector2Int[]> FindAllValidMoves(int[,] board)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);
        var result = new List<Vector2Int[]>();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (c + 1 < cols && CheckSwapMatch(board, r, c, r, c + 1))
                    result.Add(new[] { new Vector2Int(r, c), new Vector2Int(r, c + 1) });
                if (r + 1 < rows && CheckSwapMatch(board, r, c, r + 1, c))
                    result.Add(new[] { new Vector2Int(r, c), new Vector2Int(r + 1, c) });
            }
        }
        return result;
    }

    // ============================================================
    // 6. 洗牌 / 下落 / 补充
    // ============================================================

    public static void ShuffleBoard(int[,] board, int colorCount)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);

        var values = new List<int>();
        var positions = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!IsEmpty(board[r, c]))
                {
                    values.Add(board[r, c]);
                    positions.Add(new Vector2Int(r, c));
                }

        int guard = 0;
        while (guard++ < 50)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }

            for (int i = 0; i < positions.Count; i++)
                board[positions[i].x, positions[i].y] = values[i];

            RemoveInitialMatches(board, colorCount);

            if (HasValidMove(board)) return;
        }
    }

    public static void DropTiles(int[,] board)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);

        for (int c = 0; c < cols; c++)
        {
            int writeRow = rows - 1;
            for (int r = rows - 1; r >= 0; r--)
            {
                if (!IsEmpty(board[r, c]))
                {
                    if (writeRow != r)
                    {
                        board[writeRow, c] = board[r, c];
                        board[r, c] = 0;
                    }
                    writeRow--;
                }
            }
        }
    }

    public static void RefillBoard(int[,] board, int colorCount)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (IsEmpty(board[r, c]))
                    board[r, c] = MakeValue(UnityEngine.Random.Range(1, colorCount + 1));
    }

    // ============================================================
    // 7. 得分
    // ============================================================

    public static int CalcScore(int matchCount, int combo, GameMatch3ScoreData cfg)
    {
        if (cfg == null) return 0;
        int baseScore = cfg.baseMatch3;
        if (matchCount > 3) baseScore += (matchCount - 3) * cfg.extraPerTile;
        float mult = 1f + cfg.comboMultiplier * (combo - 1);
        return Mathf.RoundToInt(baseScore * mult);
    }

    public static int CalcSpecialScore(int specialType, int matchCount, int combo,
        GameMatch3ScoreData cfg, GameMatch3SpecialTilesData specialCfg)
    {
        int baseScore = CalcScore(matchCount, combo, cfg);
        if (specialCfg == null) return baseScore;

        float mult = specialType switch
        {
            SPECIAL_LINE_H or SPECIAL_LINE_V => specialCfg.lineMultiplier,
            SPECIAL_BOMB => specialCfg.bombMultiplier,
            SPECIAL_MAGICBIRD => specialCfg.magicBirdMultiplier,
            _ => 1f
        };
        return Mathf.RoundToInt(baseScore * mult);
    }

    // ============================================================
    // 8. 特效消除范围
    // ============================================================

    public static List<Vector2Int> CollectLineHEffect(int[,] board, Vector2Int center)
    {
        int cols = board.GetLength(1);
        var result = new List<Vector2Int>();
        for (int c = 0; c < cols; c++)
            if (c != center.y) result.Add(new Vector2Int(center.x, c));
        return result;
    }

    public static List<Vector2Int> CollectLineVEffect(int[,] board, Vector2Int center)
    {
        int rows = board.GetLength(0);
        var result = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
            if (r != center.x) result.Add(new Vector2Int(r, center.y));
        return result;
    }

    public static List<Vector2Int> CollectBombEffect(int[,] board, Vector2Int center)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);
        var result = new List<Vector2Int>();

        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int r = center.x + dr, c = center.y + dc;
                if (r < 0 || r >= rows || c < 0 || c >= cols) continue;
                if (r == center.x && c == center.y) continue;
                result.Add(new Vector2Int(r, c));
            }
        return result;
    }

    public static List<Vector2Int> CollectMagicBirdEffect(int[,] board, int targetColor)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);
        var result = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (GetColor(board[r, c]) == targetColor && !IsEmpty(board[r, c]))
                    result.Add(new Vector2Int(r, c));
        return result;
    }

    // ============================================================
    // 9. 调试
    // ============================================================

    public static string DebugPrint(int[,] board)
    {
        int rows = board.GetLength(0), cols = board.GetLength(1);
        var sb = new System.Text.StringBuilder();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int v = board[r, c];
                int color = GetColor(v);
                int sp = GetSpecial(v);
                string tag = sp switch
                {
                    SPECIAL_LINE_H => "H",
                    SPECIAL_LINE_V => "V",
                    SPECIAL_BOMB => "B",
                    SPECIAL_MAGICBIRD => "M",
                    _ => "."
                };
                sb.Append($"{color}{tag} ");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
