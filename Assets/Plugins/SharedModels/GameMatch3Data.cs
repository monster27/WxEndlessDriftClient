using System;
using UnityEngine;

// ==================== 棋盘配置 ====================
[Serializable]
public class GameMatch3BoardData
{
    public int rows = 8;          // 棋盘行数
    public int cols = 8;          // 棋盘列数
    public int colorCount = 5;    // 颜色种类数
}

// ==================== 时间配置 ====================
[Serializable]
public class GameMatch3TimeData
{
    public float initialTime = 60f;      // 初始时间（秒）
    public float timeAddSeconds = 30f;   // "时间增加"道具加多少秒
}

// ==================== 得分配置 ====================
[Serializable]
public class GameMatch3ScoreData
{
    public int baseMatch3 = 30;              // 基础 3 连得分
    public int extraPerTile = 15;            // 每多 1 个元素额外得分
    public float comboMultiplier = 0.5f;     // 连锁倍数系数（第 n 次连锁：1 + 0.5*(n-1)）
}

// ==================== 特殊元素配置 ====================
[Serializable]
public class GameMatch3SpecialTilesData
{
    public float lineMultiplier = 1.5f;        // 直线特效得分倍数
    public float bombMultiplier = 2.0f;        // 爆炸特效得分倍数
    public float magicBirdMultiplier = 2.5f;   // 魔力鸟得分倍数
}

// ==================== 道具配置 ====================
[Serializable]
public class GameMatch3PropsData
{
    public int hintBaseCount = 1;       // 提示道具基础次数（等级 1）
    public int refreshBaseCount = 1;    // 刷新道具基础次数（等级 1）
    public int timeAddBaseCount = 1;    // 时间增加道具基础次数（等级 1）
}

// ==================== 总配置 Wrapper ====================
[Serializable]
public class GameMatch3ConfigWrapper
{
    public GameMatch3BoardData board = new GameMatch3BoardData();
    public GameMatch3TimeData time = new GameMatch3TimeData();
    public GameMatch3ScoreData score = new GameMatch3ScoreData();
    public GameMatch3SpecialTilesData specialTiles = new GameMatch3SpecialTilesData();
    public GameMatch3PropsData props = new GameMatch3PropsData();
}
